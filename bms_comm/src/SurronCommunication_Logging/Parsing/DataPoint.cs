using System.Collections.Generic;

namespace SurronCommunication.Parameter.Parsing
{
    public class DataPoint
    {
        public DataPoint(string measurement, Dictionary<string, string> labels, string fieldName, object value, string? unit = null)
        {
            Measurement = measurement;
            Labels = labels;
            FieldName = fieldName;
            Value = value;
            Unit = unit;
        }

        public string Measurement { get; }
        public Dictionary<string, string> Labels { get; }
        public string FieldName { get; }
        public object Value { get; }
        public string? Unit { get; }
    }
}
