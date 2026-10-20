namespace PCL.Core.App;

/// <summary>
/// 主题模式（亮/暗/系统）
/// </summary>
public enum ColorMode
{
    Light = 0,
    Dark = 1,
    System = 2
}

/// <summary>
/// 配色主题
/// </summary>
public enum ColorTheme
{
    SkyBlue = 0,
    CatBlue = 1,
    DeathBlue = 2,
    HmclBlue = 3,
    Pink = 4,
    Violet = 5,
    Green = 6,
    Orange = 7,
    Red = 8,
    Browns = 9,
    Custom = 10,

    // ---- 社区版扩展主题 ----
    // 注意：必须追加在 Custom 之后，因为 UI 下拉框的 SelectedIndex 直接等于枚举值，
    // 在中间插入会打乱已保存配置（如旧的 Custom=10）的语义。
    Cyan = 11,
    Mint = 12,
    Indigo = 13,
    Amber = 14,
    Sakura = 15,
    Steel = 16,
    Olive = 17,
    Crimson = 18,
    Neptune = 19
}

/// <summary>
/// 游戏窗口大小模式
/// </summary>
public enum GameWindowSizeMode
{
    Fullscreen = 0,
    Default = 1,
    Launcher = 2,
    Custom = 3,
    Maximized = 4
}

/// <summary>
/// 游戏进程优先级
/// </summary>
public enum GameProcessPriority
{
    AboveNormal = 0,
    Normal = 1,
    BelowNormal = 2,
    High = 3,
    RealTime = 4
}

/// <summary>
/// 游戏启动后启动器可见性
/// </summary>
public enum LauncherVisibility
{
    ExitImmediately = 0,
    ObsoleteCaseDoNotUse = 1,
    HideAndExit = 2,
    HideAndReopen = 3,
    MinimizeAndReopen = 4,
    DoNothing = 5
}

/// <summary>
/// JVM 优先 IP 栈类型
/// </summary>
public enum JvmPreferredIpStack
{
    PreferV4 = 0,
    Default = 1,
    PreferV6 = 2
}

public enum LauncherTitleType
{
    None = 0,
    Default = 1,
    Text = 2,
    Image = 3
}
