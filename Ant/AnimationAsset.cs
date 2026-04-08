using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
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

        // Hardcoded rig GUID for PvZ GW2 filtering
        private static readonly Guid HardcodedRigGuid = new Guid("00080608-0000-0000-0000-000000000000");

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
            Debug.WriteLine($"[Anim Export] GetChannels called for animation '{Name}' (ID: {ID})");

            LayoutHierarchyAsset hierarchy = null;
            ChannelToDofAsset dof = null;

            switch ((ProfileVersion)ProfilesLibrary.DataVersion)
            {
                case ProfileVersion.PlantsVsZombiesGardenWarfare2:
                    Debug.WriteLine("[Anim Export] Profile: PvZ GW2");
                    dof = (ChannelToDofAsset)AntRefTable.Get(ChannelToDofAsset);
                    StorageType = dof.StorageType;
                    Debug.WriteLine($"[Anim Export] StorageType: {StorageType}, ChannelToDofAsset: {ChannelToDofAsset}");

                    foreach (var c in AntRefTable.Refs)
                    {
                        if (c.Value is ClipControllerAsset cl)
                        {
                            if (cl.Anims.Contains(ID))
                            {
                                var targetAsset = AntRefTable.Get(cl.Target);
                                if (targetAsset is LayoutHierarchyAsset lh)
                                {
                                    FPS = cl.FPS;
                                    hierarchy = lh;
                                    Debug.WriteLine($"[Anim Export] Found ClipControllerAsset '{c.Key}' with FPS={FPS}, Target={cl.Target}");
                                    break;
                                }
                                else
                                {
                                    Debug.WriteLine($"[Anim Export Warning] ClipController target is not LayoutHierarchyAsset (type: {targetAsset?.GetType()})");
                                }
                            }
                        }
                    }
                    break;

                case ProfileVersion.PlantsVsZombiesGardenWarfare:
                    Debug.WriteLine("[Anim Export] Profile: PvZ GW1");
                    dof = (ChannelToDofAsset)AntRefTable.Get(channelToDofAsset);
                    StorageType = dof.StorageType;
                    foreach (var c in AntRefTable.Refs)
                    {
                        if (c.Value is ClipControllerAsset cl)
                        {
                            if (cl.Anim == ID)
                            {
                                var targetAsset = AntRefTable.Get(cl.Target);
                                if (targetAsset is LayoutHierarchyAsset lh)
                                {
                                    FPS = cl.FPS;
                                    hierarchy = lh;
                                    Debug.WriteLine($"[Anim Export] Found ClipControllerAsset '{c.Key}' with FPS={FPS}");
                                    break;
                                }
                            }
                        }
                    }
                    break;

                case ProfileVersion.Battlefield4:
                default:
                    Debug.WriteLine($"[Anim Export] Profile: {(ProfileVersion)ProfilesLibrary.DataVersion}");
                    dof = (ChannelToDofAsset)AntRefTable.Get(channelToDofAsset);
                    StorageType = dof.StorageType;
                    foreach (var c in AntRefTable.Refs)
                    {
                        if (c.Value is ClipControllerData cl)
                        {
                            if (cl.Anim == ID || cl.Anim == AntRefTable.InternalRefs[ID])
                            {
                                var targetAsset = AntRefTable.Get(cl.Target);
                                if (targetAsset is LayoutHierarchyAsset lh)
                                {
                                    FPS = cl.FPS;
                                    hierarchy = lh;
                                    Debug.WriteLine($"[Anim Export] Found ClipControllerData with FPS={FPS}");
                                    break;
                                }
                            }
                        }
                    }
                    break;
            }

            if (hierarchy == null)
            {
                Debug.WriteLine($"[Anim Export Error] No valid LayoutHierarchyAsset found for animation '{Name}' (ID: {ID})");
                return new Dictionary<string, BoneChannelType>();
            }

            Debug.WriteLine($"[Anim Export] Hierarchy found: {hierarchy.Name} (ID: {hierarchy.ID})");

            // Build channel name mapping from layout assets
            var channelNames = new Dictionary<string, BoneChannelType>();
            for (int i = 0; i < hierarchy.LayoutAssets.Length; i++)
            {
                AntAsset layoutAsset = AntRefTable.Get(hierarchy.LayoutAssets[i]);

                if (layoutAsset is LayoutAsset la)
                {
                    Debug.WriteLine($"[Anim Export] Processing LayoutAsset '{la.Name}' with {la.Slots.Count} slots");
                    for (int x = 0; x < la.Slots.Count; x++)
                    {
                        channelNames[la.Slots[x].Name] = la.Slots[x].Type;
                        Debug.WriteLine($"[Anim Export]   Slot: {la.Slots[x].Name} -> Type {la.Slots[x].Type}");
                    }
                }
                else if (layoutAsset is DeltaTrajLayoutAsset)
                {
                    Debug.WriteLine($"[Anim Export] DeltaTrajLayoutAsset found, adding 8 rotation channels");
                    for (int x = 0; x < 8; x++)
                    {
                        channelNames["" + x] = BoneChannelType.Rotation;
                    }
                }
                else
                {
                    Debug.WriteLine($"[Anim Export Warning] Unknown layout asset type: {layoutAsset?.GetType()}");
                }
            }

            uint[] data = dof.IndexData;
            Debug.WriteLine($"[Anim Export] dof.IndexData length: {data.Length}");
            var channelNamesList = channelNames.ToList();
            var channels = new List<string>();

            // GW2 specific handling with fixes
            if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesGardenWarfare2))
            {
                Debug.WriteLine("[Anim Export] Processing GW2 DOF remapping with fixes");

                // Retrieve the rig using the hardcoded GUID
                var rig = AntRefTable.Get(HardcodedRigGuid) as RigAsset;
                if (rig == null)
                {
                    Debug.WriteLine($"[Anim Export Error] Hardcoded rig GUID {HardcodedRigGuid} not found or not a RigAsset");
                    return new Dictionary<string, BoneChannelType>();
                }

                Debug.WriteLine($"[Anim Export] Using rig '{rig.Name}' (ID: {rig.ID}) with {rig.DofIds?.Length ?? 0} DOF IDs");

                // Build a fast lookup dictionary for DOF ID to channel index
                var dofIdToIndex = new Dictionary<ushort, int>();
                for (int i = 0; i < rig.DofIds.Length; i++)
                {
                    dofIdToIndex[rig.DofIds[i]] = i;
                }

                uint[] actualData = new uint[data.Length];
                for (int i = 0; i < data.Length; i++)
                {
                    channels.Add("");
                    ushort rawDofId = (ushort)data[i];  // FIX: cast to ushort for correct lookup
                    if (dofIdToIndex.TryGetValue(rawDofId, out int idx))
                    {
                        actualData[i] = (uint)idx;
                        Debug.WriteLine($"[Anim Export Debug] data[{i}] = {data[i]} (0x{data[i]:X}) -> remapped to index {idx}");
                    }
                    else
                    {
                        actualData[i] = 0xFFFFFFFF;
                        Debug.WriteLine($"[Anim Export Warning] DOF ID {rawDofId} not found in rig's DofIds array");
                    }
                }

                // FIX: use actualData instead of raw data for channel mapping
                for (int i = 0; i < data.Length; i++)
                {
                    int channelId = (int)actualData[i];
                    if (channelId >= 0 && channelId < channelNamesList.Count)
                    {
                        channels[i] = channelNamesList[channelId].Key;
                        Debug.WriteLine($"[Anim Export Debug] Channel {i}: mapped to '{channels[i]}' (index {channelId})");
                    }
                    else
                    {
                        Debug.WriteLine($"[Anim Export Warning] GW2: Skipped out-of-bounds bone channel ID {channelId} at position {i}");
                    }
                }

                // Filter: only keep animation if it actually produced any channels using this rig
                int validChannelCount = channels.Count(c => !string.IsNullOrEmpty(c));
                Debug.WriteLine($"[Anim Export] GW2: Total channels after mapping = {channels.Count}, valid non-empty = {validChannelCount}");
                if (validChannelCount == 0)
                {
                    Debug.WriteLine($"[Anim Export] Animation '{Name}' does not match hardcoded rig {HardcodedRigGuid} - skipping extraction");
                    return new Dictionary<string, BoneChannelType>();
                }
            }
            else if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesGardenWarfare))
            {
                Debug.WriteLine("[Anim Export] Processing GW1 DOF mapping (no changes applied)");
                for (int i = 0; i < data.Length; i++) channels.Add("");

                for (int i = 0; i < data.Length; i++)
                {
                    int channelId = (int)data[i];
                    if (channelId >= 0 && channelId < channelNamesList.Count)
                    {
                        channels[i] = channelNamesList[channelId].Key;
                        Debug.WriteLine($"[Anim Export Debug] GW1: Channel {i} -> '{channels[i]}'");
                    }
                    else
                    {
                        Debug.WriteLine($"[Anim Export Warning] GW1: Skipped out-of-bounds bone channel {channelId}");
                    }
                }
            }
            else
            {
                Debug.WriteLine($"[Anim Export] Processing non-PvZ profile with StorageType={StorageType}");
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
                                    Debug.WriteLine($"[Anim Export Debug] Overwrite: Channel {i} -> '{channels[i]}'");
                                }
                                else
                                {
                                    Debug.WriteLine($"[Anim Export Warning] Overwrite: Skipped out-of-bounds bone channel {channelId}");
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
                                    Debug.WriteLine($"[Anim Export Debug] Append: channelId {channelId} -> '{channelKey}', targetIndex {targetIndex}");
                                }
                                else
                                {
                                    Debug.WriteLine($"[Anim Export Warning] Append: Skipped out-of-bounds bone channel {channelId}");
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

            // Build final output dictionary
            var output = new Dictionary<string, BoneChannelType>();
            for (int i = 0; i < channels.Count; i++)
            {
                string ch = channels[i];
                if (string.IsNullOrEmpty(ch)) continue;

                if (channelNames.TryGetValue(ch, out BoneChannelType bct))
                {
                    output[ch] = bct;
                    Debug.WriteLine($"[Anim Export] Final mapping: {ch} -> {bct}");
                }
                else
                {
                    Debug.WriteLine($"[Anim Export Warning] Missing mapping: Channel '{ch}' was not found in LayoutHierarchy.");
                }
            }

            Debug.WriteLine($"[Anim Export] GetChannels returning {output.Count} valid bone channels for animation '{Name}'");
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