namespace SurronBms.Common
{
    public static class HexUtils
    {
        public static byte[] HexToBytes(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }


        public static string BytesToHex(byte[] bytes)
        {
            return string.Join(string.Empty, bytes.Select(x => x.ToString("X2")));
        }
    }
}
