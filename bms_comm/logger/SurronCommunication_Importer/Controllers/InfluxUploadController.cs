using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SurronCommunication_Logging.Parsing;
using System.Net.Http.Headers;
using System.Text;
using System.Web;

namespace SurronCommunication_Importer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InfluxUploadController : ControllerBase
    {
        private readonly ILogger<InfluxUploadController> _logger;
        private readonly InfluxConfiguration _influxConfig;

        public InfluxUploadController(ILogger<InfluxUploadController> logger, IOptions<InfluxConfiguration> options)
        {
            _logger = logger;
            _influxConfig = options.Value;
        }

        [HttpPost]
        public async Task Post()
        {
            _logger.LogInformation("Got request");

            using var inputMs = new MemoryStream();
            await Request.Body.CopyToAsync(inputMs);
            inputMs.Position = 0;

            _logger.LogInformation("Got {Bytes} bytes of binary data", inputMs.Length);

            var lines = LogConverter.ReadAndConvertToInflux(inputMs);
            var client = new HttpClient();

            var fullUri = new Uri($"{_influxConfig.Url.TrimEnd('/')}/write?db={HttpUtility.UrlEncode(_influxConfig.Database)}");

            var requestUri = fullUri.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.UserInfo, UriFormat.UriEscaped);

            var credentials = fullUri.GetComponents(UriComponents.UserInfo, UriFormat.Unescaped);
            if (!string.IsNullOrEmpty(credentials))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));

            using var outputMs = new MemoryStream();
            foreach (var lineBatch in lines.Chunk(5000))
            {
                outputMs.SetLength(0);
                using (var lineProtocolSw = new StreamWriter(outputMs, Encoding.UTF8, leaveOpen: true) { NewLine = "\n" })
                {
                    foreach (var line in lineBatch)
                    {
                        lineProtocolSw.WriteLine(line);
                    }
                }

                var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new ByteArrayContent(outputMs.ToArray())
                };
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                _logger.LogInformation("Sent chunk to influx");
            }
        }
    }
}
