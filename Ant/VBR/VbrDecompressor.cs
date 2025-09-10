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
            { 0.250000f, 0.490393f, 0.461940f, 0.415735f, 0.353553f, 0.277785f, 0.191342f, 0.097545f,  },
            { 0.250000f, 0.415735f, 0.191342f, -0.097545f, -0.353553f, -0.490393f, -0.461940f, -0.277785f,  },
            { 0.250000f, 0.277785f, -0.191342f, -0.490393f, -0.353553f, 0.097545f, 0.461940f, 0.415735f,  },
            { 0.250000f, 0.097545f, -0.461940f, -0.277785f, 0.353553f, 0.415735f, -0.191342f, -0.490393f,  },
            { 0.250000f, -0.097545f, -0.461940f, 0.277785f, 0.353553f, -0.415735f, -0.191342f, 0.490393f,  },
            { 0.250000f, -0.277785f, -0.191342f, 0.490393f, -0.353553f, -0.097545f, 0.461940f, -0.415735f,  },
            { 0.250000f, -0.415735f, 0.191342f, 0.097545f, -0.353553f, 0.490393f, -0.461940f, 0.277785f,  },
            { 0.250000f, -0.490393f, 0.461940f, -0.415735f, 0.353554f, -0.277785f, 0.191342f, -0.097545f,  },
    };

        public float[] GenerateCoeffs(ushort frame)
        {
            float[] coeffs = new float[8];
            
            var coeffIdx = frame % 8;
            var frameBlockSize = FrameBlockSizes[frame / 8];
            for (var i = 0; i < 8; i++)
            {

                
                var coeff = DctCoeffs[coeffIdx, i];
                var value = coeff;

                coeffs[i] = value;
            }
            return coeffs;
        }
        public Vector4[] fillQuantTable(ushort frame, int[] rotTable, int[] TrjTable, int[] TraTable)
        {
            Vector4[] quantTable = new Vector4[32];
            var scalar = 0.2f * Dct;
            float[] log1 = new float[4] { 0.693147182f, 1.09861231f, 1.38629436f, 1.60943794f };
            float[] log2 = new float[4] { 1.79175949f, 1.94591010f, 2.07944155f, 2.19722462f };
            var rotScale = rotTable[frame / 8] * scalar;
            var TrjScale = TrjTable[frame / 8] * scalar;
            var TraScale = TraTable[frame / 8] * scalar;
            float divider = 32768.0f * Dct;

            for (var i =0; i<4;i++)
            {
                quantTable[i] = new Vector4(rotScale * log1[i] + 1 / divider);
                quantTable[i+4] = new Vector4(rotScale * log2[i] + 1 / divider);
                quantTable[i+8] = new Vector4(TrjScale * log1[i] + 1 / divider);
                quantTable[i + 12] = new Vector4(TrjScale * log2[i] + 1 / divider);
                quantTable[i + 24] = new Vector4(TraScale * log1[i] + 1 / divider);
                quantTable[i + 28] = new Vector4(TraScale * log2[i] + 1 / divider);
            }
            quantTable[16] = new Vector4(quantTable[10].X, quantTable[26].X, quantTable[26].X, quantTable[26].X);
            quantTable[17] = new Vector4(quantTable[11].X, quantTable[27].X, quantTable[27].X, quantTable[27].X);
            quantTable[18] = new Vector4(quantTable[8].X, quantTable[24].X, quantTable[24].X, quantTable[24].X);
            quantTable[19] = new Vector4(quantTable[9].X, quantTable[25].X, quantTable[25].X, quantTable[25].X);
            quantTable[20] = new Vector4(quantTable[14].X, quantTable[30].X, quantTable[30].X, quantTable[30].X);
            quantTable[21] = new Vector4(quantTable[15].X, quantTable[31].X, quantTable[31].X, quantTable[31].X);
            quantTable[22] = new Vector4(quantTable[12].X, quantTable[28].X, quantTable[28].X, quantTable[28].X);
            quantTable[23] = new Vector4(quantTable[13].X, quantTable[29].X, quantTable[29].X, quantTable[29].X);






            return quantTable;
            
        }
        public Vector4 UnpackVec(List<short> values, ushort frame, int dofIdx, int[] rotTable, int[] TrjTable, int[] TraTable)
        {
            var result = new Vector4(0.0f);

            Vector4[] quantTable = fillQuantTable(frame, rotTable, TrjTable, TraTable);
            float[] deltaScale = new float[4] { QuatMax - QuatMin, TrajMax - TrajMin, Vec3Max - Vec3Min, FloatMax - FloatMin };
            float[] minusScale = new float[4]{QuatMin, TrajMin, Vec3Min, FloatMin};
            var quantIndex = 0;
            var scaleIndex = 0;
            var channelCount = dofIdx; 
            if (dofIdx >= QuaternionCount)
            {
                if (dofIdx == QuaternionCount)
                {
                    quantIndex = 8;
                    if(Vector3Count!=0)
                    {
                        scaleIndex = 1;
                    }
                }
                else if (dofIdx == (QuaternionCount + 1))
                {
                    quantIndex = 16;
                } else
                {
                    quantIndex = 24;
                    if(dofIdx<(QuaternionCount+Vector3Count))
                        {
                        scaleIndex = 2;
                    }
                    if (dofIdx > (QuaternionCount + Vector3Count))
                    {
                        scaleIndex = 3;
                    }
                }

            }
            Vector4 delta = new Vector4(deltaScale[scaleIndex]);
            Vector4 minus = new Vector4(minusScale[scaleIndex]);
            var coeffs = GenerateCoeffs(frame);
            for (var i = 0; i < 8; i++)
            {

                var vec = new Vector4((float)values[i * 4 + 0], (float)values[i * 4 + 1], (float)values[i * 4 + 2], (float)values[i * 4 + 3]);
                vec = vec * quantTable[quantIndex + i];
                result += Vector4.Multiply(vec, coeffs[i]);
                
            }
            result *= delta;
            result += minus;
            return result;
        }
        public List<Vector4> Decompress()
        {
            //    if (Name != "Gargantuar_Idle_Right_Turn_180 Anim")
            //    {
            //        return null;
            //    }
            var channelCount = (QuaternionCount * 4) + (Vector3Count * 3);
            var s_DofCount = (QuaternionCount) + (Vector3Count);
            var constDofCount = (ConstQuaternionCount * 4) + (ConstVector3Count * 3);
            var dofTable = new DofTable[s_DofCount];

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
            ushort[] bitsPerSubblock = new ushort[s_DofCount * 8];
            //turn numbits values from bytes into nibbles
            byte[] numbitsNibble = new byte[numbitsByte.Length * 2]; // Each byte becomes 2 nibbles
                for (int i = 0; i < numbitsByte.Length; i++)
                {
                    numbitsNibble[i * 2+1] = (byte)((numbitsByte[i] >> 4) & 0x0F);     // Upper nibble
                    numbitsNibble[i * 2] = (byte)(numbitsByte[i] & 0x0F);        // Lower nibble
                }
            
            //create arrays for each frameblock
            byte[][] frameBlockData = new byte[FrameBlockSize][];
            offset = 0;
            for(var i = 0; i<FrameBlockSize;i++)
            {
                frameBlockData[i] = new byte[FrameBlockSizes[i]];
        
                Array.Copy(fData, offset, frameBlockData[i], 0, FrameBlockSizes[i]);
                offset += FrameBlockSizes[i];
            }
            var numbitCount = 0;
            for(var i=0;i<numbitsNibble.Length;i++)
            {
                numbitCount += numbitsNibble[i];
            }
            int bitCount = 0;
            var bitsLeft = 0;
            int[] rotTable = new int[FrameBlockSize];
            int[] TrjTable = new int[FrameBlockSize];
            int[] TraTable = new int[FrameBlockSize];
            var blocks = new List<List<short>>();
            for (var blockFrame = 0; blockFrame < (NumKeys + 7) / 8; blockFrame++)
            {
                var r = new BitReader(new MemoryStream(frameBlockData[blockFrame]), 128, FrostySdk.IO.Endian.Big);
                bitCount = 24;
                var numbitIndex = 0;
                var presenceCount = 0;
                //read rot table bytes
                rotTable[blockFrame] = (int)r.ReadUIntHigh(8)+1;
                TrjTable[blockFrame] = (int)r.ReadUIntHigh(8)+1;
                TraTable [blockFrame] = (int)r.ReadUIntHigh(8)+1;
                //go through each channel
                var block = new List<short>();
                for (var chanIdx = 0; chanIdx < channelCount; chanIdx++)
                {
                    
                    //go through each channel 8 times
                        int[] presence = new int[8];
                        int[] sign = new int[8];
                        short value = 0;
                    //read presence bits
                    for (int j = 0; j < 8; j++)
                            {
                                if (numbitsNibble[numbitIndex+j]> 0)
                                {
                                    presence[j] = (int)r.ReadUIntHigh(1);
                                    bitCount++;
                                }
                                else presence[j] = 0;
                            }
                    for (int j = 0; j < 8; j++)
                        //read sign bits if theres a value
                    {
                        if (presence[j] >0)
                        {
                            if (numbitsNibble[numbitIndex + j] > 0)
                            {
                                sign[j] = (int)r.ReadUIntHigh(1);
                                bitCount++;
                            }
                            else presence[j] = 0;
                        }
                    }
                    for (int j = 0; j < 8; j++)
                    {
                        if (presence[j] > 0)
                        {
                            value = (short)r.ReadUIntHigh(numbitsNibble[numbitIndex]);
                            bitCount += numbitsNibble[numbitIndex];
                            if (sign[j] < 1)
                            {
                                value = (short)-value;
                            }
                        }
                        numbitIndex++;


                        block.Add(value);
//check if this boundary is correct
                        if (chanIdx > QuaternionCount * 4)
                        { 
                            if (block.Count()%3 == 0)
                            {
                                block.Add((short)0);
                                block.Clear();
                            }
                        }
                        
                    }
                    blocks.Add(block);
                }



                bitsLeft = FrameBlockSizes[blockFrame] * 8 - bitCount;
            }

            var result = new List<Vector4>();

            for (var frame = 0; frame < NumKeys; frame++)
            {
                var blockIdx = frame / 8;

                for (var dofIdx = 0; dofIdx < dofTable.Length; dofIdx++)
                {
                    var dataIdx = blockIdx * dofTable.Length + dofIdx;

                    if (dataIdx >= blocks.Count)
                        break;

                    var blockc = blocks.ElementAt(dataIdx);

                    result.Add(UnpackVec(blockc, (ushort)frame,dofIdx, rotTable, TrjTable, TraTable));
                }
            }

            return result;

        }

    }
}