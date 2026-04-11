using AssetBankPlugin.Extensions;
using AssetBankPlugin.Enums;
using FrostySdk.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AssetBankPlugin.GenericData
{
    public class SectionRef2 : Section
    {
        public const string Identifier = "GD.REF2";
        public override Endian Endianness { get; set; }
        public override uint DataSize { get; set; }
        public override uint DataOffset { get; set; }
        public Dictionary<uint, GenericClass> Classes { get; set; } = new Dictionary<uint, GenericClass>();

        public SectionRef2(NativeReader r, Endian bankEndian)
        {
            long sectionStart = r.BaseStream.Position;
            DataOffset = (uint)sectionStart;

            string magic = r.ReadSizedString(7);
            _ = r.ReadByte();
            Endianness = bankEndian;
            DataSize = r.ReadUInt(Endianness);

            ulong layoutCount = r.ReadULong(Endianness);
            Console.WriteLine($"[REF2][DIAG] Start: 0x{sectionStart:X}, DataSize: {DataSize}, Count: {layoutCount}");

            long[] layoutOffsets = new long[layoutCount];
            for (int i = 0; i < (int)layoutCount; i++)
            {
                layoutOffsets[i] = r.ReadReference(Endianness);
            }

            var posToRawLayout = new Dictionary<long, RawLayout>();
            for (int i = 0; i < (int)layoutCount; i++)
            {
                if (layoutOffsets[i] == 0) continue;
                r.BaseStream.Position = layoutOffsets[i];
                posToRawLayout[layoutOffsets[i]] = ReadRawLayout(r);
            }

            foreach (var rawLayout in posToRawLayout.Values)
            {
                var genClass = new GenericClass
                {
                    Name = rawLayout.Name,
                    Size = (int)rawLayout.DataSize,
                    Alignment = (int)rawLayout.Alignment
                };

                Console.WriteLine($"[REF2] Layout: {genClass.Name} (0x{rawLayout.Hash:X8}) size={genClass.Size}");

                foreach (var entry in rawLayout.Entries)
                {
                    if (entry.LayoutHash == 0 || entry.LayoutHash == 0xFFFFFFFF) continue;

                    var field = new GenericField
                    {
                        Name = entry.Name,
                        TypeHash = entry.LayoutHash,
                        Offset = (int)entry.Offset,
                        Size = entry.ElementSize,
                        Alignment = entry.ElementAlign,
                        IsList = (entry.Flags & EFlags.Array) != 0,
                        InlineCount = entry.Count
                    };

                    if (PrimitiveTypeMap.IsPrimitive(entry.LayoutHash))
                    {
                        field.Type = PrimitiveTypeMap.GetTypeName(entry.LayoutHash);
                        field.IsNative = true;
                    }
                    else if (entry.LayoutRef != 0 && posToRawLayout.TryGetValue(entry.LayoutRef, out var linked))
                    {
                        field.Type = linked.Name;
                        field.IsNative = linked.IsPod;
                    }
                    else
                    {
                        field.Type = $"Class_0x{entry.LayoutHash:X8}";
                    }

                    genClass.Elements.Add(field);
                    Console.WriteLine($"  Field: {field.Name} type={field.Type} off=0x{field.Offset:X}");
                }

                if (rawLayout.Reordered)
                {
                    genClass.Elements.Sort((a, b) => a.Offset.CompareTo(b.Offset));
                }
                Classes[rawLayout.Hash] = genClass;
            }

            r.BaseStream.Position = sectionStart + DataSize;
        }

        private RawLayout ReadRawLayout(NativeReader r)
        {
            var l = new RawLayout();
            l.MinSlot = r.ReadInt(Endianness);
            l.MaxSlot = r.ReadInt(Endianness);
            l.DataSize = r.ReadUInt(Endianness);
            l.Alignment = r.ReadUInt(Endianness);
            uint stringTableSize = r.ReadUInt(Endianness);
            _ = r.ReadUInt(Endianness);
            l.Reordered = r.ReadByte() != 0;
            l.IsPod = r.ReadByte() != 0;
            _ = r.ReadUShort(Endianness);
            l.Hash = r.ReadUInt(Endianness);

            int count = l.MaxSlot - l.MinSlot + 1;
            l.Entries = new RawEntry[count];

            for (int i = 0; i < count; i++)
            {
                l.Entries[i] = new RawEntry
                {
                    LayoutHash = r.ReadUInt(Endianness),
                    ElementSize = r.ReadUInt(Endianness),
                    Offset = r.ReadUInt(Endianness),
                    NameIdx = r.ReadInt(Endianness),
                    Count = r.ReadUShort(Endianness),
                    Flags = (EFlags)r.ReadUShort(Endianness),
                    ElementAlign = r.ReadUShort(Endianness),
                    RLE = r.ReadShort(Endianness),
                    LayoutRef = r.ReadReference(Endianness)
                };
            }

            if (stringTableSize > 0)
            {
                byte[] strings = r.ReadBytes((int)stringTableSize);
                l.Name = GetString(strings, 1);
                for (int i = 0; i < count; i++)
                {
                    int idx = l.Entries[i].NameIdx;
                    l.Entries[i].Name = (idx >= 0 && idx < strings.Length) ? GetString(strings, idx) : "unknown";
                }
            }
            return l;
        }

        private string GetString(byte[] data, int idx)
        {
            int end = idx;
            while (end < data.Length && data[end] != 0) end++;
            return Encoding.ASCII.GetString(data, idx, end - idx);
        }

        private class RawLayout
        {
            public int MinSlot, MaxSlot;
            public uint DataSize, Alignment, Hash;
            public bool Reordered, IsPod;
            public string Name;
            public RawEntry[] Entries;
        }

        private class RawEntry
        {
            public uint LayoutHash, ElementSize, Offset;
            public int NameIdx;
            public ushort Count, ElementAlign;
            public short RLE;
            public EFlags Flags;
            public long LayoutRef;
            public string Name;
        }
    }
}