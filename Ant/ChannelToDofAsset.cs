using AssetBankPlugin.Enums;
using FrostySdk;
using System;
using System.Collections.Generic;

namespace AssetBankPlugin.Ant
{
    public class ChannelToDofAsset : AntAsset
    {
        public override string Name { get; set; }
        public override Guid ID { get; set; }

        public StorageType StorageType { get; set; } = StorageType.Overwrite;
        public uint[] IndexData { get; set; }

        public override void SetData(Dictionary<string, object> data)
        {
            Name = Convert.ToString(data["__name"]);
            ID = (Guid)data["__guid"];

            switch ((ProfileVersion)ProfilesLibrary.DataVersion)
            {
                
                
                case ProfileVersion.Battlefield1:
                {
                    byte[] dofIds = (byte[])data["IndexData"];
                    IndexData = Array.ConvertAll(dofIds, val => checked((uint)val));
                } break;
                case ProfileVersion.PlantsVsZombiesGardenWarfare:
                case ProfileVersion.Battlefield4:
                {
                    StorageType = (StorageType)Convert.ToInt32(data["StorageType"]);
                    var dofIds = (byte[])data["IndexData"];
                    IndexData = Array.ConvertAll(dofIds, val => checked((uint)val));
                } break;
                case ProfileVersion.PlantsVsZombiesGardenWarfare2:
                    {
                        UInt16[] dofIds = (UInt16[])data["DofIds"];
                        IndexData = Array.ConvertAll(dofIds, val => checked((uint)val));
                    }
                    break;


                default: throw new NotImplementedException();
            }
        }
    }
}
