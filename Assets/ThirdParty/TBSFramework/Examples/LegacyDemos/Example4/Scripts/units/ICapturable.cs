using System;
using TurnBasedStrategyFramework.Common.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units
{
    /// <summary>
    /// Defines the interface for units or structures that can be captured by other players.
    /// </summary>
    public interface ICapturable
    {
        /// <summary>
        /// Event invoked when sctructure is captured.
        /// </summary>
        event Action<CaptureEventArgs> Captured;

        /// <summary>
        /// Gets the maximum loyalty of the capturable object.
        /// </summary>
        public int MaxLoyality { get; }

        /// <summary>
        /// Gets or sets the current loyalty of the capturable object.
        /// </summary>
        public int CurrentLoyality { get; set; }

        /// <summary>
        /// Gets the unit reference associated with the capturable object.
        /// </summary>
        IUnit UnitReference { get; }

        /// <summary>
        /// Captures the object by decreasing its loyalty and assigning it to the specified player.
        /// </summary>
        /// <param name="amount">The amount by which to decrease the loyalty.</param>
        /// <param name="playerNumber">The number of the player capturing the object.</param>
        /// <param name="color">The color representing the capturing player.</param>
        void Capture(IUnit capturingUnit, int amount, int playerNumber, Color color);

        /// <summary>
        /// Resets loyalty to the default value effectively canceling the capture process.
        /// </summary>
        void ResetLoyalty();
    }

    public readonly struct CaptureEventArgs
    {
        public readonly int FormerOwnerPlayerNumber;
        public readonly int CurrentOwnerPlayerNumber;

        public CaptureEventArgs(int formerOwnerPlayerNumber, int currentOwnerPlayerNumber)
        {
            FormerOwnerPlayerNumber = formerOwnerPlayerNumber;
            CurrentOwnerPlayerNumber = currentOwnerPlayerNumber;
        }
    }
}