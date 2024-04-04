using SurronCommunication_Logging.Logging;
using System;
using System.IO;
using System.Diagnostics;


#if NANOFRAMEWORK_1_0
using System.Buffers.Binary;
using System.Collections;
#else
using SurronCommunication.Parameter.Parsing;
using System.Collections.Generic;
#endif

namespace SurronCommunication_Logging.Parsing
{
    public static class LogConverter
    {
#if !NANOFRAMEWORK_1_0
        public static IEnumerable<string> ReadAndConvertToInflux(Stream stream)
        {
            byte[]? buffer = null;
            var bufferPos = 0;

            var currentValues = new LogEntryStore();
            while (true)
            {
                var entry = ReadFromStream(stream, ref buffer, ref bufferPos);

                if (entry == null)
                    break;

                var allValues = currentValues.AddAndGetLogEntriesForCategory(entry.Category, entry.Values);

                var parameterType = entry.Category switch
                {
                    LogCategory.BmsFast => ParameterType.Bms,
                    LogCategory.BmsSlow => ParameterType.Bms,
                    LogCategory.Esc => ParameterType.Esc,
                    _ => throw new NotSupportedException(),
                };

                foreach (LogEntryValue logValue in allValues)
                {
                    var dataPoints = ParameterParser.GetDataPointsForParameter(parameterType, logValue.Param, logValue.Data);

                    foreach (var point in dataPoints)
                    {
                        yield return InfluxConverter.GetInfluxLine(point.Measurement, point.Labels, point.Fields, point.Values, entry.Time);
                    }
                }
            }
        }
#endif

#if NANOFRAMEWORK_1_0
        public class LogEntryStore
        {
            private Hashtable[] _entries;

            public LogEntryStore()
            {
                _entries = new Hashtable[3]
                {
                    new Hashtable(), // BmsFast = 1,
                    new Hashtable(), // BmsSlow,
                    new Hashtable(), // Esc
                };
            }

            public ICollection AddAndGetLogValuesForCategory(LogCategory category, ICollection values)
            {
                var table = _entries[(byte)category-1];
                foreach (LogEntryValue entryValue in values)
                {
                    if (entryValue.Data == null)
                        throw new ArgumentException("Data must be set");

                    table[entryValue.Param] = entryValue;
                }
                return table.Values;
            }
        }
#else
        public class LogEntryStore
        {
            private Dictionary<LogCategory, Dictionary<byte, LogEntryValue>> _entries;

            public LogEntryStore()
            {
                _entries = new Dictionary<LogCategory, Dictionary<byte, LogEntryValue>>()
                {
                    { LogCategory.BmsFast, new Dictionary<byte, LogEntryValue>() },
                    { LogCategory.BmsSlow, new Dictionary<byte, LogEntryValue>() },
                    { LogCategory.Esc, new Dictionary<byte, LogEntryValue>() }
                };
            }

            public ICollection<LogEntryValue> AddAndGetLogEntriesForCategory(LogCategory category, ICollection<LogEntryValue> values)
            {
                var table = _entries[category];
                foreach (LogEntryValue entryValue in values)
                {
                    if (entryValue.Data == null)
                        throw new ArgumentException("Data must be set");

                    table[entryValue.Param] = entryValue;
                }
                return table.Values;
            }
        }
#endif

        public static LogEntry? ReadFromStream(Stream logFile)
        {
            // this always reads one bufferLength of bytes, deserializes a single log entry,
            // then resets the stream position to the beginning of the next log entry, then repeats (until the end of the file).
            //
            // this is pretty inefficient (because of constant rewinding and reading of the stream) but is a lot simpler than having to deal
            // with entries at the end of the buffer.

            var bufferLength = 256;
            byte[] buffer = new byte[bufferLength];
            var oldFilePosition = logFile.Position;

            var bufferPosition = 0;
            while (bufferPosition < bufferLength)
            {
                int readBytes = logFile.Read(buffer, bufferPosition, bufferLength - bufferPosition);
                if (readBytes == 0)
                    break;
                bufferPosition += readBytes;
            }

            var handledLength = LogSerializer.Deserialize(buffer.AsSpan(0, bufferPosition), out var logEntry);
            if (handledLength == 0 || logEntry == null)
                return null;
            logFile.Position = oldFilePosition + handledLength;
            return logEntry;
        }

        public static LogEntry? ReadFromStream(Stream logFile, ref byte[]? buffer, ref int bufferPos)
        {
            // this is janky as heck
            // first we read bufferSize bytes and once we cross bufferSize-packetLen, we just seek back bufferSize-packetLen, fill the buffer again and set the buffer position to the right place.
            // this is probably buggy, idk

            var bufferSize = 512;

            if (buffer == null)
            {
                buffer = new byte[bufferSize];
                logFile.Read(buffer, 0, buffer.Length);
            }

            var bufferLength = buffer.Length;
            var packetLen = 256;

            var threshold = bufferLength - packetLen;

            // don't do this if we are at end of file, as seeking back one threshold doesn't give us the correct position
            // (because the file position is not the last read position + bufferLength)
            if (bufferPos > threshold && logFile.Position < logFile.Length)
            {
                bufferPos -= threshold;
                logFile.Seek(-packetLen, SeekOrigin.Current);
                var readBytes = logFile.Read(buffer, 0, bufferLength);
                if (readBytes < bufferLength)
                {
                    // handle end of file by just zeroing out the rest and relying on deserialize to detect that and stop
                    // janky but should work?
                    Array.Clear(buffer, readBytes, bufferLength - readBytes);
                }
            }

            var handledLength = LogSerializer.Deserialize(buffer.AsSpan(bufferPos), out var logEntry);
            Debug.Assert(handledLength < packetLen);
            if (handledLength == 0 || logEntry == null)
                return null;
            bufferPos += handledLength;
            return logEntry;
        }
    }
}
