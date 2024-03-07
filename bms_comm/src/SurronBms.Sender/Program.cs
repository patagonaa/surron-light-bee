using SurronBms.Common;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;

namespace SurronBms.Sender
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (o, args) => { cts.Cancel(); args.Cancel = true; };

            using var sp = new SerialPort("COM8")
            {
                BaudRate = 9600,
                ReadTimeout = 100
            };

            sp.Open();

            const ushort bmsAddress = 0x116;
            var registers = new List<(byte Register, byte RegisterLength)> 
            {
                (0, 4),
                (7, 1),
                (8, 6),
                (9, 4), // battery voltage
                (10, 4),
                (13, 1), // battery percent
                (14, 4),
                (15, 4),
                (16, 4),
                (17, 2),
                (20, 4),
                (21, 64), // length unknown
                (22, 9),
                (23, 4),
                (24, 4),
                (25, 4),
                (26, 8),
                (27, 4),
                (28, 4),
                (29, 6),
                (30, 6),
                (32, 16),
                (33, 32),
                (34, 16),
                (35, 32),
                (36, 32), // cell voltages
                (37, 32),
                (38, 14),
                (39, 64), // length unknown
                (48, 64), // length unknown
                (120, 64), // length unknown
                (160, 32) // length unknown
            };

            while (!cts.IsCancellationRequested)
            {
                foreach (var (register, registerLength) in registers)
                {
                    try
                    {
                        var response = await ReadRegister(sp, bmsAddress, register, registerLength, cts.Token);

                        if (!registerValues.TryGetValue(register, out var oldValue) || !oldValue.SequenceEqual(response))
                        {
                            registerValues[register] = response;
                            switch (register)
                            {
                                case 9:
                                    Console.WriteLine($"Battery Voltage: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000d:00.000}V");
                                    break;
                                case 13:
                                    Console.WriteLine($"Battery Percent: {response[0]}%");
                                    break;
                                case 36:
                                    var voltages = new List<double>();
                                    for (int batIdx = 0; batIdx < 16; batIdx++)
                                    {
                                        voltages.Add(BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(batIdx * 2, 2)) / 1000d);
                                    }
                                    Console.WriteLine($"Cell Voltages: {string.Join(' ', voltages.Select(x => $"{x:0.000}V"))}");
                                    break;
                                default:
                                    Console.WriteLine($"{register,3}: {HexUtils.BytesToHex(oldValue ?? [])} -> {HexUtils.BytesToHex(response)}");
                                    break;
                            }
                        }

                    }
                    catch (TimeoutException)
                    {
                        Console.WriteLine($"{register}: <Timeout>");
                    }
                }

                Console.WriteLine();
                await Task.Delay(1000);
            }

            sp.Close();
        }

        private static async Task<byte[]> ReadRegister(SerialPort sp, ushort address, byte parameter, byte paramLength, CancellationToken cancellationToken)
        {
            var sendPacket = SurronDataPacket.Create(SurronCmd.ReadRequest, address, parameter, paramLength, null);

            var toSend = sendPacket.ToBytes();

            var recvBuffer = new byte[1024];

            for (int sendRetryCounter = 0; ; sendRetryCounter++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                sp.DiscardInBuffer();
                Debug.WriteLine($">{HexUtils.BytesToHex(toSend)}");
                sp.Write(toSend, 0, toSend.Length);

                // response takes a bit, so we want to wait until a bit so we don't get an incomplete packet
                // at 9600 baud, we get around 960 bytes/s, so these 200ms are enough for ~192 bytes.
                // always waiting the full time works, but is slow so we just wait until we have our expected number of bytes 
                var expectedResponseLength = SurronDataPacket.GetLength(SurronCmd.ReadResponse, paramLength);
                for (int readWaitCount = 0; readWaitCount < 20; readWaitCount++)
                {
                    if (sp.BytesToRead >= expectedResponseLength)
                        break;
                    await Task.Delay(10, cancellationToken);
                }

                int readBytes;
                try
                {
                    readBytes = sp.Read(recvBuffer, 0, recvBuffer.Length);
                }
                catch (TimeoutException)
                {
                    if (sendRetryCounter >= 3)
                        throw;
                    continue;
                }
                if (readBytes > 0)
                {
                    Debug.WriteLine($"<{HexUtils.BytesToHex(recvBuffer[0..readBytes])}");
                    try
                    {
                        var packet = SurronDataPacket.FromBytes(recvBuffer[0..readBytes]);
                        if (packet.Address == address && packet.Parameter == parameter && packet.DataLength == paramLength)
                            return packet.CommandData!;
                        Debug.WriteLine($"Wrong Packet {packet}");
                    }
                    catch (ArgumentException)
                    {
                        Debug.WriteLine($"Invalid Response {HexUtils.BytesToHex(recvBuffer[0..readBytes])}");
                    }
                }
                await Task.Delay(100, cancellationToken);
            }
        }
    }
}
