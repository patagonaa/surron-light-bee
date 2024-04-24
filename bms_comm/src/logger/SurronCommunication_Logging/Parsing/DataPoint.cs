#if NANOFRAMEWORK_1_0
using LabelCollection = System.Collections.Hashtable;
#else
using LabelCollection = System.Collections.Generic.Dictionary<string, string>;
#endif

namespace SurronCommunication.Parameter.Parsing
{
    public class DataPoint
    {
        public DataPoint(string measurement, string? labelKey, string? labelValue, string fieldName, object value)
            : this(measurement, labelKey, labelValue, new[] { fieldName }, new[] { value })
        {
        }

        public DataPoint(string measurement, string? labelKey, string? labelValue, string[] fields, object[] values)
        {
            Measurement = measurement;
            LabelKey = labelKey;
            LabelValue = labelValue;
            Fields = fields;
            Values = values;
        }

        public string Measurement { get; }
        public string? LabelKey { get; }
        public string? LabelValue { get; }
        public string[] Fields { get; }
        public object[] Values { get; }
    }
}
