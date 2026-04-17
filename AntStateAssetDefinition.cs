using AssetBankPlugin.Ant;
using AssetBankPlugin.Export;
using AssetBankPlugin.GenericData;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using FrostySdk;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AssetBankPlugin
{
    public class AntStateAssetDefinition : AssetDefinition
    {
        /// Tracker to prevent loading the same level context twice
        private static HashSet<string> _loadedContexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _loadLock = new object();

        /// This loads all AssetBank bundles that share the same folder prefix as the current asset.
        /// It ensures that cross‑referenced AnimationAssets (e.g. boss <-> level) are in AntRefTable.
        /// Ts is currently broken i believe
        public static void LoadContextualBanks(EbxAssetEntry entry)
        {
            if (entry == null || entry.Bundles.Count == 0)
                return;

            string bundleName = App.AssetManager.GetBundleEntry(entry.Bundles[0]).Name;
            string prefix = bundleName.Contains("/")
                ? bundleName.Substring(0, bundleName.LastIndexOf('/') + 1)
                : bundleName;

            lock (_loadLock)
            {
                if (_loadedContexts.Contains(prefix))
                    return;

                var matchingBundles = App.AssetManager.EnumerateBundles()
                    .Where(b => b.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

                foreach (var bundle in matchingBundles)
                {
                    LoadAntStateFromBundle(bundle);
                }

                _loadedContexts.Add(prefix);
            }
        }

        public override void GetSupportedExportTypes(List<AssetExportType> exportTypes)
        {
            exportTypes.Add(new AssetExportType("gltf", "GL Transfer format"));
            exportTypes.Add(new AssetExportType("xml", "XML Animation Keyframe Dump"));
            exportTypes.Add(new AssetExportType("smd", "Source StudioMdl Data"));
            base.GetSupportedExportTypes(exportTypes);
        }

        public override FrostyAssetEditor GetEditor(ILogger logger) => new AntStateAssetEditor(logger);

        public override bool Export(EbxAssetEntry entry, string path, string filterType)
        {
            var opt = new AnimationOptions();
            opt.Load();

            /// Load cross‑referenced banks before reading the main asset.
            LoadContextualBanks(entry);

            EbxAsset asset = App.AssetManager.GetEbx(entry);
            dynamic antStateAsset = asset.RootObject;

            Stream s;
            int bundleId = 0;
            if (antStateAsset.StreamingGuid == Guid.Empty)
            {
                ResAssetEntry res = App.AssetManager.GetResEntry(entry.Name);
                bundleId = res.Bundles[0];
                s = App.AssetManager.GetRes(res);
            }
            else
            {
                ChunkAssetEntry chunk = App.AssetManager.GetChunkEntry(antStateAsset.StreamingGuid);
                bundleId = chunk.Bundles[0];
                s = App.AssetManager.GetChunk(chunk);
            }

            using (var r = new NativeReader(s))
            {
                var bank = new Bank(r, bundleId);

                var skelEbx = App.AssetManager.GetEbx(opt.ExportSkeletonAsset);
                dynamic skel = skelEbx.RootObject;
                var skeleton = SkeletonAssetExport.ConvertToInternal(skel);

                foreach (var dataName in bank.DataNames)
                {
                    var dat = AntRefTable.Get(dataName.Value);
                    if (dat is AnimationAsset anim)
                    {
                        anim.Name = dataName.Key;
                        anim.Channels = anim.GetChannels(anim.ChannelToDofAsset);

                        /// Skip assets with broken references (caused by missing context)
                        if (anim.Channels == null || anim.Channels.Count == 0)
                            continue;

                        var intern = anim.ConvertToInternal();
                        if (intern != null)
                            new AnimationExporterSEANIM().Export(intern, skeleton, Path.GetDirectoryName(path));
                    }
                }
            }

            FrostyMessageBox.Show($"Exported {entry.Name} for {ProfilesLibrary.ProfileName}", "Export Finished");
            return true;
        }

        public static void LoadAntStateFromBundle(BundleEntry bundle)
        {
            /// AssetBank resource types (may vary slightly between games)
            var resources = App.AssetManager.EnumerateRes(bundle)
                .Where(r => r.ResType == 0x51A3C853 || r.ResType == 0xEC1B7BF4);

            foreach (var res in resources)
            {
                using (var antBank = App.AssetManager.GetRes(res))
                using (var antReader = new NativeReader(antBank))
                {
                    _ = new Bank(antReader, App.AssetManager.GetBundleId(bundle));
                }
            }
        }
    }
}
