using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using UnityEngine;

namespace Tactics.Common.Units
{
    /// <summary>
    /// Represents a unit within the game.
    /// </summary>
    public interface IUnit : IMoveable, ICombatant
    {
        /// <summary>
        /// Triggered when the unit's mana changes.
        /// </summary>
        event Action<ManaChangedEventArgs> ManaChanged;

        /// <summary>
        /// Triggered when the unit is selected.
        /// </summary>
        event Action<IUnit> UnitSelected;

        /// <summary>
        /// Triggered when the unit is deselected.
        /// </summary>
        event Action<IUnit> UnitDeselected;

        /// <summary>
        /// Triggered when the unit is clicked.
        /// </summary>
        event Action<IUnit> UnitClicked;

        /// <summary>
        /// Triggered when the unit is highlighted, typically when the mouse cursor moves over it.
        /// </summary>
        event Action<IUnit> UnitHighlighted;

        /// <summary>
        /// Triggered when the unit is dehighlighted, typically when the mouse cursor leaves it.
        /// </summary>
        event Action<IUnit> UnitDehighlighted;

        /// <summary>
        /// Triggered when an ability is used by the unit.
        /// </summary>
        event Action<AbilityUsedEventArgs> AbilityUsed;

        /// <summary>
        /// Triggered when a basic ability is marked as used this turn.
        /// </summary>
        event Action<string> BasicAbilityUsed;

        /// <summary>Raised whenever the unit's runtime four-direction facing changes.</summary>
        event Action<FacingChangedEventArgs> FacingChanged;

        /// <summary>
        /// Invokes the ManaChanged event to signal that the unit's mana has changed.
        /// </summary>
        void InvokeManaChanged(ManaChangedEventArgs eventArgs);

        /// <summary>
        /// Invokes the UnitSelected event to signal that the unit has been selected.
        /// </summary>
        void InvokeUnitSelected();

        /// <summary>
        /// Invokes the UnitDeselected event to signal that the unit has been deselected.
        /// </summary>
        void InvokeUnitDeselected();

        /// <summary>
        /// Invokes the UnitClicked event to signal that the unit has been clicked.
        /// </summary>
        void InvokeUnitClicked();

        /// <summary>
        /// Invokes the UnitHighlighted event to signal that the unit has been highlighted.
        /// </summary>
        void InvokeUnitHighlighted();

        /// <summary>
        /// Invokes the UnitDehighlighted event to signal that the unit is no longer highlighted.
        /// </summary>
        void InvokeUnitDehighlighted();

        /// <summary>
        /// Invokes the AbilityUsed event to signal that the unit has used an ability.
        /// </summary>
        /// <param name="args">The event arguments containing ability usage data.</param>
        void InvokeAbilityUsed(AbilityUsedEventArgs args);

        /// <summary>
        /// Invokes the BasicAbilityUsed event to signal that a basic ability has been used this turn.
        /// </summary>
        /// <param name="abilityName">The name of the basic ability that was used.</param>
        void InvokeBasicAbilityUsed(string abilityName);

        /// <summary>
        /// The cell that the unit currently occupies.
        /// </summary>
        ICell CurrentCell { get; set; }

        /// <summary>
        /// The world position of the unit.
        /// </summary>
        Vector3Impl WorldPosition { get; set; }

        int Strength { get; set; }
        int Agility { get; set; }
        int Constitution { get; set; }
        int Intelligence { get; set; }
        float Speed { get; set; }
        int Charisma { get; set; }
        int Luck { get; set; }
        float DodgeRate { get; set; }
        float Mana { get; set; }
        float MaxMana { get; set; }

        /// <summary>
        /// 先攻值，由 Speed × 2 计算得出，决定行动顺序。
        /// </summary>
        float Initiative { get; set; }

        /// <summary>Runtime-only four-direction facing for the current battle.</summary>
        FacingDirection Facing { get; set; }

        /// <summary>Stable visual state key consumed by animation and gameplay tests.</summary>
        string FacingVisualKey { get; }

        /// <summary>
        /// 近战攻击范围，基础值为 1。
        /// </summary>
        int Reach { get; set; }

        /// <summary>
        /// 远程攻击范围。
        /// </summary>
        int Range { get; set; }

        /// <summary>
        /// 昏迷状态，当 HP 低于 0 时触发。
        /// </summary>
        bool IsDowned { get; set; }

        /// <summary>
        /// 尸体标记，敌人死亡后原地留下的尸体。
        /// </summary>
        bool IsCorpse { get; set; }

        /// <summary>
        /// Whether positive health changes can restore this unit's health.
        /// </summary>
        bool CanReceiveHealing { get; set; }

        /// <summary>
        /// 召唤物归属 ID，-1 表示无归属。
        /// </summary>
        int OwnerUnitId { get; set; }

        /// <summary>
        /// 当前召唤的单位引用（死灵法师持有），null 表示无召唤物。
        /// </summary>
        IUnit SummonedUnit { get; set; }

        /// <summary>
        /// 召唤者的直接引用（骷髅持有），null 表示无归属。
        /// </summary>
        IUnit OwnerUnit { get; set; }

