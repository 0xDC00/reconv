using System;
using System.Text;
using BufLib.Common.IO;

namespace ExR.Format
{
    public static class NumberHelper
    {
        public static short Reverse(this short value)
        {
            return EndiannessHelper.Reverse(value);
        }

        public static int Reverse(this int value)
        {
            return EndiannessHelper.Reverse(value);
        }

        public static long Reverse(this long value)
        {
            return EndiannessHelper.Reverse(value);
        }

        public static ushort Reverse(this ushort value)
        {
            return EndiannessHelper.Reverse(value);
        }

        public static uint Reverse(this uint value)
        {
            return EndiannessHelper.Reverse(value);
        }

        public static ulong Reverse(this ulong value)
        {
            return EndiannessHelper.Reverse(value);
        }
    }

    public static class HexStringHelper
    {
        public static string ByteArrayToString(this byte[] array)
        {
            return Convert.ToHexString(array);
        }

        public static int HexStringToInt(this string hex)
        {
            return int.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        }

        public static long HexStringToLong(this string hex)
        {
            return long.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        }

        public static byte[] HexStringToByteArray(this string hex)
        {
            return System.Convert.FromHexString(hex);
            
        }

        /// <summary>
        /// (
        /// </summary>
        /// <param name="hex">($00)</param>
        /// <returns></returns>
        public static byte[] CustomHexStringToByteArray(this string hex)
        {
            var span = hex.AsSpan(2, hex.Length - 3);
            return System.Convert.FromHexString(span);
        }

        public static string ByteArrayToStringCustom(this byte[] array)
        {
            return "($" + Convert.ToHexString(array) + ")";
        }

        public static string ByteToString(this byte b)
        {
            return hexStringTable[b];
        }

        static readonly string[] hexStringTable = new string[] {
            "00", "01", "02", "03", "04", "05", "06", "07", "08", "09", "0A", "0B", "0C", "0D", "0E", "0F",
            "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "1A", "1B", "1C", "1D", "1E", "1F",
            "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "2A", "2B", "2C", "2D", "2E", "2F",
            "30", "31", "32", "33", "34", "35", "36", "37", "38", "39", "3A", "3B", "3C", "3D", "3E", "3F",
            "40", "41", "42", "43", "44", "45", "46", "47", "48", "49", "4A", "4B", "4C", "4D", "4E", "4F",
            "50", "51", "52", "53", "54", "55", "56", "57", "58", "59", "5A", "5B", "5C", "5D", "5E", "5F",
            "60", "61", "62", "63", "64", "65", "66", "67", "68", "69", "6A", "6B", "6C", "6D", "6E", "6F",
            "70", "71", "72", "73", "74", "75", "76", "77", "78", "79", "7A", "7B", "7C", "7D", "7E", "7F",
            "80", "81", "82", "83", "84", "85", "86", "87", "88", "89", "8A", "8B", "8C", "8D", "8E", "8F",
            "90", "91", "92", "93", "94", "95", "96", "97", "98", "99", "9A", "9B", "9C", "9D", "9E", "9F",
            "A0", "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9", "AA", "AB", "AC", "AD", "AE", "AF",
            "B0", "B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8", "B9", "BA", "BB", "BC", "BD", "BE", "BF",
            "C0", "C1", "C2", "C3", "C4", "C5", "C6", "C7", "C8", "C9", "CA", "CB", "CC", "CD", "CE", "CF",
            "D0", "D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "D9", "DA", "DB", "DC", "DD", "DE", "DF",
            "E0", "E1", "E2", "E3", "E4", "E5", "E6", "E7", "E8", "E9", "EA", "EB", "EC", "ED", "EE", "EF",
            "F0", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "FA", "FB", "FC", "FD", "FE", "FF",
        };

        // https://www.codeproject.com/Articles/36747/Quick-and-Dirty-HexDump-of-a-Byte-Array
        public static string HexDump(this byte[] bytes, int offset = 0, int maxlen = 0x40, int bytesPerLine = 16)
        {
            if (bytes == null) return "<null>";
            int bytesLength = maxlen;

            char[] HexChars = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

            int firstHexColumn =
                  8                   // 8 characters for the address
                + 2;                  // 2 spaces

            int firstCharColumn = firstHexColumn
                + bytesPerLine * 3       // - 2 digit for the hexadecimal value and 1 space
                + (bytesPerLine - 1) / 8 // - 1 extra space every 8 characters from the 9th
                + 1;                     //   1 spaces 

            int lineLength = firstCharColumn
                + bytesPerLine           // - characters to show the ascii value
                + Environment.NewLine.Length; // Carriage return and line feed (should normally be 2)

            char[] line = (new String(' ', lineLength - Environment.NewLine.Length) + Environment.NewLine).ToCharArray();
            int expectedLines = (bytesLength + bytesPerLine - 1) / bytesPerLine;
            StringBuilder result = new StringBuilder(expectedLines * lineLength + 77 + Environment.NewLine.Length);
            result.AppendLine("           0  1  2  3  4  5  6  7   8  9  A  B  C  D  E  F   0123456789ABCDEF");
            for (int i = offset; i < bytesLength; i += bytesPerLine)
            {
                // address column
                line[0] = HexChars[(i >> 28) & 0xF];
                line[1] = HexChars[(i >> 24) & 0xF];
                line[2] = HexChars[(i >> 20) & 0xF];
                line[3] = HexChars[(i >> 16) & 0xF];
                line[4] = HexChars[(i >> 12) & 0xF];
                line[5] = HexChars[(i >> 8) & 0xF];
                line[6] = HexChars[(i >> 4) & 0xF];
                line[7] = HexChars[(i >> 0) & 0xF];

                int hexColumn = firstHexColumn;
                int charColumn = firstCharColumn;

                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (j > 0 && (j & 7) == 0) hexColumn++;
                    if (i + j >= bytesLength)
                    {
                        line[hexColumn] = ' ';
                        line[hexColumn + 1] = ' ';
                        line[charColumn] = ' ';
                    }
                    else
                    {
                        byte b = bytes[i + j];
                        line[hexColumn] = HexChars[(b >> 4) & 0xF];
                        line[hexColumn + 1] = HexChars[b & 0xF];
                        line[charColumn] = ((b < 32 || b > 0x7E) ? '.' : (char)b);
                    }
                    hexColumn += 3;
                    charColumn++;
                }
                result.Append(line);
            }
            return result.ToString();
        }
    }
}
