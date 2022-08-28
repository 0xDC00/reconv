using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BufLib.Common.IO
{
    public partial class EndianBinaryReader
    {
        public byte[] ReadTerminatedArray(byte terminated = 0)
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = ReadByte()) != terminated)
                bytes.Add(b);

            return bytes.ToArray();
        }

        public string ReadTerminatedString(Encoding encoding)
        {
            return ReadTerminatedString(0, encoding);
        }

        public string ReadTerminatedString(byte terminated, Encoding encoding)
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = ReadByte()) != terminated)
                bytes.Add(b);

            return encoding.GetString(bytes.ToArray());
        }

        public string ReadTerminatedWideString(Encoding encoding)
        {
            return ReadTerminatedWideString(0, encoding);
        }

        public string ReadTerminatedWideString(ushort terminated, Encoding encoding)
        {
            int lenght = 0;
            var pos = BaseStream.Position;
            while (ReadUInt16() != terminated)
                lenght += 2;

            BaseStream.Position = pos;
            var bytes = ReadBytes(lenght);
            BaseStream.Position += 2; // skip terminated.

            return encoding.GetString(bytes);
        }

        public string[] ReadTerminatedStrings(int count, Encoding encoding)
        {
            string[] value = new string[count];
            for (int i = 0; i < value.Length; i++)
                value[i] = ReadTerminatedString(encoding);
            return value;
        }

        public string[] ReadTerminatedStrings(int count, byte terminated, Encoding encoding)
        {
            string[] value = new string[count];
            for (int i = 0; i < value.Length; i++)
                value[i] = ReadTerminatedString(terminated, encoding);
            return value;
        }

        public string[] ReadTerminatedWideStrings(int count, Encoding encoding)
        {
            string[] value = new string[count];
            for (int i = 0; i < value.Length; i++)
                value[i] = ReadTerminatedWideString(encoding);
            return value;
        }

        public string[] ReadTerminatedWideStrings(int count, ushort terminated, Encoding encoding)
        {
            string[] value = new string[count];
            for (int i = 0; i < value.Length; i++)
                value[i] = ReadTerminatedWideString(terminated, encoding);
            return value;
        }

        public string ReadStringFixedLength(int fixedLength, Encoding encoding)
        {
            byte[] bytes = ReadBytes(fixedLength);
            return encoding.GetString(bytes);
        }

        public string[] ReadStringPrefixedLengths(int count, int fixedLength, Encoding encoding)
        {
            string[] value = new string[count];
            for (int i = 0; i < value.Length; i++)
                value[i] = ReadStringFixedLength(fixedLength, encoding);
            return value;
        }

        public string ReadStringPrefixedLength8(Encoding encoding)
        {
            byte length = ReadByte();
            return ReadStringFixedLength(length, encoding);
        }

        public string[] ReadStringPrefixedLength8s(int count, Encoding encoding)
        {
            string[] value = new string[count];
            for (int i = 0; i < value.Length; i++)
                value[i] = ReadStringPrefixedLength8(encoding);
            return value;
        }

        public string ReadStringPrefixedLength16(Encoding encoding)
        {
            ushort length = ReadUInt16();
            return ReadStringFixedLength(length, encoding);
        }

        public string[] ReadStringPrefixedLength16s(int count, Encoding encoding)
        {
            string[] value = new string[count];
            for (int i = 0; i < value.Length; i++)
                value[i] = ReadStringPrefixedLength16(encoding);
            return value;
        }

        public string ReadStringPrefixedLengt32(Encoding encoding)
        {
            int length = ReadInt32();
            return ReadStringFixedLength(length, encoding);
        }

        public string[] ReadStringPrefixedLength32s(int count, Encoding encoding)
        {
            string[] value = new string[count];
            for (int i = 0; i < value.Length; i++)
                value[i] = ReadStringPrefixedLengt32(encoding);
            return value;
        }
    }
}
