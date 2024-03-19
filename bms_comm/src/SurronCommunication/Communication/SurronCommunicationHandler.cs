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

        public static SurronCommunicationHandler FromSerialPort(string serialPort)
        {
            return new SurronCommunicationHandler(new SerialCommunication(serialPort), serialPort);
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
                    var packet = ReceivePacket(200, cancellationToken);

                    if (packet != null)
                    {
                        if (packet.Address == address && packet.Parameter == parameter && packet.DataLength == paramLength)
                            return packet.CommandData!;
                        Debug.WriteLine($"Wrong Packet {packet}");
                        continue;
                    }
                    Debug.WriteLine("Timeout");
                    continue;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (InvalidDataException dataEx)
                {
                    Debug.WriteLine($"Invalid Data: {dataEx.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Unknown Exception: {ex}");
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

        public SurronDataPacket? ReceivePacket(int timeoutMillis, CancellationToken token)
        {
            var buffer = new byte[512];
            var bufferPos = 0;

            var headerLength = SurronDataPacket.HeaderLength;
            if (!_communication.ReadExactly(buffer.AsSpan(bufferPos, headerLength), timeoutMillis, token))
                return null;
            bufferPos += headerLength;

            try
            {
                var restLength = SurronDataPacket.GetPacketLengthFromHeader(buffer.AsSpan(0, headerLength)) - headerLength;
                if (!_communication.ReadExactly(buffer.AsSpan(bufferPos, restLength), timeoutMillis, token))
                    return null;
                bufferPos += restLength;

                Debug.WriteLine($"{_logPrefix}<{HexUtils.BytesToHex(buffer.AsSpan(0, bufferPos).ToArray())}");
                return SurronDataPacket.FromBytes(buffer.AsSpan(0, bufferPos));
            }
            catch (InvalidDataException)
            {
                _communication.DiscardInBuffer(token);
                throw;
            }
        }

        public void Dispose()
        {
            _communication.Dispose();
        }
    }
}
