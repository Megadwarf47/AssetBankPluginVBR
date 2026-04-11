using AssetBankPlugin.Ant;
using AssetBankPlugin.Enums;
using FrostySdk;
using FrostySdk.IO;
using System;
using System.Collections.Generic;

namespace AssetBankPlugin.GenericData
{
    public class Bank
    {
        public uint PackagingType { get; set; }
        public List<Section> Sections { get; set; } = new List<Section>();
        public Dictionary<uint, GenericClass> Classes { get; set; } = new Dictionary<uint, GenericClass>();
        public Dictionary<string, Guid> DataNames { get; set; } = new Dictionary<string, Guid>();

        public Bank(NativeReader r, int bundleId)
        {
            PackagingType = r.ReadUInt(Endian.Big);

            switch ((ProfileVersion)ProfilesLibrary.DataVersion)
            {
                case ProfileVersion.PlantsVsZombiesGardenWarfare2:
                case ProfileVersion.PlantsVsZombiesGardenWarfare:
                case ProfileVersion.Battlefield4:
                case ProfileVersion.Battlefield1:
                    {
                        if (PackagingType == 3)
                        {
                            r.BaseStream.Position = 56;
                            uint antRefMapCount = r.ReadUInt(Endian.Big) / 20;

                            for (int i = 0; i < antRefMapCount; i++)
                            {
                                Guid a = r.ReadGuid();
                                byte[] bytes = new byte[16];
                                bytes[0] = r.ReadByte();
                                bytes[1] = r.ReadByte();
                                bytes[2] = r.ReadByte();
                                bytes[3] = r.ReadByte();

                                Guid b = new Guid(bytes);
                                AntRefTable.InternalRefs[a] = b;
                                Cache.AntRefMap[a] = b;
                            }
                            r.BaseStream.Position = 4;
                        }
                    }
                    break;
            }

            uint headerStart = (uint)r.BaseStream.Position;
            uint headerSize;

            // Some AntPackages seem to have no header. In that case, jump directly to reading the sections.
            string str = r.ReadSizedString(3);
            bool hasHeader = str != "GD.";
            r.BaseStream.Position -= 3;
            if (hasHeader)
                headerSize = r.ReadUInt(Endian.Big);
            else
                headerSize = 0;

            r.BaseStream.Position = headerStart + headerSize;

            //Read all sections
            while (r.BaseStream.Position < r.BaseStream.Length)
            {
                Sections.Add(Section.ReadSection(r));
            }

            //Process sections
            List<SectionData2> data2Sections = new List<SectionData2>();

            for (int i = 0; i < Sections.Count; i++)
            {
                var section = Sections[i];
                if (section is SectionStrm)
                {
                    // STRM sections are ignored
                }
                else if (section is SectionRefl reflSection)
                {
                    // overwrite the entire Classes dictionary
                    Classes = reflSection.Classes;
                }
                else if (section is SectionRef2 ref2Section)
                {
                    // NEW: Support for SectionRef2
                    foreach (var kvp in ref2Section.Classes)
                        Classes[kvp.Key] = kvp.Value;
                }
                else if (section is SectionData dataSection)
                {
                    // Original inline deserialization for SectionData
                    var asset = AntAsset.Deserialize(r, dataSection, Classes, this);
                    if (asset != null)
                    {
                        AddAsset(asset, bundleId);
                    }
                }
                else if (section is SectionData2 data2Section)
                {
                    // NEW: Collect SectionData2 for second pass
                    data2Sections.Add(data2Section);
                }
            }

            // NEW: Second pass for SectionData2 (only affects files that contain DAT2)
            foreach (var data2Section in data2Sections)
            {
                long headerOffset = data2Section.DataOffset + 28; // DAT2 header is 28 bytes
                int objIdx = 0;

                while (headerOffset + 16 < data2Section.DataOffset + data2Section.DataSize)
                {
                    var asset = AntAsset.Deserialize(r, data2Section, Classes, this, headerOffset);
                    if (asset != null)
                    {
                        AddAsset(asset, bundleId);
#if DEBUG
                        Console.WriteLine($"[DAT2][DIAG] Parsed Object {objIdx} at 0x{headerOffset:X}: {asset.GetType().Name}");
#endif
                    }

                    headerOffset = data2Section.GetNextObjectOffset(headerOffset, Classes);
                    if (headerOffset == -1) break;
                    objIdx++;
                }
            }
        }

        // Helper method
        private void AddAsset(AntAsset asset, int bundleId)
        {
            string name = asset.Name;
            int index = 0;
            while (DataNames.ContainsKey(name))
            {
                name = asset.Name + " [" + index + "]";
                index++;
            }
            DataNames.Add(name, asset.ID);
            Cache.AntStateBundleIndices[asset.ID] = bundleId;
        }
    }
}