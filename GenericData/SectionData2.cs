using AssetBankPlugin.Extensions;
using FrostySdk.IO;
using System;
using System.Collections.Generic;
using System.IO;

namespace AssetBankPlugin.GenericData
{
    public class SectionData2 : Section
    {
        public const string Identifier = "GD.DAT2";
        public override Endian Endianness { get; set; }
        public override uint DataSize { get; set; }
        public override uint DataOffset { get; set; }

        public uint ObjectCount { get; private set; }

        private byte[] _patchedData;
        private NativeReader _patchedReader;

        public SectionData2(NativeReader r, Endian bankEndian)
        {
            long startPos = r.BaseStream.Position;
            DataOffset = (uint)startPos;

            string magic = r.ReadSizedString(7);
            _ = r.ReadByte();
            Endianness = bankEndian;
            DataSize = r.ReadUInt(Endianness);

            long magicConstant = r.ReadLong(Endianness);
            ObjectCount = (uint)r.ReadULong(Endianness);

            r.BaseStream.Position = startPos;
            _patchedData = r.ReadBytes((int)DataSize);

            _patchedReader = new NativeReader(new MemoryStream(_patchedData), null);
            r.BaseStream.Position = startPos + DataSize;
        }

        public NativeReader GetPatchedReader() => _patchedReader;

        public Dictionary<string, object> ReadObjectAt(long headerFileOffset, Dictionary<uint, GenericClass> classes)
        {
            int bufOff = (int)(headerFileOffset - DataOffset);
            if (bufOff < 0 || bufOff + 16 > _patchedData.Length) return null;

            _patchedReader.BaseStream.Position = bufOff;
            uint typeHash = _patchedReader.ReadUInt(Endianness);

            if (!classes.TryGetValue(typeHash, out var layout)) return null;

            return ReadValues(bufOff + 16, typeHash, classes);
        }

        private Dictionary<string, object> ReadValues(long bufOff, uint typeHash, Dictionary<uint, GenericClass> classes)
        {
            var layout = classes[typeHash];
            var result = new Dictionary<string, object>();

            foreach (var field in layout.Elements)
            {
                _patchedReader.BaseStream.Position = bufOff + field.Offset;

                if (field.IsList || field.Type == "String")
                    result[field.Name] = ReadHeapItem(field, classes);
                else if (field.InlineCount > 1)
                    result[field.Name] = ReadArray(bufOff + field.Offset, field, (int)field.InlineCount, classes);
                else
                    result[field.Name] = ReadPrimitive(bufOff + field.Offset, field, classes);
            }
            return result;
        }

        private object ReadHeapItem(GenericField field, Dictionary<uint, GenericClass> classes)
        {
            uint capacity = _patchedReader.ReadUInt(Endianness);
            int count = _patchedReader.ReadInt(Endianness);
            long rawVal = _patchedReader.ReadLong(Endianness);

            if (rawVal == 0 || rawVal == -1 || count <= 0)
                return (field.Type == "String") ? (object)"" : (object)new object[0];

            long pointerPosInFile = DataOffset + (_patchedReader.BaseStream.Position - 8);
            long absoluteTarget = pointerPosInFile + ((rawVal << 4) >> 4);
            long oldPos = _patchedReader.BaseStream.Position;

            // Safety check against corrupted pointers
            if (absoluteTarget < DataOffset || absoluteTarget > DataOffset + DataSize)
            {
                _patchedReader.BaseStream.Position = oldPos;
                return (field.Type == "String") ? (object)"" : (object)new object[0];
            }

            _patchedReader.BaseStream.Position = absoluteTarget - DataOffset;

