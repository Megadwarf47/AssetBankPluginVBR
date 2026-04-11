using AssetBankPlugin.Enums;
using AssetBankPlugin.GenericData;
using FrostySdk.IO;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace AssetBankPlugin.Extensions
{
    public static class NativeReaderExtensions
    {
        // Aligns the stream to the specified boundary ( 8 byts)
        public static void Align(this NativeReader r, int alignment)
        {
            long pos = r.BaseStream.Position;
            long mod = pos % alignment;
            if (mod != 0)
                r.BaseStream.Position += (alignment - mod);
        }

        public static long ReadReference(this NativeReader r, Endian endian)
        {
            long basePos = r.BaseStream.Position;
            long val = r.ReadLong(endian);
            if (val == 0 || val == -1) return 0;

            // Top 4 bits (63‑60) are flags, bottom 60 bits are signed offset.
            long offset = (val << 4) >> 4;
            long absolute = basePos + offset;

            if (absolute < 0 || absolute > r.BaseStream.Length)
            {
                throw new InvalidDataException(
                    $"[FATAL] Pointer out of bounds!\n" +
                    $"Location: 0x{basePos:X}\n" +
                    $"Raw Val: 0x{val:X}\n" +
                    $"Extracted Offset: 0x{offset:X}\n" +
                    $"Target: 0x{absolute:X}\n" +
                    $"File length: 0x{r.BaseStream.Length:X}");
            }

            return absolute;
        }

        public static ReflLayout ReadReflLayoutEntry(this NativeReader r, Endian bigEndian, out int fieldSize)
        {
            var layout = new ReflLayout();
            layout.mMinSlot = r.ReadInt(bigEndian);
            layout.mMaxSlot = r.ReadInt(bigEndian);
            fieldSize = (layout.mMinSlot * -1) + layout.mMaxSlot + 1;
            layout.mDataSize = r.ReadUInt(bigEndian);
            layout.mAlignment = r.ReadUInt(bigEndian);
            layout.mStringTableOffset = r.ReadUInt(bigEndian);
            layout.mStringTableLength = r.ReadUInt(bigEndian);
            layout.mReordered = r.ReadBoolean();
            layout.mNative = r.ReadBoolean();
            _ = r.ReadBytes(2);
            layout.mHash = r.ReadUInt(bigEndian);
            layout.mEntries = new ReflEntry[fieldSize];
            for (int j = 0; j < fieldSize; j++)
            {
            LABEL_1:
                var entry = new ReflEntry();
                entry.mLayoutHash = r.ReadUInt(bigEndian);
                entry.mElementSize = r.ReadUInt(bigEndian);
                entry.mOffset = r.ReadUInt(bigEndian);
                entry.mName = r.ReadUInt(bigEndian);
                entry.mCount = r.ReadUShort(bigEndian);
                entry.mFlags = (EFlags)r.ReadUShort(bigEndian);
                entry.mElementAlign = r.ReadUShort(bigEndian);
                entry.mRLE = r.ReadShort(bigEndian);
                entry.mLayout = r.ReadLong(bigEndian);
                if (entry.mElementSize != 0 && entry.mCount != 0) layout.mEntries[j] = entry;
                else { fieldSize -= 1; goto LABEL_1; }
            }
            _ = r.ReadBytes(1);
            layout.mName = r.ReadNullTerminatedString();
            layout.mFieldNames = new string[fieldSize];
            for (int j = 0; j < fieldSize; j++) layout.mFieldNames[j] = r.ReadNullTerminatedString();
            return layout;
        }

        public static ReflLayout ReadRef2LayoutEntry(this NativeReader r, Endian endian, out int fieldCount)
        {
            var layout = new ReflLayout();
            layout.mMinSlot = r.ReadInt(endian);
            layout.mMaxSlot = r.ReadInt(endian);
            layout.mDataSize = (uint)r.ReadInt(endian);
            layout.mAlignment = (uint)r.ReadInt(endian);
            _ = r.ReadUInt(endian);
            int stringTableLength = r.ReadInt(endian);
            layout.mReordered = r.ReadByte() != 0;
            layout.mNative = r.ReadByte() != 0;
            _ = r.ReadUShort(endian);
            layout.mHash = r.ReadUInt(endian);
            fieldCount = layout.mMaxSlot - layout.mMinSlot + 1;
            layout.mEntries = new ReflEntry[fieldCount];
            var nameIndices = new int[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                var entry = new ReflEntry();
                entry.mLayoutHash = r.ReadUInt(endian);
                entry.mElementSize = (uint)(short)r.ReadInt(endian);
                entry.mOffset = (uint)(short)r.ReadInt(endian);
                nameIndices[i] = (short)r.ReadInt(endian);
                entry.mCount = r.ReadUShort(endian);
                entry.mFlags = (EFlags)r.ReadUShort(endian);
                entry.mElementAlign = r.ReadUShort(endian);
                entry.mRLE = r.ReadShort(endian);
                entry.mLayout = r.ReadReference(endian);
                layout.mEntries[i] = entry;
            }
            if (stringTableLength > 0)
            {
                byte[] stringData = r.ReadBytes(stringTableLength);
                layout.mName = GetString(stringData, 1);
                layout.mFieldNames = new string[fieldCount];
                for (int i = 0; i < fieldCount; i++) layout.mFieldNames[i] = (nameIndices[i] >= 0) ? GetString(stringData, nameIndices[i]) : "";
            }
            return layout;
        }

        private static string GetString(byte[] data, int offset)
        {
            if (data == null || offset >= data.Length) return "";
            int end = offset;
            while (end < data.Length && data[end] != 0) end++;
            return Encoding.ASCII.GetString(data, offset, end - offset);
        }

        public static void ReadDataHeader(this NativeReader r, Endian bigEndian, out uint hash, out uint type, out uint offset)
        {
            hash = (uint)r.ReadULong(bigEndian);
            r.ReadBytes(8);
            type = (uint)r.ReadULong(bigEndian);
            r.ReadBytes(4);
            offset = r.ReadUShort(bigEndian);
            r.ReadBytes(2);
        }

        // Array Helpers
        public static sbyte[] ReadSByteArray(this NativeReader r, int count) { var a = new sbyte[count]; for (int i = 0; i < count; i++) a[i] = r.ReadSByte(); return a; }
        public static byte[] ReadByteArray(this NativeReader r, int count) { var a = new byte[count]; for (int i = 0; i < count; i++) a[i] = r.ReadByte(); return a; }
        public static short[] ReadShortArray(this NativeReader r, int count, Endian e) { var a = new short[count]; for (int i = 0; i < count; i++) a[i] = r.ReadShort(e); return a; }
        public static ushort[] ReadUShortArray(this NativeReader r, int count, Endian e) { var a = new ushort[count]; for (int i = 0; i < count; i++) a[i] = r.ReadUShort(e); return a; }
        public static int[] ReadIntArray(this NativeReader r, int count, Endian e) { var a = new int[count]; for (int i = 0; i < count; i++) a[i] = r.ReadInt(e); return a; }
        public static uint[] ReadUIntArray(this NativeReader r, int count, Endian e) { var a = new uint[count]; for (int i = 0; i < count; i++) a[i] = r.ReadUInt(e); return a; }
        public static long[] ReadLongArray(this NativeReader r, int count, Endian e) { var a = new long[count]; for (int i = 0; i < count; i++) a[i] = r.ReadLong(e); return a; }
        public static ulong[] ReadULongArray(this NativeReader r, int count, Endian e) { var a = new ulong[count]; for (int i = 0; i < count; i++) a[i] = r.ReadULong(e); return a; }
        public static float[] ReadSingleArray(this NativeReader r, int count, Endian e) { var a = new float[count]; for (int i = 0; i < count; i++) a[i] = r.ReadFloat(e); return a; }
        public static double[] ReadDoubleArray(this NativeReader r, int count, Endian e) { var a = new double[count]; for (int i = 0; i < count; i++) a[i] = r.ReadDouble(e); return a; }
        public static Guid[] ReadGuidArray(this NativeReader r, int count, Endian e) { var a = new Guid[count]; for (int i = 0; i < count; i++) a[i] = r.ReadGuid(e); return a; }
    }
}