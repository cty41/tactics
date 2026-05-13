using UnityEditor;
using Tactics.Runtime.Utilities;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TbsFramework.EditorUtils
{
    public static class HighlightLayerTools
    {
        [MenuItem("Tactics/Tilemap/Clear HighlightLayer Tiles")]
        private static void ClearHighlightLayerTiles()
        {
            var tilemap = FindHighlightLayerTilemap();
            if (tilemap == null)
            {
                EditorUtility.DisplayDialog(
                    "HighlightLayer Not Found",
                    "Could not find a Tilemap GameObject named 'HighlightLayer' in loaded scenes.",
                    "OK"
                );
                return;
            }

            ClearTilemap(tilemap);
            TLog.Info($"Cleared all tiles on HighlightLayer: {GetHierarchyPath(tilemap.transform)}");
        }

        [MenuItem("Tactics/Tilemap/Clear Selected Tilemap Tiles")]
        private static void ClearSelectedTilemapTiles()
        {
            var tilemap = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<Tilemap>()
                : null;

            if (tilemap == null)
            {
                EditorUtility.DisplayDialog(
                    "No Tilemap Selected",
                    "Please select a GameObject with a Tilemap component.",
                    "OK"
                );
                return;
            }

            ClearTilemap(tilemap);
            TLog.Info($"Cleared all tiles on selected Tilemap: {GetHierarchyPath(tilemap.transform)}");
        }

        [MenuItem("Tactics/Tilemap/Clear Selected Tilemap Tiles", true)]
        private static bool ValidateClearSelectedTilemapTiles()
        {
            return Selection.activeGameObject != null &&
                   Selection.activeGameObject.GetComponent<Tilemap>() != null;
        }

        private static Tilemap FindHighlightLayerTilemap()
        {
            foreach (var tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                if (tilemap != null && tilemap.gameObject.name == "HighlightLayer")
                {
                    return tilemap;
                }
            }

            return null;
        }

        private static void ClearTilemap(Tilemap tilemap)
        {
            Undo.RecordObject(tilemap, "Clear Tilemap Tiles");
            tilemap.ClearAllTiles();
            EditorUtility.SetDirty(tilemap);
            EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
        }

        private static string GetHierarchyPath(Transform target)
        {
            if (target == null) return "<null>";

            var path = target.name;
            var current = target.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
