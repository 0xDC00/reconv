using System.Runtime.InteropServices;
using System.Text;
using static BufLib.TextFormats.DataModels.Catherine;

namespace BufLib.TextFormats.BinaryModels.Catherine
{
    public static partial class BMD
    {
        [StructLayout(LayoutKind.Sequential, Size = Size)]
        struct Header
        {
            public const int Size = 0x18;

            public int Magic;     // 0x12345678
            public short Field04; // 1
            public short Field06; // 0
            public int Field08;   // 0
            public int FileSize;
            public int RelocationTableOffset;
            public int RelocationTableSize;
        }

        [StructLayout(LayoutKind.Sequential, Size = Size)]
        struct SubHeader
        {
            public const int Size = 0x10;

            public int PointerTableOffset; // relative
            public int NumMsg;
            public int SpeakerNameArrayOffset; // relative
            public int SpeakerCount;
        }

        public enum MSGType : int
        {
            Dialogue, // 0
            Selection // 1
        }

        // https://docs.microsoft.com/en-us/dotnet/framework/interop/default-marshaling-for-strings#type-library-representation
        [StructLayout(LayoutKind.Sequential, Size = Size)]
        public struct MSGHeader
        {
            public const int Size = 0x28;

            public MSGType Type;
#if !BRIDGE_DOTNET
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x20)]
#endif
            public byte[] Title;

            public short NumLine;

            public short SpeakerIndex;

            public MSGHeaderS ToMSGHeader()
            {
                return new MSGHeaderS()
                {
                    Type = (int)Type,
                    Title = Encoding.ASCII.GetString(Title).TrimEnd('\0'),
                    NumLine = NumLine,
                    SpeakerIndex = SpeakerIndex
                };
            }
        }
    }
}
