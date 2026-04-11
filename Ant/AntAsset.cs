using AssetBankPlugin.Extensions;
using AssetBankPlugin.GenericData;
using FrostySdk.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetBankPlugin.Ant
{
    public abstract class AntAsset
    {
        public abstract string Name { get; set; }
        public abstract Guid ID { get; set; }

        public Bank Bank { get; set; }

        public abstract void SetData(Dictionary<string, object> data);

        public static AntAsset Deserialize(NativeReader r, SectionData section, Dictionary<uint, GenericClass> classes, Bank bank)
        {
            r.BaseStream.Position = section.DataOffset;
            r.ReadDataHeader(section.Endianness, out uint hash, out uint type, out uint offset);

            var values = section.ReadValues(r, classes, section.DataOffset + offset, type);

            // Add the base values if class inherits from another class.
            if ((long)values["__base"] != 0)
            {
                r.BaseStream.Position = section.DataOffset + (long)values["__base"];

                r.ReadDataHeader(section.Endianness, out uint base_hash, out uint base_type, out uint base_offset);

                var baseValues = section.ReadValues(r,
                    classes,
                    section.DataOffset + base_offset + Convert.ToUInt32(values["__base"]),
                    base_type);

                foreach (var value in baseValues)
                {
                    if (!values.ContainsKey(value.Key))
                        values.Add(value.Key, value.Value);
                }
            }

            Type assetType = Type.GetType("AssetBankPlugin.Ant." + classes[type].Name);

            // If assetType is not null, that means we have a supported AntAsset. In that case, parse it and add it to the AntRefTable.
            if (assetType != null)
            {
                AntAsset asset = (AntAsset)Activator.CreateInstance(assetType);
                asset.Bank = bank;
                asset.SetData(values);

                AntRefTable.Add(asset);
                return asset;
            }
            else
            {
                return null;
            }
        }

        // New Deserialize using SectionData2 (with diagnostic dump)
        public static AntAsset Deserialize(NativeReader r, SectionData2 section, Dictionary<uint, GenericClass> classes, Bank bank, long objectHeaderOffset)
        {
            var values = section.ReadObjectAt(objectHeaderOffset, classes);
            if (values == null) return null;

            var patchedReader = section.GetPatchedReader();
            patchedReader.BaseStream.Position = objectHeaderOffset - section.DataOffset;
            uint typeHash = patchedReader.ReadUInt(section.Endianness);

            if (!classes.TryGetValue(typeHash, out var layout)) return null;

            // Print everything inside this object
            Console.WriteLine($"[DAT2][DUMP] Object at 0x{objectHeaderOffset:X} Type: {layout.Name}");
            DumpObject(values, "  ");

            Type assetType = Type.GetType("AssetBankPlugin.Ant." + layout.Name);
            if (assetType == null) return null;

            AntAsset asset = (AntAsset)Activator.CreateInstance(assetType);
            asset.Bank = bank;

            try
            {
                asset.SetData(values);
            }
            catch (KeyNotFoundException)
            {
                Console.WriteLine($"[FATAL] Key missing in C# Class for {layout.Name}");
            }

            AntRefTable.Add(asset);
            return asset;
        }

        private static void DumpObject(Dictionary<string, object> data, string indent)
        {
            foreach (var kvp in data)
            {
                if (kvp.Value is Dictionary<string, object> nested)
                {
                    Console.WriteLine($"{indent}Field '{kvp.Key}': [Nested Object]");
                    DumpObject(nested, indent + "    ");
                }
                else if (kvp.Value is object[] arr)
                {
                    Console.WriteLine($"{indent}Field '{kvp.Key}': [Array, Count={arr.Length}]");
                    for (int i = 0; i < Math.Min(arr.Length, 5); i++) // Limit dump to 5 items
                    {
                        if (arr[i] is Dictionary<string, object> obj) DumpObject(obj, indent + "    ");
                        else Console.WriteLine($"{indent}  [{i}] = {arr[i]}");
                    }
                }
                else if (kvp.Value is float[] vec)
                {
                    Console.WriteLine($"{indent}Field '{kvp.Key}': [{string.Join(", ", vec)}]");
                }
                else
                {
                    Console.WriteLine($"{indent}Field '{kvp.Key}': {kvp.Value} ({kvp.Value?.GetType().Name ?? "null"})");
                }
            }
        }
    }
}