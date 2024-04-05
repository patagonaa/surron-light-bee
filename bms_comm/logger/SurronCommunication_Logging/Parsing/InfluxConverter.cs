using System;
using System.Text;

namespace SurronCommunication_Logging.Parsing
{
    public static class InfluxConverter
    {
        private static readonly char[] _commaSpace = new[] { ' ', ',' };
        private static readonly char[] _commaEqualsSpace = new[] { ' ', ',', '=' };
        private static readonly char[] _doubleQuote = new[] { '"' };
        private static readonly long _unixEpochTicks = DateTime.UnixEpoch.Ticks;

        private static readonly Encoding _encoding = Encoding.UTF8;
        public static void AppendInfluxLine(byte[] buffer, ref int bufferPos, string measurement, string? labelKey, string? labelValue, string[] valueKeys, object[] values, DateTime time)
        {
            var measurementEscaped = Escape(measurement, _commaSpace);
            bufferPos += _encoding.GetBytes(measurementEscaped, 0, measurementEscaped.Length, buffer, bufferPos);

            if (labelKey != null && labelValue != null)
            {
                buffer[bufferPos++] = (byte)',';

                var labelKeyEscaped = Escape(labelKey, _commaEqualsSpace);
                bufferPos += _encoding.GetBytes(labelKeyEscaped, 0, labelKeyEscaped.Length, buffer, bufferPos);

                buffer[bufferPos++] = (byte)'=';

                var labelValueEscaped = Escape(labelValue, _commaEqualsSpace);
                bufferPos += _encoding.GetBytes(labelValueEscaped, 0, labelValueEscaped.Length, buffer, bufferPos);
            }

            buffer[bufferPos++] = (byte)' ';

            for (int i = 0; i < valueKeys.Length; i++)
            {
                if (i != 0)
                {
                    buffer[bufferPos++] = (byte)',';
                }

                var key = Escape(valueKeys[i], _commaEqualsSpace);
                bufferPos += _encoding.GetBytes(key, 0, key.Length, buffer, bufferPos);

                buffer[bufferPos++] = (byte)'=';

                var value = FormatValue(values[i]);
                bufferPos += _encoding.GetBytes(value, 0, value.Length, buffer, bufferPos);
            }
            buffer[bufferPos++] = (byte)' ';

            var timestamp = ((time.Ticks - _unixEpochTicks) * 100).ToString();
            bufferPos += _encoding.GetBytes(timestamp, 0, timestamp.Length, buffer, bufferPos);

            buffer[bufferPos++] = (byte)'\n';
        }

        public static string GetInfluxLine(string measurement, string? labelKey, string? labelValue, string[] valueKeys, object[] values, DateTime time)
        {
            var formattedLabels = string.Empty;
            if (labelKey != null && labelValue != null)
            {
                formattedLabels = $",{Escape(labelKey, _commaEqualsSpace)}={Escape(labelValue, _commaEqualsSpace)}";
            }

            var sb = new StringBuilder(32);
            for (int i = 0; i < valueKeys.Length; i++)
            {
                if (i != 0)
                {
                    sb.Append(',');
                }
                sb.Append($"{Escape(valueKeys[i], _commaEqualsSpace)}={FormatValue(values[i])}");
            }
            var formattedValues = sb.ToString();

            return $"{Escape(measurement, _commaSpace)}{formattedLabels} {formattedValues} {(time.Ticks - _unixEpochTicks) * 100}";
        }

        private static string Escape(string value, char[] chars)
        {
#if NANOFRAMEWORK_1_0
            return value; // don't care, we know what we're getting here
#else
            var sb = new StringBuilder(value);
            sb.Replace("\\", "\\\\");
            foreach (var c in chars)
            {
                sb.Replace($"{c}", $"\\{c}");
            }
            return sb.ToString();
#endif
        }

        private static string FormatValue(object x)
        {
            if (x is string strValue)
            {
                return $"\"{Escape(strValue, _doubleQuote)}\"";
            }
            else if (x is int intValue)
            {
                return $"{intValue}i";
            }
            else if (x is uint uintValue)
            {
                return $"{uintValue}i";
            }
            else if (x is byte byteValue)
            {
                return $"{byteValue}i";
            }
            else if (x is double doubleValue)
            {
                return doubleValue.ToString("F6");
            }
            else
            {
                throw new ArgumentException($"Invalid Object {x}");
            }
        }
    }
}
