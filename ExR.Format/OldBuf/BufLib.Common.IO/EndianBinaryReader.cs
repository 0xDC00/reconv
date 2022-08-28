/*
from: https://github.com/xdanieldzd/Scarlet/blob/master/Scarlet/IO/EndianBinaryReader.cs

https://github.com/TGEnigma/Amicitia.IO
*/

using System;
using System.IO;
using System.Text;
using static BufLib.Common.IO.EndiannessHelper;

namespace BufLib.Common.IO
{
    /// <summary>
    /// Read data from stream with data of specified endianness
    /// </summary>
    public partial class EndianBinaryReader : BinaryReader
    {
        /* TODO: BIGENDIAN check taken from BitConverter source; does this work as intended? */
#if BIGENDIAN
        public const Endian NativeEndianness = Endian.BigEndian;
#else
        public const Endian NativeEndianness = Endian.LittleEndian;
#endif

        /// <summary>
        /// Currently specified endianness
        /// </summary>
        public Endian Endianness { get; set; }

        /// <summary>
        /// Boolean representing if the currently specified endianness equal to the system's native endianness
        /// </summary>
        public bool IsNativeEndianness { get { return (NativeEndianness == Endianness); } }

        /* TODO: doublecheck every non-native read result; slim down reverse functions? */

        public EndianBinaryReader(Stream input) : this(input, Endian.LittleEndian) { }
        public EndianBinaryReader(Stream input, Encoding encoding) : this(input, encoding, Endian.LittleEndian) { }
        public EndianBinaryReader(Stream input, Endian endianness) : this(input, Encoding.UTF8, endianness) { }

        public EndianBinaryReader(Stream input, Encoding encoding, Endian endianness)
            : base(input, encoding)
        {
            this.Endianness = endianness;
        }

        public override float ReadSingle()
        {
            return ReadSingle(Endianness);
        }

        public float ReadSingle(Endian endianness)
        {
            if (endianness == NativeEndianness)
                return base.ReadSingle();
            else
                return BitConverter.ToSingle(BitConverter.GetBytes(Reverse(base.ReadUInt32())), 0);
        }

        public float[] ReadSingles(int count)
        {
            float[] array = new float[count];
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = base.ReadSingle();
            }
            else
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = BitConverter.ToSingle(BitConverter.GetBytes(Reverse(base.ReadUInt32())), 0);
            }

            return array;
        }

        public override double ReadDouble()
        {
            return ReadDouble(Endianness);
        }

        public double ReadDouble(Endian endianness)
        {
            if (endianness == NativeEndianness)
                return base.ReadDouble();
            else
            {
                return BitConverter.ToDouble(BitConverter.GetBytes(Reverse(base.ReadUInt64())), 0);
            }
        }

        public double[] ReadDoubles(int count)
        {
            double[] array = new double[count];
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = base.ReadDouble();
            }
            else
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = BitConverter.ToDouble(BitConverter.GetBytes(Reverse(base.ReadUInt64())), 0);
            }

            return array;
        }

        public override short ReadInt16()
        {
            return ReadInt16(Endianness);
        }

        public short ReadInt16(Endian endianness)
        {
            if (endianness == NativeEndianness)
                return base.ReadInt16();
            else
                return Reverse(base.ReadInt16());
        }

        public short[] ReadInt16s(int count)
        {
            short[] array = new short[count];
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = base.ReadInt16();
            }
            else
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = Reverse(base.ReadInt16());
            }

            return array;
        }

        public override ushort ReadUInt16()
        {
            return ReadUInt16(Endianness);
        }

        public ushort ReadUInt16(Endian endianness)
        {
            if (endianness == NativeEndianness)
                return base.ReadUInt16();
            else
                return Reverse(base.ReadUInt16());
        }

        public ushort[] ReadUInt16s(int count)
        {
            ushort[] array = new ushort[count];
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = base.ReadUInt16();
            }
            else
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = Reverse(base.ReadUInt16());
            }

            return array;
        }

        public override int ReadInt32()
        {
            return ReadInt32(Endianness);
        }

        public int ReadInt32(Endian endianness)
        {
            if (endianness == NativeEndianness)
                return base.ReadInt32();
            else
                return Reverse(base.ReadInt32());
        }

        public int[] ReadInt32s(int count)
        {
            int[] array = new int[count];
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = base.ReadInt32();
            }
            else
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = Reverse(base.ReadInt32());
            }

            return array;
        }

        public override uint ReadUInt32()
        {
            return ReadUInt32(Endianness);
        }

        public uint ReadUInt32(Endian endianness)
        {
            if (endianness == NativeEndianness)
                return base.ReadUInt32();
            else
                return Reverse(base.ReadUInt32());
        }

        public uint[] ReadUInt32s(int count)
        {
            uint[] array = new uint[count];
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = base.ReadUInt32();
            }
            else
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = Reverse(base.ReadUInt32());
            }

            return array;
        }

        public override long ReadInt64()
        {
            return ReadInt64(Endianness);
        }

        public long ReadInt64(Endian endianness)
        {
            if (endianness == NativeEndianness)
                return base.ReadInt64();
            else
                return Reverse(base.ReadInt64());
        }

        public long[] ReadInt64s(int count)
        {
            long[] array = new long[count];
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = base.ReadInt64();
            }
            else
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = Reverse(base.ReadInt64());
            }

            return array;
        }

        public override ulong ReadUInt64()
        {
            return ReadUInt64(Endianness);
        }

        public ulong ReadUInt64(Endian endianness)
        {
            if (endianness == NativeEndianness)
                return base.ReadUInt64();
            else
                return Reverse(base.ReadUInt64());
        }

        public ulong[] ReadUInt64s(int count)
        {
            ulong[] array = new ulong[count];
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = base.ReadUInt64();
            }
            else
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = Reverse(base.ReadUInt64());
            }

            return array;
        }
    }
}