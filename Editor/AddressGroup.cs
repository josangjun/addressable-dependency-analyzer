using System.Collections.Generic;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace XSystem.Addressable.Analyzer
{
    public class AddressGroup
    {
        public readonly string address;
        public readonly AddressableAssetEntry entry;
        public AddressableAssetGroup group => entry.parentGroup;
        
        public AddressGroup(string address)
        {
            this.address = address;
            entry = GetAssetEntry(AddressableAssetSettingsDefaultObject.Settings, address);
        }
        
        private AddressableAssetEntry GetAssetEntry(AddressableAssetSettings settings, string address)
        {
            if (settings == null || string.IsNullOrEmpty(address))
            {
                return default;
            }

            foreach (var currentGroup in settings.groups)
            {
                if (currentGroup?.entries == null || currentGroup.entries.Count == 0)
                {
                    continue;
                }

                foreach (var currentEntry in currentGroup.entries)
                {
                    if (currentEntry.IsFolder)
                    {
                        if (currentEntry.SubAssets == null || currentEntry.SubAssets.Count == 0)
                        {
                            var subAssets = new List<AddressableAssetEntry>();
                            currentEntry.GatherAllAssets(subAssets, false, true, false);
                        }

                        foreach (var subEntry in currentEntry.SubAssets)
                        {
                            if (subEntry.address == address)
                            {
                                return subEntry;
                            }
                        }

                        continue;
                    }

                    if (currentEntry.address == address)
                    {
                        return currentEntry;
                    }
                }
            }

            return default;
        }
        
        public bool isSelected;
        
        public void MoveToGroup(AddressableAssetGroup newGroup)
        {
            newGroup.Settings.MoveEntry(entry, newGroup, false, true);
        }
        
        public override string ToString()
        {
            return $"{address} ({group?.name ?? "No Group"})";
        }
    }
}