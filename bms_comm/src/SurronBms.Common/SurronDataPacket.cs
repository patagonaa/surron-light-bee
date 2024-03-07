using System.Buffers.Binary;

namespace SurronBms.Common
{
    public class SurronDataPacket
    {
        private SurronDataPacket(SurronCmd command, ushort address, byte parameter, byte dataLength, byte[]? commandData)
        {
            Command = command;
            Address = address;
            Parameter = parameter;
            DataLength = dataLength;
            CommandData = commandData;
        }

        public SurronCmd Command { get; }
        public ushort Address { get; }
        public byte Parameter { get; }
        public byte DataLength { get; }
        public byte[]? CommandData { get; }

        public static SurronDataPacket Create(SurronCmd command, ushort address, byte parameter, byte dataLength, byte[]? commandData)
        {
            if (command == SurronCmd.ReadRequest && commandData != null && commandData.Length > 0)
                throw new ArgumentException($"{nameof(SurronCmd.ReadRequest)} command may not have command data");
            if ((command == SurronCmd.ReadResponse || command == SurronCmd.Status) && (commandData == null || commandData.Length != dataLength))
                throw new ArgumentException($"{nameof(SurronCmd.ReadResponse)}/{nameof(SurronCmd.Status)} command must have command data with length equal to dataLength");
            return new SurronDataPacket(command, address, parameter, dataLength, commandData);
        }

        public static SurronDataPacket FromBytes(byte[] data)
        {
            if (data.Length < 6)
                throw new ArgumentException("Message too short (less than 6 bytes)");

            var calcChecksum = data[..^1].Aggregate((byte)0, (a, b) => (byte)(a + b));
            var readChecksum = data[^1];

            if (readChecksum != calcChecksum)
                throw new ArgumentException($"Invalid checksum (calculated: {calcChecksum:X2}, read: {readChecksum:X2})");

            var command = (SurronCmd)data[0];

            var address = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(1, 2));
            var parameter = data[3];
            var dataLength = command == SurronCmd.Status ? (byte)(data[4] - 1) : data[4];

            var expectedLength = GetLength(command, dataLength);
            if (data.Length != expectedLength)
                throw new ArgumentException($"Message too short (expected {expectedLength}, got {data.Length})");

            var commandData = command == SurronCmd.ReadRequest ? null : data.AsSpan(5, dataLength).ToArray();

            return new SurronDataPacket(command, address, parameter, dataLength, commandData);
        }

        public byte[] ToBytes()
        {
            var length = GetLength(Command, DataLength);
            var bytes = new byte[length];
            bytes[0] = (byte)Command;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(1, 2), Address);
            bytes[3] = Parameter;
            bytes[4] = Command == SurronCmd.Status ? (byte)(DataLength + 1) : DataLength;
            if (Command != SurronCmd.ReadRequest)
                CommandData.CopyTo(bytes.AsSpan(5, DataLength));
            var checksum = bytes[..^1].Aggregate((byte)0, (a, b) => (byte)(a + b));
            bytes[^1] = checksum;
            return bytes;
        }

        public static int GetLength(SurronCmd command, int dataLen)
        {
            return
                1 + // command
                2 + // address
                1 + // parameter
                1 + // parameterlength
                (command == SurronCmd.ReadRequest ? 0 : dataLen) + // data
                1; // checksum
        }

        public override string ToString()
        {
            return $"{Command} {Address:X4} {Parameter:X2} - {HexUtils.BytesToHex(CommandData ?? [])}";
        }
    }
}
