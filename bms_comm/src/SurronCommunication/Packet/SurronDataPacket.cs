using System;
using System.Buffers.Binary;
using System.IO;

namespace SurronCommunication.Packet
{
    public class SurronDataPacket
    {
        public const int HeaderLength = 5;

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

        public static SurronDataPacket FromBytes(Span<byte> data)
        {
            if (data.Length < 6)
                throw new ArgumentException("Message too short (less than 6 bytes)");

            byte calcChecksum = 0;
            for (int i = 0; i < data.Length - 1; i++)
            {
                unchecked
                {
                    calcChecksum += data[i];
                }
            }

            var readChecksum = data[^1];

            if (readChecksum != calcChecksum)
                throw new InvalidDataException($"Invalid checksum (calculated: {calcChecksum:X2}, read: {readChecksum:X2})");

            var (command, address, parameter, dataLength) = ReadHeader(data);

            var expectedLength = GetPacketLength(command, dataLength);
            if (data.Length != expectedLength)
                throw new InvalidDataException($"Message too short (expected {expectedLength}, got {data.Length})");

            var commandData = command == SurronCmd.ReadRequest ? null : data.Slice(5, dataLength).ToArray();

            return new SurronDataPacket(command, address, parameter, dataLength, commandData);
        }

        public byte[] ToBytes()
        {
            var length = GetPacketLength(Command, DataLength);
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

        private static (SurronCmd Command, ushort Address, byte Parameter, byte DataLength) ReadHeader(Span<byte> header)
        {
            if (header.Length < HeaderLength)
                throw new ArgumentException($"Header must be at least {HeaderLength} bytes long");

            var command = (SurronCmd)header[0];

            if (!Enum.IsDefined(command))
                throw new InvalidDataException($"Command {command} is not valid.");

            var address = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(1, 2));
            var parameter = header[3];
            var dataLength = command == SurronCmd.Status ? (byte)(header[4] - 1) : header[4];

            return (command, address, parameter, dataLength);
        }

        public static int GetPacketLengthFromHeader(Span<byte> header)
        {
            var (command, _, _, dataLen) = ReadHeader(header);
            return GetPacketLength(command, dataLen);
        }

        public static int GetPacketLength(SurronCmd command, int dataLen)
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
