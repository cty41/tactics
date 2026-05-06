# Unity 核心规范 - C# & MonoBehaviour

## 快速参考

| 元素 | 命名约定 | 示例 |
|------|----------|------|
| 类 / 结构体 / 接口 | PascalCase | `PlayerController`, `IHealthSystem` |
| 私有字段 | `_camelCase` | `_moveSpeed`, `_isGrounded` |
| 公开属性 | PascalCase | `MoveSpeed`, `IsMoving` |
| 常量 | PascalCase | `MaxHealth` |
| 静态字段 | `_camelCase` | `_instanceCount` |
| 方法 | PascalCase | `ProcessInput()` |
| 事件 | `On` + PascalCase | `OnPlayerDeath` |
| 异步方法 | `Async` 后缀 | `LoadDataAsync()` |
| 布尔值 | `is` / `has` / `can` 前缀 | `_isGrounded`, `_hasWeapon` |

## Unity 6.2 命名规范

### 类和结构体
```
// ✅ 正确：PascalCase
public class PlayerController : MonoBehaviour { }

// ✅ 正确：只读结构体保证数据完整性
public readonly struct GameConfig { }

public interface IHealthSystem { }

// ❌ 错误
public class player_controller { } // 蛇形命名
public class playerController { }  // 驼峰命名
public struct MutableConfig { }    // 避免可变结构体
```

### 字段和属性
```
public class Example : MonoBehaviour
{
    // ✅ 正确：私有字段使用 _ 前缀（.NET 风格）
    [SerializeField] private float _moveSpeed = 5f;
    private Transform _targetTransform;
    
    // ✅ 正确：公开属性使用 PascalCase
    public float MoveSpeed => _moveSpeed;
    public bool IsMoving { get; private set; }
    
    // ✅ 正确：常量使用 PascalCase（Microsoft 标准）
    private const float MaxHealth = 100f;
    private const string PlayerTag = "Player";
    
    // ✅ 正确：静态字段使用 _ 前缀
    private static int _instanceCount = 0;
    
    // ✅ 正确：布尔值使用 is/has/can 前缀
    private bool _isGrounded;
    private bool _hasWeapon;
    private bool _canJump;
    
    // ❌ 错误：无 [SerializeField] 的公开字段
    public float moveSpeed; // 应使用属性或 [SerializeField] private
    
    // ❌ 错误：匈牙利命名法
    private float m_Speed; 
    private float fSpeed;
}
```

### 方法和事件
```
public class EventExample : MonoBehaviour
{
    // ✅ 正确：方法使用 PascalCase
    public void ProcessInput() { }
    private void HandleCollision() { }
    
    // ✅ 正确：事件使用 On 前缀
    public event Action OnPlayerDeath;
    public event Action<int> OnScoreChanged;
    
    // ✅ 正确：异步方法使用 Async 后缀（Unity 6.2 使用 Awaitable）
    private async Awaitable LoadDataAsync() { }
    public async Awaitable<bool> TryConnectAsync() { }
}
```

## MonoBehaviour 生命周期

### 正确的方法顺序

```
public class Example : MonoBehaviour
{
    // 1. 序列化字段
    // 2. 私有字段
    // 3. 属性
    // 4. Unity 生命周期方法（按调用顺序）
    private void Awake()     { /* 初始化组件, TryGetComponent */ }
    private void OnEnable()  { /* 订阅事件 */ }
    private void Start()     { /* 所有 Awake 调用完成后初始化 */ }
    private void FixedUpdate() { /* 物理 */ }
    private void Update()    { /* 游戏逻辑和输入 */ }
    private void LateUpdate()  { /* 摄像机、后更新 */ }
    private void OnDisable() { /* 取消订阅事件 */ }
    private void OnDestroy() { /* 清理 */ }
    // 5. 自定义方法
}
```

## 序列化

