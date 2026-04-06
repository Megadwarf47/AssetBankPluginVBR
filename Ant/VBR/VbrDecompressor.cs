using AssetBankPlugin.Export;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using FrostySdk;

namespace AssetBankPlugin.Ant
{
    public partial class VbrAnimationAsset : AnimationAsset
    {
        public static float[,] DctCoeffs = new float[8, 8] {
            {  0.12500000f,  0.24519631f,  0.23096988f,  0.20786740f,  0.17677669f,  0.13889255f,  0.09567086f,  0.04877256f },
            {  0.12500000f,  0.20786740f,  0.09567086f, -0.04877258f, -0.17677669f, -0.24519633f, -0.23096988f, -0.13889250f },
            {  0.12500000f,  0.13889255f, -0.09567088f, -0.24519633f, -0.17677666f,  0.04877260f,  0.23096989f,  0.20786734f },
            {  0.12500000f,  0.04877256f, -0.23096991f, -0.13889250f,  0.17677675f,  0.20786734f, -0.09567098f, -0.24519630f },
            {  0.12500000f, -0.04877258f, -0.23096988f,  0.13889261f,  0.17677669f, -0.20786744f, -0.09567074f,  0.24519636f },
            {  0.12500000f, -0.13889259f, -0.09567078f,  0.24519631f, -0.17677681f, -0.04877255f,  0.23096983f, -0.20786740f },
            {  0.12500000f, -0.20786741f,  0.09567090f,  0.04877252f, -0.17677663f,  0.24519633f, -0.23096994f,  0.13889278f },
            {  0.12500000f, -0.24519633f,  0.23096989f, -0.20786744f,  0.17677671f, -0.13889271f,  0.09567098f, -0.04877289f },
        };

        // Pre-computed QuantTable to prevent memory allocations per frame
        public static float[] StaticQuantTable = new float[8] {
            0.000189714F, 0.000229301F, 0.000257388F, 0.000279174F,
            0.000296974F, 0.000312024F, 0.000325061F, 0.000336561F
        };

        // Pre-compute the multiplied matrix. Memory is assigned ONCE when the app starts.
        public static float[,] PrecomputedMathMatrix;

