using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SurronCommunication.Parameter
{
    public static class BmsParameters
    {
        public const ushort BmsAddress = 0x116;

        public static readonly ParameterDefinition Unknown_0 = new(0, 4);
        public static readonly ParameterDefinition Unknown_7 = new(7, 1);
        public static readonly ParameterDefinition Temperatures = new(8, 8);
        public static readonly ParameterDefinition BatteryVoltage = new(9, 4);
        public static readonly ParameterDefinition BatteryCurrent = new(10, 4);
        public static readonly ParameterDefinition BatteryPercent = new(13, 1);
        public static readonly ParameterDefinition BatteryHealth = new(14, 4);
        public static readonly ParameterDefinition RemainingCapacity = new(15, 4);
        public static readonly ParameterDefinition TotalCapacity = new(16, 4);
        public static readonly ParameterDefinition Unknown_17 = new(17, 2);
        public static readonly ParameterDefinition Unknown_20 = new(20, 4);
        public static readonly ParameterDefinition Statistics = new(21, 12);
        public static readonly ParameterDefinition BmsStatus = new(22, 10);
        public static readonly ParameterDefinition ChargeCycles = new(23, 4);
        public static readonly ParameterDefinition DesignedCapacity = new(24, 4);
        public static readonly ParameterDefinition DesignedVoltage = new(25, 4);
        public static readonly ParameterDefinition Versions = new(26, 8);
        public static readonly ParameterDefinition ManufacturingDate = new(27, 3);
        public static readonly ParameterDefinition Unknown_28 = new(28, 4);
        public static readonly ParameterDefinition RtcTime = new(29, 6);
        public static readonly ParameterDefinition Unknown_30 = new(30, 6);
        public static readonly ParameterDefinition BmsManufacturer = new(32, 16);
        public static readonly ParameterDefinition BatteryModel = new(33, 32);
        public static readonly ParameterDefinition CellType = new(34, 16);
        public static readonly ParameterDefinition SerialNumber = new(35, 32);
        public static readonly ParameterDefinition CellVoltages1 = new(36, 32);
        public static readonly ParameterDefinition CellVoltages2 = new(37, 32);
        public static readonly ParameterDefinition History = new(38, 14);
        public static readonly ParameterDefinition Unknown_39 = new(39, 64); // length unknown
        public static readonly ParameterDefinition Unknown_48 = new(48, 64); // length unknown
        public static readonly ParameterDefinition Unknown_120 = new(120, 64); // length unknown
        public static readonly ParameterDefinition Unknown_160 = new(160, 32); // length unknown

        public static IList<(string ParameterName, ParameterDefinition Definition)> GetAll()
        {
            var fields = typeof(BmsParameters).GetFields(BindingFlags.Public | BindingFlags.Static);
            return fields
                .Where(x => x.FieldType == typeof(ParameterDefinition))
                .Select(x => (x.Name, (ParameterDefinition)(x.GetValue(null) ?? throw new Exception("ParameterDefinition may not be null"))))
                .ToList();
        }
    }
}