            object resultVal;
            if (field.Type == "String")
            {
                resultVal = _patchedReader.ReadNullTerminatedString();
            }
            else
            {
                if (field.ElementTypeHash != 0 && field.ElementSize > 0)
                {
                    var elemField = new GenericField
                    {
                        TypeHash = field.ElementTypeHash,
                        Size = field.ElementSize,
                        Alignment = field.ElementAlignment
                    };
                    if (PrimitiveTypeMap.IsPrimitive(elemField.TypeHash))
                        elemField.Type = PrimitiveTypeMap.GetTypeName(elemField.TypeHash);
                    else if (classes.TryGetValue(elemField.TypeHash, out var elemClass))
                        elemField.Type = elemClass.Name;
                    else
                        elemField.Type = $"Class_0x{elemField.TypeHash:X8}";

                    resultVal = ReadArray(_patchedReader.BaseStream.Position, elemField, count, classes);
                }
                else
                {
                    resultVal = ReadArray(_patchedReader.BaseStream.Position, field, count, classes);
                }
            }

            _patchedReader.BaseStream.Position = oldPos;
            return resultVal;
        }

        private object ReadPrimitive(long bufOff, GenericField field, Dictionary<uint, GenericClass> classes)
        {
            if (bufOff < 0 || bufOff >= _patchedData.Length) return null;

            _patchedReader.BaseStream.Position = bufOff;
            switch (field.Type)
            {
                case "Bool": return _patchedReader.ReadByte() != 0;
                case "Int8": return _patchedReader.ReadSByte();
                case "UInt8": return _patchedReader.ReadByte();
                case "Int16": return _patchedReader.ReadShort(Endianness);
                case "UInt16": return _patchedReader.ReadUShort(Endianness);
                case "Int32": return _patchedReader.ReadInt(Endianness);
                case "UInt32": return _patchedReader.ReadUInt(Endianness);
                case "Int64": return _patchedReader.ReadLong(Endianness);
                case "UInt64": return _patchedReader.ReadULong(Endianness);
                case "Float": return _patchedReader.ReadFloat(Endianness);
                case "Double": return _patchedReader.ReadDouble(Endianness);
                case "Guid": return _patchedReader.ReadGuid(Endianness);
                case "Key": return _patchedReader.ReadULong(Endianness).ToString("X16");
                case "Vector2": return new float[] { _patchedReader.ReadFloat(Endianness), _patchedReader.ReadFloat(Endianness) };
                case "Vector3": return new float[] { _patchedReader.ReadFloat(Endianness), _patchedReader.ReadFloat(Endianness), _patchedReader.ReadFloat(Endianness) };
                case "Vector4":
                case "Quaternion": return new float[] { _patchedReader.ReadFloat(Endianness), _patchedReader.ReadFloat(Endianness), _patchedReader.ReadFloat(Endianness), _patchedReader.ReadFloat(Endianness) };
                case "Matrix44":
                    float[] m = new float[16];
                    for (int i = 0; i < 16; i++) m[i] = _patchedReader.ReadFloat(Endianness);
                    return m;
                case "DataRef":
                    long pVal = _patchedReader.ReadLong(Endianness);
                    if (pVal == 0 || pVal == -1) return null;
                    long pPos = DataOffset + (_patchedReader.BaseStream.Position - 8);
                    return ReadObjectAt(pPos + ((pVal << 4) >> 4), classes);
                default:
                    if (field.Size == 8 && field.Alignment == 8 && !PrimitiveTypeMap.IsPrimitive(field.TypeHash))
                    {
                        long refVal = _patchedReader.ReadLong(Endianness);
                        if (refVal == 0 || refVal == -1) return null;
                        long refPos = DataOffset + (_patchedReader.BaseStream.Position - 8);
                        long target = refPos + ((refVal << 4) >> 4);
                        if (field.Type == "String" || (field.ElementTypeHash != 0 && field.ElementTypeHash == 0x11))
                        {
                            long old = _patchedReader.BaseStream.Position;
                            _patchedReader.BaseStream.Position = target - DataOffset;
                            uint cap = _patchedReader.ReadUInt(Endianness);
                            uint cnt = _patchedReader.ReadUInt(Endianness);
                            long ptr = _patchedReader.ReadLong(Endianness);
                            if (ptr != 0 && ptr != -1 && cnt > 0)
                            {
                                long strTarget = target + ((ptr << 4) >> 4);
                                _patchedReader.BaseStream.Position = strTarget - DataOffset;
                                string s = _patchedReader.ReadNullTerminatedString();
                                _patchedReader.BaseStream.Position = old;
                                return s;
                            }
                            _patchedReader.BaseStream.Position = old;
                            return "";
                        }
                        else
                        {
                            return ReadObjectAt(target, classes);
                        }
                    }
                    else
                    {
                        return ReadValues(bufOff, field.TypeHash, classes);
                    }
            }
        }

