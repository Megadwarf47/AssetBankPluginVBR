using FrostySdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetBankPlugin.Ant
{
    public class ClipControllerAsset : AntAsset
    {
        public override string Name { get; set; }
        public override Guid ID { get; set; }

        // Schema Properties
        public string Key { get; set; }
        public Guid TagCollectionSetAsset { get; set; }
        public string TagCollectionSetAssetHash { get; set; }
        public Guid Anim { get; set; }
        public string HashAnim { get; set; }
        public string DofCodecAnim { get; set; }
        public float TickOffset { get; set; }
        public float FPS { get; set; }
        public float TimeScale { get; set; }
        public float Distance { get; set; }
        public int Modes { get; set; }

        // Compatibility Properties
        public Guid[] Anims { get; set; }
        public Guid Target { get; set; }
        public float NumTicks { get; set; }
        public int TrajectoryAnimIndex { get; set; }
        public Guid TagCollectionSet { get; set; }

        public override void SetData(Dictionary<string, object> data)
        {
            // Internal Frosty Keys
            Name = data.ContainsKey("__name") ? Convert.ToString(data["__name"]) : "";
            ID = GetGuid(data, "__guid");
            if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesBattleforNeighborville))
            {
                // Exact mapping from the BFN generated schema
                Key = (string)(data["__key"]);
                TagCollectionSetAssetHash = (string)(data["TagCollectionSetAsset"]); 
                HashAnim = (string)(data["Anim"]);
                DofCodecAnim = (string)(data["DofCodecAnim"]); 
                TickOffset = GetFloat(data, "TickOffset");
                FPS = GetFloat(data, "FPS");
                TimeScale = GetFloat(data, "TimeScale");
                Distance = GetFloat(data, "Distance");
                Modes = GetInt(data, "Modes"); // schema says uint8 GetInt handles it

                // Compatibility mapping
                TagCollectionSet = TagCollectionSetAsset;
            }
            else if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesGardenWarfare))
            {
                Anim = GetGuid(data, "Anim");
                Target = GetGuid(data, "Target");
                NumTicks = GetFloat(data, "NumTicks");
                FPS = GetFloat(data, "FPS");
                TimeScale = GetFloat(data, "FPSScale");
                Distance = GetFloat(data, "Distance");
                TrajectoryAnimIndex = GetInt(data, "DeltaTrajectory");
                TagCollectionSet = GetGuid(data, "TagCollectionSet");
            }
            else
            {
                // Default / GW2
                Anims = data.ContainsKey("Anims") ? (Guid[])data["Anims"] : null;
                Target = GetGuid(data, "Target");
                NumTicks = GetFloat(data, "NumTicks");
                TickOffset = GetFloat(data, "TickOffset");
                FPS = GetFloat(data, "FPS");
                TimeScale = GetFloat(data, "TimeScale");
                Distance = GetFloat(data, "Distance");
                TrajectoryAnimIndex = GetInt(data, "TrajectoryAnimIndex");
                Modes = GetInt(data, "Modes");
                TagCollectionSet = GetGuid(data, "TagCollectionSet");
            }
        }

        private float GetFloat(Dictionary<string, object> data, string key)
        {
            if (!data.ContainsKey(key)) return 0.0f;
            return Convert.ToSingle(data[key]);
        }

        private int GetInt(Dictionary<string, object> data, string key)
        {
            if (!data.ContainsKey(key)) return 0;
            return Convert.ToInt32(data[key]);
        }

        private Guid GetGuid(Dictionary<string, object> data, string key)
        {
            if (!data.ContainsKey(key)) return Guid.Empty;
            object val = data[key];
            if (val is Guid guid) return guid;
            if (val == null) return Guid.Empty;

            try { return new Guid(val.ToString()); }
            catch { return Guid.Empty; }
        }
    }
}