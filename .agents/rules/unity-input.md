---
description: "Unity Input System 使用规范与推荐模式"
when_to_read: "处理玩家输入、按键绑定、Input Actions 相关代码时"
---

# Unity Input System Rules

## REQUIRED: New Input System

⚠️ **Legacy Input System is FORBIDDEN for new projects**

```csharp
// ❌ FORBIDDEN
if (Input.GetKey(KeyCode.W)) { }
float horizontal = Input.GetAxis("Horizontal");

// ✅ CORRECT
Vector2 move = _inputActions.Gameplay.Move.ReadValue<Vector2>();
_inputActions.Gameplay.Jump.performed += OnJump;
```

## Input Actions Asset

Create via `Assets → Create → Input Actions`.

Recommended structure:

```
PlayerInputActions
├── Gameplay
│   ├── Move (Value, Vector2)
│   ├── Look (Value, Vector2)
│   ├── Jump (Button)
│   └── Fire (Button)
└── UI
    ├── Navigate (Value, Vector2)
    ├── Submit (Button)
    └── Cancel (Button)
```

Generate C# class from the asset: `Inspector → Generate C# Class`.

## Recommended Approach: Generated C# Class

```csharp
using UnityEngine;
using MyGame.Input; // Generated namespace

public class InputHandler : MonoBehaviour
{
    private PlayerInputActions _inputActions;

    public Vector2 MoveInput { get; private set; }
    public bool IsJumping { get; private set; }

    private void Awake()
    {
        _inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        _inputActions.Gameplay.Jump.performed += OnJump;
        _inputActions.Gameplay.Fire.performed += OnFire;
        _inputActions.Gameplay.Enable();
    }

    private void OnDisable()
    {
        _inputActions.Gameplay.Jump.performed -= OnJump;
        _inputActions.Gameplay.Fire.performed -= OnFire;
        _inputActions.Gameplay.Disable();
    }

    private void Update()
    {
        MoveInput = _inputActions.Gameplay.Move.ReadValue<Vector2>();
        ProcessMovement(MoveInput);
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        IsJumping = true;
        PerformJump();
    }

    private void OnFire(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        PerformFire();
    }

    private void ProcessMovement(Vector2 input) { }
    private void PerformJump() { }
    private void PerformFire() { }
}
```

### Key Rules

1. **Use explicit handler methods** — avoid lambdas to prevent duplicate subscriptions.
2. **Subscribe in `OnEnable`, unsubscribe in `OnDisable`** — always pair them.
3. **Enable the action map**, not individual actions.
4. **Read continuous input in `Update`** (`ReadValue<T>`).
5. **Handle event-based input in callbacks** (`performed` / `canceled`).
6. **Do NOT reset event flags blindly in `Update`** — set transient flags in the event handler and reset them after consumption in a dedicated method or next frame if needed.

## Alternative Approaches (Legacy / Not Recommended)

### PlayerInput Component
- Use only if you need automatic device pairing (local multiplayer) or message-based wiring.
- Prefer `InputAction.CallbackContext` over legacy `InputValue`.

### Manual InputActionAsset Reference
- Use only if runtime asset swapping is required.
- Remember to `FindActionMap` / `FindAction` with `throwIfNotFound: true`.

## Multiplayer Input

Use `PlayerInputManager` for local co-op. Subscribe to `onPlayerJoined` / `onPlayerLeft` in `OnEnable`/`OnDisable`.

## Input Rebinding

Use `PerformInteractiveRebinding()` on the generated action instance. Always `Dispose()` the `RebindingOperation` in `OnComplete` / `OnCancel`.

## DO's and DON'Ts

### ❌ DON'T

```csharp
// Legacy Input — FORBIDDEN
if (Input.GetKey(KeyCode.W)) { }
if (Input.GetMouseButton(0)) { }
float horizontal = Input.GetAxis("Horizontal");

// Unpaired subscription
void OnEnable() => _jumpAction.performed += ctx => Jump();
// Missing unsubscribe

// Resetting event flag blindly every frame
void Update() { _jumpPressed = false; }
```

### ✅ DO

```csharp
// New Input System
Vector2 move = _inputActions.Gameplay.Move.ReadValue<Vector2>();
_inputActions.Gameplay.Jump.performed += OnJump;

// Proper pairing
void OnEnable()  { _inputActions.Gameplay.Jump.performed += OnJump; _inputActions.Gameplay.Enable(); }
void OnDisable() { _inputActions.Gameplay.Jump.performed -= OnJump; _inputActions.Gameplay.Disable(); }
```
