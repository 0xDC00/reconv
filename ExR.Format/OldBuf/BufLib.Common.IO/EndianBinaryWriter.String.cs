using System.Collections.Generic;
using System.Text;

namespace BufLib.Common.IO
{
    public partial class EndianBinaryWriter
    {
        public void WriteTerminatedString(string s, Encoding encoding)
        {
            WriteTerminatedString(s, 0, encoding);
        }

        public void WriteTerminatedString(string s, byte terminated, Encoding encoding)
        {
            byte[] bytes = encoding.GetBytes(s);
            Write(bytes);
            Write(terminated);
        }

        public void WriteTerminatedWideString(string s, Encoding encoding)
        {
            WriteTerminatedWideString(s, 0, encoding);
        }

        public void WriteTerminatedWideString(string s, ushort terminated, Encoding encoding)
        {
            byte[] bytes = encoding.GetBytes(s);
            Write(bytes);
            Write(terminated);
        }

        public void WriteStringWithoutPrefix(string s, Encoding encoding)
        {
            byte[] bytes = encoding.GetBytes(s);
            Write(bytes);
        }

        public void WriteStringFixedLength(string s, int fixedLength, Encoding encoding)
        {
            byte[] bytes = encoding.GetBytes(s);
            System.Array.Resize(ref bytes, fixedLength);
            Write(bytes);
        }

        public void WriteStringPrefixedLength8(string s, Encoding encoding)
        {
            byte[] bytes = encoding.GetBytes(s);
            Write((byte)bytes.Length);
            Write(bytes);
        }

        public void WriteStringPrefixedLength16(string s, Encoding encoding)
        {
            byte[] bytes = encoding.GetBytes(s);
            Write((ushort)bytes.Length);
            Write(bytes);
        }

        public void WriteStringPrefixedLengt32(string s, Encoding encoding)
        {
            byte[] bytes = encoding.GetBytes(s);
            Write(bytes.Length);
            Write(bytes);
        }
    }
}
