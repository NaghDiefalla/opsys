using System;
using System.Text;

namespace MiniFatFs
{
    public static class Converter
    {
        public static byte[] StringToBytes(string s)
        {
            // Use ASCII as it's a fixed-width encoding suitable for 8.3 names
            return Encoding.ASCII.GetBytes(s);
        }

        public static string BytesToString(byte[] bytes, int length)
        {
            // Use ASCII
            return Encoding.ASCII.GetString(bytes, 0, length);
        }
    }
}