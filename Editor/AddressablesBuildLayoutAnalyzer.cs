using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace XSystem.Addressable.Analyzer
{
    public class AddressablesBuildLayoutAnalyzer
    {
        private readonly Dictionary<string, AddressableAssetGroup> guidGroupDic = new ();
        private readonly Dictionary<BuildLayout.ExplicitAsset, HashSet<BuildLayout.ExplicitAsset>> remoteDepGroups;

        // Public 프로퍼티 추가
        public Dictionary<BuildLayout.ExplicitAsset, HashSet<BuildLayout.ExplicitAsset>> RemoteDepGroups => remoteDepGroups;

        public AddressablesBuildLayoutAnalyzer(string path)
        {
            var buildLayout = BuildLayout.Open(path, true, true);
            foreach (var g in AddressableAssetSettingsDefaultObject.Settings.groups)
            {
                guidGroupDic[g.Guid] = g;
            }
            remoteDepGroups = FindLocalToRemoteRefs(buildLayout);
        }

        private Dictionary<BuildLayout.ExplicitAsset, HashSet<BuildLayout.ExplicitAsset>> FindLocalToRemoteRefs(BuildLayout buildLayout)
        {
            var remoteAssets = new Dictionary<BuildLayout.ExplicitAsset, HashSet<BuildLayout.ExplicitAsset>>();

            foreach (var group in buildLayout.Groups)
            {
                if (!TryGetLocalGroup(group.Guid, out _))
                    continue;

                ProcessBundleGroup(remoteAssets, group);
            }

            return remoteAssets;
        }

        private bool TryGetLocalGroup(string guid, out AddressableAssetGroup group)
        {
            if (!guidGroupDic.TryGetValue(guid, out group))
                return false;

            return IsLocal(group);
        }

        private void ProcessBundleGroup(Dictionary<BuildLayout.ExplicitAsset, HashSet<BuildLayout.ExplicitAsset>> remoteAssets, BuildLayout.Group group)
        {
            foreach (var bundle in group.Bundles)
            {
                foreach (var file in bundle.Files)
                {
                    ProcessFile(remoteAssets, file);
                }
            }
        }

        private void ProcessFile(Dictionary<BuildLayout.ExplicitAsset, HashSet<BuildLayout.ExplicitAsset>> remoteAssets, BuildLayout.File file)
        {
            foreach (var asset in file.Assets)
            {
                ProcessAsset(remoteAssets, asset);
            }
        }

        private void ProcessAsset(Dictionary<BuildLayout.ExplicitAsset, HashSet<BuildLayout.ExplicitAsset>> remoteAssets, BuildLayout.ExplicitAsset asset)
        {
            foreach (var externalAsset in asset.ExternallyReferencedAssets)
            {
                AddRemoteReference(remoteAssets, asset, externalAsset);
            }
        }

        private void AddRemoteReference(Dictionary<BuildLayout.ExplicitAsset, HashSet<BuildLayout.ExplicitAsset>> remoteAssets, BuildLayout.ExplicitAsset asset, BuildLayout.ExplicitAsset externalAsset)
        {
            if (!guidGroupDic.TryGetValue(externalAsset.GroupGuid, out var group) || IsLocal(group))
                return;

            if (!remoteAssets.TryGetValue(asset, out var refs))
            {
                refs = new HashSet<BuildLayout.ExplicitAsset>();
                remoteAssets[asset] = refs;
            }

            refs.Add(externalAsset);
        }

        public static bool IsLocal(AddressableAssetGroup g)
        {
            var bundledSchema = g.GetSchema<BundledAssetGroupSchema>();
            if (bundledSchema != null)
            {
                var buildPath = bundledSchema.BuildPath.GetName(g.Settings);
                return buildPath == AddressableAssetSettings.kLocalBuildPath;
            }

            return true;
        }
        
        public void PrintLocalToRemoteRefs()
        {
            foreach (var kv in remoteDepGroups)
            {
                var file = kv.Key;
                var deps = kv.Value;
                if (deps.Count > 0)
                {
                    UnityEngine.Debug.Log($"Reference {file.Bundle.Name} -> {string.Join(", ", deps.Select(d => d.AddressableName))}");
                }
            }
        }
    }
}


