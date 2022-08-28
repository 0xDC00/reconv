using System;
using System.Linq;
using System.Runtime.CompilerServices;
#if !BRIDGE_DOTNET
using System.Runtime.InteropServices;
#endif

namespace BufLib.Common.IO
{
    /// <summary>
    /// Specifies an endianness
    /// </summary>
    public enum Endian : ushort
    {
        /// <summary>
        /// Little endian (i.e. DDCCBBAA)
        /// </summary>
        LittleEndian = 0xFEFF,

        /// <summary>
        /// Big endian (i.e. AABBCCDD)
        /// </summary>
        BigEndian = 0xFFFE
    }

    public static class EndiannessHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Reverse(short value)
        {
            return (short)(
                ((value & 0xFF00) >> 8) << 0 |
                ((value & 0x00FF) >> 0) << 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Reverse(ushort value)
        {
            return (ushort)(
                ((value & 0xFF00) >> 8) << 0 |
                ((value & 0x00FF) >> 0) << 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Reverse(int value)
        {
            return (int)(
                (((uint)value & 0xFF000000) >> 24) << 0 |
                (((uint)value & 0x00FF0000) >> 16) << 8 |
                (((uint)value & 0x0000FF00) >> 8) << 16 |
                (((uint)value & 0x000000FF) >> 0) << 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Reverse(uint value)
        {
            return (uint)(
                ((value & 0xFF000000) >> 24) << 0 |
                ((value & 0x00FF0000) >> 16) << 8 |
                ((value & 0x0000FF00) >> 8) << 16 |
                ((value & 0x000000FF) >> 0) << 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Reverse(long value)
        {
            return (long)(
                (((ulong)value & 0xFF00000000000000UL) >> 56) << 0 |
                (((ulong)value & 0x00FF000000000000UL) >> 48) << 8 |
                (((ulong)value & 0x0000FF0000000000UL) >> 40) << 16 |
                (((ulong)value & 0x000000FF00000000UL) >> 32) << 24 |
                (((ulong)value & 0x00000000FF000000UL) >> 24) << 32 |
                (((ulong)value & 0x0000000000FF0000UL) >> 16) << 40 |
                (((ulong)value & 0x000000000000FF00UL) >> 8) << 48 |
                (((ulong)value & 0x00000000000000FFUL) >> 0) << 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Reverse(ulong value)
        {
            return (ulong)(
                ((value & 0xFF00000000000000UL) >> 56) << 0 |
                ((value & 0x00FF000000000000UL) >> 48) << 8 |
                ((value & 0x0000FF0000000000UL) >> 40) << 16 |
                ((value & 0x000000FF00000000UL) >> 32) << 24 |
                ((value & 0x00000000FF000000UL) >> 24) << 32 |
                ((value & 0x0000000000FF0000UL) >> 16) << 40 |
                ((value & 0x000000000000FF00UL) >> 8) << 48 |
                ((value & 0x00000000000000FFUL) >> 0) << 56);
        }

#if !BRIDGE_DOTNET
        // TODO: https://github.com/TGEnigma/Amicitia.IO/blob/95a874f5d094f0487002a8b66684e67a2c461c51/src/Amicitia.IO/Binary/BinaryOperations.cs#L29
        // from: https://github.com/IcySon55/Kuriimu/blob/master/src/Kontract/IO/Extensions.cs#L35
        public static void AdjustByteOrder(Type type, byte[] buffer, int startOffset = 0)
        {
            if (type.IsPrimitive)
            {
                if (type == typeof(short) || type == typeof(ushort) ||
                    type == typeof(int) || type == typeof(uint) ||
                    type == typeof(long) || type == typeof(ulong))
                {
                    Array.Reverse(buffer);
                    return;
                }
            }

            foreach (var field in type.GetFields())
            {
                var fieldType = field.FieldType;

                // Ignore static fields
                if (field.IsStatic) continue;

                if (fieldType.BaseType == typeof(Enum) && fieldType != typeof(Endian))
                    fieldType = fieldType.GetFields()[0].FieldType;

                // Swap bytes only for the following types (incomplete just like BinaryReaderX is)
                if (fieldType == typeof(short) || fieldType == typeof(ushort) ||
                    fieldType == typeof(int) || fieldType == typeof(uint) ||
                    fieldType == typeof(long) || fieldType == typeof(ulong))
                {
                    var offset = Marshal.OffsetOf(type, field.Name).ToInt32();

                    // Enums
                    if (fieldType.IsEnum)
                        fieldType = Enum.GetUnderlyingType(fieldType);

                    // Check for sub-fields to recurse if necessary
                    var subFields = fieldType.GetFields().Where(subField => subField.IsStatic == false).ToArray();
                    var effectiveOffset = startOffset + offset;

                    if (subFields.Length == 0)
                        Array.Reverse(buffer, effectiveOffset, Marshal.SizeOf(fieldType));
                    else
                        AdjustByteOrder(fieldType, buffer, effectiveOffset);
                }
            }
        }
#endif
    }
}
