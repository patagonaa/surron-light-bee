namespace SurronCommunication.Parameter
{
    public static class BmsParameters
    {
        public const ushort BmsAddress = 0x116;

        public static readonly ParameterDefinition Temperatures = new(8, 8);
        public static readonly ParameterDefinition BatteryVoltage = new(9, 4);
        public static readonly ParameterDefinition BatteryCurrent = new(10, 4);
        public static readonly ParameterDefinition BatteryPercent = new(13, 1);
        public static readonly ParameterDefinition BatteryHealth = new(14, 4);
        public static readonly ParameterDefinition RemainingCapacity = new(15, 4);
        public static readonly ParameterDefinition TotalCapacity = new(16, 4);
        public static readonly ParameterDefinition Statistics = new(21, 12);
        public static readonly ParameterDefinition BmsStatus = new(22, 10);
        public static readonly ParameterDefinition ChargeCycles = new(23, 4);
        public static readonly ParameterDefinition DesignedCapacity = new(24, 4);
        public static readonly ParameterDefinition DesignedVoltage = new(25, 4);
        public static readonly ParameterDefinition Versions = new(26, 8);
        public static readonly ParameterDefinition ManufacturingDate = new(27, 3);
        public static readonly ParameterDefinition RtcTime = new(29, 6);
        public static readonly ParameterDefinition BmsManufacturer = new(32, 16);
        public static readonly ParameterDefinition BatteryModel = new(33, 32);
        public static readonly ParameterDefinition CellType = new(34, 16);
        public static readonly ParameterDefinition SerialNumber = new(35, 32);
        public static readonly ParameterDefinition CellVoltages1 = new(36, 32);
        public static readonly ParameterDefinition CellVoltages2 = new(37, 32);
        public static readonly ParameterDefinition HistoryValues = new(38, 14);
    }
}
