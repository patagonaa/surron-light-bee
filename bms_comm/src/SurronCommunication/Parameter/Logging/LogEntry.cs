using System;
using System.Text;

#if NANOFRAMEWORK_1_0
using LogEntryValueList = System.Collections.IList;
#else
using LogEntryValueList = System.Collections.Generic.IList<SurronCommunication.Parameter.Logging.LogEntryValue>;
#endif

namespace SurronCommunication.Parameter.Logging
{
    public class LogEntry
    {
        public LogEntry(DateTime time, ushort addr, LogEntryValueList values)
        {
            Time = time;
            Addr = addr;
            Values = values;
        }

        public DateTime Time { get; }
        public ushort Addr { get; }
        public LogEntryValueList Values { get; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Values.Count; i++)
            {
                var logEntryValue = Values[i];
                if (i != 0)
                    sb.Append(", ");
                sb.Append(logEntryValue.ToString());
            }

            return $"{Time:s} - {sb}";
        }
    }
}
