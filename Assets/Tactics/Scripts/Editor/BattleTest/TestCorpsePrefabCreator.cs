using Tactics.Common.Interactables;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.BattleTest
{
    public static class TestCorpsePrefabCreator
    {
        private const string DefaultOutputDir = "Assets/Tactics/Arts/Prefabs/Units";

        [MenuItem("Tactics/Battle Test/Create Test Corpse Prefab")]
        private static void CreateCorpsePrefab()
        {
            var savePath = EditorUtility.SaveFilePanelInProject(
                "Save Corpse Prefab",
                "TestCorpse.prefab",
                "prefab",
                "Choose output path for the corpse prefab",
                DefaultOutputDir);

            if (string.IsNullOrEmpty(savePath))
                return;

            var go = new GameObject("TestCorpse");

            var corpse = go.AddComponent<Corpse>();

            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.8f, 0.8f);

            var spriteChild = new GameObject("Sprite");
            spriteChild.transform.SetParent(go.transform);
            spriteChild.transform.localPosition = new Vector3(0, -0.15f, 0);
            spriteChild.transform.localRotation = Quaternion.Euler(0, 0, 90);

            var sr = spriteChild.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);

            PrefabUtility.SaveAsPrefabAsset(go, savePath, out bool success);
            Object.DestroyImmediate(go);

            if (success)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[TestCorpsePrefabCreator] Corpse prefab saved to: {savePath}");
            }
            else
            {
                Debug.LogError("[TestCorpsePrefabCreator] Failed to save corpse prefab.");
            }
        }
    }
}
