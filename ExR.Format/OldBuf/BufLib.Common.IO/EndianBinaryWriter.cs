using System;
using System.IO;
using System.Text;
using static BufLib.Common.IO.EndiannessHelper;

namespace BufLib.Common.IO
{
    /// <summary>
    ///
    /// </summary>
    public partial class EndianBinaryWriter : BinaryWriter
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

        public EndianBinaryWriter(Stream input) : this(input, Endian.LittleEndian) { }
        public EndianBinaryWriter(Stream input, Encoding encoding) : this(input, encoding, Endian.LittleEndian) { }
        public EndianBinaryWriter(Stream input, Endian endianness) : this(input, Encoding.UTF8, endianness) { }

        public EndianBinaryWriter(Stream input, Encoding encoding, Endian endianness)
            : base(input, encoding)
        {
            this.Endianness = endianness;
        }

        public override void Write(float value)
        {
            Write(value, Endianness);
        }

        public void Write(float value, Endian endianness)
        {
            if (endianness == NativeEndianness)
                base.Write(value);
            else
            {
                byte[] bytes = BitConverter.GetBytes(value);
                Array.Reverse(bytes);
                base.Write(bytes);
            }
        }

        public void Write(float[] value)
        {
            if (Endianness == NativeEndianness)
            {
                for(int i=0; i<value.Length; i++)
                {
                    base.Write(value[i]);
                }
            }
            else
            {
                for (int i = 0; i < value.Length; i++)
                {
                    byte[] bytes = BitConverter.GetBytes(value[i]);
                    Array.Reverse(bytes);
                    base.Write(bytes);
                }
            }
        }

        public override void Write(double value)
        {
            Write(value, Endianness);
        }

        public void Write(double value, Endian endianness)
        {
            if (endianness == NativeEndianness)
                base.Write(value);
            else
            {
                byte[] bytes = BitConverter.GetBytes(value);
                Array.Reverse(bytes);
                base.Write(bytes);
            }
        }

        public void Write(double[] value)
        {
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    base.Write(value[i]);
                }
            }
            else
            {
                for (int i = 0; i < value.Length; i++)
                {
                    byte[] bytes = BitConverter.GetBytes(value[i]);
                    Array.Reverse(bytes);
                    base.Write(bytes);
                }
            }
        }

        public override void Write(short value)
        {
            Write(value, Endianness);
        }

        public void Write(short value, Endian endianness)
        {
            if (endianness == NativeEndianness)
                base.Write(value);
            else
                base.Write(Reverse(value));
        }

        public void Write(short[] value)
        {
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    base.Write(value[i]);
                }
            }
            else
            {
                for (int i = 0; i < value.Length; i++)
                {
                    base.Write(Reverse(value[i]));
                }
            }
        }

        public override void Write(ushort value)
        {
            Write(value, Endianness);
        }

        public void Write(ushort value, Endian endianness)
        {
            if (endianness == NativeEndianness)
                base.Write(value);
            else
                base.Write(Reverse(value));
        }

        public void Write(ushort[] value)
        {
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    base.Write(value[i]);
                }
            }
            else
            {
                for (int i = 0; i < value.Length; i++)
                {
                    base.Write(Reverse(value[i]));
                }
            }
        }

        public override void Write(int value)
        {
            Write(value, Endianness);
        }

        public void Write(int value, Endian endianness)
        {
            if (endianness == NativeEndianness)
                base.Write(value);
            else
                base.Write(Reverse(value));
        }

        public void Write(int[] value)
        {
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    base.Write(value[i]);
                }
            }
            else
            {
                for (int i = 0; i < value.Length; i++)
                {
                    base.Write(Reverse(value[i]));
                }
            }
        }

        public override void Write(uint value)
        {
            Write(value, Endianness);
        }

        public void Write(uint value, Endian endianness)
        {
            if (endianness == NativeEndianness)
                base.Write(value);
            else
                base.Write(Reverse(value));
        }

        public void Write(uint[] value)
        {
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    base.Write(value[i]);
                }
            }
            else
            {
                for (int i = 0; i < value.Length; i++)
                {
                    base.Write(Reverse(value[i]));
                }
            }
        }

        public override void Write(long value)
        {
            Write(value, Endianness);
        }

        public void Write(long value, Endian endianness)
        {
            if (endianness == NativeEndianness)
                base.Write(value);
            else
                base.Write(Reverse(value));
        }

        public void Write(long[] value)
        {
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    base.Write(value[i]);
                }
            }
            else
            {
                for (int i = 0; i < value.Length; i++)
                {
                    base.Write(Reverse(value[i]));
                }
            }
        }

        public override void Write(ulong value)
        {
            Write(value, Endianness);
        }

        public void Write(ulong value, Endian endianness)
        {
            if (endianness == NativeEndianness)
                base.Write(value);
            else
                base.Write(Reverse(value));
        }

        public void Write(ulong[] value)
        {
            if (Endianness == NativeEndianness)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    base.Write(value[i]);
                }
            }
            else
            {
                for (int i = 0; i < value.Length; i++)
                {
                    base.Write(Reverse(value[i]));
                }
            }
        }
    }
}
