using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetBankPlugin.Ant
{
    public class RigAsset : AntAsset
    {
        public override string Name { get; set; }
        public override Guid ID { get; set; }
        public Guid FeatureCollection { get; set; }
        public  Guid Skeleton { get; set; }
        public Guid[] DofSetLists { get; set; }
        public Object DefaultVector3Values { get; set; }
        public Object DefaultVector4Values { get; set; }

        public override void SetData(Dictionary<string, object> data)
        {
            Name = Convert.ToString(data["__name"]);
            ID = (Guid)data["__guid"];
            FeatureCollection = (Guid)data["FeatureCollection"];
            Skeleton = (Guid)data["Skeleton"];
            DofSetLists = (Guid[])data["DofSetLists"];
            DefaultVector3Values = data["DefaultVector3Values"];
            DefaultVector4Values = data["DefaultVector4Values"];
            

        }
    }
}
