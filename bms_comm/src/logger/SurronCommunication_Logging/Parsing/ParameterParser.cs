﻿using System;
using System.Buffers.Binary;
using System.Text;
#if NANOFRAMEWORK_1_0
using LabelCollection = System.Collections.Hashtable;
#else
using LabelCollection = System.Collections.Generic.Dictionary<string, string>;
using SpanByte = System.Span<byte>;
#endif


namespace SurronCommunication.Parameter.Parsing
{
    public static class ParameterParser
    {
        private static readonly string[] _cellLabelValues;

        static ParameterParser()
        {
            _cellLabelValues = new string[32];
            for (int i = 0; i < 32; i++)
            {
                _cellLabelValues[i] = $"cell{(i + 1).ToString().PadLeft(2, '0')}";
            }
        }

        public static DataPoint[] GetDataPointsForParameter(ParameterType parameterType, byte parameter, byte[] data)
        {
            if (parameterType == ParameterType.Bms)
            {
                switch ((BmsParameterId)parameter)
                {
                    case BmsParameterId.Temperatures:
                        return new[]
                        {
                            new DataPoint("temperature", "sensor", "cells1", "value", (double)(sbyte)data[0]),
                            new DataPoint("temperature", "sensor", "cells2", "value", (double)(sbyte)data[1]),
                            new DataPoint("temperature", "sensor", "cells3", "value", (double)(sbyte)data[2]),
                            // new DataPoint("temperature", "sensor", "cells4", "value", (double)(sbyte)data[3]), always 0
                            new DataPoint("temperature", "sensor", "dischargeFet", "value", (double)(sbyte)data[4]),
                            new DataPoint("temperature", "sensor", "chargeFet", "value", (double)(sbyte)data[5]),
                            new DataPoint("temperature", "sensor", "softStart", "value", (double)(sbyte)data[6]),
                        };
                    case BmsParameterId.BatteryVoltage:
                        return new[] { new DataPoint("batteryVoltage", null, null, "measured", BinaryPrimitives.ReadUInt32LittleEndian(data) / 1000d) };
                    case BmsParameterId.BatteryCurrent:
                        return new[] { new DataPoint("batteryCurrent", null, null, "value", BinaryPrimitives.ReadInt32LittleEndian(data) / 1000d) };
                    case BmsParameterId.BatteryPercent:
                        return new[] { new DataPoint("batteryPercent", null, null, "value", data[0]) };
                    case BmsParameterId.BatteryHealth:
                        return new[] { new DataPoint("batteryHealth", null, null, "value", data[0]) };
                    case BmsParameterId.RemainingCapacity:
                        return new[] { new DataPoint("batteryCapacity", null, null, "remaining", BinaryPrimitives.ReadUInt32LittleEndian(data) / 1000d) };
                    case BmsParameterId.TotalCapacity:
                        return new[] { new DataPoint("batteryCapacity", null, null, "total", BinaryPrimitives.ReadUInt32LittleEndian(data) / 1000d) };
                    case BmsParameterId.Statistics:
                        {
                            // duplicate
                            //new DataPoint("batteryCapacity", null, "total", BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0, 4)) / 1000d, "Ah");
                            var keys = new[] { "lifetime", "cycle" };
                            var values = new object[] { BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4)) / 1000d, BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8, 4)) / 1000d };

