using Frosty.Controls;
using FrostySdk.IO;
using System;

namespace AssetBankPlugin.GenericData
{
    public abstract class Section
    {
        public abstract Endian Endianness { get; set; }
        public abstract uint DataSize { get; set; }
        public abstract uint DataOffset { get; set; }

        public static Section ReadSection(NativeReader r)
        {
            if (r.BaseStream.Position + 12 > r.BaseStream.Length)
                return null;

            long startPos = r.BaseStream.Position;

            // PEEK first 3 bytes to see if we are at a "GD." tag
            string peek = r.ReadSizedString(3);
            r.BaseStream.Position = startPos; // Reset immediately

            if (peek != "GD.")
            {
                // Not a header. This is likely alignment padding (00 00 00...)
                return null;
            }

            // Now read the actual header
            string blockType = r.ReadSizedString(7); // "GD.REF2", "GD.DAT2" etc
            byte endianChar = (byte)r.ReadByte();    // 'l' or 'b'
            Endian endian = (endianChar == 'b') ? Endian.Big : Endian.Little;
            uint size = r.ReadUInt(endian);

            // Reset position so the specific Section constructor can read the full 12 bytes?
            r.BaseStream.Position = startPos;

            switch (blockType)
            {
                case SectionStrm.Identifier: return new SectionStrm(r, endian);
                case SectionRefl.Identifier: return new SectionRefl(r, endian);
                case SectionData.Identifier: return new SectionData(r, endian);
                case SectionRef2.Identifier: return new SectionRef2(r, endian);
                case SectionData2.Identifier: return new SectionData2(r, endian);
                default:
                    Console.WriteLine($"[DEBUG] Found unknown GD tag '{blockType}' at 0x{startPos:X}. Skipping {size} bytes.");
                    r.BaseStream.Position = startPos + 12 + size;
                    return null;
            }
        }
    }
}