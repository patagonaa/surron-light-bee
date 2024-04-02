using SurronCommunication.Parameter.Parsing;
using SurronCommunication_Logging.Logging;
using System;
using System.Buffers.Binary;
using System.Collections;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace SurronCommunication_Logging.Parsing
{
    public static class LogConverter
    {
        public static IEnumerable<string> ReadAndConvertToInflux(Stream stream)
        {
            var entries = ReadFromStream(stream);

            var currentValues = new Dictionary<(LogCategory Category, byte ParamId), byte[]>();
            foreach (var entry in entries)
            {
                foreach (var entryValue in entry.Values)
                {
                    currentValues[(entry.Category, entryValue.Param)] = entryValue.Data;
                }

                var parameterType = entry.Category switch
                {
                    LogCategory.BmsFast => ParameterType.Bms,
                    LogCategory.BmsSlow => ParameterType.Bms,
                    LogCategory.Esc => ParameterType.Esc,
                    _ => throw new NotSupportedException(),
                };

                var parsedData = currentValues.Where(x => x.Key.Category == entry.Category).SelectMany(x => ParameterParser.GetDataPointsForParameter(parameterType, x.Key.ParamId, x.Value));
                foreach (var lineGroup in parsedData.GroupBy(x => (x.Measurement, string.Join(',', x.Labels))))
                {
                    var groupList = lineGroup.ToList();
                    yield return GetInfluxLine(lineGroup.Key.Measurement, groupList[0].Labels, groupList.Select(x => (x.FieldName, x.Value)), entry.Time);
                }
            }
        }

        private static IEnumerable<LogEntry> ReadFromStream(Stream logFile)
        {
            // this always reads one bufferLength of bytes, deserializes a single log entry,
            // then resets the stream position to the beginning of the next log entry, then repeats (until the end of the file).
            //
            // this is pretty inefficient (because of constant rewinding and reading of the stream) but is a lot simpler than having to deal
            // with entries at the end of the buffer.

            var bufferLength = 256;
            byte[] buffer = new byte[bufferLength];
            var position = 0;
            while (true)
            {
                logFile.Position = position;
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
                    break;
                yield return logEntry;
                position += handledLength;
            }
        }

        private static string GetInfluxLine(string measurement, Dictionary<string, string> labels, IEnumerable<(string FieldName, object Value)> values, DateTime time)
        {
            return $"{Escape(measurement, [',', ' '])}" +
                $"{string.Join("", labels.Select(x => $",{Escape(x.Key, [',', '=', ' '])}={Escape(x.Value, [',', '=', ' '])}"))} " +
                $"{string.Join(",", values.Select(x => $"{Escape(x.FieldName, [',', '=', ' '])}={FormatValue(x.Value)}"))} " +
                $"{(ulong)(time - DateTime.UnixEpoch).TotalNanoseconds}";

            static string Escape(string value, char[] chars)
            {
                var sb = new StringBuilder(value);
                sb.Replace("\\", "\\\\");
                foreach (var c in chars)
                {
                    sb.Replace($"{c}", $"\\{c}");
                }
                return sb.ToString();
            }

            static string FormatValue(object x)
            {
                if (x is string strValue)
                {
                    return $"\"{Escape(strValue, ['"'])}\"";
                }
                else if (IsInteger(x))
                {
                    return $"{x}i";
                }
                else if (IsNumeric(x))
                {
                    return x.ToString()!;
                }
                else
                {
                    throw new ArgumentException($"Invalid Object {x}");
                }
                static bool IsInteger(object o)
                {
                    var numType = typeof(IBinaryInteger<>);
                    return o.GetType().GetInterfaces().Any(iface =>
                        iface.IsGenericType && (iface.GetGenericTypeDefinition() == numType));
                }
                static bool IsNumeric(object o)
                {
                    var numType = typeof(INumber<>);
                    return o.GetType().GetInterfaces().Any(iface =>
                        iface.IsGenericType && (iface.GetGenericTypeDefinition() == numType));
                }
            }
        }
    }
}
