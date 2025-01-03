﻿using SurronCommunication_Logging.Logging;
using System;
using System.Buffers.Binary;
using System.Collections;
using System.Diagnostics;
using System.IO;
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
        private readonly string _path;

        private FileStream? _file;

        public DataLogger(string path)
        {
            _path = path;
            //var files = Directory.GetFiles("I:");
            //foreach (var file in files)
            //{
            //    File.Delete(file);
            //}
        }

        public void Run(CancellationToken token)
        {
            var buffer = new byte[_writeChunkSize * 2];
            var bufferPos = 0;
            var pendingLogEntries = 0;

            var cancelWaitHandle = token.WaitHandle;

            while (!token.IsCancellationRequested)
            {
                LogEntry? logEntry = null;
                lock (_writeQueue.SyncRoot)
                {
                    if (_writeQueue.Count > 0)
                        logEntry = (LogEntry)_writeQueue.Dequeue();
                }
                if (logEntry == null)
                {
                    cancelWaitHandle.WaitOne(10000, false);
                    continue;
                }

                bufferPos += LogSerializer.Serialize(buffer.AsSpan(bufferPos), logEntry);
                pendingLogEntries++;
                Thread.Sleep(0);

                //TODO: if entry is longer than writeChunkSize, this may break
                // if we wrote more than the writeChunkSize (half the buffer), write the first half of the buffer, move the second half to the first half and continue writing where we left off
                // this is basically a simple ring buffer
                if (bufferPos >= _writeChunkSize)
                {
                    WriteToFile(buffer, 0, _writeChunkSize);
                    Console.WriteLine($"Written {pendingLogEntries} log entries to file!");
                    pendingLogEntries = 0;
                    Thread.Sleep(0);
                    Array.Copy(buffer, _writeChunkSize, buffer, 0, _writeChunkSize);
                    bufferPos -= _writeChunkSize;
                }
            }

            if (bufferPos > 0)
            {
                WriteToFile(buffer, 0, bufferPos);
            }

            _file?.Dispose();

            Debug.WriteLine("Exiting Data Logger");
        }

        private void WriteToFile(byte[] buffer, int offset, int length)
        {
            _file ??= File.OpenWrite(_path);
            _file.Write(buffer, offset, length);
        }

        public void SetData(DateTime updateTime, LogCategory logCategory, Hashtable newData)
        {
            var changedList = new ArrayList();
            foreach (byte paramId in newData.Keys)
            {
                var key = ((byte)logCategory) << 8 | paramId;

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

            // enqueue even if there have been no changes to insert data point at timestamp
            lock (_writeQueue.SyncRoot)
            {
                _writeQueue.Enqueue(new LogEntry(updateTime, logCategory, changedList));
                // TODO: prevent queue from growing too large
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
    }
}