        static VbrAnimationAsset()
        {
            PrecomputedMathMatrix = new float[8, 8];
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    PrecomputedMathMatrix[r, c] = DctCoeffs[r, c] * StaticQuantTable[c];
                }
            }
        }

        public float UnpackVec(short[] values, ushort frame, int chanIdx)
        {
            float result = 0.5f;
            int coeffIdx = frame % 8;

            // Math happens instantly using the precomputed matrix. No arrays are created here.
            for (int i = 0; i < 8; i++)
            {
                result += (float)values[i] * PrecomputedMathMatrix[coeffIdx, i];
            }

            // Assign delta and minus efficiently based on original logic without allocating arrays (not sure about this one)
            float delta, minus;
            if (chanIdx >= QuaternionCount * 4)
            {
                delta = Vec3Max - Vec3Min;
                minus = Vec3Min;
            }
            else
            {
                delta = QuatMax - QuatMin;
                minus = QuatMin;
            }

            result *= delta;
            result += minus;

            return result;
        }

        static byte ReverseBits(byte b)
        {
            b = (byte)((b >> 4) | (b << 4));
            b = (byte)(((b & 0xCC) >> 2) | ((b & 0x33) << 2));
            b = (byte)(((b & 0xAA) >> 1) | ((b & 0x55) << 1));
            return b;
        }

        public static uint ReverseBits(uint value, int bitLength)
        {
            uint result = 0;
            for (int i = 0; i < bitLength; i++)
            {
                result = (result << 1) | (value & 1);
                value >>= 1;
            }
            return result;
        }

        public List<Vector4> Decompress()
        {
            var channelCount = (QuaternionCount * 4) + (Vector3Count * 3);
            var DofCount = (QuaternionCount) + (Vector3Count);
            var constDofCount = (ConstQuaternionCount * 4) + (ConstVector3Count * 3);

            for (var i = 0; i < FrameBlockSize; i++)
            {
                offset += FrameBlockSizes[i];
            }
            offset = Data.Length - offset;
            CatchAllBitCount = 15;
            byte[] fData = new byte[Data.Length - offset];
            byte[] MetaData = new byte[offset];
            byte[] numbitsByte = new byte[channelCount * 4];
            Array.Copy(Data, offset, fData, 0, fData.Length);
            Array.Copy(Data, 0, MetaData, 0, offset);

            PaletteIndexes = new byte[constDofCount];
            Array.Copy(MetaData, PaletteIndexes, constDofCount);

            ConstChanMap = new byte[ConstChanMapSize];
            Array.Copy(MetaData, constDofCount, ConstChanMap, 0, ConstChanMapSize);

            offset = constDofCount + ConstChanMapSize;
            Array.Copy(MetaData, offset, numbitsByte, 0, numbitsByte.Length);

            VectorOffsets = new byte[VectorOffsetSize];
            Array.Copy(MetaData, constDofCount + ConstChanMapSize + channelCount * 4, VectorOffsets, 0, VectorOffsetSize);

            byte[] numbitsNibble = new byte[numbitsByte.Length * 2];
            for (int i = 0; i < numbitsByte.Length; i++)
            {
                numbitsNibble[i * 2] = (byte)((numbitsByte[i] >> 4) & 0x0F);
                numbitsNibble[i * 2 + 1] = (byte)(numbitsByte[i] & 0x0F);
            }

            byte[][] frameBlockData = new byte[FrameBlockSize][];
            offset = 0;
            for (var i = 0; i < FrameBlockSize; i++)
            {
                frameBlockData[i] = new byte[FrameBlockSizes[i]];
                Array.Copy(fData, offset, frameBlockData[i], 0, FrameBlockSizes[i]);
                Array.Reverse(frameBlockData[i]);
                for (var j = 0; j < FrameBlockSizes[i]; j++)
                {
                    frameBlockData[i][j] = ReverseBits(frameBlockData[i][j]);
                }
                offset += FrameBlockSizes[i];
            }

            // Preallocate lists to prevent memory thrashing (i believe this should save millions of operations)
            var blocks = new List<short[]>(((NumKeys + 7) / 8) * channelCount);

            for (var blockFrame = 0; blockFrame < (NumKeys + 7) / 8; blockFrame++)
            {
                var r = new BitReader(new MemoryStream(frameBlockData[blockFrame]), 8, FrostySdk.IO.Endian.Little);
                int numbits;

                for (var chanIdx = 0; chanIdx < channelCount; chanIdx++)
                {
                    short[] block = new short[8];
                    int[] numbitsOrdered = new int[8];
                    int numbitIndex = chanIdx * 8;

                    numbitsOrdered[0] = numbitsNibble[numbitIndex + 1];
                    numbitsOrdered[1] = numbitsNibble[numbitIndex];
                    numbitsOrdered[2] = numbitsNibble[numbitIndex + 3];
                    numbitsOrdered[3] = numbitsNibble[numbitIndex + 2];
                    numbitsOrdered[4] = numbitsNibble[numbitIndex + 5];
                    numbitsOrdered[5] = numbitsNibble[numbitIndex + 4];
                    numbitsOrdered[6] = numbitsNibble[numbitIndex + 7];
                    numbitsOrdered[7] = numbitsNibble[numbitIndex + 6];

                    int[] presence = new int[8];
                    int[] sign = new int[8];

                    for (int j = 0; j < 8; j++)
                    {
                        if (numbitsOrdered[j] > 0)
                        {
                            presence[j] = (int)r.ReadUIntHigh(1);
                        }
                    }

                    for (int j = 0; j < 8; j++)
                    {
                        if (presence[j] > 0 && numbitsOrdered[j] > 0)
                        {
                            sign[j] = (int)r.ReadUIntHigh(1);
                        }
                    }

                    for (int j = 0; j < 8; j++)
                    {
                        if (presence[j] > 0)
                        {
                            numbits = numbitsOrdered[j];
                            uint readValue = (uint)r.ReadUIntHigh(numbits);
                            short value = (short)ReverseBits(readValue, numbits);
                            if (sign[j] < 1)
                            {
                                value = (short)-value;
                            }
                            block[j] = value;
                        }
                    }
                    blocks.Add(block);
                }
            }

            var result = new List<float>(NumKeys * channelCount);
            for (var frame = 0; frame < NumKeys; frame++)
            {
                var blockIdx = frame / 8;
                for (var chanIdx = 0; chanIdx < channelCount; chanIdx++)
                {
                    var dataIdx = blockIdx * channelCount + chanIdx;

                    if (dataIdx >= blocks.Count)
                        break;

                    // Replaced massive .ElementAt() bottleneck with standard array indexing
                    var blockc = blocks[dataIdx];
                    result.Add(UnpackVec(blockc, (ushort)frame, chanIdx));
                }
            }

            List<Vector4> resultDof = new List<Vector4>(NumKeys * DofCount);
            for (int frame = 0; frame < NumKeys; frame++)
            {
                for (int dofidx = 0; dofidx < QuaternionCount; dofidx++)
                {
                    var resultIdx = dofidx * 4 + (frame * channelCount);
                    // Safe guard to prevent IndexOutOfRange crashes on corrupted Vbr Data
                    if (resultIdx + 3 < result.Count)
                    {
                        resultDof.Add(new Vector4(
                            result[resultIdx],
                            result[resultIdx + 1],
                            result[resultIdx + 2],
                            result[resultIdx + 3]
                        ));
                    }
                    else
                    {
                        resultDof.Add(new Vector4(0.0f)); // Add zeroed-out bone if data is cut off
                    }
                }
                for (int dofidx = 0; dofidx < Vector3Count; dofidx++)
                {
                    var resultIdx = (dofidx * 3) + (frame * channelCount) + (QuaternionCount * 4);
                    // Safe guard
                    if (resultIdx + 2 < result.Count)
                    {
                        resultDof.Add(new Vector4(
                            result[resultIdx],
                            result[resultIdx + 1],
                            result[resultIdx + 2],
                            0.0f
                        ));
                    }
                    else
                    {
                        resultDof.Add(new Vector4(0.0f));
                    }
                }
            }
            return resultDof;
        }
    }
}