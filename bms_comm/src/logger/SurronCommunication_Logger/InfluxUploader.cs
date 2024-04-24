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
            Console.WriteLine("Connecting to wifi");
            var success = WifiNetworkHelper.Reconnect(token: cs.Token);
            if (!success)
            {
                Console.WriteLine($"Can't connect to the network, error: {WifiNetworkHelper.Status}");
                if (WifiNetworkHelper.HelperException != null)
                {
                    Console.WriteLine($"ex: {WifiNetworkHelper.HelperException}");
                }
                return;
            }
            Console.WriteLine("Connected");

            var uri = $"{_uri.TrimEnd('/')}/write?db={_database}";
            var credential = new NetworkCredential(_username, _password, AuthenticationType.Basic);

            var files = Directory.GetFiles(path);

            foreach (var logFile in files)
            {
                if (!logFile.EndsWith(".bin"))
                    continue;

                using var fileStream = File.OpenRead(logFile);

                for (int i = 0; ; i++)
                {
                    try
                    {
                        Console.WriteLine($"Starting upload of {logFile}");

                        UploadFile(fileStream, credential, uri);

                        Console.WriteLine($"Finished {logFile}");
                        File.Delete(logFile);

                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception: {ex}");
                        if (i >= 10)
                        {
                            Console.WriteLine("Retry limit reached!");
                            break;
                        }
                    }
                }
            }
        }

        private void UploadFile(Stream stream, NetworkCredential credential, string uri)
        {
            using var wr = (HttpWebRequest)WebRequest.Create(uri);
            wr.SendChunked = true;
            wr.KeepAlive = false;
            wr.Credentials = credential;
            wr.Method = "POST";

            using var requestStream = wr.GetRequestStream();

            var currentValues = new LogConverter.LogEntryStore();
            var outputBuffer = new byte[8192];
            var outputBufferPos = 0;

            var numLines = 0;

            var stepSw = Stopwatch.StartNew();
            var totalSw = Stopwatch.StartNew();

            byte[]? inputBuffer = null;
            int inputBufferPos = 0;

            long outBytes = 0;

            while (true)
            {
                var entry = LogConverter.ReadFromStream(stream, ref inputBuffer, ref inputBufferPos);

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

                    foreach (DataPoint dataPoint in dataPoints)
                    {
                        InfluxConverter.AppendInfluxLine(outputBuffer, ref outputBufferPos, dataPoint.Measurement, dataPoint.LabelKey, dataPoint.LabelValue, dataPoint.Fields, dataPoint.Values, entry.Time);
                        numLines++;

                        if (numLines % 100 == 0)
                        {
                            var collectMs = stepSw.ElapsedMilliseconds;
                            stepSw.Restart();

                            WriteChunk(requestStream, outputBuffer, outputBufferPos);

                            stepSw.Stop();
                            outBytes += outputBufferPos;
                            Console.WriteLine($"Uploaded Influx batch ({outputBufferPos} bytes, {(double)stream.Position / stream.Length * 100:F2}%, {stream.Position / ((totalSw.ElapsedMilliseconds + 1) / 1000d):F2}B/s in, {outBytes / ((totalSw.ElapsedMilliseconds + 1) / 1000d):F2}B/s out, Upload: {stepSw.ElapsedMilliseconds}, Collect: {collectMs})!");

                            outputBufferPos = 0;
                            stepSw.Restart();
                        }
                    }
                }
            }

            WriteChunk(requestStream, outputBuffer, outputBufferPos);
            outBytes += outputBufferPos;

            var chunkedEnd = Encoding.UTF8.GetBytes("0\r\n\r\n");
            requestStream.Write(chunkedEnd, 0, chunkedEnd.Length);

            requestStream.Flush();

            Debug.WriteLine($"Uploaded Influx file ({stream.Length} -> {outBytes} bytes / {numLines} lines in {totalSw.ElapsedMilliseconds}ms)!");

            using (var response = (HttpWebResponse)wr.GetResponse())
            {
                if (response.StatusCode >= HttpStatusCode.OK && response.StatusCode < HttpStatusCode.BadRequest)
                {
                    return;
                }
                using (var streamReader = new StreamReader(response.GetResponseStream()))
                {
                    throw new Exception($"Influx Upload failed: {response.StatusCode} {streamReader.ReadToEnd()}");
                }
            }
        }

        private void WriteChunk(Stream requestStream, byte[] buffer, int count)
        {
            var chunkHeader = Encoding.UTF8.GetBytes($"{count:X}\r\n");
            requestStream.Write(chunkHeader, 0, chunkHeader.Length);

            requestStream.Write(buffer, 0, count);

            var chunkFooter = Encoding.UTF8.GetBytes($"\r\n");
            requestStream.Write(chunkFooter, 0, chunkFooter.Length);
        }
    }
}
