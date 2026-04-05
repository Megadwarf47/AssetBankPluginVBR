using AssetBankPlugin.Export;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media.Animation;

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

        public float[] GenerateCoeffs(ushort frame, float[] quantTable)
        {
            float[] coeffs = new float[8];
            
            var coeffIdx = frame % 8;
            var frameBlockSize = FrameBlockSizes[frame / 8];
            for (var i = 0; i < 8; i++)
            {

                
                var coeff = DctCoeffs[coeffIdx, i];
                var value = coeff*quantTable[i];

                coeffs[i] = value;
            }
            return coeffs;
        }
        public float[] fillQuantTable(ushort frame, int[] rotTable, int[] TrjTable, int[] TraTable)
        {
            float[] quantTable = {
                0.000189714F, 0.000229301F, 0.000257388F, 0.000279174F,
                0.000296974F, 0.000312024F, 0.000325061F, 0.000336561F
            };





            return quantTable;
            
        }
        public float UnpackVec(short[] values, ushort frame, int chanIdx, int[] rotTable, int[] TrjTable, int[] TraTable)
        {
            float result = 0f;

            float[] quantTable = fillQuantTable(frame, rotTable, TrjTable, TraTable);
            float[] deltaScale = new float[4] { QuatMax - QuatMin, TrajMax - TrajMin, Vec3Max - Vec3Min, FloatMax - FloatMin };
            float[] minusScale = new float[4]{QuatMin, TrajMin, Vec3Min, FloatMin};
            var quantIndex = 0;
            var scaleIndex = 0;
            var channelCount = chanIdx; 
            if(chanIdx>=QuaternionCount*4)
            {
                scaleIndex = 2;
            }
            
            float delta = (deltaScale[scaleIndex]);
            float minus = (minusScale[scaleIndex]);
            float coeffQuantValue = 0;
            var coeffs = GenerateCoeffs(frame, quantTable);
                float[] floatValues = Array.ConvertAll(values, x => (float)x);
            result += 0.5F;
                for (int i = 0; i < 8; i++)
                {
                    coeffQuantValue = floatValues[i] * coeffs[i];
                    result += coeffQuantValue;
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
            //if (Name != "SniperCactus_Idle Anim")
            //{
            //    return null;
            //}
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
            byte[] DofTableDescBytes = new byte[ConstChanMapSize];
            byte[] numbitsByte = new byte[channelCount * 4];
            Array.Copy(Data, offset, fData, 0, fData.Length);
            int count = 0;
            Array.Copy(Data, 0, MetaData, 0, offset);
            //read constant indice values from data header
            PaletteIndexes = new byte[constDofCount];
            Array.Copy(MetaData,PaletteIndexes,constDofCount);
            //read constChanMap values from data header
            ConstChanMap = new byte[ConstChanMapSize];
            Array.Copy(MetaData, constDofCount, ConstChanMap,0, ConstChanMapSize);
            
            //read numbits values from data header
            offset = constDofCount + ConstChanMapSize;
            Array.Copy(MetaData, offset, numbitsByte, 0, numbitsByte.Length);
            //read vector offsets from data header
            VectorOffsets = new byte[VectorOffsetSize];
            Array.Copy(MetaData, constDofCount + ConstChanMapSize + channelCount * 4, VectorOffsets, 0, VectorOffsetSize);
            ushort[] bitsPerSubblock = new ushort[DofCount * 8];
            //turn numbits values from bytes into nibbles
            byte[] numbitsNibble = new byte[numbitsByte.Length * 2]; // Each byte becomes 2 nibbles
                for (int i = 0; i < numbitsByte.Length; i++)
                {
                numbitsNibble[i * 2] = (byte)((numbitsByte[i] >> 4) & 0x0F);
                numbitsNibble[i * 2+1] = (byte)(numbitsByte[i] & 0x0F); 

            }
            
            //create arrays for each frameblock
            byte[][] frameBlockData = new byte[FrameBlockSize][];
            offset = 0;
            for(var i = 0; i<FrameBlockSize;i++)
            {
                frameBlockData[i] = new byte[FrameBlockSizes[i]];
        
                Array.Copy(fData, offset, frameBlockData[i], 0, FrameBlockSizes[i]);
                Array.Reverse(frameBlockData[i]);
                for(var j = 0; j < FrameBlockSizes[i];j++)
                {
                    frameBlockData[i][j] = ReverseBits(frameBlockData[i][j]);
                }
                offset += FrameBlockSizes[i];
            }
            int numbitTotalBits = 0;
            int numbitZeroCount = 0;
            for(var i=0;i<numbitsNibble.Length;i++)
            {
                numbitTotalBits += numbitsNibble[i];
                if (numbitsNibble[i]==0)
                {
                    numbitZeroCount++;
                }
            }
            int expectedBits = 0;
            expectedBits=numbitTotalBits+2*(numbitsNibble.Length-numbitZeroCount);
            int bitCount = 0;
            var bitsLeft = 0;
            int[] rotTable = new int[FrameBlockSize];
            int[] TrjTable = new int[FrameBlockSize];
            int[] TraTable = new int[FrameBlockSize];
            var blocks = new List<short[]>();
            for (var blockFrame = 0; blockFrame < (NumKeys + 7) / 8; blockFrame++)
            {
                var r = new BitReader(new MemoryStream(frameBlockData[blockFrame]), 8, FrostySdk.IO.Endian.Little);
                bitCount = 0;
                int numbitIndex = 0;
                int numbits = 0;
                int presenceCount = 0;
                int maxBits = FrameBlockSizes[blockFrame] * 8;
                //read rot table bytes
                rotTable[blockFrame] = (int)0;
                TrjTable[blockFrame] = (int)0;
                TraTable [blockFrame] = (int)0;
                //go through each channel
                
                for (var chanIdx = 0; chanIdx < channelCount; chanIdx++)
                {
                    short[] block = new short[8];
                    int[]numbitsOrdered = new int[8];
                    numbitIndex = chanIdx * 8;
                    //reorder numbits
                    numbitsOrdered[0] = numbitsNibble[numbitIndex+1];
                    numbitsOrdered[1] = numbitsNibble[numbitIndex];
                    numbitsOrdered[2] = numbitsNibble[numbitIndex+3];
                    numbitsOrdered[3] = numbitsNibble[numbitIndex+2];
                    numbitsOrdered[4] = numbitsNibble[numbitIndex+5];
                    numbitsOrdered[5] = numbitsNibble[numbitIndex+4];
                    numbitsOrdered[6] = numbitsNibble[numbitIndex+7];
                    numbitsOrdered[7] = numbitsNibble[numbitIndex+6];
                    //go through each channel 8 times
                    int[] presence = new int[8];
                        int[] sign = new int[8];
                        uint readValue = 0;
                        short value = 0;
                    //read presence bits
                    for (int j = 0; j < 8; j++)
                    {
                                if (numbitsOrdered[j]> 0)
                                {   
                                    presence[j] = (int)r.ReadUIntHigh(1);
                                    if(presence[j] > 0)
                                        presenceCount++;
                                    bitCount++;
                                }
                                else presence[j] = 0;
                     }
                    
                    for (int j = 0; j < 8; j++)
                        //read sign bits if theres a value
                    {
                        if (presence[j] >0)
                        {
                            if (numbitsOrdered[j] > 0)
                            {
                                sign[j] = (int)r.ReadUIntHigh(1);
                                bitCount++;
                            }
                        }
                    }
                    for (int j = 0; j < 8; j++)
                    {
                        if (presence[j] > 0)
                        {
                            numbits = numbitsOrdered[j];
                            readValue = (uint)r.ReadUIntHigh(numbits);

                            value = (short)ReverseBits(readValue, numbits);
                            bitCount += numbitsOrdered[j];
                            if (sign[j] < 1)
                            {
                                value = (short)-value;
                            }
                        }
                        block[j]=value;
                        value = 0;
                    }
                    blocks.Add(block);
                }



                bitsLeft = FrameBlockSizes[blockFrame] * 8 - bitCount;
            }

            var result = new List<float>();
            var zeroValue = new List<float>(new float[8]);
            for (var frame = 0; frame < NumKeys; frame++)
            {
                var blockIdx = frame / 8;

                for (var chanIdx =  0; chanIdx < channelCount; chanIdx++)
                {
                    var dataIdx = blockIdx * channelCount + chanIdx;

                    if (dataIdx >= blocks.Count)
                        break;

                    var blockc = blocks.ElementAt(dataIdx);

                    result.Add(UnpackVec(blockc, (ushort)frame,chanIdx, rotTable, TrjTable, TraTable));
                }
            }
            List<Vector4> resultDof = new List<Vector4>();
            for(int frame = 0; frame<NumKeys;frame++)
            {
                for(int dofidx =0; dofidx<QuaternionCount;dofidx++)
                {
                    var dofValue = new Vector4(0.0f);
                    var resultIdx = dofidx * 4 + (frame * channelCount);
                    dofValue.X = result[resultIdx];
                    dofValue.Y = result[resultIdx+1];
                    dofValue.Z = result[resultIdx+2];
                    dofValue.W = result[resultIdx+3];
                    resultDof.Add(dofValue);
                }
                for(int dofidx=0;dofidx<Vector3Count;dofidx++)
                {
                    var dofValue = new Vector4(0.0f);
                    var resultIdx = (dofidx * 3)+(frame * channelCount)+(QuaternionCount*4);
                    dofValue.X = result[resultIdx];
                    dofValue.Y = result[resultIdx + 1];
                    dofValue.Z = result[resultIdx + 2];
                    resultDof.Add(dofValue);
                }
            }
            return resultDof;

        }

    }
}