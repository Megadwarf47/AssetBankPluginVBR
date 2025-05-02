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
        public static short[] BytesToSignedShorts(ArraySegment<byte> segment)
        {
            byte[] bytes = segment.Array;
            int offset = segment.Offset;
            int length = segment.Count;

            if (length % 2 != 0)
                throw new ArgumentException("Byte array length must be even.");

            short[] shorts = new short[length / 2];
            for (int i = 0; i < length; i += 2)
            {
                shorts[i / 2] = (short)(bytes[offset + i] | (bytes[offset + i + 1] << 8));
            }
            return shorts;
        }
        public List<Vector4> Decompress()
        {
            if(Name!="Gargantuar_Idle_Right_Turn_180 Anim")
            {
                return null;
            }
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
            byte[] bitsPerSubbloc = new byte[channelCount * 4];
            Array.Copy(Data, offset, fData, 0, fData.Length);
            int count = 0;
            ArraySegment<byte> segment = new ArraySegment<byte>(MetaData, count, channelCount * 2);
            DeltaBaseX = BytesToSignedShorts(segment);
            Array.Copy(Data, 0, MetaData, 0, offset);
            //read constant indice values from data header
            PaletteIndexes = new byte[constDofCount];
            Array.Copy(MetaData,PaletteIndexes,constDofCount);
            //read constChanMap values from data header
            ConstChanMap = new byte[ConstChanMapSize];
            Array.Copy(MetaData, constDofCount, ConstChanMap,0, ConstChanMapSize);
            //read vector offsets from data header
            VectorOffsets = new byte[VectorOffsetSize];
            Array.Copy(MetaData, constDofCount+ConstChanMapSize, VectorOffsets, 0, VectorOffsetSize);
            //read bitspersubblock values from data header
            offset = constDofCount + ConstChanMapSize;
            Array.Copy(MetaData, offset, bitsPerSubbloc, 0, bitsPerSubbloc.Length);
            ushort[] bitsPerSubblock = new ushort[s_DofCount * 8];
            
            // turn bits persubbloc into nibbles for sorting out vectors
            byte[] bitsPerSubblockNibble = new byte[bitsPerSubbloc.Length * 2];

            for (int i = 0; i < bitsPerSubbloc.Length; i++)
            {
                bitsPerSubblockNibble[i * 2] = (byte)((bitsPerSubbloc[i] >> 4) & 0x0F); // High nibble
                bitsPerSubblockNibble[i * 2 + 1] = (byte)(bitsPerSubbloc[i] & 0x0F);     // Low nibble
            }
            //turn nibbles into ushorts for processing
            for (   int i = 0; i < s_DofCount; i++)
            {

                if (i < QuaternionCount)
                {
                    for(int j = 0; j < 8; j++)
                    {
                        byte bitsX = bitsPerSubblockNibble[(i * 32)+j];
                        byte bitsY = bitsPerSubblockNibble[(i * 32) + 8+j];
                        byte bitsZ = bitsPerSubblockNibble[(i * 32) + 16+j];
                        byte bitsW = bitsPerSubblockNibble[(i * 32) + 24+j];
                        ushort bits = (ushort)(
                            (bitsX << 12) |  // Shift 1st nibble to bits 12-15
                            (bitsY << 8) |  // Shift 2nd nibble to bits 8-11
                            (bitsZ << 4) |  // Shift 3rd nibble to bits 4-7
                            bitsW            // 4th nibble stays in bits 0-3
                        );
                        bitsPerSubblock[(i*8)+j] = bits;
                    }
                 
                }
                else
                {
                    int quatNibbles = QuaternionCount * 32;
                    int vectorNibbles = (i - QuaternionCount) * 24;
                    for (int j = 0; j < 8; j++)
                    {
                        
                        byte bitsX = bitsPerSubblockNibble[quatNibbles+vectorNibbles + j];
                        byte bitsY = bitsPerSubblockNibble[quatNibbles + vectorNibbles + 8 + j];
                        byte bitsZ = bitsPerSubblockNibble[quatNibbles + vectorNibbles + 8 + j];
                        byte bitsW = 0;
                        ushort bits = (ushort)(
                            (bitsX << 12) |  // Shift 1st nibble to bits 12-15
                            (bitsY << 8) |  // Shift 2nd nibble to bits 8-11
                            (bitsZ << 4) |  // Shift 3rd nibble to bits 4-7
                            bitsW            // 4th nibble stays in bits 0-3
                        );
                        bitsPerSubblock[i*8+j] = bits;
                    }
                }
            }

            var s_SubBlockTotal = 0;
            for (var i = 0; i < s_DofCount; i++)
            {

                var s_SubBlocksCount = (byte)(8);

                var s_DofData = new DofTable(s_SubBlocksCount);
                //creates table to store bit sizes for each 
                {
                    s_DofData.BitsPerSubBlock = new DofTable.BitsPerComponent[s_DofData.SubBlockCount];
                    for (var j = 0; j < s_DofData.SubBlockCount; j++)
                        // decodes the bits per subblock number into the actual bits for that subblock using a bitmask(each byte in the ushort represent x,y,z or w)
                        s_DofData.BitsPerSubBlock[j] = new DofTable.BitsPerComponent(bitsPerSubblock[s_SubBlockTotal + j]);

                }


                dofTable[i] = s_DofData;

                s_SubBlockTotal += s_SubBlocksCount;

            }
            int bitCount = 0;
            int counter = 0;
            //create arrays for each frameblock
            byte[][] frameBlockData = new byte[FrameBlockSize][];
            offset = 0;
            for(var i = 0; i<FrameBlockSize;i++)
            {
                frameBlockData[i] = new byte[FrameBlockSizes[i]];
        
                Array.Copy(fData, offset, frameBlockData[i], 0, FrameBlockSizes[i]);
                offset += FrameBlockSizes[i];
            }
            var numbitSum=0;
            for(var i = 0;i<s_DofCount;i++)
            {
                for (var j = 0; j < 8; j++)
                {
                    numbitSum += dofTable[i].BitsPerSubBlock[j].BitSum;
                }
            }
            bitCount = 0;
            var bitsLeft = 0;
            int[] rotTable = new int[FrameBlockSize];
            int[] TrjTable = new int[FrameBlockSize];
            int[] TraTable = new int[FrameBlockSize];
            var blocks = new List<List<short>>();
            for (var blockFrame = 0; blockFrame < (NumKeys + 7) / 8; blockFrame++)
            {
                var r = new BitReader(new MemoryStream(frameBlockData[blockFrame]), 64, FrostySdk.IO.Endian.Big);
                bitCount = 24;
                //read rot table bytes
                rotTable[blockFrame] = (int)r.ReadUIntHigh(8)+1;
                TrjTable[blockFrame] = (int)r.ReadUIntHigh(8)+1;
                TraTable [blockFrame] = (int)r.ReadUIntHigh(8)+1;
                // go through each dof
                for (var dofIdx = 0; dofIdx < dofTable.Length; dofIdx++)
                {
                    var block = new List<short>();
                    var blockx = new List<short>();
                    var blocky = new List<short>();
                    var blockz = new List<short>();
                    var blockw = new List<short>();
                    var channelType = "x";
                    var subBlock = dofTable[dofIdx];

                    var s_Components = subBlock.BitsPerSubBlock;

                    //go through each channel once at a time
                    channelType = "x";
                    blockx = readChannel(s_Components, r, channelType);
                    bitCount += blockx.Last();
                    blockx.RemoveAt(blockx.Count-1);
                    channelType = "y";

                    blocky = readChannel(s_Components, r, channelType);
                    bitCount += blocky.Last();
                    blocky.RemoveAt(blocky.Count - 1);
                    channelType = "z";

                    blockz = readChannel(s_Components, r, channelType);
                    bitCount += blockz.Last();
                    blockz.RemoveAt(blockz.Count - 1);
                    channelType = "x";

                    //if quaternion then we have a w channel
                    if (dofIdx < QuaternionCount)
                    {
                        blockw = readChannel(s_Components, r, channelType);
                        bitCount += blockw.Last();
                        blockw.RemoveAt(blockw.Count - 1);


                    }
                    else // if vector we pad with an empty value
                    {
                       for(var j = 0;j<8;j++)
                        {
                            blockw.Add(0);
                        }
                    }
                        //sort blocks into a one final block
                        for (int i = 0; i < 8; i++)
                        {
                            block.Add(blockx[i]);
                            block.Add(blocky[i]);
                        block.Add(blockz[i]);
                                block.Add(blockw[i]);
                        }
                    blocks.Add(block);
                }



                bitsLeft = FrameBlockSizes[blockFrame]*8 - bitCount;
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
        public List<short> readChannel(DofTable.BitsPerComponent[] s_Components,BitReader r,string channelType)
        {
            int[] presence = new int[8];
            int[] sign = new int[8];
            var block = new List<short>();
            short channelData = 0;
            short bitsToRead = 0;
            short bitCount=0;

            var count = 0;
            var result = 0;
            foreach (var s_Component in s_Components)
            {
                switch (channelType)
                {
                    case "x":
                        bitsToRead = (short)s_Component.SafeBitsX(CatchAllBitCount);
                        break;
                    case "y":
                        bitsToRead = (short)s_Component.SafeBitsY(CatchAllBitCount);
                        break;
                    case "z":
                        bitsToRead = (short)s_Component.SafeBitsZ(CatchAllBitCount);
                        break;
                    case "w":
                        bitsToRead = (short)s_Component.SafeBitsW(CatchAllBitCount);
                        break;
                }
                // dont read presence bit if numbits is 0
                if (bitsToRead > 0)
                {
                    presence[count] = (int)r.ReadUIntHigh(1);
                 
                    count++;

                }
                
                bitCount += 1;
            }
            
            for (int j = 0; j < 8; j++)
            {
                // dont read sign bit if presence bit is 0
                if (presence[j] > 0)
                {
                    sign[j] = (int)r.ReadUIntHigh(1);
                    bitCount += 1;
                }
            }
            count = 0;
            foreach (var s_Component in s_Components)
            {
                
                if (presence[count] > 0)
                {
                    switch (channelType)
                    {
                        case "x":
                            bitsToRead = (short)s_Component.SafeBitsX(CatchAllBitCount);
                            break;
                        case "y":
                            bitsToRead = (short)s_Component.SafeBitsY(CatchAllBitCount);
                            break;
                        case "z":
                            bitsToRead = (short)s_Component.SafeBitsZ(CatchAllBitCount);
                            break;
                        case "w":
                            bitsToRead = (short)s_Component.SafeBitsW(CatchAllBitCount);
                            break;
                    }
                    bitCount += bitsToRead;

                    channelData = (short)r.ReadUIntHigh(bitsToRead);
                    if (sign[count] == 0)
                    {
                        channelData *= -1;
                    }
                    block.Add(channelData);
                    count++;
                } else
                {
                    channelData = 0;
                    block.Add(channelData);
                    count++;
                }
            }
            block.Add(bitCount);
            return block;
        }

    }
}