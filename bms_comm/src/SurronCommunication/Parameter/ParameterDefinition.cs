namespace SurronCommunication.Parameter
{
    public class ParameterDefinition
    {
        public ParameterDefinition(byte id, byte length)
        {
            Id = id;
            Length = length;
        }

        public byte Id { get; }
        public byte Length { get; }
    }
}
