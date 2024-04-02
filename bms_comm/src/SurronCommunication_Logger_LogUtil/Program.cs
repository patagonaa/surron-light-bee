using SurronCommunication.Parameter.Parsing;
using SurronCommunication_Logging.Logging;
using SurronCommunication_Logging.Parsing;
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
            using var fileStream = File.OpenRead(file);

            var lines = LogConverter.ReadAndConvertToInflux(fileStream);

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

    }
}
