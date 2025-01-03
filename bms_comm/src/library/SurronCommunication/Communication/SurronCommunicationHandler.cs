﻿using SurronCommunication.Packet;
using System;
using System.Diagnostics;
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
                    // also, BMS takes some time to responsd sometimes when it is busy updating the display (>80ms in some cases)
                    var result = ReceivePacket(200, cancellationToken, out var packet);

                    switch (result)
                    {
                        case SurronReadResult.Success:
                            {
                                if (packet!.Address == address && packet.Parameter == parameter && packet.DataLength == paramLength)
                                    return packet.CommandData!;
                                Debug.WriteLine($"{_logPrefix}: Wrong Packet {packet}");
                                break;
                            }
                        case SurronReadResult.Timeout:
                            Console.WriteLine($"{_logPrefix}: Timeout");
                            break;
                        case SurronReadResult.InvalidData:
                            Console.WriteLine($"{_logPrefix}: Invalid Data");
                            break;
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
            //Debug.WriteLine($"{_logPrefix}>{HexUtils.BytesToHex(toSend)}");
            _communication.Write(toSend, token);
        }

        public SurronReadResult ReceivePacket(int timeoutMillis, CancellationToken token, out SurronDataPacket? packet)
        {
            var buffer = new byte[512];
            var bufferPos = 0;

            var headerLength = SurronDataPacket.HeaderLength;

            if (!_communication.ReadExactly(buffer.AsSpan(bufferPos, headerLength), timeoutMillis, token))
            {
                _communication.Reset();
                packet = null;
                return SurronReadResult.Timeout;
            }
            bufferPos += headerLength;

            var restLength = SurronDataPacket.GetPacketLengthFromHeader(buffer.AsSpan(0, headerLength)) - headerLength;
            if (restLength < 0)
            {
                Debug.WriteLine($"Invalid data: {HexUtils.BytesToHex(buffer.AsSpan(0, headerLength).ToArray())}");
                _communication.Reset();
                packet = null;
                return SurronReadResult.InvalidData;
            }

            if (!_communication.ReadExactly(buffer.AsSpan(bufferPos, restLength), timeoutMillis, token))
            {
                _communication.Reset();
                packet = null;
                return SurronReadResult.Timeout;
            }
            bufferPos += restLength;

            //Debug.WriteLine($"{_logPrefix}<{HexUtils.BytesToHex(buffer.AsSpan(0, bufferPos).ToArray())}");
            packet = SurronDataPacket.FromBytes(buffer.AsSpan(0, bufferPos));
            if (packet == null)
            {
                _communication.Reset();
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
