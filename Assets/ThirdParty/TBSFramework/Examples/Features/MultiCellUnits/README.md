Brief example showing how to implement regular-shaped units that occupy multiple cells in the Turn Based Strategy Framework. This example demonstrates specialized movement validation, occupancy logic, and attack range calculations for large-scale units on both square and hexagonal grids.

## Scripts overview
- **MultiCellUnit.cs** – The core component that extends the base `Unit` class. It overrides movement and attack logic to account for the shape of the unit.

## Prefabs overview
The demo includes **5 Unit Prefabs** ranging from size 1 (regular unit) to size 5. Each prefab consists of:
- A visual model scaled to match its intended size.
- The **MultiCellUnit.cs** script attached in place of the standard `Unit` script.

---

## Scene setup
To implement multi-cell units in your own project:

1. **Attach the Script**: Replace your standard `Unit` component with `MultiCellUnit.cs` (or a script that inherits from it) on your unit prefab.
2. **Configure Grid Type**: Toggle the `IsHexMap` boolean on the `MultiCellUnit.cs` to match your scene’s grid type.
3. **Define Size**: Set the `Unit Size` integer.
    * For **Square Maps**: Size represents the **side length** of the unit (e.g., 2 = 2x2 cells).
    * For **Hex Maps**: Size represents the **radius** from the center cell (e.g., 1 = center cell + the 6 immediately surrounding cells).
4. **Place the unit on the grid**: Use the Grid Helper to place the unit on the grid.

> **Note:** The unit’s "position" is defined by its anchor (pivot) cell. The script automatically calculates and occupies the surrounding cells based on this pivot.

---

## Understanding Multi-Cell Mechanics

The logic within `MultiCellUnit` modifies three primary framework behaviors:

### 1. Movement & Traversability
Unlike standard units, a multi-cell unit only considers a destination "valid" if **every cell** within its shape is traversable and unoccupied. If even one cell in the target area is blocked or occupied by another unit, the entire move is invalidated.

### 2. Occupancy Management
The unit automatically manages the `IsTaken` state of all cells it covers:
* When the unit is initialized or finishes a move, it adds itself to the `CurrentUnits` list of every cell in its footprint.
* It clears these references from its previous location to ensure the grid remains accurate for pathfinding.

### 3. Attack Validation
Attack range is calculated from the **closest point**. The unit is considered "in range" to attack an enemy if any cell in the attacker's footprint is within `AttackRange` of any cell in the target's footprint.