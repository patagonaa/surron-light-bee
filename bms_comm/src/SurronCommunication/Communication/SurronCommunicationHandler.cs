using SurronCommunication.Packet;
using System.Diagnostics;

namespace SurronCommunication.Communication
{
    public sealed class SurronCommunicationHandler : IDisposable
    {
        private readonly ICommunication _communication;

        internal SurronCommunicationHandler(ICommunication communication)
        {
            _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        }

        public static SurronCommunicationHandler FromSerialPort(string serialPort)
        {
            return new SurronCommunicationHandler(new SerialCommunication(serialPort));
        }

        public async Task<byte[]> ReadRegister(ushort address, byte parameter, byte paramLength, CancellationToken cancellationToken)
        {
            var sendPacket = SurronDataPacket.Create(SurronCmd.ReadRequest, address, parameter, paramLength, null);

            for (int sendRetryCounter = 0; ; sendRetryCounter++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await _communication.DiscardInBuffer(cancellationToken);
                await SendPacket(sendPacket, cancellationToken);

                try
                {
                    // 9600 baud 8N1 = ~960 bytes/s, so 200ms are enough for ~192 bytes.
                    var packet = await ReceivePacket(200, cancellationToken);
                    if (packet.Address == address && packet.Parameter == parameter && packet.DataLength == paramLength)
                        return packet.CommandData!;
                    Debug.WriteLine($"Wrong Packet {packet}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (TimeoutException)
                {
                    if (sendRetryCounter >= 3)
                        throw;
                    Debug.WriteLine("Timeout");
                }
                catch (InvalidDataException dataEx)
                {
                    Debug.WriteLine($"Invalid Data: {dataEx.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Unknown Exception: {ex}");
                }

                await Task.Delay(100, cancellationToken); // can not be too high or else BMS goes back into standby (after ~3s)
            }
        }

        public async Task SendPacket(SurronDataPacket packet, CancellationToken token)
        {
            var toSend = packet.ToBytes();
            Debug.WriteLine($">{HexUtils.BytesToHex(toSend)}");
            await _communication.Write(toSend, token);
        }

        public async Task<SurronDataPacket> ReceivePacket(int timeoutMillis, CancellationToken token)
        {
            var buffer = new byte[512];
            var bufferPos = 0;

            var headerLength = SurronDataPacket.HeaderLength;
            await _communication.ReadExactly(buffer.AsMemory(bufferPos, headerLength), timeoutMillis, token);
            bufferPos += headerLength;

            var restLength = SurronDataPacket.GetPacketLengthFromHeader(buffer) - headerLength;
            await _communication.ReadExactly(buffer.AsMemory(bufferPos, restLength), timeoutMillis, token);
            bufferPos += restLength;

            Debug.WriteLine($"<{HexUtils.BytesToHex(buffer.AsSpan(0, bufferPos).ToArray())}");
            return SurronDataPacket.FromBytes(buffer.AsSpan(0, bufferPos));
        }

        public void Dispose()
        {
            _communication.Dispose();
        }
    }
}
