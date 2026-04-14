using AssetBankPlugin.Extensions;
using AssetBankPlugin.Enums;
using FrostySdk.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

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

            // First pass create GenericClass for all layouts
            foreach (var rawLayout in posToRawLayout.Values)
            {
                var genClass = new GenericClass
                {
                    Name = rawLayout.Name,
                    Size = (int)rawLayout.DataSize,
                    Alignment = (int)rawLayout.Alignment
                };

                Console.WriteLine($"[REF2] Layout: {genClass.Name} (0x{rawLayout.Hash:X8}) size={genClass.Size} align={genClass.Alignment}");

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
                    Console.WriteLine($"  Field: {field.Name} type={field.Type} off=0x{field.Offset:X} size={field.Size} align={field.Alignment} isList={field.IsList} inlineCount={field.InlineCount}");
                }

                if (rawLayout.Reordered)
                {
                    genClass.Elements.Sort((a, b) => a.Offset.CompareTo(b.Offset));
                }
                Classes[rawLayout.Hash] = genClass;
            }

            // Second pass: resolve element types for array fields
            foreach (var cls in Classes.Values.ToList())
            {
                foreach (var field in cls.Elements)
                {
                    if (field.IsList)
                    {
                        if (!Classes.TryGetValue(field.TypeHash, out var arrayLayout))
                        {
                            if (PrimitiveTypeMap.IsPrimitive(field.TypeHash))
                            {
                                field.ElementTypeHash = field.TypeHash;
                                field.ElementSize = (uint)PrimitiveTypeMap.GetTypeSize(field.TypeHash);
                                field.ElementAlignment = Math.Min(field.ElementSize, 8u);
                                field.Type = PrimitiveTypeMap.GetTypeName(field.TypeHash) + "[]";
                                Console.WriteLine($"[REF2] Array field '{field.Name}' using primitive fallback: element size={field.ElementSize}");
                            }
                            else
                            {
                                Console.WriteLine($"[REF2] ERROR: Could not find array layout for hash 0x{field.TypeHash:X8} (field '{field.Name}')");
                                continue;
                            }
                        }
                        else
                        {
                            var elemField = arrayLayout.Elements.FirstOrDefault(e => e.TypeHash != 0);
                            if (elemField != null)
                            {
                                field.ElementTypeHash = elemField.TypeHash;
                                field.ElementSize = elemField.Size;
                                field.ElementAlignment = elemField.Alignment;
                                if (PrimitiveTypeMap.IsPrimitive(elemField.TypeHash))
                                    field.Type = PrimitiveTypeMap.GetTypeName(elemField.TypeHash) + "[]";
                                else if (Classes.TryGetValue(elemField.TypeHash, out var elemClass))
                                    field.Type = elemClass.Name + "[]";
                                else
                                    field.Type = $"Class_0x{elemField.TypeHash:X8}[]";
                            }
                            else
                            {
                                Console.WriteLine($"[REF2] WARNING: Array layout for '{field.Name}' has no valid element entry");
                                if (PrimitiveTypeMap.IsPrimitive(field.TypeHash))
                                {
                                    field.ElementTypeHash = field.TypeHash;
                                    field.ElementSize = (uint)PrimitiveTypeMap.GetTypeSize(field.TypeHash);
                                    field.ElementAlignment = Math.Min(field.ElementSize, 8u);
                                    field.Type = PrimitiveTypeMap.GetTypeName(field.TypeHash) + "[]";
                                }
                            }
                        }

                        if (field.ElementSize > 0)
                            Console.WriteLine($"[REF2] Array field '{field.Name}' element type={field.Type} element size={field.ElementSize} element align={field.ElementAlignment}");
                    }
                    else if (field.InlineCount > 1)
                    {
                        field.ElementTypeHash = field.TypeHash;
                        field.ElementSize = field.Size;
                        field.ElementAlignment = field.Alignment;
                        if (PrimitiveTypeMap.IsPrimitive(field.TypeHash))
                            field.Type = $"{PrimitiveTypeMap.GetTypeName(field.TypeHash)}[{field.InlineCount}]";
                        else if (Classes.TryGetValue(field.TypeHash, out var elemClass))
                            field.Type = $"{elemClass.Name}[{field.InlineCount}]";
                        else
                            field.Type = $"Class_0x{field.TypeHash:X8}[{field.InlineCount}]";
                    }
                }
            }

            // Third pass resolve all zero sizes/alignments from class layouts
            ResolveClassSizes();

            r.BaseStream.Position = sectionStart + DataSize;
        }

        private void ResolveClassSizes()
        {
            bool changed;
            do
            {
                changed = false;
                foreach (var cls in Classes.Values)
                {
                    foreach (var field in cls.Elements)
                    {
                        // Correct field.Size for nonprimitive inline fields
                        if (field.Size == 0 && field.TypeHash != 0 && !PrimitiveTypeMap.IsPrimitive(field.TypeHash))
                        {
                            if (Classes.TryGetValue(field.TypeHash, out var fieldClass))
                            {
                                field.Size = (uint)fieldClass.Size;
                                field.Alignment = (uint)fieldClass.Alignment;
                                Console.WriteLine($"[REF2] Corrected field '{field.Name}' size to {field.Size} from class layout.");
                                changed = true;
                            }
                        }

                        // Correct element size for list fields
                        if (field.IsList)
                        {
                            if (field.ElementSize == 0 && field.ElementTypeHash != 0 && !PrimitiveTypeMap.IsPrimitive(field.ElementTypeHash))
                            {
                                if (Classes.TryGetValue(field.ElementTypeHash, out var elemClass))
                                {
                                    field.ElementSize = (uint)elemClass.Size;
                                    field.ElementAlignment = (uint)elemClass.Alignment;
                                    Console.WriteLine($"[REF2] Corrected list element size for '{field.Name}' to {field.ElementSize}");
                                    changed = true;
                                }
                            }
                            // Also ensur ethe inline field size for the list (which is the heap header) is correct (should be 16)
                            // The raw entry already sets Size = 16 but if its 0 we set it
                            if (field.Size == 0)
                            {
                                field.Size = 16; // capacity(4) + count(4) + pointer(8)
                                field.Alignment = 8;
                                Console.WriteLine($"[REF2] Corrected list field '{field.Name}' inline size to 16.");
                                changed = true;
                            }
                        }
                        // For inline fixed arrays
                        else if (field.InlineCount > 1)
                        {
                            if (field.ElementSize == 0 && field.ElementTypeHash != 0 && !PrimitiveTypeMap.IsPrimitive(field.ElementTypeHash))
                            {
                                if (Classes.TryGetValue(field.ElementTypeHash, out var elemClass))
                                {
                                    field.ElementSize = (uint)elemClass.Size;
                                    field.ElementAlignment = (uint)elemClass.Alignment;
                                    Console.WriteLine($"[REF2] Corrected inline array element size for '{field.Name}' to {field.ElementSize}");
                                    changed = true;
                                }
                            }
                        }
                    }
                }
            } while (changed);
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
