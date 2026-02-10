# Nexus — Unity VR Multiplayer Networking Framework

## Project Overview

Unity 6 UPM package for VR multiplayer networking. Built on Mirror + KCP Transport.
Target: Quest + PCVR cross-platform, 4-8 player rooms.

## C# Naming Conventions (Universal for all Unity projects)

### Identifiers

| Element               | Style            | Example                        |
|-----------------------|------------------|--------------------------------|
| Namespace             | PascalCase       | `Nexus.Networking.Core`        |
| Class / Struct        | PascalCase       | `NexusSession`                 |
| Interface             | I + PascalCase   | `INexusTransport`              |
| Enum                  | PascalCase       | `NexusMode`                    |
| Enum member           | PascalCase       | `NexusMode.Local`              |
| Public method         | PascalCase       | `StartHost()`                  |
| Public property       | PascalCase       | `IsActive { get; }`            |
| Private field         | _camelCase       | `_playerHealth`                |
| Local variable        | camelCase        | `int playerCount = 0;`         |
| Parameter             | camelCase        | `void Join(string roomId)`     |
| Constant              | PascalCase       | `const int MaxPlayers = 8;`    |
| Static readonly       | PascalCase       | `static readonly Vector3 Up`   |
| Event                 | On + PascalCase  | `event Action OnPlayerJoined`  |
| Generic type param    | T + PascalCase   | `TValue`, `TComponent`         |

### Code Style

- **Braces**: Allman style (opening brace on new line)
- **Access modifiers**: Always explicit (`private void Awake()`, not `void Awake()`)
- **Unity callbacks**: Mark as `private` (`private void Start()`, `private void Update()`)
- **SerializeField**: Use `[SerializeField] private` instead of `public` fields
- **var**: Use only when type is apparent from right side (`var list = new List<int>()`)
- **Namespaces**: Mirror directory structure (`Nexus.Networking.Core` for `Core/` folder)
- **File headers**: None — files start directly with `using` statements
- **One class per file**: File name must match class name
- **Comments in code**: English
- **using directives**: System namespaces first, then sorted alphabetically

### File Organization Within a Class

```csharp
public class ExampleComponent : MonoBehaviour
{
    // 1. Constants
    public const int MaxPlayers = 8;

    // 2. Static fields
    private static ExampleComponent _instance;

    // 3. Serialized fields
    [SerializeField] private float _speed = 5f;
    [SerializeField] private GameObject _prefab;

    // 4. Private fields
    private int _currentCount;
    private bool _isInitialized;

    // 5. Public properties
    public bool IsActive { get; private set; }

    // 6. Events
    public event Action<int> OnCountChanged;

    // 7. Unity callbacks (lifecycle order)
    private void Awake() { }
    private void OnEnable() { }
    private void Start() { }
    private void Update() { }
    private void OnDisable() { }
    private void OnDestroy() { }

    // 8. Public methods
    public void Initialize() { }

    // 9. Private methods
    private void HandleInput() { }
}
```

### Unity-Specific Rules

- Prefer `TryGetComponent<T>()` over `GetComponent<T>()` for safety
- Cache component references in `Awake()`, not in `Update()`
- Use `CompareTag("tag")` instead of `gameObject.tag == "tag"`
- Avoid `Find()` / `FindObjectOfType()` at runtime — use dependency injection or references
- Use `[RequireComponent(typeof(...))]` when a component depends on another
