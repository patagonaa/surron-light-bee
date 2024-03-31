using SurronCommunication.Parameter;
using SurronCommunication.Parameter.Logging;
using System.CommandLine;
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
                    IsRequired = true,
                };

                var influxDbOption = new Option<string>(
                    name: "--influxDb",
                    description: "The InfluxDB database name")
                {
                    IsRequired = true,
                };

                var uploadCommand = new Command("uploadInflux")
                {
                    influxUrlOption,
                    influxDbOption,
                    fileArgument,
                };

                uploadCommand.SetHandler((url, db, file) => Upload(file, url, db), influxUrlOption, influxDbOption, fileArgument);

                rootCommand.AddCommand(uploadCommand);
            }

            await rootCommand.InvokeAsync(args);
        }

        private static async Task Upload(string file, string influxUrl, string influxDb)
        {
            var bytes = File.ReadAllBytes(file); // TODO use a stream here for RAM/performance reasons

            var entries = new List<LogEntry>();
            var position = 0;
            while (true)
            {
                position += LogSerializer.Deserialize(bytes.AsSpan(position), out var logEntry);
                if (logEntry == null)
                    break;
                entries.Add(logEntry);
            }

            var parser = new ParameterParser();
            using var ms = new MemoryStream();
            using (var lineProtocolSw = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true) { NewLine = "\n" })
            {
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
                        lineProtocolSw.WriteLine(GetInfluxLine(lineGroup.Key.Measurement, groupList[0].Labels, groupList.Select(x => (x.FieldName, x.Value)), entry.Time));
                    }
                }
            }

            var client = new HttpClient();

            var uri = new Uri($"{influxUrl.TrimEnd('/')}/write?db={HttpUtility.UrlEncode(influxDb)}");
            var request = new HttpRequestMessage(HttpMethod.Post, uri.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.UserInfo, UriFormat.UriEscaped));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(uri.GetComponents(UriComponents.UserInfo, UriFormat.Unescaped))));
            request.Content = new ByteArrayContent(ms.ToArray());
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
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
