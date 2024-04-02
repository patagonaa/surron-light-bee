using SurronCommunication.Parameter.Parsing;
using SurronCommunication_Logging.Logging;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text;
using System.Web;

namespace SurronCommunication_Logger_LogUtil
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var rootCommand = new RootCommand("Surron log file utility");

            {
                var fileArgument = new Argument<string>("file", "The log file to upload") { Arity = ArgumentArity.ExactlyOne };

                var influxUrlOption = new Option<string>(
                    name: "--influxUrl",
                    description: "The InfluxDB URL including credentials (example: 'https://user:password@influxdb.example.com/')")
                {
                    IsRequired = true
                };

                var influxDbOption = new Option<string>(
                    name: "--influxDb",
                    description: "The InfluxDB database name")
                {
                    IsRequired = true
                };

                var uploadCommand = new Command("uploadInflux")
                {
                    influxUrlOption,
                    influxDbOption,
                    fileArgument,
                };

                uploadCommand.SetHandler((url, db, file) => RunInfluxUpload(file, url, db), influxUrlOption, influxDbOption, fileArgument);

                rootCommand.AddCommand(uploadCommand);
            }

            await rootCommand.InvokeAsync(args);
        }

        private static async Task RunInfluxUpload(string file, string influxUrl, string influxDb)
        {
            using var bytes = File.OpenRead(file);

            var entries = ParseEntries(bytes);
            var lines = ConvertToInfluxLines(entries);


            var client = new HttpClient();

            var fullUri = new Uri($"{influxUrl.TrimEnd('/')}/write?db={HttpUtility.UrlEncode(influxDb)}");

            var requestUri = fullUri.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.UserInfo, UriFormat.UriEscaped);

            var credentials = fullUri.GetComponents(UriComponents.UserInfo, UriFormat.Unescaped);
            if (!string.IsNullOrEmpty(credentials))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));

            foreach (var lineBatch in lines.Chunk(5000))
            {
                using var ms = new MemoryStream();
                using (var lineProtocolSw = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true) { NewLine = "\n" })
                {
                    foreach (var line in lineBatch)
                    {
                        lineProtocolSw.WriteLine(line);
                    }
                }

                var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new ByteArrayContent(ms.ToArray())
                };
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
            }
        }

        private static IEnumerable<LogEntry> ParseEntries(Stream logFile)
        {
            // this always reads one bufferLength of bytes, reads a single log entry,
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
                    int readBytes = logFile.Read(buffer, bufferPosition, bufferLength-bufferPosition);
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

        private static IEnumerable<string> ConvertToInfluxLines(IEnumerable<LogEntry> entries)
        {
            var parser = new ParameterParser();

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

                var parsedData = currentValues.Where(x => x.Key.Category == entry.Category).SelectMany(x => parser.ParseParameter(parameterType, x.Key.ParamId, x.Value));
                foreach (var lineGroup in parsedData.GroupBy(x => (x.Measurement, string.Join(',', x.Labels))))
                {
                    var groupList = lineGroup.ToList();
                    yield return GetInfluxLine(lineGroup.Key.Measurement, groupList[0].Labels, groupList.Select(x => (x.FieldName, x.Value)), entry.Time);
                }
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