### [SerializeField] 最佳实践
```
public class SerializationExample : MonoBehaviour
{
    // ✅ 正确：私有字段配合 [SerializeField] 和 _ 前缀
    [SerializeField] private float _health = 100f;
    [SerializeField] private GameObject _weaponPrefab;
    
    // ✅ 正确：使用 Header 分组
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _jumpForce = 10f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _jumpSound;
    [SerializeField] private AudioClip _landSound;
    
    // ✅ 正确：Tooltip 添加 Inspector 文档
    [Tooltip("Maximum speed the player can reach")]
    [SerializeField] private float _maxSpeed = 20f;
    
    // ✅ 正确：Range 限制数值范围
    [Range(0f, 1f)]
    [SerializeField] private float _volume = 0.5f;
    
    // ✅ 正确：HideInInspector 隐藏运行时值
    [HideInInspector]
    public int RuntimeValue; // 被系统使用但不可编辑
}
```

### ScriptableObject 做配置
```
// ✅ 正确：通过 ScriptableObject 做配置
[CreateAssetMenu(fileName = "WeaponConfig", menuName = "Game/Weapon Config")]
public class WeaponConfig : ScriptableObject
{
    [Header("Stats")]
    [SerializeField] private int _damage = 10;
    [SerializeField] private float _fireRate = 0.5f;
    
    [Header("Visuals")]
    [SerializeField] private GameObject _modelPrefab;
    [SerializeField] private ParticleSystem _muzzleFlash;
    
    public int Damage => _damage;
    public float FireRate => _fireRate;
    public GameObject ModelPrefab => _modelPrefab;
}

public class Weapon : MonoBehaviour
{
    [SerializeField] private WeaponConfig _config;
    
    public void Fire()
    {
        // 使用配置
        DealDamage(_config.Damage);
    }
}
```

## 命名空间

```
// ✅ 正确：使用命名空间
namespace MyGame.Core
{
    public class GameManager : MonoBehaviour
    {
    }
}

namespace MyGame.Gameplay.Player
{
    public class PlayerController : MonoBehaviour
    {
    }
}

namespace MyGame.UI
{
    public class MainMenuUI : MonoBehaviour
    {
    }
}
```

## XML 文档注释

```
/// <summary>
/// 控制玩家移动和动作。
/// </summary>
public class PlayerController : MonoBehaviour
{
    /// <summary>
    /// 当前玩家移动速度。
    /// </summary>
    [SerializeField] private float _moveSpeed = 5f;
    
    /// <summary>
    /// 将玩家向指定方向移动。
    /// </summary>
    /// <param name="direction">移动方向（规范化向量）。</param>
    /// <param name="deltaTime">自上一帧以来的时间。</param>
    public void Move(Vector3 direction, float deltaTime)
    {
        transform.position += direction * _moveSpeed * deltaTime;
    }
    
    /// <summary>
    /// 检查玩家是否可以跳跃。
    /// </summary>
    /// <returns>如果可以跳跃则返回 true。</returns>
    public bool CanJump()
    {
        return IsGrounded() && !IsJumping;
    }
}
```

## 现代 C# 特性（Unity 6.2 / C# 12.0）

```
// ✅ 正确：模式匹配
public void HandleInput(InputAction.CallbackContext context)
{
    switch (context.phase)
    {
        case InputActionPhase.Started:
            OnInputStarted();
            break;
        case InputActionPhase.Performed:
            OnInputPerformed();
            break;
        case InputActionPhase.Canceled:
            OnInputCanceled();
            break;
    }
}

// ✅ 正确：Null 条件运算符
private void SafeInvoke()
{
    OnPlayerDeath?.Invoke();
}

// ✅ 正确：表达式体成员
public bool IsAlive => _health > 0;
public float HealthPercentage => _health / MaxHealth;

// ✅ 正确：字符串插值
private void LogStatus()
{
    Debug.Log($"Player Health: {_health}/{MaxHealth}");
}

// ✅ 正确：Unity Awaitable（替代 Task）
private async Awaitable PerformActionAsync()
{
    await Awaitable.NextFrameAsync();
    _health += 10;
}
```
