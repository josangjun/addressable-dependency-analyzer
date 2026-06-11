using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.GUI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XSystem.Addressable.Analyzer
{
    public class AddressablesDependencyWindow : EditorWindow, IHasCustomMenu
    {
        private Vector2 dependencyScrollPosition; // dependency 표시용 스크롤 위치
        private AddressablesBuildLayoutAnalyzer buildLayoutAnalyzer;
        private bool isSelectAddress = true;

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
                var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(asset.Guid, true);
                if (entry != null && isSelectAddress)
                {
                    // Addressable Groups window에서 entry 선택
                    var addressableGroupsWindowType = typeof(AnalyzeWindow).Assembly.GetType("UnityEditor.AddressableAssets.GUI.AddressableAssetsWindow");
                    if (addressableGroupsWindowType != null)
                    {
                        var window = EditorWindow.GetWindow(addressableGroupsWindowType, false, "Addressable Groups", false);
                        if (window != null)
                        {
                            window.Show();
                            window.Focus();

                            // TreeView에서 entry 선택을 위한 리플렉션
                            var groupEditorField = addressableGroupsWindowType.GetField("m_GroupEditor", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (groupEditorField != null)
                            {
                                var groupEditor = groupEditorField.GetValue(window);
                                if (groupEditor != null)
                                {
                                    // m_EntriesTreeView 필드를 통해 TreeView 인스턴스 접근
                                    var treeViewField = groupEditor.GetType().GetField("m_EntriesTreeView", BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (treeViewField != null)
                                    {
                                        var treeView = treeViewField.GetValue(groupEditor);
                                        if (treeView != null)
                                        {
                                            var setSelectionMethod = treeView.GetType().GetMethod("SetSelection", new[] { typeof(List<int>), typeof(bool) });
                                            var findItemMethod = treeView.GetType().GetMethod("FindItem", new[] { typeof(string) });
                                            var expandItemMethod = treeView.GetType().GetMethod("ExpandItem", new[] { typeof(int), typeof(bool) });
                                            if (setSelectionMethod != null && findItemMethod != null && expandItemMethod != null)
                                            {
                                                // entry의 GUID로 TreeViewItem을 찾음
                                                var treeViewItem = findItemMethod.Invoke(treeView, new object[] { entry.guid });
                                                if (treeViewItem != null)
                                                {
                                                    var idProperty = treeViewItem.GetType().GetProperty("id");
                                                    var parentProperty = treeViewItem.GetType().GetProperty("parent");
                                                    if (idProperty != null && parentProperty != null)
                                                    {
                                                        int id = (int)idProperty.GetValue(treeViewItem);
                                                        // 부모 chain을 따라가며 모두 expand
                                                        var parent = parentProperty.GetValue(treeViewItem);
                                                        var expandedIds = new HashSet<int>();
                                                        while (parent != null)
                                                        {
                                                            var parentIdProp = parent.GetType().GetProperty("id");
                                                            if (parentIdProp != null)
                                                            {
                                                                int parentId = (int)parentIdProp.GetValue(parent);
                                                                if (!expandedIds.Contains(parentId))
                                                                {
                                                                    expandItemMethod.Invoke(treeView, new object[] { parentId, false });
                                                                    expandedIds.Add(parentId);
                                                                }
                                                            }
                                                            parent = parentProperty.GetValue(parent);
                                                        }
                                                        // entry 선택
                                                        setSelectionMethod.Invoke(treeView, new object[] { new List<int> { id }, true });
                                                        return;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    // fallback: SelectEntries 메서드 사용
                                    var selectMethod = groupEditor.GetType().GetMethod("SelectEntries", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (selectMethod != null)
                                    {
                                        selectMethod.Invoke(groupEditor, new[] { new[] { entry } });
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
                // Entry가 없는 경우 일반 Project 창에서 ping
                var o = AssetDatabase.LoadAssetAtPath<Object>(asset.AssetPath);
                if (o != null)
                {
                    EditorGUIUtility.PingObject(o);
                    Selection.activeObject = o;
                }
                else
                {
                    Debug.LogWarning($"Asset '{asset.AssetPath}' not found");
                }
            }
        }
    }
}
