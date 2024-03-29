using System;

namespace SurronCommunication.Parameter
{
    public static class EscParameters
    {
        public const ushort EscAddress = 0x183;

        public enum Parameters
        {
            Unknown_72 = 72,
            Unknown_75 = 75,
        }

        public static byte GetLength(Parameters parameterId)
        {
            return parameterId switch
            {
                Parameters.Unknown_72 => 12,
                Parameters.Unknown_75 => 2,
                _ => throw new ArgumentException($"unknown parameter {parameterId}")
            };
        }
    }
}
