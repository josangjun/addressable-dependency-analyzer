using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace XSystem.Addressable.Analyzer
{
    public static class AddressablesStaticRemoteDependencyReporter
    {
        private const string BuildLayoutPathArg = "-buildLayoutPath";
        private const string OutputPathArg = "-outputPath";

        public static void RunFromCommandLine()
        {
            try
            {
                var buildLayoutPath = GetRequiredArgValue(BuildLayoutPathArg);
                var outputPath = GetRequiredArgValue(OutputPathArg);
                WriteReport(buildLayoutPath, outputPath);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static void WriteReport(string buildLayoutPath, string outputPath)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("AddressableAssetSettingsDefaultObject.Settings is null.");
            }

            if (string.IsNullOrWhiteSpace(buildLayoutPath) || !File.Exists(buildLayoutPath))
            {
                throw new FileNotFoundException("Build layout file not found.", buildLayoutPath);
            }

            var buildLayout = BuildLayout.Open(buildLayoutPath, true, true);
            if (buildLayout == null)
            {
                throw new InvalidOperationException($"Failed to open build layout: {buildLayoutPath}");
            }

            var groupByGuid = BuildGroupLookup(settings);
            var rows = CollectRows(buildLayout, groupByGuid);
            var lines = new List<string>(rows.Count + 1)
            {
                "source_group\tsource_file\tdependency_group\tdependency_file"
            };
            lines.AddRange(rows);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllLines(outputPath, lines);
            UnityEngine.Debug.Log($"Wrote static remote dependency report to: {outputPath} ({rows.Count} rows)");
        }

        private static Dictionary<string, AddressableAssetGroup> BuildGroupLookup(AddressableAssetSettings settings)
        {
            var lookup = new Dictionary<string, AddressableAssetGroup>(StringComparer.Ordinal);
            foreach (var group in settings.groups)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.Guid))
                {
                    continue;
                }

                lookup[group.Guid] = group;
            }

            return lookup;
        }

        private static List<string> CollectRows(
            BuildLayout buildLayout,
            Dictionary<string, AddressableAssetGroup> groupByGuid)
        {
            var rows = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var layoutGroup in buildLayout.Groups)
            {
                if (layoutGroup == null || string.IsNullOrWhiteSpace(layoutGroup.Guid))
                {
                    continue;
                }

                if (!groupByGuid.TryGetValue(layoutGroup.Guid, out var sourceGroup))
                {
                    continue;
                }

                if (!IsStaticContent(sourceGroup))
                {
                    continue;
                }

                foreach (var bundle in layoutGroup.Bundles)
                {
                    if (bundle == null)
                    {
                        continue;
                    }

                    foreach (var file in bundle.Files)
                    {
                        if (file == null)
                        {
                            continue;
                        }

                        foreach (var asset in file.Assets)
                        {
                            if (asset == null)
                            {
                                continue;
                            }

                            foreach (var dependency in asset.ExternallyReferencedAssets)
                            {
                                if (dependency == null || string.IsNullOrWhiteSpace(dependency.GroupGuid))
                                {
                                    continue;
                                }

                                if (!groupByGuid.TryGetValue(dependency.GroupGuid, out var dependencyGroup))
                                {
                                    continue;
                                }

                                if (!IsRemote(dependencyGroup))
                                {
                                    continue;
                                }

                                rows.Add(string.Join(
                                    "\t",
                                    sourceGroup.Name,
                                    Path.GetFileName(asset.AssetPath),
                                    dependencyGroup.Name,
                                    Path.GetFileName(dependency.AssetPath)));
                            }
                        }
                    }
                }
            }

            return new List<string>(rows);
        }

        private static bool IsStaticContent(AddressableAssetGroup group)
        {
            var schema = group.GetSchema<ContentUpdateGroupSchema>();
            return schema != null && schema.StaticContent;
        }
        
        public static bool IsRemote(AddressableAssetGroup group)
        {
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
            {
                return false;
            }

            var buildPath = schema.BuildPath.GetName(group.Settings);
            return !string.Equals(
                buildPath,
                AddressableAssetSettings.kLocalBuildPath,
                StringComparison.Ordinal);
        }

        private static string GetRequiredArgValue(string argName)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], argName, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            throw new ArgumentException($"Missing required command line argument: {argName}");
        }
    }
}
