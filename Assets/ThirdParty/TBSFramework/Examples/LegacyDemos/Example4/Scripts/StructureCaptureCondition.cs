using System.Linq;
using TurnBasedStrategyFramework.Common.Controllers.GameResolvers;
using TurnBasedStrategyFramework.Unity.Controllers;
using TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4
{
    public partial class StructureCaptureCondition : MonoBehaviour
    {
        [SerializeField] private Unit _structureToCapture;
        [SerializeField] private int _targetPlayerNumber;

        [SerializeField] private UnityGridController _controller;

        private void Awake()
        {
            _structureToCapture.GetComponent<ICapturable>().Captured += OnCaptured;
        }

        private void OnCaptured(CaptureEventArgs eventArgs)
        {
            if (eventArgs.CurrentOwnerPlayerNumber == _targetPlayerNumber)
            {
                GameResult gameResult = new GameResult(_controller.PlayerManager.GetPlayerByNumber(_targetPlayerNumber),
                    _controller.PlayerManager.GetPlayers().Where(p => p.PlayerNumber != _targetPlayerNumber));

                _controller.InvokeGameEnded(gameResult);
            }
        }
    }
}