using nanoFramework.Networking;
using SurronCommunication.Parameter.Parsing;
using SurronCommunication_Logging.Logging;
using SurronCommunication_Logging.Parsing;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace SurronCommunication_Logger
{
    internal class InfluxUploader
    {
        private readonly string _uri;
        private readonly string _database;
        private readonly string _username;
        private readonly string _password;

        public InfluxUploader(string uri, string database, string username, string password)
        {
            _uri = uri;
            _database = database;
            _username = username;
            _password = password;
        }

        public void Run(string path)
        {
            CancellationTokenSource cs = new(30000);
            var success = WifiNetworkHelper.Reconnect(token: cs.Token);
            if (!success)
            {
                Debug.WriteLine($"Can't connect to the network, error: {WifiNetworkHelper.Status}");
                if (WifiNetworkHelper.HelperException != null)
                {
                    Debug.WriteLine($"ex: {WifiNetworkHelper.HelperException}");
                }
                return;
            }

            var uri = $"{_uri.TrimEnd('/')}/write?db={_database}";
            var credential = new NetworkCredential(_username, _password, AuthenticationType.Basic);

            var files = Directory.GetFiles(path);

            foreach (var logFile in files)
            {
                if (!logFile.EndsWith(".bin"))
                    continue;

                using var fileStream = File.OpenRead(logFile);

                Console.WriteLine($"Starting upload of {logFile}");

                if (!UploadFile(fileStream, credential, uri))
                    break;

                Console.WriteLine($"Finished {logFile}");
                File.Delete(logFile);
            }
        }

        private bool UploadFile(Stream stream, NetworkCredential credential, string uri)
        {
            var currentValues = new LogConverter.LogEntryStore();
            using var ms = new MemoryStream();
            var sw = new StreamWriter(ms) { NewLine = "\n" };
            var numLines = 0;

            var stepSw = Stopwatch.StartNew();
            var totalSw = Stopwatch.StartNew();

            byte[]? buffer = null;
            int bufferPos = 0;

            long outBytes = 0;

            while (true)
            {
                var entry = LogConverter.ReadFromStream(stream, ref buffer, ref bufferPos);

                if (entry == null)
                    break;

                var allValues = currentValues.AddAndGetLogValuesForCategory(entry.Category, entry.Values);

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

                    foreach (DataPoint dataPoint in dataPoints)
                    {
                        InfluxConverter.AppendInfluxLine(sw, dataPoint.Measurement, dataPoint.Labels, dataPoint.Fields, dataPoint.Values, entry.Time);
                        sw.WriteLine();

                        //sw.WriteLine(InfluxConverter.GetInfluxLine(dataPoint.Measurement, dataPoint.Labels, dataPoint.Fields, dataPoint.Values, entry.Time));

                        numLines++;

                        if (numLines >= 100)
                        {
                            sw.Flush();
                            ms.Position = 0;
                            var collectMs = stepSw.ElapsedMilliseconds;
                            stepSw.Restart();

                            if (!UploadWithRetries(ms, credential, uri))
                            {
                                Debug.WriteLine("Upload failed:");
                                var arr = ms.ToArray();
                                Debug.WriteLine(Encoding.UTF8.GetString(arr, 0, arr.Length));
                                return false;
                            }
                            stepSw.Stop();
                            outBytes += ms.Length;
                            Console.WriteLine($"Uploaded Influx batch ({ms.Length} bytes, {(double)stream.Position / stream.Length * 100:F2}%, {stream.Position / ((totalSw.ElapsedMilliseconds + 1) / 1000d):F2}B/s in, {outBytes / ((totalSw.ElapsedMilliseconds + 1) / 1000d):F2}B/s out, Upload: {stepSw.ElapsedMilliseconds}, Collect: {collectMs})!");

                            ms.SetLength(0);
                            numLines = 0;
                            stepSw.Restart();
                        }
                    }
                }
            }
            sw.Flush();
            ms.Position = 0;
            if (!UploadWithRetries(ms, credential, uri))
                return false;
            Debug.WriteLine($"Uploaded Influx batch ({ms.Length} bytes)!");

            return true;
        }

        private bool UploadWithRetries(Stream stream, NetworkCredential credential, string uri)
        {
            for (int i = 0; i < 10; i++)
            {
                if (Upload(stream, credential, uri))
                    return true;
                stream.Position = 0;
                Console.WriteLine("Retry");
            }
            return false;
        }

        private bool Upload(Stream stream, NetworkCredential credential, string uri)
        {
            using var wr = (HttpWebRequest)WebRequest.Create(uri);
            wr.KeepAlive = false;
            wr.Credentials = credential;
            wr.Method = "POST";

            wr.ContentLength = stream.Length;

            try
            {
                using (var requestStream = wr.GetRequestStream())
                {
                    stream.CopyTo(requestStream);

                    using (var response = (HttpWebResponse)wr.GetResponse())
                    {
                        if (response.StatusCode > HttpStatusCode.OK && response.StatusCode < HttpStatusCode.BadRequest)
                        {
                            return true;
                        }
                        using (var streamReader = new StreamReader(response.GetResponseStream()))
                        {
                            Console.WriteLine($"Influx Upload failed: {response.StatusCode} {streamReader.ReadToEnd()}");
                        }
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Influx Upload failed: {ex}");
                return false;
            }
        }
    }
}
