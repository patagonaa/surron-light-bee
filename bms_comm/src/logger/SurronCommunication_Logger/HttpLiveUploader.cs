using nanoFramework.Networking;
using SurronCommunication_Logging.Logging;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

namespace SurronCommunication_Logger
{
    internal class HttpLiveUploader
    {
        public event ParameterUpdateEventHandler? ParameterUpdateEvent;

        private const int MaxItems = 100;

        private readonly string _uri;
        private readonly string _username;
        private readonly string _password;

        private readonly Queue _writeQueue = new Queue();

        public HttpLiveUploader(HttpUploadOptions uploadOptions)
        {
            _uri = uploadOptions.Url;
            _username = uploadOptions.Username;
            _password = uploadOptions.Password;
        }

        public void SetData(DateTime updateTime, LogCategory logCategory, Hashtable newData)
        {
            var changedList = new ArrayList();
            foreach (byte paramId in newData.Keys)
            {
                var sourceArray = (byte[])newData[paramId];
                var targetArray = new byte[sourceArray.Length];

                Array.Copy(sourceArray, targetArray, targetArray.Length);
                changedList.Add(new LogEntryValue(paramId, targetArray));
            }

            // enqueue even if there have been no changes to insert data point at timestamp
            lock (_writeQueue.SyncRoot)
            {
                _writeQueue.Enqueue(new LogEntry(updateTime, logCategory, changedList));
                while (_writeQueue.Count > MaxItems)
                {
                    _writeQueue.Dequeue();
                }
            }
        }

        public void Run(CancellationToken token)
        {
            var uri = $"{_uri.TrimEnd('/')}/InfluxUpload";
            var credential = new NetworkCredential(_username, _password, AuthenticationType.Basic);

            using var ms = new MemoryStream();

            while (!token.IsCancellationRequested)
            {
                while (WifiNetworkHelper.Status != NetworkHelperStatus.NetworkIsReady)
                {
                    Console.WriteLine("Connecting to wifi");
                    var success = WifiNetworkHelper.Reconnect(token: token);
                    if (!success)
                    {
                        Console.WriteLine($"Can't connect to the network, error: {WifiNetworkHelper.Status}");
                        if (WifiNetworkHelper.HelperException != null)
                        {
                            Console.WriteLine($"ex: {WifiNetworkHelper.HelperException}");
                        }
                        Thread.Sleep(30000);
                    }
                    Console.WriteLine("Connected");
                }

                ms.SetLength(0);
                lock (_writeQueue.SyncRoot)
                {
                    var buffer = new byte[128];
                    while (_writeQueue.Count > 0)
                    {
                        var entry = (LogEntry)_writeQueue.Dequeue();
                        var written = LogSerializer.Serialize(buffer, entry);
                        ms.Write(buffer, 0, written);
                    }
                }
                if (ms.Length > 0)
                {
                    ms.Position = 0;
                    Upload(ms, credential, uri);
                }

                Thread.Sleep(10000);
            }
        }

        private static void Upload(Stream stream, NetworkCredential credential, string uri)
        {
            using var wr = (HttpWebRequest)WebRequest.Create(uri);
            wr.KeepAlive = false;
            wr.Credentials = credential;
            wr.Method = "POST";
            wr.ContentLength = stream.Length;

            using var requestStream = wr.GetRequestStream();

            var bufferSize = 256;
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
