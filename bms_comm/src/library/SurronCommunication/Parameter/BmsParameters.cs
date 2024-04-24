using System;

namespace SurronCommunication.Parameter
{
    public static class BmsParameters
    {
        public const ushort BmsAddress = 0x116;

        public static byte GetLength(BmsParameterId parameterId)
        {
            return parameterId switch
            {
                BmsParameterId.Unknown_0 => 4,
                BmsParameterId.Unknown_7 => 1,
                BmsParameterId.Temperatures => 8,
                BmsParameterId.BatteryVoltage => 4,
                BmsParameterId.BatteryCurrent => 4,
                BmsParameterId.BatteryPercent => 1,
                BmsParameterId.BatteryHealth => 4,
                BmsParameterId.RemainingCapacity => 4,
                BmsParameterId.TotalCapacity => 4,
                BmsParameterId.Unknown_17 => 2,
                BmsParameterId.Unknown_20 => 4,
                BmsParameterId.Statistics => 12,
                BmsParameterId.BmsStatus => 10,
                BmsParameterId.ChargeCycles => 4,
                BmsParameterId.DesignedCapacity => 4,
                BmsParameterId.DesignedVoltage => 4,
                BmsParameterId.Versions => 8,
                BmsParameterId.ManufacturingDate => 3,
                BmsParameterId.Unknown_28 => 4,
                BmsParameterId.RtcTime => 6,
                BmsParameterId.Unknown_30 => 6,
                BmsParameterId.BmsManufacturer => 16,
                BmsParameterId.BatteryModel => 32,
                BmsParameterId.CellType => 16,
                BmsParameterId.SerialNumber => 32,
                BmsParameterId.CellVoltages1 => 32,
                BmsParameterId.CellVoltages2 => 32,
                BmsParameterId.History => 14,
                BmsParameterId.Unknown_39 => 64, // length unknown
                BmsParameterId.Unknown_48 => 64, // length unknown
                BmsParameterId.Unknown_120 => 64, // length unknown
                BmsParameterId.Unknown_160 => 32, // length unknown
                _ => throw new ArgumentException($"unknown parameter {parameterId}")
            };
        }

#if !NANOFRAMEWORK_1_0
        public static BmsParameterId[] GetAll()
        {
            return Enum.GetValues<BmsParameterId>();
        }
#endif
    }
}
