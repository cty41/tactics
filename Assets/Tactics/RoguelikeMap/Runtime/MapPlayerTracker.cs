using System;
using System.Collections;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Map
{
    public class MapPlayerTracker : MonoBehaviour
    {
        /// <summary>PlayerPrefs key written before loading the battle scene; read by Tactics.Roguelike.RoguelikeBattleReturn.</summary>
        public const string RoguelikeReturnScenePrefsKey = "RoguelikeReturnScene";

        public bool lockAfterSelecting = false;
        public float enterNodeDelay = 1f;
        public MapManager mapManager;
        public MapView view;

        [Header("Roguelike — battle scene flow")]
        [Tooltip("Must match a scene name in Build Settings.")]
        [SerializeField] private string battleSceneName = "Test1";
        [Tooltip("Scene that hosts MapObjects / map UI (return target after battle).")]
        [SerializeField] private string mapSceneName = "SampleScene";

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
            Locked = lockAfterSelecting;
            mapManager.CurrentMap.path.Add(mapNode.Node.point);
            mapManager.SaveMap();
            view.SetAttainableNodes();
            view.SetLineColors();
            mapNode.ShowSwirlAnimation();

            DOTween.Sequence().AppendInterval(enterNodeDelay).OnComplete(() => EnterNode(mapNode));
        }

        private void EnterNode(MapNode mapNode)
        {
            Debug.Log("Entering node: " + mapNode.Node.blueprintName + " of type: " + mapNode.Node.nodeType);

            switch (mapNode.Node.nodeType)
            {
                case NodeType.MinorEnemy:
                case NodeType.EliteEnemy:
                case NodeType.Boss:
                    EnterBattleNode();
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

        private void EnterBattleNode()
        {
            PlayerPrefs.SetString(RoguelikeReturnScenePrefsKey, mapSceneName);
            PlayerPrefs.Save();
            SceneManager.LoadScene(battleSceneName);
        }

        private void EnterStubNode(MapNode mapNode)
        {
            Debug.Log(
                $"[Roguelike stub] Node '{mapNode.Node.blueprintName}' ({mapNode.Node.nodeType}) — replace with event / chest / shop / rest UI. " +
                $"Optional blueprint ids: eventId='{mapNode.Blueprint?.eventId}', shopId='{mapNode.Blueprint?.shopId}', treasureId='{mapNode.Blueprint?.treasureId}'");
            StartCoroutine(CoUnlockAfterStub());
        }

        private IEnumerator CoUnlockAfterStub()
        {
            yield return null;
            Locked = false;
        }

        private void PlayWarningThatNodeCannotBeAccessed()
        {
            Debug.Log("Selected node cannot be accessed");
        }
    }
}
