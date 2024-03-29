using System;

namespace SurronCommunication.Parameter
{
    public static class BmsParameters
    {
        public const ushort BmsAddress = 0x116;

        public enum Parameters : byte
        {
            Unknown_0 = 0,
            Unknown_7 = 7,
            Temperatures = 8,
            BatteryVoltage = 9,
            BatteryCurrent = 10,
            BatteryPercent = 13,
            BatteryHealth = 14,
            RemainingCapacity = 15,
            TotalCapacity = 16,
            Unknown_17 = 17,
            Unknown_20 = 20,
            Statistics = 21,
            BmsStatus = 22,
            ChargeCycles = 23,
            DesignedCapacity = 24,
            DesignedVoltage = 25,
            Versions = 26,
            ManufacturingDate = 27,
            Unknown_28 = 28,
            RtcTime = 29,
            Unknown_30 = 30,
            BmsManufacturer = 32,
            BatteryModel = 33,
            CellType = 34,
            SerialNumber = 35,
            CellVoltages1 = 36,
            CellVoltages2 = 37,
            History = 38,
            Unknown_39 = 39,
            Unknown_48 = 48,
            Unknown_120 = 120,
            Unknown_160 = 160,
        }

        public static byte GetLength(Parameters parameterId)
        {
            return parameterId switch
            {
                Parameters.Unknown_0 => 4,
                Parameters.Unknown_7 => 1,
                Parameters.Temperatures => 8,
                Parameters.BatteryVoltage => 4,
                Parameters.BatteryCurrent => 4,
                Parameters.BatteryPercent => 1,
                Parameters.BatteryHealth => 4,
                Parameters.RemainingCapacity => 4,
                Parameters.TotalCapacity => 4,
                Parameters.Unknown_17 => 2,
                Parameters.Unknown_20 => 4,
                Parameters.Statistics => 12,
                Parameters.BmsStatus => 10,
                Parameters.ChargeCycles => 4,
                Parameters.DesignedCapacity => 4,
                Parameters.DesignedVoltage => 4,
                Parameters.Versions => 8,
                Parameters.ManufacturingDate => 3,
                Parameters.Unknown_28 => 4,
                Parameters.RtcTime => 6,
                Parameters.Unknown_30 => 6,
                Parameters.BmsManufacturer => 16,
                Parameters.BatteryModel => 32,
                Parameters.CellType => 16,
                Parameters.SerialNumber => 32,
                Parameters.CellVoltages1 => 32,
                Parameters.CellVoltages2 => 32,
                Parameters.History => 14,
                Parameters.Unknown_39 => 64, // length unknown
                Parameters.Unknown_48 => 64, // length unknown
                Parameters.Unknown_120 => 64, // length unknown
                Parameters.Unknown_160 => 32, // length unknown
                _ => throw new ArgumentException($"unknown parameter {parameterId}")
            };
        }

#if !NANOFRAMEWORK_1_0
        public static Parameters[] GetAll()
        {
            return Enum.GetValues<Parameters>();
        }
#endif
    }
}
