using SurronBms.Common;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text;

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
            var registers = new List<ParameterDefinition>
            {
                new(0, 4),
                new(7, 1),
                KnownBmsParameters.Temperatures,
                KnownBmsParameters.BatteryVoltage,
                KnownBmsParameters.BatteryCurrent,
                KnownBmsParameters.BatteryPercent,
                KnownBmsParameters.BatteryHealth,
                KnownBmsParameters.RemainingCapacity,
                KnownBmsParameters.TotalCapacity,
                new(17, 2),
                new(20, 4),
                KnownBmsParameters.Statistics,
                new(22, 9),
                KnownBmsParameters.ChargeCycles,
                KnownBmsParameters.DesignedCapacity,
                KnownBmsParameters.DesignedVoltage,
                KnownBmsParameters.Versions,
                KnownBmsParameters.ManufacturingDate,
                new(28, 4),
                KnownBmsParameters.RtcTime,
                new(30, 6),
                KnownBmsParameters.BmsManufacturer,
                KnownBmsParameters.BatteryModel,
                KnownBmsParameters.CellType,
                KnownBmsParameters.SerialNumber,
                KnownBmsParameters.CellVoltages,
                new(37, 32),
                KnownBmsParameters.HistoryValues,
                new(39, 64), // length unknown
                new(48, 64), // length unknown
                new(120, 64), // length unknown
                new(160, 32) // length unknown
            };

            using var logger = new Logger();

            var registerFormatHandlers = new Dictionary<byte, Func<byte[], string>>
            {
                { KnownBmsParameters.Temperatures.Id, response => $"Temperatures: {string.Join(' ', response.Select(x => $"{(sbyte)x,3:0}°C"))}"},
                { KnownBmsParameters.BatteryVoltage.Id, response => $"Battery Voltage: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m:00.000}V"},
                { KnownBmsParameters.BatteryCurrent.Id, response => $"Battery Current: {BinaryPrimitives.ReadInt32LittleEndian(response) / 1000m,8:#00.000}A"},
                { KnownBmsParameters.BatteryPercent.Id, response => $"Battery Percent: {response[0],3}%"},
                { KnownBmsParameters.BatteryHealth.Id, response => $"Battery Health: {response[0],3}%"},
                { KnownBmsParameters.RemainingCapacity.Id, response => $"Remaining Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m,6:0.000}Ah"},
                { KnownBmsParameters.TotalCapacity.Id, response => $"Total Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m,6:0.000}Ah"},
                { KnownBmsParameters.Statistics.Id, response =>
                {
                    return
                        $" Total Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(0, 4)) / 1000m,6:0.000}Ah " +
                        $" Lifetime Charged Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(4, 4)) / 1000m,10:0.000}Ah " +
                        $" Current Charge Session Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(8, 4)) / 1000m,6:0.000}Ah";
                }},
                { KnownBmsParameters.ChargeCycles.Id, response => $"Charge Cycles: {BinaryPrimitives.ReadUInt32LittleEndian(response),4}"},
                { KnownBmsParameters.DesignedCapacity.Id, response => $"Designed Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m,6:0.000}Ah"},
                { KnownBmsParameters.DesignedVoltage.Id, response => $"Designed Voltage: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m:00.000}V"},
                { KnownBmsParameters.Versions.Id, response => $"SW Version: {new Version(response[1], response[0])} HW Version: {new Version(response[3], response[2])} IDX: {AsciiToString(response.AsSpan(4, 4))}"},
                { KnownBmsParameters.ManufacturingDate.Id, response => $"Manufacturing Date: {new DateOnly(2000 + response[0], response[1], response[2]):yyyy'-'MM'-'dd}"},
                { KnownBmsParameters.RtcTime.Id, response => $"RTC Time: {new DateTime(2000 + response[0], response[1], response[2], response[3], response[4], response[5]):s}"},
                { KnownBmsParameters.BmsManufacturer.Id, response => $"BMS Manufacturer: {AsciiToString(response)}" },
                { KnownBmsParameters.BatteryModel.Id, response => $"Battery Model: {AsciiToString(response)}" },
                { KnownBmsParameters.CellType.Id, response => $"Cell Type: {AsciiToString(response)}" },
                { KnownBmsParameters.SerialNumber.Id, response => $"Serial Number: {AsciiToString(response)}" },
                { KnownBmsParameters.CellVoltages.Id, response =>
                    {
                        var voltages = new List<decimal>();
                        for (int batIdx = 0; batIdx < 16; batIdx++)
                        {
                            voltages.Add(BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(batIdx * 2, 2)) / 1000m);
                        }
                        return $"Cell Voltages: {string.Join(' ', voltages.Select(x => $"{x:0.000}V"))}";
                    }
                },
                { KnownBmsParameters.HistoryValues.Id, response =>
                    {
                        return
                            $"OutMax: {BinaryPrimitives.ReadInt32LittleEndian(response.AsSpan(0, 4)) / 1000m,7:#00.000}A " +
                            $"InMax: {BinaryPrimitives.ReadInt32LittleEndian(response.AsSpan(4, 4)) / 1000m,6:00.000}A " +
                            $"MaxCell: {BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(8, 2)) / 1000m,5:0.000}V " +
                            $"MinCell: {BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(10, 2)) / 1000m,5:0.000}V " +
                            $"MaxTemp: {(sbyte)response[12],3}°C " +
                            $"MinTemp: {(sbyte)response[13],3}°C";
                    }
                }
            };

            var registerValues = new Dictionary<byte, byte[]>();

            while (!cts.IsCancellationRequested)
            {
                logger.WriteLine($"{DateTime.Now:s}:");
                foreach (var (register, registerLength) in registers)
                {
                    try
                    {
                        var response = await ReadRegister(sp, bmsAddress, register, registerLength, cts.Token);

                        if (!registerValues.TryGetValue(register, out var oldValue) || !oldValue.SequenceEqual(response))
                        {
                            registerValues[register] = response;

                            if (registerFormatHandlers.TryGetValue(register, out var formatHandler))
                            {
                                logger.WriteLine(formatHandler(response));
                            }
                            else
                            {
                                logger.Write($"{register,3}: ");
                                for (var i = 0; i < response.Length; i++)
                                {
                                    logger.Write(response[i].ToString("X2"), oldValue != null && oldValue[i] != response[i]);
                                }
                                logger.WriteLine();
                            }
                        }

                    }
                    catch (TimeoutException)
                    {
                        logger.WriteLine($"{register}: <Timeout>");
                    }
                }
                logger.WriteLine();
                await Task.Delay(1000);
            }

            sp.Close();
        }

        private class Logger : IDisposable
        {
            private readonly StreamWriter _fileWriter;

            public Logger()
            {
                _fileWriter = new StreamWriter(File.Open($"Log_{DateTime.Now:yyyy'-'MM'-'dd'_'HH'-'mm'-'ss}.log", FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);
            }

            public void Write(string text, bool highlight = false)
            {
                if (highlight)
                {
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.BackgroundColor = ConsoleColor.Green;
                }
                Console.Write(text);
                if (highlight)
                    Console.ResetColor();
                _fileWriter.Write(text);
                _fileWriter.Flush();
            }

            public void WriteLine(string text = "")
            {
                Console.WriteLine(text);
                _fileWriter.WriteLine(text);
                _fileWriter.Flush();
            }

            public void Dispose()
            {
                _fileWriter.Dispose();
            }
        }

        private static string AsciiToString(Span<byte> bytes)
        {
            var nulIdx = bytes.IndexOf<byte>(0);
            return Encoding.ASCII.GetString(bytes.Slice(0, nulIdx > -1 ? nulIdx : bytes.Length));
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
