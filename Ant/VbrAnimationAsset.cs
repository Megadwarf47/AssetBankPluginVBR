using AssetBankPlugin.Export;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Markup;
using static FrostySdk.GeometryDeclarationDesc;

namespace AssetBankPlugin.Ant
{
    public partial class VbrAnimationAsset : AnimationAsset
    {
        public byte[] Data = new byte[0];
        public ushort NumKeys;
        public ushort NumVec3;
        public ushort NumFloat;
        public int DataSize;
        public bool Cycle;
        public float QuatMin;
        public float TrajMin;
        public float Vec3Min;
        public float FloatMin;
        public float QuatMax;
        public float TrajMax;
        public float Vec3Max;
        public float FloatMax;
        public float VectorOffsetScale;
        public float Dct;
        public ushort NumQuats;
        public ushort NumFloatVec;
        public ushort ConstChanMapSize;
        public ushort QuaternionCount;
        public ushort ConstPaletteSize;
        public ushort Vector3Count;
        public ushort ConstQuaternionCount;
        public ushort ConstVector3Count;
        public ushort VectorOffsetSize;
        public ushort CodecTypeID;
        public ushort EndFrame2;
        public ushort Flags;
        public int offset;
        public int FrameBlockSize;
        public byte[] PaletteIndexes;
        public byte[] ConstChanMap;
        public byte[] VectorOffsets;
        public byte QuantizeMultSubblock;
        public byte CatchAllBitCount;
        public float[] ConstantPalette;
        public ushort[] FrameBlockSizes;
        
        public short[] DeltaBaseX;
        public short[] DeltaBaseY;
        public short[] DeltaBaseZ;
        public short[] DeltaBaseW;
        public ushort[] BitsPerSubblock;
        
        public ushort[] KeyTimes;
        public ushort QuantizeMultBlock;


        private List<Vector4> DecompressedData = new List<Vector4>();
        public VbrAnimationAsset() { }

        public override void SetData(Dictionary<string, object> data)
        {

                ID = (Guid)data["__guid"];
                Name = (string)data["__name"];

                QuatMin = (float)data["QuatMin"];
                TrajMin = (float)data["TrajMin"];
                Vec3Min = (float)data["Vec3Min"];
                FloatMin = (float)data["FloatMin"];
                QuatMax = (float)data["QuatMax"];
                TrajMax = (float)data["TrajMax"];
                Vec3Max = (float)data["Vec3Max"];
                FloatMax = (float)data["FloatMax"];
                VectorOffsetScale = (float)data["VectorOffsetScale"];
                Dct = (float)data["Dct"];
                QuaternionCount = (ushort)data["QuaternionCount"];
                Vector3Count = (ushort)data["Vector3Count"];
                ConstQuaternionCount = (ushort)data["ConstQuaternionCount"];
                ConstVector3Count = (ushort)data["ConstVector3Count"];
                NumKeys = (ushort)data["NumKeys"];
                ConstChanMapSize = (ushort)data["ConstChanMapSize"];
                ConstPaletteSize = (ushort)data["ConstPaletteSize"];
                VectorOffsetSize = (ushort)data["VectorOffsetSize"];
                Flags = (ushort)data["Flags"];
                ConstantPalette = (float[])data["ConstantPalette"];
                FrameBlockSizes = (ushort[])data["FrameBlockSizes"];
                Data = data["Data"] as byte[];
            FrameBlockSize = FrameBlockSizes.Length;

                base.SetData(data);

                DecompressedData = Decompress();
            }

