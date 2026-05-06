# Unity Input System 规范

## 强制要求：使用新 Input System

⚠️ **新项目中严禁使用旧版 Input System**

```csharp
// ❌ 禁止
if (Input.GetKey(KeyCode.W)) { }
float horizontal = Input.GetAxis("Horizontal");

// ✅ 正确
Vector2 move = _inputActions.Gameplay.Move.ReadValue<Vector2>();
_inputActions.Gameplay.Jump.performed += OnJump;
```

## Input Actions 资源

通过 `Assets → Create → Input Actions` 创建。

推荐结构：

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

从资源生成 C# 类：`Inspector → Generate C# Class`。

## 推荐方案：生成的 C# 类

```csharp
using UnityEngine;
using MyGame.Input; // 生成的命名空间

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

### 关键规则

1. **使用显式处理方法** — 避免使用 lambda 以防重复订阅
2. **在 `OnEnable` 中订阅，在 `OnDisable` 中取消订阅** — 必须配对
3. **启用 Action Map**，而不是单独的 Action
4. **在 `Update` 中读取连续输入**（`ReadValue<T>`）
5. **在回调中处理事件式输入**（`performed` / `canceled`）
6. **不要在 `Update` 中盲目重置事件标志** — 在事件处理中设置瞬时标志，在消费完成后在专用方法中或下一帧重置

## 备选方案（旧版/不推荐）

### PlayerInput 组件
- 仅在需要自动设备配对（本地多人）或基于消息的绑定时使用
- 优先使用 `InputAction.CallbackContext` 而非旧版 `InputValue`

### 手动引用 InputActionAsset
- 仅在需要运行时替换资源时使用
- 记住使用 `FindActionMap` / `FindAction` 并传入 `throwIfNotFound: true`

## 多人输入

使用 `PlayerInputManager` 实现本地合作。在 `OnEnable`/`OnDisable` 中订阅 `onPlayerJoined` / `onPlayerLeft`。

## 输入重绑定

对生成的 Action 实例使用 `PerformInteractiveRebinding()`。在 `OnComplete` / `OnCancel` 中务必 `Dispose()` `RebindingOperation`。

## 正确与错误对照

### ❌ 禁止

```csharp
// 旧版 Input — 禁止
if (Input.GetKey(KeyCode.W)) { }
if (Input.GetMouseButton(0)) { }
float horizontal = Input.GetAxis("Horizontal");

// 未配对的订阅
void OnEnable() => _jumpAction.performed += ctx => Jump();
// 缺少取消订阅

// 每帧盲目重置事件标志
void Update() { _jumpPressed = false; }
```

### ✅ 正确

```csharp
// 新 Input System
Vector2 move = _inputActions.Gameplay.Move.ReadValue<Vector2>();
_inputActions.Gameplay.Jump.performed += OnJump;

// 正确配对
void OnEnable()  { _inputActions.Gameplay.Jump.performed += OnJump; _inputActions.Gameplay.Enable(); }
void OnDisable() { _inputActions.Gameplay.Jump.performed -= OnJump; _inputActions.Gameplay.Disable(); }
```
