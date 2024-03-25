using SurronCommunication.Packet;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
#if NANOFRAMEWORK_1_0
using System.Buffers.Binary;
#endif

namespace SurronCommunication.Communication
{
    public sealed class SurronCommunicationHandler : ISurronCommunicationHandler
    {
        private readonly ICommunication _communication;
        private readonly string _logPrefix;

        internal SurronCommunicationHandler(ICommunication communication, string logPrefix)
        {
            _communication = communication ?? throw new ArgumentNullException(nameof(communication));
            _logPrefix = logPrefix;
        }

        public static SurronCommunicationHandler FromSerialPort(string serialPort, string? logPrefix = null)
        {
            return new SurronCommunicationHandler(new SerialCommunication(serialPort), logPrefix ?? serialPort);
        }

        public byte[]? ReadRegister(ushort address, byte parameter, byte paramLength, CancellationToken cancellationToken)
        {
            var sendPacket = SurronDataPacket.Create(SurronCmd.ReadRequest, address, parameter, paramLength, null);

            for (int sendRetryCounter = 0; sendRetryCounter < 3; sendRetryCounter++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _communication.DiscardInBuffer(cancellationToken);
                SendPacket(sendPacket, cancellationToken);

                try
                {
                    // 9600 baud 8N1 = ~960 bytes/s, so 200ms are enough for ~192 bytes.
                    var result = ReceivePacket(100, cancellationToken, out var packet);

                    switch (result)
                    {
                        case SurronReadResult.Success:
                            {
                                if (packet!.Address == address && packet.Parameter == parameter && packet.DataLength == paramLength)
                                    return packet.CommandData!;
                                Debug.WriteLine($"{_logPrefix}: Wrong Packet {packet}");
                                continue;
                            }
                        case SurronReadResult.Timeout:
                            Console.WriteLine($"{_logPrefix}: Timeout");
                            continue;
                        case SurronReadResult.InvalidData:
                            Console.WriteLine($"{_logPrefix}: Invalid Data");
                            continue;
                        default:
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{_logPrefix}: Unknown Exception: {ex}");
                }

                Thread.Sleep(100); // can not be too high or else BMS goes back into standby (after ~3s)
            }

            return null;
        }

        public void SendPacket(SurronDataPacket packet, CancellationToken token)
        {
            var toSend = packet.ToBytes();
            Debug.WriteLine($"{_logPrefix}>{HexUtils.BytesToHex(toSend)}");
            _communication.Write(toSend, token);
        }

        public SurronReadResult ReceivePacket(int timeoutMillis, CancellationToken token, out SurronDataPacket? packet)
        {
            var buffer = new byte[512];
            var bufferPos = 0;

            const int cmdLength = 1;
            while (true)
            {
                if (!_communication.ReadExactly(buffer.AsSpan(bufferPos, cmdLength), timeoutMillis, token))
                {
                    packet = null;
                    return SurronReadResult.Timeout;
                }

                if (buffer[0] == (byte)SurronCmd.ReadRequest || buffer[0] == (byte)SurronCmd.ReadResponse || buffer[0] == (byte)SurronCmd.Status)
                    break;
            }

            bufferPos += cmdLength;

            var headerLength = SurronDataPacket.HeaderLength;

            if (!_communication.ReadExactly(buffer.AsSpan(bufferPos, headerLength - cmdLength), timeoutMillis, token))
            {
                packet = null;
                return SurronReadResult.Timeout;
            }
            bufferPos += headerLength - cmdLength;

            var restLength = SurronDataPacket.GetPacketLengthFromHeader(buffer.AsSpan(0, headerLength)) - headerLength;
            if (restLength < 0)
            {
                packet = null;
                return SurronReadResult.InvalidData;
            }

            if (!_communication.ReadExactly(buffer.AsSpan(bufferPos, restLength), timeoutMillis, token))
            {
                packet = null;
                return SurronReadResult.Timeout;
            }
            bufferPos += restLength;

            Debug.WriteLine($"{_logPrefix}<{HexUtils.BytesToHex(buffer.AsSpan(0, bufferPos).ToArray())}");
            packet = SurronDataPacket.FromBytes(buffer.AsSpan(0, bufferPos));
            if (packet == null)
            {
                return SurronReadResult.InvalidData;
            }

            return SurronReadResult.Success;
        }

        public void Dispose()
        {
            _communication.Dispose();
        }
    }
}
