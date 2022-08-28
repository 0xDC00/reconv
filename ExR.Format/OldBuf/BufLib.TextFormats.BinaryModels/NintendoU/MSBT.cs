using BufLib.Common.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BufLib.TextFormats.BinaryModels.NintendoU
{
    class MSBT
    {
        [DebuggerDisplay("{(string)this}")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Magic
        {
            int value;
            public static implicit operator int(Magic magic) => magic.value;
            public static implicit operator string(Magic magic) => Encoding.ASCII.GetString(BitConverter.GetBytes(magic.value));
            public static implicit operator Magic(string s) => new Magic { value = BitConverter.ToInt32(Encoding.ASCII.GetBytes(s), 0) };
            public static implicit operator Magic(int n) => new Magic { value = n };
        }

        [DebuggerDisplay("{(string)this}")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Magic8
        {
            long value;
            public static implicit operator long(Magic8 magic) => magic.value;
            public static implicit operator string(Magic8 magic) => Encoding.ASCII.GetString(BitConverter.GetBytes(magic.value));
            public static implicit operator Magic8(string s) => new Magic8 { value = BitConverter.ToInt64(Encoding.ASCII.GetBytes(s), 0) };
            public static implicit operator Magic8(long n) => new Magic8 { value = n };
        }

        public enum EncodingByte : byte
        {
            UTF8 = 0x00,
            Unicode = 0x01
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = Size)]
        public sealed class Header
        {
            public Magic8 Magic; // MsgStdBn
            public Endian ByteOrder;
            public ushort Unknown1; // Always 0x0000
            public EncodingByte EncodingByte;
            public byte Unknown2; // Always 0x03
            public ushort NumberOfSections;
            public ushort Unknown3; // Always 0x0000
            public uint FileSize;
#if !BRIDGE_DOTNET
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0xA)]
#endif
            public byte[] Padding;

            public const int Size = 0x20;
        }
        // 0x10 byte
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public sealed class Section
        {
            public Magic Magic;
            public uint Size;
#if !BRIDGE_DOTNET
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
#endif
            public byte[] Padding;
        }

        public sealed class LBL1
        {
            public Section Section { get; set; }
            public uint NumberOfGroups { get; set; }

            public List<Group> Groups = new List<Group>();
            public List<Label> Labels = new List<Label>();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public sealed class Group
        {
            public uint NumberOfLabels;
            public uint Offset;
        }

        public sealed class Label
        {
            public string Name { get; set; }
            public uint Index { get; set; }

            public uint Checksum { get; set; }
            public String String { get; set; }
            public string Attribute { get; set; }

            public string Text
            {
                get => String.Text;
                set => String.Text = value;
            }

            public Label()
            {
                Name = string.Empty;
                Index = 0;
                Checksum = 0;
                String = new String();
                Attribute = string.Empty;
            }
        }

        public sealed class NLI1
        {
            public Section Section { get; set; }
            public byte[] Unknown { get; set; } // Tons of unknown data
        }

        public sealed class ATO1
        {
            public Section Section { get; set; }
            public byte[] Unknown { get; set; } // Large collection of 0xFF
        }

        public sealed class ATR1
        {
            public Section Section { get; set; }
            public byte[] Unknown { get; set; } // Tons of unknown data
        }

        public sealed class TSY1
        {
            public Section Section { get; set; }
            public byte[] Unknown { get; set; } // Tons of unknown data
        }

        public sealed class TXT2
        {
            public Section Section { get; set; }
            public int NumberOfStrings { get; set; }

            public List<String> Strings = new List<String>();
        }

        public sealed class String
        {
            public string Text { get; set; }
            public uint Index { get; set; }
            public string LblName { get; set; }

            public String()
            {
                Text = string.Empty;
            }
        }

        public sealed class InvalidMSBTException : Exception
        {
            public InvalidMSBTException(string message) : base(message) { }
        }
    }
}