                            return new[] { new DataPoint("batteryCapacityAccumulated", null, null, keys, values) };
                        }
                    case BmsParameterId.BmsStatus:
                        return new[] { new DataPoint("bmsStatus", null, null, "value", HexUtils.BytesToHex(data)) };
                    case BmsParameterId.ChargeCycles:
                        return new[] { new DataPoint("chargeCycles", null, null, "value", BinaryPrimitives.ReadUInt32LittleEndian(data)) };
                    case BmsParameterId.DesignedCapacity:
                        return new[] { new DataPoint("batteryCapacity", null, null, "designed", BinaryPrimitives.ReadUInt32LittleEndian(data) / 1000d) };
                    case BmsParameterId.DesignedVoltage:
                        return new[] { new DataPoint("batteryVoltage", null, null, "designed", BinaryPrimitives.ReadUInt32LittleEndian(data) / 1000d) };
                    case BmsParameterId.Versions:
                        return new[]
                        {
                            new DataPoint("manufacturingData", null, null, "swVersion", new Version(data[1], data[0]).ToString()),
                            new DataPoint("manufacturingData", null, null, "hwVersion", new Version(data[3], data[2]).ToString()),
                            new DataPoint("manufacturingData", null, null, "fwIdx", AsciiToString(data, 4, 4)),
                        };
                    case BmsParameterId.ManufacturingDate:
                        return new[] { new DataPoint("manufacturingData", null, null, "manufactureDate", new DateTime(2000 + data[0], data[1], data[2]).ToString("yyyy'-'MM'-'dd")) };
                    case BmsParameterId.RtcTime:
                        return new[] { new DataPoint("rtcTime", null, null, "value", new DateTime(2000 + data[0], data[1], data[2], data[3], data[4], data[5]).ToString("s")) };
                    case BmsParameterId.BmsManufacturer:
                        return new[] { new DataPoint("manufacturingData", null, null, "bmsManufacturer", AsciiToString(data)) };
                    case BmsParameterId.BatteryModel:
                        return new[] { new DataPoint("manufacturingData", null, null, "batteryModel", AsciiToString(data)) };
                    case BmsParameterId.CellType:
                        return new[] { new DataPoint("manufacturingData", null, null, "cellType", AsciiToString(data)) };
                    case BmsParameterId.SerialNumber:
                        return new[] { new DataPoint("manufacturingData", null, null, "serialNumber", AsciiToString(data)) };
                    case BmsParameterId.CellVoltages1:
                        {
                            var toReturn = new DataPoint[16];
                            for (int batIdx = 0; batIdx < 16; batIdx++)
                            {
                                var cellVoltage = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(batIdx * 2, 2)) / 1000d;
                                toReturn[batIdx] = new DataPoint("cellVoltage", "cell", _cellLabelValues[batIdx], "value", cellVoltage);
                            }
                            return toReturn;
                        }
                    case BmsParameterId.CellVoltages2:
                        {
                            var toReturn = new DataPoint[16];
                            for (int batIdx = 0; batIdx < 16; batIdx++)
                            {
                                var cellVoltage = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(batIdx * 2, 2)) / 1000d;
                                toReturn[batIdx] = new DataPoint("cellVoltage", "cell", _cellLabelValues[batIdx + 16], "value", cellVoltage);
                            }
                            return toReturn;
                        }
                    case BmsParameterId.History:
                        {
                            var keys = new[]
                            {
                                "currentOutMax",
                                "currentInMax",
                                "voltageCellMax",
                                "voltageCellMin",
                                "temperatureMax",
                                "temperatureMin"
                            };

                            var values = new object[]
                            {
                                BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0, 4)) / 1000d,
                                BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4, 4)) / 1000d,
                                BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8, 2)) / 1000d,
                                BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(10, 2)) / 1000d,
                                (double)(sbyte)data[12],
                                (double)(sbyte)data[13]
                            };

                            return new[] { new DataPoint("history", null, null, keys, values) };
                        }
                    default:
                        return new[] { new DataPoint($"bmsUnknown", null, null, $"unknown_{parameter}", HexUtils.BytesToHex(data)) };
                }
            }
            else if (parameterType == ParameterType.Esc)
            {
                switch ((EscParameterId)parameter)
                {
                    case EscParameterId.Unknown_72:
                    case EscParameterId.Unknown_75:
                    default:
                        return new[] { new DataPoint($"escUnknown", null, null, $"unknown_{parameter}", HexUtils.BytesToHex(data)) };
                }
            }
            else
            {
                throw new ArgumentException($"Invalid parameterType {parameter}", nameof(parameter));
            }
        }

        private static string AsciiToString(byte[] bytes, int offset = 0, int count = -1)
        {
            if (count == -1)
                count = bytes.Length;
            for (int i = 0; i < count; i++)
            {
                if (bytes[offset + i] == 0)
                    return Encoding.UTF8.GetString(bytes, offset, i);
            }

            return Encoding.UTF8.GetString(bytes, offset, count);
        }
    }
}
