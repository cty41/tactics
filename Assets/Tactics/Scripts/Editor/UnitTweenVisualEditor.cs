using Tactics.Common.Units.Tween;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor
{
    /// <summary>
    /// Shows read-only runtime diagnostics for the lightweight unit presentation lifecycle.
    /// </summary>
    [CustomEditor(typeof(UnitTweenVisual))]
    public sealed class UnitTweenVisualEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Runtime Presentation State", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Runtime presentation state is available in Play Mode.",
                    MessageType.Info);
                return;
            }

            if (targets.Length != 1 || target is not UnitTweenVisual visual)
            {
                EditorGUILayout.HelpBox(
                    "Select one UnitTweenVisual to inspect its runtime state.",
                    MessageType.Info);
                return;
            }

            UnitTweenVisualDebugSnapshot snapshot = visual.GetDebugSnapshot();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Lifecycle", snapshot.Lifecycle.ToString());
                EditorGUILayout.TextField("Foreground Priority", snapshot.ForegroundPriority);
                EditorGUILayout.Toggle("Idle Tween Active", snapshot.IsIdleTweenActive);
                EditorGUILayout.Toggle("Move Tween Active", snapshot.IsMoveTweenActive);
                EditorGUILayout.Toggle("Foreground Tween Active", snapshot.IsForegroundTweenActive);
                EditorGUILayout.IntField("Foreground Version", snapshot.ForegroundVersion);
                EditorGUILayout.Toggle("Death Handoff Complete", snapshot.IsDeathHandoffComplete);
            }
        }

        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }
    }
}