        public override InternalAnimation ConvertToInternal()
        {
            var ret = new InternalAnimation();

            List<string> posChannels = new List<string>();
            List<string> rotChannels = new List<string>();
            List<string> scaleChannels = new List<string>();

            // Get all names.
            //for each channel, write which index in the rotchannel list etc it is
            var rotCounter = 0;
            var VectorCounter = 0;
            var scaleCounter = 0;
            var scaleStart = 0;
            int[] rotIndex = new int[ConstQuaternionCount+QuaternionCount];
            int[] VectorIndex = new int[ConstVector3Count+Vector3Count];
            
            foreach (var channel in Channels)
            {
                
                if (channel.Value == BoneChannelType.Rotation)
                {
                    rotChannels.Add(channel.Key);
                    rotIndex[rotCounter] = rotCounter+VectorCounter;
                    rotCounter++;
                }
                else if (channel.Value == BoneChannelType.Position)
                {
                    posChannels.Add(channel.Key);
                    VectorIndex[VectorCounter] = rotCounter + VectorCounter;
                    VectorCounter++;
                }
                else if (channel.Value == BoneChannelType.Scale)
                {
                    scaleChannels.Add(channel.Key);
                    VectorIndex[VectorCounter] = rotCounter + VectorCounter;
                    VectorCounter++;
                    if (scaleCounter == 0)
                    {
                        scaleStart = VectorCounter;
                    }
                    scaleCounter++;
                }
                
            }
            //generate mappings
            var chanMapOffset = 1;
            var chanMapCount = 0;

            var mapping = ConstChanMap[0];
            var valuesToMap = ConstChanMap[chanMapOffset];
            int[] constQuatMap = new int[ConstQuaternionCount];
            int[] QuatMap = new int[QuaternionCount];
            int[] constVectorMap = new int[ConstVector3Count];
            int[] VectorMap = new int[Vector3Count];
            for (var j = 0; j < ConstQuaternionCount; j++)
            {


                if (chanMapCount >= valuesToMap)
                {
                    mapping += ConstChanMap[chanMapOffset + 1];
                    chanMapOffset += 2;
                    valuesToMap += ConstChanMap[chanMapOffset];
                }
                constQuatMap[j] = mapping;
                mapping++;
                chanMapCount++;
            }
            for (var j = 0; j < ConstVector3Count; j++)
            {

                if (chanMapCount >= valuesToMap)
                {
                    mapping += ConstChanMap[chanMapOffset + 1];
                    chanMapOffset += 2;
                    valuesToMap += ConstChanMap[chanMapOffset];
                }
                // this mapping value refers to its position among all the channels
                // we want the value in the positions/vector channel
                constVectorMap[j] = mapping;
                mapping++;
                chanMapCount++;
            }

            //get mappings for dynamic quats+vectors using the mappings which werent used for const channels
            // all quat values come first so we can just use the original mappings

            var QuatIndex = 0;
            for (var j = 0; j < (QuaternionCount + ConstQuaternionCount); j++)
            {
                if (!constQuatMap.Contains(j))
                {
                    QuatMap[QuatIndex] = j;
                    QuatIndex++;
                }
            }
            //determine vector index using the mappings which werent used

            var Vector3Index = 0;
            for (var j = ConstQuaternionCount+QuaternionCount; j < ConstQuaternionCount + QuaternionCount+(Vector3Count+ConstVector3Count); j++)
            {
                
                if (!constVectorMap.Contains(j))
                {
                VectorMap[Vector3Index] = j;
                Vector3Index++;
            }
            }
            // Assign values to Channels.

            var dofCount = QuaternionCount + Vector3Count + 0;

            for (int i = 0; i < NumKeys; i++)
            {
                Frame frame = new Frame();

                var rotations = new List<Quaternion>(rotChannels.Count);
                var positions = new List<Vector3>(posChannels.Count);
                var scales = new List<Vector3>(scaleChannels.Count);
                //pad with empty values
                for (var j=0; j < rotations.Capacity; j++)
                    rotations.Add(new Quaternion(0.0f,0.0f,0.0f,0.0f));
                for (var j = 0; j < positions.Capacity; j++)
                    positions.Add(new Vector3(0.0f, 0.0f, 0.0f));
                for (var j = 0; j < scales.Capacity; j++)
                    scales.Add(new Vector3(0.0f, 0.0f, 0.0f));
               
                
                // add const quaternions
                var PaletteIndex = 0;
                for (int channelIdx = 0; channelIdx < ConstQuaternionCount; channelIdx++)
                        {
                    mapping = (byte)constQuatMap[channelIdx];
                    //paletteindexes has 4 indexes for each quat one after another
                    Vector4 element = new Vector4(ConstantPalette[PaletteIndexes[PaletteIndex]], ConstantPalette[PaletteIndexes[PaletteIndex+1]], ConstantPalette[PaletteIndexes[PaletteIndex+2]], ConstantPalette[PaletteIndexes[PaletteIndex+3]]);
                    rotations[mapping] = Quaternion.Normalize(new Quaternion(element.X, element.Y, element.Z, element.W));

                    PaletteIndex += 4;
                }
                for (int channelIdx = 0; channelIdx < QuaternionCount; channelIdx++)
                {
                    mapping = (byte)QuatMap[channelIdx];
                    int pos = (int)(i * dofCount + channelIdx);
                    Vector4 element = DecompressedData[pos];


                    rotations[mapping] = (Quaternion.Normalize(new Quaternion(element.X, element.Y, element.Z, element.W)));
                }
                // We need to differentiate between Scale and Position.
                //add const vectors
                scaleCounter = 0;
                for (int channelIdx = 0; channelIdx < ConstVector3Count; channelIdx++)
                {
                    mapping = (byte)constVectorMap[channelIdx];
                    //paletteindexes has 3 indexes for each vector one after another after the quats
                    Vector3 element = new Vector3(ConstantPalette[PaletteIndexes[PaletteIndex]], ConstantPalette[PaletteIndexes[PaletteIndex + 1]], ConstantPalette[PaletteIndexes[PaletteIndex + 2]]);
                    if (Channels.ElementAt(mapping).Value == BoneChannelType.Position)
                    {
                        positions[mapping-ConstQuaternionCount-QuaternionCount- scaleCounter]= (new Vector3(element.X, element.Y, element.Z));
                    }
                    else
                    {
                        scales[scaleCounter] = new Vector3(element.X, element.Y, element.Z);
                        scaleCounter++;
                    }

                        PaletteIndex += 3;
                }
                
                for (int channelIdx = 0; channelIdx < Vector3Count; channelIdx++)
                {
                    int pos = (int)(i * dofCount + QuaternionCount + channelIdx);
                    Vector4 element = DecompressedData[pos];
                    if (Channels.ElementAt(mapping).Value == BoneChannelType.Position)
                    {
                        

                        positions[mapping - ConstQuaternionCount - QuaternionCount-scaleCounter] = (new Vector3(element.X, element.Y, element.Z));
                    }
                    else
                    {
                        scales[scaleCounter] = (new Vector3(element.X, element.Y, element.Z));
                        scaleCounter++;
                    }
                    PaletteIndex += 3;
                }

                frame.Rotations = rotations;
                frame.Positions = positions;
                frame.Scales = scales;

                ret.Frames.Add(frame);
            }
            for (int i = 0; i < NumKeys; i++)
            {
                Frame f = ret.Frames[i];
                f.FrameIndex = i*3;
                ret.Frames[i] = f;
            }



            for (int r = 0; r < rotChannels.Count; r++)
                rotChannels[r] = rotChannels[r].Replace(".q", "");
            for (int r = 0; r < posChannels.Count; r++)
                posChannels[r] = posChannels[r].Replace(".t", "");
            for (int r = 0; r < scaleChannels.Count; r++)
                scaleChannels[r] = scaleChannels[r].Replace(".s", "");

            ret.Name = Name;
            ret.PositionChannels = posChannels;
            ret.RotationChannels = rotChannels;
            ret.ScaleChannels = scaleChannels;
            ret.Additive = Additive;
            return ret;
        }

    }
}