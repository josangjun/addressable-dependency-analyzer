using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace XSystem.Addressable.Analyzer
{
    public class AddressablesDependencyWindow : EditorWindow, IHasCustomMenu
    {
        private Vector2 dependencyScrollPosition; // dependency 표시용 스크롤 위치
        private AddressablesBuildLayoutAnalyzer buildLayoutAnalyzer;
        private bool isSelectAddress = true;
        private readonly Dictionary<string, string[]> referenceHierarchyCache = new();

        private sealed class DependencyFixCandidate
        {
            public string guid;
            public string address;
            public AddressableAssetGroup targetGroup;
        }

        [MenuItem("Tools/Addressables/Dependency Analyzer")]
        public static void ShowWindow()
        {
            GetWindow<AddressablesDependencyWindow>("Addressables Dependency");
        }
        
        private void OnGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Scope:", GUILayout.Width(100));
            EditorGUILayout.LabelField("Local -> Remote", GUILayout.Width(200));
            if (GUILayout.Button("Load BuildReport"))
            {
                var path = EditorUtility.OpenFilePanel("Select Addressables Build Report", "Library/com.unity.addressables/BuildReports", "json");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    buildLayoutAnalyzer = new AddressablesBuildLayoutAnalyzer(path);
                    buildLayoutAnalyzer.PrintLocalToRemoteRefs();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(position.width));
            DisplayAssetDependencies();
            GUILayout.EndVertical();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Select Address"), isSelectAddress, () => isSelectAddress = !isSelectAddress);
        }

        private void DisplayAssetDependencies()
        {
            if (buildLayoutAnalyzer?.RemoteDepGroups == null)
            {
                EditorGUILayout.HelpBox("Please load a build report first.", MessageType.Warning);
                return;
            } else if (buildLayoutAnalyzer.RemoteDepGroups.Count == 0)
            {
                EditorGUILayout.HelpBox("No local to remote dependencies found in the loaded build report.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Local → Remote Dependency", EditorStyles.boldLabel);
            if (GUILayout.Button("Fix All: Move Remote Dependencies to Local Groups", GUILayout.Height(24)))
            {
                FixAllRemoteDependencies();
                return;
            }
            
            dependencyScrollPosition = EditorGUILayout.BeginScrollView(dependencyScrollPosition, GUILayout.ExpandHeight(true));
            foreach (var dep in buildLayoutAnalyzer.RemoteDepGroups)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);

                var localAddr = isSelectAddress ? dep.Key.AddressableName : dep.Key.AssetPath;
                if (GUILayout.Button($"[{dep.Key.Bundle.Group.Name}] {localAddr}", EditorStyles.miniLabel))
                {
                    PingAsset(dep.Key);
                }
                
                if (dep.Value != null && dep.Value.Count > 0)
                {
                    foreach (var remoteDep in dep.Value)
                    {
                        EditorGUILayout.BeginHorizontal(GUI.skin.textArea);
                        
                        EditorGUILayout.LabelField("→", GUILayout.Width(15));
                        var addr = isSelectAddress ? remoteDep.AddressableName : remoteDep.AssetPath;
                        if (GUILayout.Button($"[{remoteDep.Bundle.Group.Name}] {addr}", EditorStyles.linkLabel, GUILayout.ExpandWidth(true), GUILayout.Height(18)))
                        {
                            PingAsset(remoteDep);
                        }
                        EditorGUILayout.EndHorizontal();

                        foreach (var hierarchy in FindReferenceHierarchies(dep.Key, remoteDep))
                        {
                            EditorGUILayout.LabelField($"  ↳ {hierarchy}", EditorStyles.miniLabel);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No remote dependencies found");
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
            
            EditorGUILayout.EndScrollView();

            void PingAsset(BuildLayout.ExplicitAsset asset)
            {
                var assetPath = asset.AssetPath;
                if (string.IsNullOrWhiteSpace(assetPath) && !string.IsNullOrWhiteSpace(asset.Guid))
                {
                    assetPath = AssetDatabase.GUIDToAssetPath(asset.Guid);
                }

                var o = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (o != null)
                {
                    Selection.activeObject = o;
                    EditorUtility.FocusProjectWindow();
                    EditorGUIUtility.PingObject(o);
                }
                else
                {
                    Debug.LogWarning($"Asset '{assetPath}' not found");
                }
            }
        }

        private void FixAllRemoteDependencies()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || buildLayoutAnalyzer?.RemoteDepGroups == null)
            {
                return;
            }

            var candidates = new Dictionary<string, DependencyFixCandidate>();
            var skippedCount = 0;
            foreach (var dependencyGroup in buildLayoutAnalyzer.RemoteDepGroups)
            {
                var sourceEntry = settings.FindAssetEntry(dependencyGroup.Key.Guid, true);
                var targetGroup = sourceEntry?.parentGroup;
                if (targetGroup == null)
                {
                    skippedCount += dependencyGroup.Value.Count;
                    continue;
                }

                foreach (var dependency in dependencyGroup.Value)
                {
                    if (string.IsNullOrWhiteSpace(dependency.Guid))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (!candidates.ContainsKey(dependency.Guid))
                    {
                        candidates.Add(dependency.Guid, new DependencyFixCandidate
                        {
                            guid = dependency.Guid,
                            address = dependency.AddressableName,
                            targetGroup = targetGroup
                        });
                    }
                }
            }

            if (candidates.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Addressables Dependency Analyzer",
                    "No dependencies can be moved automatically.",
                    "OK");
                return;
            }

            var confirmed = EditorUtility.DisplayDialog(
                "Move Remote Dependencies to Local",
                $"Move {candidates.Count} remote asset(s) to the Local group of the referencing Prefab?\n\n" +
                "The asset GUID and Address will be preserved. Folder entries will be overridden by explicit Local entries.\n\n" +
                (skippedCount > 0 ? $"Skipped references: {skippedCount}" : string.Empty),
                "Move",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            var movedEntries = new List<AddressableAssetEntry>();
            foreach (var candidate in candidates.Values)
            {
                var entry = settings.CreateOrMoveEntry(candidate.guid, candidate.targetGroup, false, false);
                if (entry == null)
                {
                    skippedCount++;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(candidate.address))
                {
                    entry.SetAddress(candidate.address, false);
                }

                movedEntries.Add(entry);
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, movedEntries, true, true);
            AssetDatabase.SaveAssets();
            referenceHierarchyCache.Clear();
            buildLayoutAnalyzer = null;

            EditorUtility.DisplayDialog(
                "Addressables Dependency Analyzer",
                $"Moved {movedEntries.Count} asset(s) to Local groups." +
                (skippedCount > 0 ? $"\nSkipped: {skippedCount}" : string.Empty) +
                "\n\nReload the build report to verify the result.",
                "OK");
        }

        private string[] FindReferenceHierarchies(
            BuildLayout.ExplicitAsset sourceAsset,
            BuildLayout.ExplicitAsset dependencyAsset)
        {
            var sourcePath = ResolveAssetPath(sourceAsset);
            var dependencyPath = ResolveAssetPath(dependencyAsset);
            if (!sourcePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                (string.IsNullOrWhiteSpace(dependencyAsset.Guid) && string.IsNullOrWhiteSpace(dependencyPath)))
            {
                return Array.Empty<string>();
            }

            var cacheKey = $"{sourcePath}|{dependencyAsset.Guid}|{dependencyPath}";
            if (referenceHierarchyCache.TryGetValue(cacheKey, out var cachedHierarchies))
            {
                return cachedHierarchies;
            }

            var hierarchies = new List<string>();
            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(sourcePath);
                if (prefabRoot == null)
                {
                    return CacheReferenceHierarchies(cacheKey, hierarchies);
                }

                foreach (var component in prefabRoot.GetComponentsInChildren<Component>(true))
                {
                    if (component == null)
                    {
                        continue;
                    }

                    var serializedObject = new SerializedObject(component);
                    var property = serializedObject.GetIterator();
                    var enterChildren = true;
                    while (property.Next(enterChildren))
                    {
                        enterChildren = false;
                        if (property.propertyType != SerializedPropertyType.ObjectReference ||
                            property.objectReferenceValue == null ||
                            !IsReferencedAsset(property.objectReferenceValue, dependencyAsset, dependencyPath))
                        {
                            continue;
                        }

                        hierarchies.Add(
                            $"{GetHierarchyPath(prefabRoot.transform, component.transform)} " +
                            $"({component.GetType().Name}.{property.propertyPath})");
                    }
                }
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            return CacheReferenceHierarchies(cacheKey, hierarchies);
        }

        private string[] CacheReferenceHierarchies(string cacheKey, List<string> hierarchies)
        {
            var result = hierarchies.ToArray();
            referenceHierarchyCache[cacheKey] = result;
            return result;
        }

        private static bool IsReferencedAsset(
            UnityEngine.Object referencedObject,
            BuildLayout.ExplicitAsset dependencyAsset,
            string dependencyPath)
        {
            var referencedPath = AssetDatabase.GetAssetPath(referencedObject);
            if (!string.IsNullOrWhiteSpace(dependencyPath) &&
                string.Equals(referencedPath, dependencyPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(dependencyAsset.Guid) &&
                   string.Equals(AssetDatabase.AssetPathToGUID(referencedPath), dependencyAsset.Guid, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetHierarchyPath(Transform root, Transform target)
        {
            var names = new List<string>();
            var current = target;
            while (current != null)
            {
                names.Add(current.name);
                if (current == root)
                {
                    break;
                }

                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static string ResolveAssetPath(BuildLayout.ExplicitAsset asset)
        {
            if (!string.IsNullOrWhiteSpace(asset.AssetPath))
            {
                return asset.AssetPath;
            }

            return string.IsNullOrWhiteSpace(asset.Guid)
                ? string.Empty
                : AssetDatabase.GUIDToAssetPath(asset.Guid);
        }
    }
}
