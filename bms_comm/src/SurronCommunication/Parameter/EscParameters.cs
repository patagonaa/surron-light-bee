using System;

namespace SurronCommunication.Parameter
{
    public static class EscParameters
    {
        public const ushort EscAddress = 0x183;

        public static byte GetLength(EscParameterId parameterId)
        {
            return parameterId switch
            {
                EscParameterId.Unknown_72 => 12,
                EscParameterId.Unknown_75 => 2,
                _ => throw new ArgumentException($"unknown parameter {parameterId}")
            };
        }
    }
}
