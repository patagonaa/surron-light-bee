namespace SurronBms.Common
{
    public enum SurronCmd : byte
    {
        ReadRequest = 0x46,
        ReadResponse = 0x47,
        Status = 0x57
    }
}
