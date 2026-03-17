using TMPro;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.GUI
{
    public class PlayerPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text _playerNumberText;
        [SerializeField] private TMP_Text _unitsText;
        [SerializeField] private TMP_Text _unitsLostText;
        [SerializeField] private TMP_Text _basesText;
        [SerializeField] private TMP_Text _incomeText;
        [SerializeField] private TMP_Text _fundsText;

        public TMP_Text PlayerNumberText
        {
            get
            {
                return _playerNumberText;
            }
        }
        public TMP_Text UnitsText
        {
            get
            {
                return _unitsText;
            }
        }
        public TMP_Text UnitsLostText
        {
            get
            {
                return _unitsLostText;
            }
        }
        public TMP_Text BasesText
        {
            get
            {
                return _basesText;
            }
        }
        public TMP_Text IncomeText
        {
            get
            {
                return _incomeText;
            }
        }
        public TMP_Text FundsText
        {
            get
            {
                return _fundsText;
            }
        }
    }
}

