#if NANOFRAMEWORK_1_0
using LabelCollection = System.Collections.Hashtable;
#else
using LabelCollection = System.Collections.Generic.Dictionary<string, string>;
#endif

namespace SurronCommunication.Parameter.Parsing
{
    public class DataPoint
    {
        public DataPoint(string measurement, LabelCollection? labels, string fieldName, object value)
        {
            Measurement = measurement;
            Labels = labels;
            Fields = new[] { fieldName };
            Values = new[] { value };
        }
        public DataPoint(string measurement, LabelCollection? labels, string[] fields, object[] values)
        {
            Measurement = measurement;
            Labels = labels;
            Fields = fields;
            Values = values;
        }

        public string Measurement { get; }
        public LabelCollection? Labels { get; }
        public string[] Fields { get; }
        public object[] Values { get; }
    }
}
