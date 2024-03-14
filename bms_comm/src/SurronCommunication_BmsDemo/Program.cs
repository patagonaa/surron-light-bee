using SurronCommunication.Communication;
using SurronCommunication.Parameter;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace SurronCommunication_BmsDemo
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (o, args) => { cts.Cancel(); args.Cancel = true; };

            const ushort bmsAddress = BmsParameters.BmsAddress;
            var registers = new List<ParameterDefinition>
            {
                new(0, 4),
                new(7, 1),
                BmsParameters.Temperatures,
                BmsParameters.BatteryVoltage,
                BmsParameters.BatteryCurrent,
                BmsParameters.BatteryPercent,
                BmsParameters.BatteryHealth,
                BmsParameters.RemainingCapacity,
                BmsParameters.TotalCapacity,
                new(17, 2),
                new(20, 4),
                BmsParameters.Statistics,
                BmsParameters.BmsStatus,
                BmsParameters.ChargeCycles,
                BmsParameters.DesignedCapacity,
                BmsParameters.DesignedVoltage,
                BmsParameters.Versions,
                BmsParameters.ManufacturingDate,
                new(28, 4),
                BmsParameters.RtcTime,
                new(30, 6),
                BmsParameters.BmsManufacturer,
                BmsParameters.BatteryModel,
                BmsParameters.CellType,
                BmsParameters.SerialNumber,
                BmsParameters.CellVoltages1,
                BmsParameters.CellVoltages2,
                BmsParameters.HistoryValues,
                new(39, 64), // length unknown
                new(48, 64), // length unknown
                new(120, 64), // length unknown
                new(160, 32) // length unknown
            };

            using var logger = new Logger();

            var registerFormatHandlers = new Dictionary<byte, Func<byte[], string>>
            {
                { BmsParameters.Temperatures.Id, response => $"Temperatures: Cells: {string.Join(' ', response[0..3].Select(x => $"{(sbyte)x,3:0}°C"))} Discharge FET: {(sbyte)response[4],3:0}°C Charge FET: {(sbyte)response[5],3:0}°C Soft Start Circuit: {(sbyte)response[6],3:0}°C"},
                { BmsParameters.BatteryVoltage.Id, response => $"Battery Voltage: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m:00.000}V"},
                { BmsParameters.BatteryCurrent.Id, response => $"Battery Current: {BinaryPrimitives.ReadInt32LittleEndian(response) / 1000m,8:#00.000}A"},
                { BmsParameters.BatteryPercent.Id, response => $"Battery Percent: {response[0],3}%"},
                { BmsParameters.BatteryHealth.Id, response => $"Battery Health: {response[0],3}%"},
                { BmsParameters.RemainingCapacity.Id, response => $"Remaining Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m,6:0.000}Ah"},
                { BmsParameters.TotalCapacity.Id, response => $"Total Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m,6:0.000}Ah"},
                { BmsParameters.Statistics.Id, response =>
                {
                    return
                        $" Total Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(0, 4)) / 1000m,6:0.000}Ah " +
                        $" Lifetime Charged Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(4, 4)) / 1000m,10:0.000}Ah " +
                        $" Current Charge Session Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(8, 4)) / 1000m,6:0.000}Ah";
                }},
                { BmsParameters.ChargeCycles.Id, response => $"Charge Cycles: {BinaryPrimitives.ReadUInt32LittleEndian(response),4}"},
                { BmsParameters.DesignedCapacity.Id, response => $"Designed Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m,6:0.000}Ah"},
                { BmsParameters.DesignedVoltage.Id, response => $"Designed Voltage: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m:00.000}V"},
                { BmsParameters.Versions.Id, response => $"SW Version: {new Version(response[1], response[0])} HW Version: {new Version(response[3], response[2])} IDX: {AsciiToString(response.AsSpan(4, 4))}"},
                { BmsParameters.ManufacturingDate.Id, response => $"Manufacturing Date: {new DateOnly(2000 + response[0], response[1], response[2]):yyyy'-'MM'-'dd}"},
                { BmsParameters.RtcTime.Id, response => $"RTC Time: {new DateTime(2000 + response[0], response[1], response[2], response[3], response[4], response[5]):s}"},
                { BmsParameters.BmsManufacturer.Id, response => $"BMS Manufacturer: {AsciiToString(response)}" },
                { BmsParameters.BatteryModel.Id, response => $"Battery Model: {AsciiToString(response)}" },
                { BmsParameters.CellType.Id, response => $"Cell Type: {AsciiToString(response)}" },
                { BmsParameters.SerialNumber.Id, response => $"Serial Number: {AsciiToString(response)}" },
                { BmsParameters.CellVoltages1.Id, response =>
                    {
                        var voltages = new List<decimal>();
                        for (int batIdx = 0; batIdx < 16; batIdx++)
                        {
                            voltages.Add(BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(batIdx * 2, 2)) / 1000m);
                        }
                        return $"Cell Voltages 1: {string.Join(' ', voltages.Select(x => $"{x:0.000}V"))}";
                    }
                },
                { BmsParameters.CellVoltages2.Id, response =>
                    {
                        var voltages = new List<decimal>();
                        for (int batIdx = 0; batIdx < 16; batIdx++)
                        {
                            voltages.Add(BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(batIdx * 2, 2)) / 1000m);
                        }
                        return $"Cell Voltages 2: {string.Join(' ', voltages.Select(x => $"{x:0.000}V"))}";
                    }
                },
                { BmsParameters.HistoryValues.Id, response =>
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

            using var communicationHandler = SurronCommunicationHandler.FromSerialPort("COM8");

            try
            {
                while (true)
                {
                    logger.WriteLine($"{DateTime.Now:s}:");
                    foreach (var (register, registerLength) in registers)
                    {
                        try
                        {
                            var response = await communicationHandler.ReadRegister(bmsAddress, register, registerLength, cts.Token);

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
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
            }
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
    }
}
