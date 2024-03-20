using SurronCommunication;
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace SurronCommunication_Logger
{
    internal class DataLogger
    {
        private readonly Hashtable _currentValues = new();
        private readonly Queue _writeQueue = new();

        public DataLogger()
        {
        }

        public void Run()
        {
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
                    Thread.Sleep(1000);
                    continue;
                }
                Console.WriteLine(logEntry.ToString());
            }
        }

        public void SetData(DateTime updateTime, Hashtable newData)
        {
            var changedList = new ArrayList();
            foreach (byte key in newData.Keys)
            {
                var sourceArray = (byte[])newData[key];
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
                    changedList.Add(new LogEntryValue(key, (byte[])targetArray.Clone()));
                }
            }
            if (changedList.Count > 0)
            {
                lock (_writeQueue.SyncRoot)
                {
                    _writeQueue.Enqueue(new LogEntry(updateTime, changedList));
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
            public LogEntry(DateTime time, ArrayList values)
            {
                Time = time;
                Values = values;
            }

            public DateTime Time { get; }
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
                Parameter = parameter;
                Data = data;
            }

            public byte Parameter { get; }
            public byte[] Data { get; }
            public override string ToString()
            {
                return $"{Parameter,3}: {HexUtils.BytesToHex(Data)}";
            }
        }
    }
}
