using System;
using System.Buffers.Binary;

#if NANOFRAMEWORK_1_0
using ReadOnlySpanByte = System.SpanByte;
#else
using ReadOnlySpanByte = System.ReadOnlySpan<byte>;
using SpanByte = System.Span<byte>;
#endif

namespace SurronCommunication_Logging.Logging
{
    public static class LogSerializer
    {
        public static int Serialize(SpanByte buffer, LogEntry logEntry)
        {
            var position = 0;

            BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(position, 8), (logEntry.Time - DateTime.UnixEpoch).Ticks);
            position += 8;

            buffer[position++] = (byte)logEntry.Category;

            if (logEntry.Values.Count > 255)
                throw new ArgumentException("must not have more than 255 values!", nameof(logEntry));
            buffer[position++] = (byte)logEntry.Values.Count;

            foreach (LogEntryValue logEntryValue in logEntry.Values)
            {
                buffer[position++] = logEntryValue.Param;

                var data = logEntryValue.Data;

                var dataLength = data.Length;
                if (dataLength > 255)
                    throw new ArgumentException("data must not have more than 255 bytes!", nameof(logEntry));
                buffer[position++] = (byte)dataLength;

                data.CopyTo(buffer.Slice(position, dataLength));
                position += dataLength;
            }

            buffer[position] = CalcChecksum(buffer.Slice(0, position)); // mostly a marker for end of packet to detect incomplete packets
            position += 1;

            return position;
        }

        public static int Deserialize(ReadOnlySpanByte buffer, out LogEntry? logEntry)
        {
            if (buffer.Length == 0)
            {
                logEntry = null;
                return 0;
            }
            try
            {
                var position = 0;

                var time = DateTime.UnixEpoch.AddTicks(BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(position, 8)));
                position += 8;

                var category = (LogCategory)buffer[position++];
                if (category == 0)
                {
                    logEntry = null;
                    return 0;
                }

                var valueCount = buffer[position++];

                var values = new LogEntryValue[valueCount];

                for (int i = 0; i < valueCount; i++)
                {
                    var paramId = buffer[position++];
                    var dataLength = buffer[position++];
                    var data = buffer.Slice(position, dataLength).ToArray();
                    position += dataLength;

                    values[i] = new LogEntryValue(paramId, data);
                }

                var calcChecksum = CalcChecksum(buffer.Slice(0, position));
                var readChecksum = buffer[position++];

                if (calcChecksum != readChecksum)
                {
                    logEntry = null;
                    return 0;
                }

                logEntry = new LogEntry(time, category, values);
                return position;
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException || ex is ArgumentOutOfRangeException)
            {
                // buffer data incomplete
                logEntry = null;
                return 0;
            }
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
    }
}
