using BufLib.Common.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BufLib.TextFormats.BinaryModels.NieRAutomata
{
    internal static partial class BIN
    {
        [StructLayout(LayoutKind.Sequential, Size = Size, Pack = 1)]
        public struct RITE
        {
            public const int Size = 0x16;

            public int Magic;
            public int Verion;
            public ushort CRC16_CCITT; // REPACK -> fix
            public uint FileSize; // REPACK -> fix
            public int Compiler_name;
            public int Compiler_version;

            public RITE(EndianBinaryReader br)
            {
                Magic = br.ReadInt32();
                Verion = br.ReadInt32();
                CRC16_CCITT = br.ReadUInt16();
                FileSize = br.ReadUInt32();
                Compiler_name = br.ReadInt32();
                Compiler_version = br.ReadInt32();
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct IREP_SECTION
        {

            public int signature;
            public int Size; // REPACK -> fix
            public int vm_version;

            public IREP_SECTION(EndianBinaryReader br)
            {
                signature = br.ReadInt32();
                Size = br.ReadInt32();
                vm_version = br.ReadInt32();
            }
        }
    }
}
