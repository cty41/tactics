Brief example showing how to implement the Deploy feature in the Turn Based Strategy Framework. This example demonstrates a simple deployment phase where units are spawned on valid deployment tiles and assigned to players.

## Scripts overview
- **DeployAbility.cs** – Defines deployment logic. Holds a list of deployable cells and deployable units.
- **DummyUnit.cs** – A disposable unit that holds the DeployAbility. Its purpose is to initialize the deployment process. Once finished, the unit is removed from the game.
- **DeployEntry.cs** – A wrapper for a unit prefab that includes a reference to the prefab and its name. Used by DeployAbility.

## Prefabs overview
- **DummyUnit** – An empty GameObject with `DummyUnit.cs` and `DeployAbility.cs` attached. Includes a Canvas with:
    - **DeployButton** – Selects this DummyUnit and initializes deployment by activating `DeployAbility`.
    - **FinishButton** – Finishes deployment and ends the turn, transitioning to the next player.
    - **Panel** – Holds buttons for selecting units to deploy.
        - **DeployButtonTemplate** – A template for deployment buttons. Instantiated by `DeployAbility` as many times as needed.

    Notes for DummyUnit setup:
    - Assign the `DeployButtonTemplate` to the appropriate field on DeployAbility.
    - The unit uses `MarkAsFriendly` function to activate the Canvas, displaying the deployment UI when its turn begins.
    - The unit has `DeployEntry` components as children. Set these up and assign them to the appropriate list on DeployAbility.
    - No need to set up prefabs of units to be deployed in any special way.

## Scene setup
To set up the scene for deployment:
1. For each player that should spawn units, add a DummyUnit to the scene and parent it to the UnitManager GameObject.
>Note: No need to use the Grid Helper; this unit is disposable and not assigned to any cell.
2. Assign `PlayerNumber` on each DummyUnit.
3. Assign deployable cells to the appropriate field on DeployAbility for each DummyUnit.
>Note: With a DummyUnit selected, use the Inspector lock to assign cells more easily.
4. Select all `FinishButton`s and assign `UnityGridController.EndTurn` to their OnClick handler.
>Note: Typically, the main end-turn button should remain non-interactable until deployment is complete. You can enable it by linking its `button.interactable` field to the OnClick handler of the last player’s `FinishButton`.
