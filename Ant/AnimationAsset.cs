using System;
using System.Collections.Generic;
using System.Linq;
using AssetBankPlugin.Enums;
using AssetBankPlugin.Export;
using FrostySdk;

namespace AssetBankPlugin.Ant
{
    public class AnimationAsset : AntAsset
    {
        public override string Name { get; set; }
        public override Guid ID { get; set; }

        public int CodecType;
        public int AnimId;
        public float TrimOffset;
        public ushort EndFrame;
        public bool Additive;
        public Guid ChannelToDofAsset;
        public Dictionary<string, BoneChannelType> Channels;
        public float FPS;

        public StorageType StorageType;

        public AnimationAsset() { }

        public override void SetData(Dictionary<string, object> data)
        {
            Name = (string)data["__name"];
            ID = (Guid)data["__guid"];
            CodecType = Convert.ToInt32(data["CodecType"]);
            AnimId = Convert.ToInt32(data["AnimId"]);
            TrimOffset = (float)data["TrimOffset"];
            EndFrame = Convert.ToUInt16(data["EndFrame"]);

            if (data.TryGetValue("Additive", out object additive))
            {
                Additive = (bool)additive;
            }
            ChannelToDofAsset = (Guid)data["ChannelToDofAsset"];
        }

        public Dictionary<string, BoneChannelType> GetChannels(Guid channelToDofAsset)
        {
            LayoutHierarchyAsset hierarchy = null;
            ChannelToDofAsset dof;

            switch ((ProfileVersion)ProfilesLibrary.DataVersion)
            {
                case ProfileVersion.PlantsVsZombiesGardenWarfare2:
                case ProfileVersion.Battlefield1:
                    {
                        dof = (ChannelToDofAsset)AntRefTable.Get(ChannelToDofAsset);
                        foreach (var c in AntRefTable.Refs)
                        {
                            if (c.Value is ClipControllerAsset cl)
                            {
                                if (cl.Anims.Contains(ID))
                                {
                                    // FIX: Safely check if the target is actually a LayoutHierarchyAsset
                                    var targetAsset = AntRefTable.Get(cl.Target);
                                    if (targetAsset is LayoutHierarchyAsset lh)
                                    {
                                        FPS = cl.FPS;
                                        hierarchy = lh;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    break;
                case ProfileVersion.PlantsVsZombiesGardenWarfare:
                    {
                        dof = (ChannelToDofAsset)AntRefTable.Get(channelToDofAsset);
                        StorageType = dof.StorageType;
                        foreach (var c in AntRefTable.Refs)
                        {
                            if (c.Value is ClipControllerAsset cl)
                            {
                                if (cl.Anim == ID)
                                {
                                    // FIX: Safe casting
                                    var targetAsset = AntRefTable.Get(cl.Target);
                                    if (targetAsset is LayoutHierarchyAsset lh)
                                    {
                                        FPS = cl.FPS;
                                        hierarchy = lh;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    break;
                case ProfileVersion.Battlefield4:
                default:
                    {
                        dof = (ChannelToDofAsset)AntRefTable.Get(channelToDofAsset);
                        StorageType = dof.StorageType;
                        foreach (var c in AntRefTable.Refs)
                        {
                            if (c.Value is ClipControllerData cl)
                            {
                                if (cl.Anim == ID || cl.Anim == AntRefTable.InternalRefs[ID])
                                {
                                    // FIX: Safe casting
                                    var targetAsset = AntRefTable.Get(cl.Target);
                                    if (targetAsset is LayoutHierarchyAsset lh)
                                    {
                                        FPS = cl.FPS;
                                        hierarchy = lh;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    break;
            }

            // Guard: if no matching controller with a LayoutHierarchy target was found, stop here safely.
            if (hierarchy == null)
                return new Dictionary<string, BoneChannelType>();

            var channelNames = new Dictionary<string, BoneChannelType>();
            for (int i = 0; i < hierarchy.LayoutAssets.Length; i++)
            {
                AntAsset layoutAsset = AntRefTable.Get(hierarchy.LayoutAssets[i]);

                if (layoutAsset is LayoutAsset la)
                {
                    for (int x = 0; x < la.Slots.Count; x++)
                    {
                        channelNames[la.Slots[x].Name] = la.Slots[x].Type;
                    }
                }
                else if (layoutAsset is DeltaTrajLayoutAsset)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        channelNames["" + x] = BoneChannelType.Rotation;
                    }
                }
            }

            uint[] data = dof.IndexData;
            var channelNamesList = channelNames.ToList();
            var channels = new List<string>();

            if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesGardenWarfare2) || ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesGardenWarfare))
            {
                for (int i = 0; i < data.Length; i++) channels.Add("");

                for (int i = 0; i < data.Length; i++)
                {
                    int channelId = (int)data[i];
                    if (channelId >= 0 && channelId < channelNamesList.Count)
                    {
                        channels[i] = channelNamesList[channelId].Key;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Anim Export Warning] PvZ: Skipped out-of-bounds bone channel {channelId}");
                    }
                }
            }
            else
            {
                switch (StorageType)
                {
                    case StorageType.Overwrite:
                        {
                            for (int i = 0; i < data.Length; i++) channels.Add("");

                            for (int i = 0; i < data.Length; i++)
                            {
                                int channelId = (int)data[i];
                                if (channelId >= 0 && channelId < channelNamesList.Count)
                                {
                                    channels[i] = channelNamesList[channelId].Key;
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"[Anim Export Warning] Overwrite: Skipped out-of-bounds bone channel {channelId}");
                                }
                            }
                        }
                        break;
                    case StorageType.Append:
                        {
                            var offsets = new Dictionary<int, int>();
                            int offset = 0;
                            for (int i = 0; i < data.Length; i += 2)
                            {
                                int appendTo = (int)data[i];
                                int channelId = (int)data[i + 1];

                                offsets[appendTo] = offset;
                                offset++;

                                int targetIndex = offsets[appendTo];
                                string channelKey = "";
                                if (channelId >= 0 && channelId < channelNamesList.Count)
                                {
                                    channelKey = channelNamesList[channelId].Key;
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"[Anim Export Warning] Append: Skipped out-of-bounds bone channel {channelId}");
                                }

                                if (targetIndex <= channels.Count)
                                {
                                    channels.Insert(targetIndex, channelKey);
                                }
                                else
                                {
                                    channels.Add(channelKey);
                                }
                            }
                        }
                        break;
                }
            }

            var output = new Dictionary<string, BoneChannelType>();
            for (int i = 0; i < channels.Count; i++)
            {
                string ch = channels[i];
                if (string.IsNullOrEmpty(ch)) continue;

                if (channelNames.TryGetValue(ch, out BoneChannelType bct))
                {
                    output[ch] = bct;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Anim Export Warning] Missing mapping: Channel '{ch}' was not found in LayoutHierarchy.");
                }
            }

            return output;
        }

        public virtual InternalAnimation ConvertToInternal() { return null; }
    }

    public enum BoneChannelType
    {
        None = 0,
        Rotation = 14,
        Position = 2049856663,
        Scale = 2049856454,
    }
}