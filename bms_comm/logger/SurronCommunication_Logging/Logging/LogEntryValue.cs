using SurronCommunication;

namespace SurronCommunication_Logging.Logging
{
    public class LogEntryValue
    {
        public LogEntryValue(byte param, byte[] data)
        {
            Param = param;
            Data = data;
        }

        public byte Param { get; }
        public byte[] Data { get; }
        public override string ToString()
        {
            return $"{Param,3}: {HexUtils.BytesToHex(Data)}";
        }
    }
}
