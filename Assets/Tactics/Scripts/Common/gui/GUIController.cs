using System;
using Tactics.Common.Controllers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tactics.Common.Gui
{
    /// <summary>
    /// Basic GUI Controller for managing turn transitions.
    /// </summary>
    [Obsolete("M-key shortcut and EndTurn logic have been consolidated into BattleUIController. " +
              "This component will be removed in a future version.")]
    public class GUIController : MonoBehaviour
    {
        [SerializeField] UnityGridController _gridController;

        private void Update()
        {
            if (Keyboard.current.mKey.wasPressedThisFrame)
            {
                EndTurn();
            }
        }

        public void EndTurn()
        {
            _gridController.EndTurn();
        }

        public void SetGridController(UnityGridController gridController) 
        {
            _gridController = gridController;
        }
    }
}
