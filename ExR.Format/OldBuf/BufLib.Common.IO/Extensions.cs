using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BufLib.Common.IO
{
    public static class EndianBinaryReaderExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Align(this EndianBinaryReader reader, int alignment)
        {
            reader.BaseStream.Position = (reader.BaseStream.Position + (alignment - 1)) & ~(alignment - 1);
        }
    }

    public static class EndianBinaryWriterExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Align(this EndianBinaryWriter writer, int alignment)
        {
            var seek = AlignmentHelper.GetAlignedDifference(writer.BaseStream.Position, alignment);
            if (seek > 0)
                writer.Write(new byte[seek]);
        }
    }

    public static class ListExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Align<T>(this List<T> list, int alignment)
        {
            var seek = AlignmentHelper.GetAlignedDifference(list.Count, alignment);
            list.AddRange(new T[seek]);
        }
    }

    public static class ArrayExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Align(this byte[] array, int alignment)
        {
            Array.Resize(ref array, (array.Length + (alignment - 1)) & ~(alignment - 1));
            return array;
        }
    }
}
