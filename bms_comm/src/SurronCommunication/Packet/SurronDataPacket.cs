using System;
using System.Buffers.Binary;
using System.IO;
#if !NANOFRAMEWORK_1_0
using SpanByte = System.Span<byte>;
#endif

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

        public static SurronDataPacket FromBytes(SpanByte data)
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

            var readChecksum = data[data.Length - 1];

            if (readChecksum != calcChecksum)
                throw new InvalidDataException($"Invalid checksum (calculated: {calcChecksum:X2}, read: {readChecksum:X2})");

            var header = ReadHeader(data);

            var expectedLength = GetPacketLength(header.Command, header.DataLength);
            if (data.Length != expectedLength)
                throw new InvalidDataException($"Message too short (expected {expectedLength}, got {data.Length})");

            var commandData = header.Command == SurronCmd.ReadRequest ? null : data.Slice(5, header.DataLength).ToArray();

            return new SurronDataPacket(header.Command, header.Address, header.Parameter, header.DataLength, commandData);
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

            byte calcChecksum = 0;
            for (int i = 0; i < bytes.Length - 1; i++)
            {
                unchecked
                {
                    calcChecksum += bytes[i];
                }
            }
            bytes[bytes.Length - 1] = calcChecksum;

            return bytes;
        }

        private static SurronHeader ReadHeader(SpanByte header)
        {
            if (header.Length < HeaderLength)
                throw new ArgumentException($"Header must be at least {HeaderLength} bytes long");

            var command = (SurronCmd)header[0];

            if (!(command == SurronCmd.Status || command == SurronCmd.ReadResponse || command == SurronCmd.ReadRequest))
                throw new InvalidDataException($"Command {command} is not valid.");

            var address = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(1, 2));
            var parameter = header[3];
            var dataLength = command == SurronCmd.Status ? (byte)(header[4] - 1) : header[4];

            return new SurronHeader(command, address, parameter, dataLength);
        }

        public static int GetPacketLengthFromHeader(SpanByte headerBytes)
        {
            var header = ReadHeader(headerBytes);
            return GetPacketLength(header.Command, header.DataLength);
        }

        private class SurronHeader
        {
            public SurronCmd Command { get; }
            public ushort Address { get; }
            public byte Parameter { get; }
            public byte DataLength { get; }

            public SurronHeader(SurronCmd command, ushort address, byte parameter, byte dataLength)
            {
                Command = command;
                Address = address;
                Parameter = parameter;
                DataLength = dataLength;
            }
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
            return $"{Command} {Address:X4} {Parameter:X2} - {(CommandData != null ? HexUtils.BytesToHex(CommandData) : "")}";
        }
    }
}
