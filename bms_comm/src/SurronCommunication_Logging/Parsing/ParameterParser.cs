using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace SurronCommunication.Parameter.Parsing
{
    public class ParameterParser
    {
        public IEnumerable<DataPoint> ParseParameter(ParameterType parameterType, byte parameter, byte[] data)
        {
            if (parameterType == ParameterType.Bms)
            {
                switch ((BmsParameters.Parameters)parameter)
                {
                    case BmsParameters.Parameters.Temperatures:
                        yield return new DataPoint("temperature", new Dictionary<string, string> { { "sensor", "cells1" } }, "value", (decimal)(sbyte)data[0], "°C");
                        yield return new DataPoint("temperature", new Dictionary<string, string> { { "sensor", "cells2" } }, "value", (decimal)(sbyte)data[1], "°C");
                        yield return new DataPoint("temperature", new Dictionary<string, string> { { "sensor", "cells3" } }, "value", (decimal)(sbyte)data[2], "°C");
                        // always 0
                        // yield return new DataPoint("temperature", new Dictionary<string, string> { { "sensor", "cells4" } }, "value", (decimal)(sbyte)data[3], "°C");
                        yield return new DataPoint("temperature", new Dictionary<string, string> { { "sensor", "dischargeFet" } }, "value", (decimal)(sbyte)data[4], "°C");
                        yield return new DataPoint("temperature", new Dictionary<string, string> { { "sensor", "chargeFet" } }, "value", (decimal)(sbyte)data[5], "°C");
                        yield return new DataPoint("temperature", new Dictionary<string, string> { { "sensor", "softStart" } }, "value", (decimal)(sbyte)data[6], "°C");
                        break;
                    case BmsParameters.Parameters.BatteryVoltage:
                        yield return new DataPoint("batteryVoltage", [], "measured", BinaryPrimitives.ReadUInt32LittleEndian(data) / 1000m, "V");
                        break;
                    case BmsParameters.Parameters.BatteryCurrent:
                        yield return new DataPoint("batteryCurrent", [], "value", BinaryPrimitives.ReadInt32LittleEndian(data) / 1000m, "A");
                        break;
                    case BmsParameters.Parameters.BatteryPercent:
                        yield return new DataPoint("batteryPercent", [], "value", data[0], "%");
                        break;
                    case BmsParameters.Parameters.BatteryHealth:
                        yield return new DataPoint("batteryHealth", [], "value", data[0], "%");
                        break;
                    case BmsParameters.Parameters.RemainingCapacity:
                        yield return new DataPoint("batteryCapacity", [], "remaining", BinaryPrimitives.ReadUInt32LittleEndian(data) / 1000m, "Ah");
                        break;
                    case BmsParameters.Parameters.TotalCapacity:
                        yield return new DataPoint("batteryCapacity", [], "total", BinaryPrimitives.ReadUInt32LittleEndian(data) / 1000m, "Ah");
                        break;
                    case BmsParameters.Parameters.Statistics:
                        // duplicate
                        //yield return new DataPoint("batteryCapacity", [], "total", BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0, 4)) / 1000m, "Ah");
                        yield return new DataPoint("batteryCapacityAccumulated", [], "lifetime", BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4)) / 1000m, "Ah");
                        yield return new DataPoint("batteryCapacityAccumulated", [], "cycle", BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8, 4)) / 1000m, "Ah");
                        break;
                    case BmsParameters.Parameters.BmsStatus:
                        yield return new DataPoint("bmsStatus", [], "value", HexUtils.BytesToHex(data));
                        break;
                    case BmsParameters.Parameters.ChargeCycles:
                        yield return new DataPoint("chargeCycles", [], "value", BinaryPrimitives.ReadUInt32LittleEndian(data));
                        break;
                    case BmsParameters.Parameters.DesignedCapacity:
                        yield return new DataPoint("batteryCapacity", [], "designed", BinaryPrimitives.ReadUInt32LittleEndian(data) / 1000m, "Ah");
                        break;
                    case BmsParameters.Parameters.DesignedVoltage:
                        yield return new DataPoint("batteryVoltage", [], "designed", BinaryPrimitives.ReadUInt32LittleEndian(data) / 1000m, "V");
                        break;
                    case BmsParameters.Parameters.Versions:
                        yield return new DataPoint("manufacturingData", [], "swVersion", new Version(data[1], data[0]).ToString());
                        yield return new DataPoint("manufacturingData", [], "hwVersion", new Version(data[3], data[2]).ToString());
                        yield return new DataPoint("manufacturingData", [], "fwIdx", AsciiToString(data.AsSpan(4, 4)));
                        break;
                    case BmsParameters.Parameters.ManufacturingDate:
                        yield return new DataPoint("manufacturingData", [], "manufactureDate", new DateOnly(2000 + data[0], data[1], data[2]).ToString("yyyy'-'MM'-'dd"));
                        break;
                    case BmsParameters.Parameters.RtcTime:
                        yield return new DataPoint("rtcTime", [], "value", new DateTime(2000 + data[0], data[1], data[2], data[3], data[4], data[5]).ToString("s"));
                        break;
                    case BmsParameters.Parameters.BmsManufacturer:
                        yield return new DataPoint("manufacturingData", [], "bmsManufacturer", AsciiToString(data));
                        break;
                    case BmsParameters.Parameters.BatteryModel:
                        yield return new DataPoint("manufacturingData", [], "batteryModel", AsciiToString(data));
                        break;
                    case BmsParameters.Parameters.CellType:
                        yield return new DataPoint("manufacturingData", [], "cellType", AsciiToString(data));
                        break;
                    case BmsParameters.Parameters.SerialNumber:
                        yield return new DataPoint("manufacturingData", [], "serialNumber", AsciiToString(data));
                        break;
                    case BmsParameters.Parameters.CellVoltages1:
                        {
                            for (int batIdx = 0; batIdx < 16; batIdx++)
                            {
                                var cellVoltage = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(batIdx * 2, 2)) / 1000m;
                                yield return new DataPoint("cellVoltage", new Dictionary<string, string> { { "cell", $"cell{batIdx + 1:00}" } }, "value", cellVoltage, "V");
                            }
                            break;
                        }
                    case BmsParameters.Parameters.CellVoltages2:
                        {
                            for (int batIdx = 0; batIdx < 16; batIdx++)
                            {
                                var cellVoltage = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(batIdx * 2, 2)) / 1000m;
                                yield return new DataPoint("cellVoltage", new Dictionary<string, string> { { "cell", $"cell{batIdx + 1 + 16:00}" } }, "value", cellVoltage, "V");
                            }
                            break;
                        }
                    case BmsParameters.Parameters.History:
                        yield return new DataPoint("history", [], $"currentOutMax", BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0, 4)) / 1000m, "A");
                        yield return new DataPoint("history", [], $"currentInMax", BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4, 4)) / 1000m, "A");
                        yield return new DataPoint("history", [], $"voltageCellMax", BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8, 2)) / 1000m, "V");
                        yield return new DataPoint("history", [], $"voltageCellMin", BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(10, 2)) / 1000m, "V");
                        yield return new DataPoint("history", [], $"temperatureMax", (decimal)(sbyte)data[12], "°C");
                        yield return new DataPoint("history", [], $"temperatureMin", (decimal)(sbyte)data[13], "°C");
                        break;
                    default:
                        yield return new DataPoint($"bmsUnknown", [], $"unknown_{parameter}", HexUtils.BytesToHex(data));
                        break;
                }
            }
            else if (parameterType == ParameterType.Esc)
            {
                switch ((EscParameters.Parameters)parameter)
                {
                    case EscParameters.Parameters.Unknown_72:
                    case EscParameters.Parameters.Unknown_75:
                    default:
                        yield return new DataPoint($"escUnknown", [], $"unknown_{parameter}", HexUtils.BytesToHex(data));
                        break;
                }
            }
            else
            {
                throw new ArgumentException($"Invalid parameterType {parameter}", nameof(parameter));
            }
        }

        private static string AsciiToString(Span<byte> bytes)
        {
            var nulIdx = bytes.IndexOf<byte>(0);
            return Encoding.ASCII.GetString(bytes.Slice(0, nulIdx > -1 ? nulIdx : bytes.Length));
        }
    }
}
