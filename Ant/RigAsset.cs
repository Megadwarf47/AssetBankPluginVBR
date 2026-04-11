using FrostySdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AssetBankPlugin.Ant
{
    public class RigAsset : AntAsset
    {
        public override string Name { get; set; }
        public override Guid ID { get; set; }
        public string Key { get; set; }
        public Guid FeatureCollection { get; set; }
        public  Guid Skeleton { get; set; }
        public Guid[] DofSetLists { get; set; }
        public Guid[] RigDofSets { get; set; }
        public Object DefaultVector3Values { get; set; }
        public Object DefaultVector4Values { get; set; }
        public UInt16[] DofIds { get; set; }
        public string[] HashDofIds { get; set; }

        public override void SetData(Dictionary<string, object> data)
        {
            Name = Convert.ToString(data["__name"]);
            if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesBattleforNeighborville))
            {
                Key = (string)data["__key"];
                HashDofIds = (string[])data["DofIds"];
            }
            else
            {
                ID = (Guid)data["__guid"];
            }

            if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesGardenWarfare2))
            {
                
                FeatureCollection = (Guid)data["FeatureCollection"];
                Skeleton = (Guid)data["Skeleton"];
                DofSetLists = (Guid[])data["DofSetLists"];
                RigDofSets = (Guid[])data["RigDofSets"];
                DefaultVector3Values = data["DefaultVector3Values"];
                DefaultVector4Values = data["DefaultVector4Values"];
                DofIds = (UInt16[])data["DofIds"];
            }

        }
    }
}