        private object ReadArray(long bufOff, GenericField field, int count, Dictionary<uint, GenericClass> classes)
        {
            if (count <= 0) return new object[0];

            // Failsafe: Prevent OOM crashes if a corrupt pointer is hit. 
            if (count > 5000000)
            {
                Console.WriteLine($"[ERROR] ReadArray: Prevented massive array allocation of {count} elements on field '{field.Name}'. Data likely misaligned.");
                return new object[0];
            }

            bool isReference = (field.Size == 8 && field.Alignment == 8 &&
                                !PrimitiveTypeMap.IsPrimitive(field.TypeHash) &&
                                field.TypeHash != 0);

            bool isStringArray = (field.Type == "String[]" ||
                                  (field.ElementTypeHash != 0 && field.ElementTypeHash == 0x11));

            uint elemSize = field.Size;
            uint elemAlign = field.Alignment;

            if (elemSize == 0 && field.TypeHash != 0 && !PrimitiveTypeMap.IsPrimitive(field.TypeHash))
            {
                if (classes.TryGetValue(field.TypeHash, out var classLayout))
                {
                    elemSize = (uint)classLayout.Size;
                    elemAlign = (uint)classLayout.Alignment;
                }
            }

            if (elemSize == 0) elemSize = 1;
            if (elemAlign == 0) elemAlign = 1; // CRITICAL: prevents stride from becoming 0

            // stride = (size + align - 1) & ~(align - 1)
            uint stride = (elemSize + elemAlign - 1) & ~(elemAlign - 1);

            object[] arr = new object[count];
            for (int i = 0; i < count; i++)
            {
                // offset = i * stride
                long offset = i * stride;

                if (isReference)
                {
                    long oldPos = _patchedReader.BaseStream.Position;
                    _patchedReader.BaseStream.Position = bufOff + offset;
                    long refVal = _patchedReader.ReadLong(Endianness);
                    _patchedReader.BaseStream.Position = oldPos;

                    if (refVal == 0 || refVal == -1)
                    {
                        arr[i] = null;
                        continue;
                    }
                    long refPos = DataOffset + (bufOff + offset + 8);
                    long target = refPos + ((refVal << 4) >> 4);

                    if (isStringArray)
                    {
                        long old = _patchedReader.BaseStream.Position;
                        _patchedReader.BaseStream.Position = target - DataOffset;
                        uint cap = _patchedReader.ReadUInt(Endianness);
                        uint cnt = _patchedReader.ReadUInt(Endianness);
                        long ptr = _patchedReader.ReadLong(Endianness);
                        string s = "";
                        if (ptr != 0 && ptr != -1 && cnt > 0)
                        {
                            long strTarget = target + ((ptr << 4) >> 4);
                            _patchedReader.BaseStream.Position = strTarget - DataOffset;
                            s = _patchedReader.ReadNullTerminatedString();
                        }
                        _patchedReader.BaseStream.Position = old;
                        arr[i] = s;
                    }
                    else
                    {
                        arr[i] = ReadObjectAt(target, classes);
                    }
                }
                else
                {
                    arr[i] = ReadPrimitive(bufOff + offset, field, classes);
                }
            }
            return arr;
        }

        public long GetNextObjectOffset(long currentHeaderOffset, Dictionary<uint, GenericClass> classes)
        {
            int bufPos = (int)(currentHeaderOffset - DataOffset);
            if (bufPos < 0 || bufPos + 4 > _patchedData.Length) return -1;

            _patchedReader.BaseStream.Position = bufPos;
            uint typeHash = _patchedReader.ReadUInt(Endianness);
            if (!classes.TryGetValue(typeHash, out var layout)) return -1;

            long endOfObject = currentHeaderOffset + 16 + (uint)layout.Size;
            return (endOfObject + 7) & ~7;
        }
    }
}
