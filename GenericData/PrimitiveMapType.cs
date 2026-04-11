using System.Collections.Generic;

namespace AssetBankPlugin.GenericData
{
    public static class PrimitiveTypeMap
    {
        private static readonly Dictionary<uint, string> NameMap = new Dictionary<uint, string>
        {
            { 0x01, "Bool" },
            { 0x02, "Int8" },
            { 0x03, "UInt8" },
            { 0x04, "Int16" },
            { 0x05, "UInt16" },
            { 0x06, "Int32" },
            { 0x07, "UInt32" },
            { 0x08, "Int64" },
            { 0x09, "UInt64" },
            { 0x0A, "Float" },
            { 0x0B, "Vector2" },
            { 0x0C, "Vector3" },
            { 0x0D, "Vector4" },
            { 0x0E, "Quaternion" },
            { 0x0F, "Matrix44" },
            { 0x11, "String" },
            { 0x12, "DataRef" },
            { 0x13, "Double" },          // ← FIXED: added missing primitive
            { 0x23, "Key" }
        };

        private static readonly Dictionary<uint, int> SizeMap = new Dictionary<uint, int>
        {
            { 0x01, 1 }, { 0x02, 1 }, { 0x03, 1 },
            { 0x04, 2 }, { 0x05, 2 },
            { 0x06, 4 }, { 0x07, 4 },
            { 0x08, 8 }, { 0x09, 8 },
            { 0x0A, 4 }, { 0x0B, 8 }, { 0x0C, 12 }, { 0x0D, 16 }, { 0x0E, 16 }, { 0x0F, 64 },
            { 0x11, 8 }, { 0x12, 8 }, { 0x13, 8 },   // ← Double = 8 bytes
            { 0x23, 8 }
        };

        public static string GetTypeName(uint hash)
        {
            NameMap.TryGetValue(hash, out string name);
            return name;
        }

        public static int GetTypeSize(uint hash)
        {
            return SizeMap.TryGetValue(hash, out int size) ? size : 0;
        }

        public static bool IsPrimitive(uint hash)
        {
            return NameMap.ContainsKey(hash);
        }
    }
}