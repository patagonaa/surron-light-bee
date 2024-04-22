using nanoFramework.Networking;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

namespace SurronCommunication_Logger
{
    internal class HttpUploader
    {
        private readonly string _uri;
        private readonly string _username;
        private readonly string _password;

        public HttpUploader(string uri, string username, string password)
        {
            _uri = uri;
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

            var uri = $"{_uri.TrimEnd('/')}/InfluxUpload";
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
            wr.KeepAlive = false;
            wr.Credentials = credential;
            wr.Method = "POST";
            wr.ContentLength = stream.Length;

            using var requestStream = wr.GetRequestStream();

            var bufferSize = 16384;
            var buffer = new byte[bufferSize];

            var totalSw = Stopwatch.StartNew();
            var transferred = 0;
            var i = 0;

            int read = 0;
            while ((read = stream.Read(buffer, 0, bufferSize)) > 0)
            {
                requestStream.Write(buffer, 0, read);
                transferred += read;
                i++;
                if (i % 10 == 0)
                {
                    Console.WriteLine($"Uploaded ({(double)transferred / stream.Length * 100:F2}%, {transferred / ((totalSw.ElapsedMilliseconds + 1) / 1000d):F2}B/s)");
                }
            }

            Console.WriteLine($"Uploaded file ({transferred} bytes in {totalSw.ElapsedMilliseconds}ms)!");

            using (var response = (HttpWebResponse)wr.GetResponse())
            {
                if (response.StatusCode >= HttpStatusCode.OK && response.StatusCode < HttpStatusCode.BadRequest)
                {
                    return;
                }
                using (var streamReader = new StreamReader(response.GetResponseStream()))
                {
                    throw new Exception($"Upload failed: {response.StatusCode} {streamReader.ReadToEnd()}");
                }
            }
        }
    }
}
