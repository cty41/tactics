using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Tactics.AssetPipeline;
using UnityEngine;

namespace Tactics.Equipment
{
    public static class EquipmentDatabase
    {
        private const string EquipmentJsonPath = "Assets/Tactics/GameData/Equipment.json";
        private static readonly Dictionary<string, EquipmentDefinition> _definitions = new Dictionary<string, EquipmentDefinition>();
        private static bool _isLoaded;

        public static void Load()
        {
            if (_isLoaded)
                return;

            string json = null;
            var mgr = GameAssetManager.Instance;

            if (mgr != null && mgr.IsInitialized)
            {
                var textAsset = mgr.Load<TextAsset>(EquipmentJsonPath);
                if (textAsset != null)
                {
                    json = textAsset.text;
                    mgr.Release(EquipmentJsonPath);
                }
            }
#if UNITY_EDITOR
            else if (File.Exists(EquipmentJsonPath))
            {
                json = File.ReadAllText(EquipmentJsonPath);
            }
#endif

            if (json == null)
            {
                Debug.LogError($"[EquipmentDatabase] Equipment.json not found at {EquipmentJsonPath}");
                _isLoaded = true;
                return;
            }

            try
            {
                var list = JsonConvert.DeserializeObject<List<EquipmentDefinition>>(json);
                if (list == null)
                {
                    Debug.LogError("[EquipmentDatabase] Failed to deserialize Equipment.json");
                    _isLoaded = true;
                    return;
                }

                foreach (var def in list)
                {
                    if (string.IsNullOrEmpty(def.Id))
                    {
                        Debug.LogWarning("[EquipmentDatabase] Skipping equipment with empty Id");
                        continue;
                    }

                    if (_definitions.ContainsKey(def.Id))
                        Debug.LogWarning($"[EquipmentDatabase] Duplicate equipment Id: {def.Id}");
                    else
                        _definitions[def.Id] = def;
                }

                _isLoaded = true;
                Debug.Log($"[EquipmentDatabase] Loaded {_definitions.Count} equipment definitions.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EquipmentDatabase] Failed to parse Equipment.json: {ex.Message}");
                _isLoaded = true;
            }
        }

        public static EquipmentDefinition GetById(string id)
        {
            if (!_isLoaded)
                Load();

            if (string.IsNullOrEmpty(id))
                return null;

            _definitions.TryGetValue(id, out var def);
            return def;
        }

        public static bool Contains(string id)
        {
            if (!_isLoaded)
                Load();

            return !string.IsNullOrEmpty(id) && _definitions.ContainsKey(id);
        }
    }
}
