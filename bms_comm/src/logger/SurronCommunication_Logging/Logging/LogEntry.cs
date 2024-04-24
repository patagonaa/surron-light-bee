using System;
using System.Text;

#if NANOFRAMEWORK_1_0
using LogEntryValueList = System.Collections.ICollection;
#else
using LogEntryValueList = System.Collections.Generic.ICollection<SurronCommunication_Logging.Logging.LogEntryValue>;
#endif

namespace SurronCommunication_Logging.Logging
{
    public class LogEntry
    {
        public LogEntry(DateTime time, LogCategory category, LogEntryValueList values)
        {
            Time = time;
            Category = category;
            Values = values;
        }

        public DateTime Time { get; }
        public LogCategory Category { get; }
        public LogEntryValueList Values { get; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            int i = 0;
            foreach (LogEntryValue logEntryValue in Values)
            {
                if (i != 0)
                    sb.Append(", ");
                sb.Append(logEntryValue.ToString());
                i++;
            }

            return $"{Time:yyyy'-'MM'-'dd'T'HH'-'mm'-'ss.fff'Z'} - {Category} - {sb}";
        }
    }
}
