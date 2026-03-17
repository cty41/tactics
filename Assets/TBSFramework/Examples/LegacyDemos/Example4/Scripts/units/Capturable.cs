using System;
using TMPro;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units
{
    public class Capturable : MonoBehaviour, ICapturable
    {
        [SerializeField] private int _maxLoyality;
        private int _currentLoyalty;
        [SerializeField] private Unit _unitReference;

        [SerializeField] private SpriteRenderer _mask;
        [SerializeField] private TMP_Text _loyaltyLabel;

        public int MaxLoyality => _maxLoyality;
        public int CurrentLoyality { get { return _currentLoyalty; } set { _currentLoyalty = value; UpdateLoyaltyUI(); } }
        public IUnit UnitReference { get { return _unitReference; } }

        public event Action<CaptureEventArgs> Captured;

        private IUnit _capturingUnit;

        private void Start()
        {
            _currentLoyalty = MaxLoyality;
            _loyaltyLabel.text = "";
        }

        public void Capture(IUnit capturingUnit, int amount, int playerNumber, Color color)
        {
            if (_capturingUnit != null)
            {
                capturingUnit.UnitDestroyed -= OnUnitDestroyed;
            }
            capturingUnit.UnitDestroyed += OnUnitDestroyed;

            CurrentLoyality += amount;
            if (CurrentLoyality <= 0)
            {
                CurrentLoyality = MaxLoyality;

                UnitReference.PlayerNumber = playerNumber;
                (UnitReference as IColoredUnit).Color = color;
                _mask.color = color;

                var captureEventArgs = new CaptureEventArgs(formerOwnerPlayerNumber: UnitReference.PlayerNumber, currentOwnerPlayerNumber: playerNumber);
                Captured?.Invoke(captureEventArgs);
            }
            UpdateLoyaltyUI();
        }

        public void ResetLoyalty()
        {
            CurrentLoyality = MaxLoyality;
            UpdateLoyaltyUI();
        }

        private void OnUnitDestroyed(UnitDestroyedEventArgs obj)
        {
            _capturingUnit = null;
            CurrentLoyality = MaxLoyality;
            UpdateLoyaltyUI();
        }

        private void UpdateLoyaltyUI()
        {
            _loyaltyLabel.text = CurrentLoyality == MaxLoyality ? "" : $"{CurrentLoyality}";
        }
    }
}