using nanoFramework.Json;
using nanoFramework.Json.Configuration;
using nanoFramework.Json.Converters;
using SurronCommunication;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace SurronCommunication_Logger
{
    internal class DataLogger
    {
        // https://github.com/nanoframework/nf-interpreter/blob/12abaab58412cd0208fd0d09293eda0fa4e10426/targets/ESP32/_IDF/sdkconfig.default.esp32s3#L1015C1-L1015C24
        // https://docs.espressif.com/projects/esp-idf/en/v4.4.7/esp32s3/api-reference/storage/spiffs.html#mkspiffs
        private const int _writeChunkSize = 4096;

        private readonly Hashtable _currentValues = new(10);
        private readonly Queue _writeQueue = new();
        private readonly FileStream _file;

        public DataLogger(string path)
        {
            ConvertersMapping.Add(typeof(JsonBase64Wrapper), new JsonBase64WrapperConverter());

            var files = Directory.GetFiles("I:");
            //foreach (var file in files)
            //{
            //    File.Delete(file);
            //}

            _file = File.OpenWrite(path);
        }

        public void Run()
        {
            var jsonBuffer = new byte[_writeChunkSize * 2];
            var jsonBufferPos = 0;

            while (true)
            {
                LogEntry? logEntry = null;
                lock (_writeQueue.SyncRoot)
                {
                    if (_writeQueue.Count > 0)
                        logEntry = (LogEntry)_writeQueue.Dequeue();
                }
                if (logEntry == null)
                {
                    Thread.Sleep(10000);
                    continue;
                }

                var json = JsonConvert.SerializeObject(logEntry);
                Thread.Sleep(0);
                Debug.WriteLine(json);
                jsonBufferPos += Encoding.UTF8.GetBytes(json, 0, json.Length, jsonBuffer, jsonBufferPos);
                jsonBuffer[jsonBufferPos++] = (byte)'\n';

                //TODO: if json is longer than writeChunkSize, this may break
                if (jsonBufferPos >= _writeChunkSize)
                {
                    WriteToFile(jsonBuffer, 0, _writeChunkSize);
                    Thread.Sleep(0);
                    Array.Copy(jsonBuffer, _writeChunkSize, jsonBuffer, 0, _writeChunkSize);
                    jsonBufferPos -= _writeChunkSize;
                }
            }
        }

        private void WriteToFile(byte[] buffer, int offset, int length)
        {
            _file.Write(buffer, offset, length);
            Console.WriteLine("Written to file!");
        }

        public void SetData(DateTime updateTime, ushort addr, Hashtable newData)
        {
            var changedList = new ArrayList();
            foreach (byte paramId in newData.Keys)
            {
                var key = addr << 8 | paramId;

                var sourceArray = (byte[])newData[paramId];
                var targetArray = (byte[])_currentValues[key];

                bool changed;
                if (targetArray == null)
                {
                    targetArray = new byte[sourceArray.Length];
                    _currentValues[key] = targetArray;
                    changed = true;
                }
                else
                {
                    changed = !SequenceEqual(sourceArray, targetArray);
                }

                Debug.Assert(sourceArray.Length == targetArray.Length);
                Array.Copy(sourceArray, targetArray, targetArray.Length);

                if (changed)
                {
                    changedList.Add(new LogEntryValue(paramId, (byte[])targetArray.Clone()));
                }
            }
            if (changedList.Count > 0)
            {
                lock (_writeQueue.SyncRoot)
                {
                    _writeQueue.Enqueue(new LogEntry(updateTime, addr, changedList));
                    // TODO: prevent queue from growing too large
                }
            }
        }

        private static bool SequenceEqual(byte[] arr1, byte[] arr2)
        {
            if (arr1.Length != arr2.Length)
                return false;

            for (int i = 0; i < arr1.Length; i++)
            {
                if (arr1[i] != arr2[i])
                {
                    return false;
                }
            }
            return true;
        }

        private class LogEntry
        {
            public LogEntry(DateTime time, ushort address, ArrayList values)
            {
                Time = time;
                Addr = address;
                Values = values;
            }

            public DateTime Time { get; }
            public ushort Addr { get; }
            public ArrayList Values { get; }

            public override string ToString()
            {
                var sb = new StringBuilder();
                for (int i = 0; i < Values.Count; i++)
                {
                    var logEntryValue = (LogEntryValue)Values[i];
                    if (i != 0)
                        sb.Append(", ");
                    sb.Append(logEntryValue.ToString());
                }

                return $"{Time:s} - {sb}";
            }
        }

        private class LogEntryValue
        {
            public LogEntryValue(byte parameter, byte[] data)
            {
                Param = parameter;
                Data = new JsonBase64Wrapper(data);
            }

            public byte Param { get; }
            public JsonBase64Wrapper Data { get; }
            public override string ToString()
            {
                return $"{Param,3}: {HexUtils.BytesToHex(Data.Data)}";
            }
        }
    }

    internal class JsonBase64Wrapper
    {
        public JsonBase64Wrapper(byte[] data)
        {
            Data = data;
        }

        public byte[] Data { get; }
    }

    internal class JsonBase64WrapperConverter : IConverter
    {
        public string ToJson(object value)
        {
            return $"\"{Convert.ToBase64String(((JsonBase64Wrapper)value).Data)}\"";
        }

        public object ToType(object value)
        {
            throw new NotSupportedException();
        }
    }
}
