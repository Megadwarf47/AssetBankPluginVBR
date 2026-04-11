using FrostySdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetBankPlugin.Ant
{
    public class AnimatableConfigAsset : AntAsset
    {
        public override string Name { get; set; }
        public override Guid ID { get; set; }
        public string Key { get; set; }

        public override void SetData(Dictionary<string, object> data)
        {
            Name = Convert.ToString(data["__name"]);
            if (ProfilesLibrary.IsLoaded(ProfileVersion.PlantsVsZombiesBattleforNeighborville))
            {
                Key = (string)data["__key"];
            }
            else
            {
                ID = (Guid)data["__guid"];
            }



        }
    }
}
