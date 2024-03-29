using SurronCommunication;
using SurronCommunication.Communication;
using SurronCommunication.Parameter;
using System.Buffers.Binary;
using System.CommandLine;
using System.Globalization;
using System.Text;

namespace SurronCommunication_BmsDemo
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var rootCommand = new RootCommand("Surron RS485 BMS data reader");

            var serialPortOption = new Option<string>("--serialPort", "The serial port to use for RS485 communication. \"{dummy}\" for random data") { IsRequired = true };
            rootCommand.AddOption(serialPortOption);

            var readIntervalOption = new Option<int?>("--readInterval", "Read data every ~x ms instead of once");
            rootCommand.AddOption(readIntervalOption);

            var onlyChangesOption = new Option<bool>("--onlyChanges", "Only log parameters that have changed");
            rootCommand.AddOption(onlyChangesOption);

            var logTextOption = new Option<string?>("--logText", "Log output to file in text form. \"{date}\" can be used to insert the current date and time.");
            rootCommand.AddOption(logTextOption);

            var logHexOption = new Option<string?>("--logHex", "Log output to file in hex form. \"{date}\" can be used to insert the current date and time.");
            rootCommand.AddOption(logHexOption);

            var allParameters = BmsParameters.GetAll();
            var defaultParameters = new[]
            {
                BmsParameters.Parameters.Temperatures,
                BmsParameters.Parameters.BatteryVoltage,
                BmsParameters.Parameters.BatteryCurrent,
                BmsParameters.Parameters.BatteryPercent,
                BmsParameters.Parameters.BatteryHealth,
                BmsParameters.Parameters.RemainingCapacity,
                BmsParameters.Parameters.TotalCapacity,
                BmsParameters.Parameters.Statistics,
                BmsParameters.Parameters.ChargeCycles,
                BmsParameters.Parameters.DesignedCapacity,
                BmsParameters.Parameters.ManufacturingDate,
                BmsParameters.Parameters.BmsManufacturer,
                BmsParameters.Parameters.BatteryModel,
                BmsParameters.Parameters.CellType,
                BmsParameters.Parameters.SerialNumber,
                BmsParameters.Parameters.CellVoltages1,
                BmsParameters.Parameters.CellVoltages2,
                BmsParameters.Parameters.History
            };
            var parametersOption = new Option<string[]>(
                "--parameter",
                () => allParameters.Where(x => defaultParameters.Any(y => x == y)).Select(x => x.ToString()).ToArray(),
                $"Parameters to read from BMS (number / name). Known parameters: {string.Join(", ", allParameters.Select(x => $"{x.ToString()}({(byte)x})"))}");
            rootCommand.AddOption(parametersOption);

            rootCommand.SetHandler(async context =>
            {
                var serialPort = context.ParseResult.GetValueForOption(serialPortOption)!;
                var readInterval = context.ParseResult.GetValueForOption(readIntervalOption);

                var argParameters = context.ParseResult.GetValueForOption(parametersOption)!;
                var parameters = argParameters.Select(x => GetParamByString(x)).ToList();

                var logOnlyChanges = context.ParseResult.GetValueForOption(onlyChangesOption);
                var logFileText = context.ParseResult.GetValueForOption(logTextOption);
                var logFileHex = context.ParseResult.GetValueForOption(logHexOption);

                await Run(serialPort, parameters, readInterval, logOnlyChanges, logFileText, logFileHex, context.GetCancellationToken());
            });

            await rootCommand.InvokeAsync(args);
        }

        private static async Task Run(string serialPort, List<BmsParameters.Parameters> registers, int? readInterval, bool logOnlyChanges, string? logFileText, string? logFileHex, CancellationToken cancellationToken)
        {
            const ushort bmsAddress = BmsParameters.BmsAddress;

            var logger = new Logger(logOnlyChanges, logFileText, logFileHex, readInterval.HasValue && !logOnlyChanges, GetRegisterFormatHandlers());

            var registerValues = new Dictionary<byte, byte[]>();

            using var communicationHandler = GetCommunicationHandler(serialPort);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    logger.BeginTransmission();
                    foreach (var register in registers)
                    {
                        var registerId = (byte)register;
                        var registerLength = BmsParameters.GetLength(register);

                        var newValue = communicationHandler.ReadRegister(bmsAddress, registerId, registerLength, cancellationToken);

                        if (newValue == null)
                        {
                            logger.LogParameterTimeout(registerId);
                            continue;
                        }
                        registerValues.TryGetValue(registerId, out var oldValue);
                        registerValues[registerId] = newValue;

                        logger.LogParameter(registerId, oldValue, newValue);
                    }
                    if (!readInterval.HasValue)
                        break;
                    await Task.Delay(readInterval.Value, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private static ISurronCommunicationHandler GetCommunicationHandler(string serialPort)
        {
            if (serialPort == "{dummy}")
            {
                return new DummySurronCommunicationHandler();
            }
            else
            {
                return SurronCommunicationHandler.FromSerialPort(serialPort);
            }
        }

        private static Dictionary<byte, Func<byte[], string>> GetRegisterFormatHandlers()
        {
            var registerFormatHandlers = new Dictionary<BmsParameters.Parameters, Func<byte[], string>>
            {
                { BmsParameters.Parameters.Temperatures, response => $"Temperatures: Cells: {string.Join(' ', response[0..3].Select(x => $"{(sbyte)x,3:0}°C"))} Discharge FET: {(sbyte)response[4],3:0}°C Charge FET: {(sbyte)response[5],3:0}°C Soft Start Circuit: {(sbyte)response[6],3:0}°C"},
                { BmsParameters.Parameters.BatteryVoltage, response => $"Battery Voltage: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m:00.000}V"},
                { BmsParameters.Parameters.BatteryCurrent, response => $"Battery Current: {BinaryPrimitives.ReadInt32LittleEndian(response) / 1000m,8:#00.000}A"},
                { BmsParameters.Parameters.BatteryPercent, response => $"Battery Percent: {response[0],3}%"},
                { BmsParameters.Parameters.BatteryHealth, response => $"Battery Health: {response[0],3}%"},
                { BmsParameters.Parameters.RemainingCapacity, response => $"Remaining Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m,6:0.000}Ah"},
                { BmsParameters.Parameters.TotalCapacity, response => $"Total Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m,6:0.000}Ah"},
                { BmsParameters.Parameters.Statistics, response =>
                {
                    return
                        $"Total Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(0, 4)) / 1000m,6:0.000}Ah " +
                        $"Lifetime Charged Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(4, 4)) / 1000m,10:0.000}Ah " +
                        $"Current Cycle Charged Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(8, 4)) / 1000m,6:0.000}Ah";
                }},
                { BmsParameters.Parameters.ChargeCycles, response => $"Charge Cycles: {BinaryPrimitives.ReadUInt32LittleEndian(response),4}"},
                { BmsParameters.Parameters.DesignedCapacity, response => $"Designed Capacity: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m,6:0.000}Ah"},
                { BmsParameters.Parameters.DesignedVoltage, response => $"Designed Voltage: {BinaryPrimitives.ReadUInt32LittleEndian(response) / 1000m:00.000}V"},
                { BmsParameters.Parameters.Versions, response => $"SW Version: {new Version(response[1], response[0])} HW Version: {new Version(response[3], response[2])} IDX: {AsciiToString(response.AsSpan(4, 4))}"},
                { BmsParameters.Parameters.ManufacturingDate, response => $"Manufacturing Date: {new DateOnly(2000 + response[0], response[1], response[2]):yyyy'-'MM'-'dd}"},
                { BmsParameters.Parameters.RtcTime, response => $"RTC Time: {new DateTime(2000 + response[0], response[1], response[2], response[3], response[4], response[5]):s}"},
                { BmsParameters.Parameters.BmsManufacturer, response => $"BMS Manufacturer: {AsciiToString(response)}" },
                { BmsParameters.Parameters.BatteryModel, response => $"Battery Model: {AsciiToString(response)}" },
                { BmsParameters.Parameters.CellType, response => $"Cell Type: {AsciiToString(response)}" },
                { BmsParameters.Parameters.SerialNumber, response => $"Serial Number: {AsciiToString(response)}" },
                { BmsParameters.Parameters.CellVoltages1, response =>
                    {
                        var voltages = new List<decimal>();
                        for (int batIdx = 0; batIdx < 16; batIdx++)
                        {
                            voltages.Add(BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(batIdx * 2, 2)) / 1000m);
                        }
                        return $"Cell Voltages 1: {string.Join(' ', voltages.Select(x => $"{x:0.000}V"))}";
                    }
                },
                { BmsParameters.Parameters.CellVoltages2, response =>
                    {
                        var voltages = new List<decimal>();
                        for (int batIdx = 0; batIdx < 16; batIdx++)
                        {
                            voltages.Add(BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(batIdx * 2, 2)) / 1000m);
                        }
                        return $"Cell Voltages 2: {string.Join(' ', voltages.Select(x => $"{x:0.000}V"))}";
                    }
                },
                { BmsParameters.Parameters.History, response =>
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
            return registerFormatHandlers.ToDictionary(x => (byte)x.Key, x => x.Value);
        }

        private static string AsciiToString(Span<byte> bytes)
        {
            var nulIdx = bytes.IndexOf<byte>(0);
            return Encoding.ASCII.GetString(bytes.Slice(0, nulIdx > -1 ? nulIdx : bytes.Length));
        }

        private static BmsParameters.Parameters GetParamByString(string str)
        {
            if (Enum.TryParse<BmsParameters.Parameters>(str.Trim(), out var parsed))
            {
                return parsed;
            }
            else
            {
                throw new ArgumentException($"String \"{str}\" is not a valid BMS parameter");
            }
        }

        private class Logger : IDisposable
        {
            private readonly bool _logOnlyChanges;
            private readonly bool _alwaysWriteOnTop;
            private readonly Dictionary<byte, Func<byte[], string>> _formatHandlers;
            private readonly StreamWriter? _logFileText;
            private readonly StreamWriter? _logFileHex;

            public Logger(bool logOnlyChanges, string? logFileText, string? logFileHex, bool alwaysWriteOnTop, Dictionary<byte, Func<byte[], string>> formatHandlers)
            {
                _logOnlyChanges = logOnlyChanges;
                _alwaysWriteOnTop = alwaysWriteOnTop;
                if (_logOnlyChanges && _alwaysWriteOnTop)
                    throw new ArgumentException("LogOnlyChanges and AlwaysWriteOnTop can not be used together");
                if (_alwaysWriteOnTop)
                    Console.Clear();
                _formatHandlers = formatHandlers;
                var dt = DateTime.Now;

                if (logFileText != null)
                    _logFileText = new StreamWriter(File.Open(logFileText.Replace("{date}", dt.ToString("yyyy'-'MM'-'dd'_'HH'-'mm'-'ss")), FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);
                if (logFileHex != null)
                    _logFileHex = new StreamWriter(File.Open(logFileHex.Replace("{date}", dt.ToString("yyyy'-'MM'-'dd'_'HH'-'mm'-'ss")), FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);
            }

            public void Dispose()
            {
                _logFileText?.Dispose();
                _logFileHex?.Dispose();
            }

            public void BeginTransmission()
            {
                var header = $"---{DateTime.Now:yyyy'-'MM'-'dd' 'HH'-'mm'-'ss}---";
                if (_alwaysWriteOnTop)
                {
                    Console.SetCursorPosition(0, 0);
                }
                else
                {
                    Console.WriteLine();
                }

                Console.WriteLine(header);
                _logFileHex?.WriteLine();
                _logFileHex?.WriteLine(header);

                _logFileText?.WriteLine();
                _logFileText?.WriteLine(header);
            }

            public void LogParameter(byte register, byte[]? oldValue, byte[] newValue)
            {
                if (_logOnlyChanges && oldValue != null && oldValue.SequenceEqual(newValue))
                    return;

                if (_formatHandlers.TryGetValue(register, out var formatHandler))
                {
                    string formatted;
                    try
                    {
                        formatted = formatHandler(newValue);
                    }
                    catch (Exception)
                    {
                        formatted = $"{register,3}: {HexUtils.BytesToHex(newValue)} (Invalid)";
                    }

                    Console.Write(formatted);
                    ConsoleFillAndWriteLine();
                    _logFileText?.WriteLine(formatted);
                }
                else
                {
                    ConsoleLogRawParameter(register, oldValue, newValue);
                    ConsoleFillAndWriteLine();
                    _logFileText?.WriteLine($"{register,3}: {HexUtils.BytesToHex(newValue)}");
                }
                _logFileHex?.WriteLine($"{register,3}: {HexUtils.BytesToHex(newValue)}");

                _logFileText?.Flush();
                _logFileHex?.Flush();
            }

            public void LogParameterTimeout(byte register)
            {
                var text = $"{register,3}: <Timeout>";
                Console.Write(text);
                ConsoleFillAndWriteLine();
                _logFileHex?.WriteLine(text);
                _logFileText?.WriteLine(text);
            }

            private static void ConsoleLogRawParameter(byte register, byte[]? oldValue, byte[] newValue)
            {
                Console.Write($"{register,3}: ");
                for (var i = 0; i < newValue.Length; i++)
                {
                    var highlight = oldValue == null || oldValue[i] != newValue[i];
                    if (highlight)
                    {
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.BackgroundColor = ConsoleColor.Green;
                    }
                    Console.Write(newValue[i].ToString("X2"));
                    if (highlight)
                    {
                        Console.ResetColor();
                    }
                }
            }

            private void ConsoleFillAndWriteLine()
            {
                if (!_alwaysWriteOnTop)
                {
                    Console.WriteLine();
                    return;
                }

                int toFill;
                try
                {
                    toFill = Console.BufferWidth - Console.CursorLeft - 1;
                }
                catch (Exception)
                {
                    toFill = 10;
                }

                Console.Write(new string(' ', toFill));
                Console.WriteLine();

            }
        }
    }
}
