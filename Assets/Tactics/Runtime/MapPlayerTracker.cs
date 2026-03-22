using System;
using System.Collections;
using System.Linq;
using DG.Tweening;
using Tactics.AssetPipeline;
using UnityEngine;

namespace Map
{
    public class MapPlayerTracker : MonoBehaviour
    {
        /// <summary>PlayerPrefs key written before loading the battle scene; read by Tactics.Roguelike.RoguelikeBattleReturn.</summary>
        public const string RoguelikeReturnScenePrefsKey = "RoguelikeReturnScene";

        /// <summary>PlayerPrefs key: "x,y" of the node entered when loading battle; committed to map path on victory.</summary>
        public const string RoguelikePendingNodePrefsKey = "RoguelikePendingNode";

        public float enterNodeDelay = 1f;
        public MapManager mapManager;
        public MapView view;

        [Header("Roguelike — battle scene flow")]
        [Tooltip("Short name under Assets/Tactics/Scenes/ or full Assets/.../Scene.unity. Must be in asset pipeline manifest.")]
        [SerializeField]
        private string battleSceneName = "Test1";
        [Tooltip("Return target after battle: short name or full path; stored in PlayerPrefs for RoguelikeBattleReturn.")]
        [SerializeField]
        private string mapSceneName = "SampleScene";

        public static MapPlayerTracker Instance;

        public bool Locked { get; set; }

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>Clears selection lock when the map scene loads again (e.g. after returning from battle).</summary>
        private void OnEnable()
        {
            Locked = false;
        }

        public void SelectNode(MapNode mapNode)
        {
            if (Locked) return;

            if (mapManager.CurrentMap.path.Count == 0)
            {
                if (mapNode.Node.point.y == 0)
                    SendPlayerToNode(mapNode);
                else
                    PlayWarningThatNodeCannotBeAccessed();
            }
            else
            {
                Vector2Int currentPoint = mapManager.CurrentMap.path[mapManager.CurrentMap.path.Count - 1];
                Node currentNode = mapManager.CurrentMap.GetNode(currentPoint);

                if (currentNode != null && currentNode.outgoing.Any(point => point.Equals(mapNode.Node.point)))
                    SendPlayerToNode(mapNode);
                else
                    PlayWarningThatNodeCannotBeAccessed();
            }
        }

        private void SendPlayerToNode(MapNode mapNode)
        {
            Locked = true;
            mapNode.ShowSwirlAnimation();

            DOTween.Sequence().AppendInterval(enterNodeDelay).OnComplete(() => EnterNode(mapNode));
        }

        private void CommitPathForNode(MapNode mapNode)
        {
            mapManager.CurrentMap.path.Add(mapNode.Node.point);
            mapManager.SaveMap();
            view.SetAttainableNodes();
            view.SetLineColors();
        }

        private void EnterNode(MapNode mapNode)
        {
            Debug.Log("Entering node: " + mapNode.Node.blueprintName + " of type: " + mapNode.Node.nodeType);

            switch (mapNode.Node.nodeType)
            {
                case NodeType.MinorEnemy:
                case NodeType.EliteEnemy:
                case NodeType.Boss:
                    EnterBattleNode(mapNode);
                    break;
                case NodeType.RestSite:
                case NodeType.Treasure:
                case NodeType.Store:
                case NodeType.Mystery:
                    EnterStubNode(mapNode);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void EnterBattleNode(MapNode mapNode)
        {
            Vector2Int p = mapNode.Node.point;
            PlayerPrefs.SetString(RoguelikePendingNodePrefsKey, $"{p.x},{p.y}");
            PlayerPrefs.SetString(RoguelikeReturnScenePrefsKey, mapSceneName);
            PlayerPrefs.Save();
            SceneProjectPathHelper.TryLoadSceneViaAssetManager(battleSceneName);
        }

        private void EnterStubNode(MapNode mapNode)
        {
            Debug.Log(
                $"[Roguelike stub] Node '{mapNode.Node.blueprintName}' ({mapNode.Node.nodeType}) — replace with event / chest / shop / rest UI. " +
                $"Optional blueprint ids: eventId='{mapNode.Blueprint?.eventId}', shopId='{mapNode.Blueprint?.shopId}', treasureId='{mapNode.Blueprint?.treasureId}'");
            StartCoroutine(CoUnlockAfterStub(mapNode));
        }

        private IEnumerator CoUnlockAfterStub(MapNode mapNode)
        {
            yield return null;
            CommitPathForNode(mapNode);
            Locked = false;
        }

        private void PlayWarningThatNodeCannotBeAccessed()
        {
            Debug.Log("Selected node cannot be accessed");
        }
    }
}