        /// <summary>
        /// Gets the portrait sprite of the unit.
        /// </summary>
        Sprite Portrait { get; }

        /// <summary>
        /// The number of the player that owns the unit.
        /// </summary>
        int PlayerNumber { get; set; }

        int UnitID { get; set; }

        /// <summary>
        /// Initializes the unit when it is added to the game.
        /// </summary>
        /// <param name="gridController">The grid controller.</param>
        void Initialize(IGridController gridController);

        /// <summary>
        /// Retrieves the base abilities available to the unit. Base abilities are those that get activated automatically when the unit is selected.
        /// </summary>
        /// <returns>A collection of base abilities.</returns>
        IEnumerable<IAbility> GetBaseAbilities();

        /// <summary>
        /// Checks if a basic ability has been used this turn.
        /// </summary>
        bool HasUsedBasicAbilityThisTurn(string abilityName);

        /// <summary>
        /// Marks a basic ability as used for this turn.
        /// </summary>
        void MarkBasicAbilityUsed(string abilityName);

        /// <summary>
        /// Registers a new ability for the unit.
        /// </summary>
        /// <param name="ability">The ability to register.</param>
        /// <param name="gridController">The grid controller.</param>
        void RegisterAbility(IAbility ability, IGridController gridController);

        /// <summary>
        /// Applies a buff to this unit.
        /// </summary>
        /// <param name="buff">The buff to apply.</param>
        void AddBuff(Buff buff);

        /// <summary>
        /// Removes a specific buff from this unit.
        /// </summary>
        /// <param name="buff">The buff to remove.</param>
        void RemoveBuff(Buff buff);

        /// <summary>
        /// Gets all active buffs on this unit.
        /// </summary>
        IReadOnlyList<Buff> GetActiveBuffs();

        /// <summary>
        /// Executes a given ability.
        /// </summary>
        /// <param name="command">The command representing the ability to execute.</param>
        /// <param name="preAction">An action to perform before executing the ability.</param>
        /// <param name="postAction">An action to perform after executing the ability.</param>
        /// <param name="isNetworkInvoked">Indicates whether the action was triggered by a remote player. 
        Task ExecuteAbility(ICommand command, Func<IGridController, Task> preAction, Func<IGridController, Task> postAction, bool isNetworkInvoked = false);

        /// <summary>
        /// Helper method to execute an ability as a human-controlled unit with default pre-action and post-action steps.
        /// </summary>
        /// <param name="command">The command representing the ability to execute.</param>
        /// <param name="gridController">The grid controller.</param>
        /// <param name="isNetworkInvoked">Indicates whether the action was triggered by a remote player. 
        Task HumanExecuteAbility(ICommand command, IGridController gridController, bool isNetworkInvoked = false);
        Task HumanExecuteAbility(ICommand command, IGridController gridController, Func<IGridController, Task> preAction, Func<IGridController, Task> postAction, bool isNetworkInvoked = false);

        /// <summary>
        /// Helper method to execute an ability as an AI-controlled unit with default pre-action and post-action steps.
        /// </summary>
        /// <param name="command">The command representing the ability to execute.</param>
        /// <param name="gridController">The grid controller.</param>
        /// <param name="tcs">A task completion source to signal when execution is complete.</param>
        /// <param name="isNetworkInvoked">Indicates whether the action was triggered by a remote player. 
        Task AIExecuteAbility(ICommand command, IGridController gridController, TaskCompletionSource<bool> tcs, bool isNetworkInvoked = false);
        Task AIExecuteAbility(ICommand command, IGridController gridController, TaskCompletionSource<bool> tcs, Func<IGridController, Task> preAction, Func<IGridController, Task> postAction, bool isNetworkInvoked = false);

        /// <summary>
        /// Called at the start of the unit's turn.
        /// </summary>
        /// <param name="gridController">The grid controller for managing unit interactions.</param>
        void OnTurnStart(IGridController gridController);

        /// <summary>
        /// Prepares unit resources for its new turn without triggering buff start logic.
        /// </summary>
        void PrepareForTurn() {}

        /// <summary>Recomputes movement and initiative after a runtime status change.</summary>
        void RefreshDerivedStats() {}


        /// <summary>
        /// Called at the end of the unit's turn.
        /// </summary>
        /// <param name="gridController">The grid controller for managing unit interactions.</param>
        void OnTurnEnd(IGridController gridController);

        void Cleanup(IGridController gridController);

        /// <summary>
        /// Called when the unit is destroyed, typically when unit health drops to 0.
        /// </summary>
        /// <param name="gridController">The grid controller.</param>
        void OnDestroyed(IGridController gridController);

        /// <summary>
        /// Arbitrarily removes the unit from the game, performing necessary cleanup and detaching the unit from game logic.
        /// </summary>
        void RemoveFromGame();

        /// <summary>
        /// Whether this unit can act this turn (not frozen/stunned).
        /// </summary>
        bool CanAct { get; }

        /// <summary>
        /// Gets the buff component of this unit.
        /// </summary>
        BuffComponent BuffComponent { get; }
    }
}
