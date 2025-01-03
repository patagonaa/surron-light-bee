﻿using System;
using System.Buffers.Binary;
using System.IO;
#if NANOFRAMEWORK_1_0
using System.Diagnostics;
using ReadOnlySpanByte = System.SpanByte;
#else
using ReadOnlySpanByte = System.ReadOnlySpan<byte>;
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

        public static SurronDataPacket? FromBytes(ReadOnlySpanByte data)
        {
            if (data.Length < 6)
                throw new ArgumentException("Message too short (less than 6 bytes)");

            var calcChecksum = CalcChecksum(data.Slice(0, data.Length - 1));
            var readChecksum = data[data.Length - 1];

            if (readChecksum != calcChecksum)
                return (SurronDataPacket?)HandleDataError($"Invalid checksum (calculated: {calcChecksum:X2}, read: {readChecksum:X2})");

            var header = ReadHeader(data);
            if (header == null)
                return null;

            var expectedLength = GetPacketLength(header.Command, header.DataLength);
            if (data.Length != expectedLength)
                return (SurronDataPacket?)HandleDataError($"Message too short (expected {expectedLength}, got {data.Length})");

            var commandData = header.Command == SurronCmd.ReadRequest ? null : data.Slice(5, header.DataLength).ToArray();

            return Create(header.Command, header.Address, header.Parameter, header.DataLength, commandData);
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
                CommandData!.CopyTo(bytes.AsSpan(5, DataLength));

            bytes[bytes.Length - 1] = CalcChecksum(bytes.AsSpan(0, bytes.Length - 1));

            return bytes;
        }

        private static byte CalcChecksum(ReadOnlySpanByte bytes)
        {
            byte calcChecksum = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                unchecked
                {
                    calcChecksum += bytes[i];
                }
            }
            return calcChecksum;
        }

        private static SurronHeader? ReadHeader(ReadOnlySpanByte header)
        {
            if (header.Length < HeaderLength)
                throw new ArgumentException($"Header must be at least {HeaderLength} bytes long");

            var command = (SurronCmd)header[0];

            if (!(command == SurronCmd.Status || command == SurronCmd.ReadResponse || command == SurronCmd.ReadRequest))
                return (SurronHeader?)HandleDataError($"Command {command} is not valid.");

            var address = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(1, 2));
            var parameter = header[3];
            var dataLength = command == SurronCmd.Status ? (byte)(header[4] - 1) : header[4];

            return new SurronHeader(command, address, parameter, dataLength);
        }

        private static object? HandleDataError(string error)
        {
#if NANOFRAMEWORK_1_0
            Debug.WriteLine($"Data error: {error}");
            return null;
#else
            throw new InvalidDataException(error);
#endif
        }

        public static int GetPacketLengthFromHeader(ReadOnlySpanByte headerBytes)
        {
            var header = ReadHeader(headerBytes);
            if (header == null)
                return -1;
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
            var commandStr = Command switch
            {
                SurronCmd.ReadRequest => nameof(SurronCmd.ReadRequest),
                SurronCmd.ReadResponse => nameof(SurronCmd.ReadResponse),
                SurronCmd.Status => nameof(SurronCmd.Status),
                _ => "[Invalid]"
            };

            return $"{commandStr} {Address:X4} {Parameter:X2} - {(CommandData != null ? HexUtils.BytesToHex(CommandData) : "")}";
        }
    }
}
