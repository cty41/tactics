Brief example showing how to implement an Initiative (Active Time Battle) system in the Turn Based Strategy Framework. This example demonstrates a time-based turn order where units accumulate charge based on their initiative stat and act only when the threshold is reached.

## Scripts overview
- **InitiativeTurnResolver.cs** – Defines the turn resolution logic. Processes charge accumulation for all units and manages a queue of units ready to act.
- **InitiativeComponent.cs** – Holds a unit's speed (Initiative) and current progress (Charge).
- **DummyPlayer.cs** – An automated player that takes "empty" turns to advance time when no units have reached the charge threshold.
- **ActionBar.cs** – Visualizes charge progress on a UI element (similar to a health bar).

## Prefabs overview
Unit Prefab – A standard unit with InitiativeComponent.cs attached. Includes a World Space Canvas with:

- **Action Bar** – An image or panel with ActionBar.cs attached to visualize turn charge.

Set the Initiative value on the component to determine how quickly the unit gains its turn.

## Scene setup
1. Add an empty GameObject under the PlayerManager GameObject and attach the `DummyPlayer.cs` script.
2. Set the Player Number field on the `DummyPlayer` to a value different from other players.
3. Add `InitiativeTurnResolver.cs` to the GridController GameObject.
4. Remove the default `SubsequentTurnResolver` from the GridController.
5. Assign the `InitiativeTurnResolver` to the `TurnResolver` field on the GridController.
6. Set the `DummyPlayerNumber` field on the `InitiativeTurnResolver` to the value assigned in step 2.
7. Add `InitiativeComponent` to all units in the scene.

## Understanding the charging mechanics
 The InitiativeTurnResolver updates the charge of all units every time a turn ends. This process effectively creates a game tick, and the duration of that tick is controlled by the DummyPlayer.

### Charge Formula
The amount of charge incremented for a unit in a single tick is calculated as: 
```charge_added = unit_initiative * multiplier```
A unit takes its turn when its Charge reaches or exceeds the Threshold.

### Tuning the Simulation Speed
The interaction between the four parameters allows you to control the simulation speed and visual smoothness:
- Unit Initiative Value (on InitiativeComponent): Determines the relative speed of a unit compared to others. Higher values mean faster charge accumulation.
    valid value range: Threshold >= **Initiative** > 0
- Threshold (on InitiativeTurnResolver): This is the target goal (default is 1). If you increase the Threshold, units must accumulate more charge, slowing down the overall combat speed.
    valid value range: **Threshold** > 0
- Multiplier (on InitiativeTurnResolver): A global speed scaler (default is 1). Increasing this value speeds up all units equally.
    valid value range: **Multiplier** > 0
- Delay (on DummyPlayer): This controls the tick frequency (default is 100ms).
    - For smoother charging, use lower dealy with lower multiplier 
    - For coarser, chunkier charging updates use higher delay with higher multiplier.
    valid value range: **Delay** > 0