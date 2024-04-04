using System;
using System.IO;
using System.Text;

#if NANOFRAMEWORK_1_0
using LabelCollection = System.Collections.Hashtable;
#else
using LabelCollection = System.Collections.Generic.Dictionary<string, string>;
#endif

namespace SurronCommunication_Logging.Parsing
{
    public static class InfluxConverter
    {
        private static readonly char[] _commaSpace = new[] { ' ', ',' };
        private static readonly char[] _commaEqualsSpace = new[] { ' ', ',', '=' };
        private static readonly char[] _doubleQuote = new[] { '"' };
        private static readonly long _unixEpochTicks = DateTime.UnixEpoch.Ticks;
        public static void AppendInfluxLine(TextWriter tw, string measurement, LabelCollection? labels, string[] valueKeys, object[] values, DateTime time)
        {
            tw.Write(Escape(measurement, _commaSpace));
            if (labels != null)
            {
                foreach (string key in labels.Keys)
                {
                    var value = (string)labels[key];
                    tw.Write($",{Escape(key, _commaEqualsSpace)}={Escape(value, _commaEqualsSpace)}");
                }
            }
            tw.Write(' ');

            for (int i = 0; i < valueKeys.Length; i++)
            {
                if (i != 0)
                {
                    tw.Write(',');
                }

                tw.Write($"{Escape(valueKeys[i], _commaEqualsSpace)}={FormatValue(values[i])}");
            }
            tw.Write(' ');
            tw.Write((time.Ticks - _unixEpochTicks) * 100);
        }

        public static string GetInfluxLine(string measurement, LabelCollection? labels, string[] valueKeys, object[] values, DateTime time)
        {
            var sb = new StringBuilder(32);
            var formattedLabels = string.Empty;
            if (labels != null)
            {
                foreach (string key in labels.Keys)
                {
                    var value = (string)labels[key];
                    sb.Append($",{Escape(key, _commaEqualsSpace)}={Escape(value, _commaEqualsSpace)}");
                }
                formattedLabels = sb.ToString();
                sb.Clear();
            }

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
