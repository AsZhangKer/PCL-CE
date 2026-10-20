using System;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.IoC;

namespace PCL.Core.UI.Theme;

/// <summary>
/// 配色模式更改事件。
/// </summary>
/// <param name="isDarkMode">当前是否为深色模式</param>
/// <param name="theme">当前配色主题</param>
public delegate void ColorModeChangedEvent(bool isDarkMode, ColorTheme theme);

/// <summary>
/// 配色主题更改事件。
/// </summary>
/// <param name="theme">当前配色主题</param>
public delegate void ColorThemeChangedEvent(ColorTheme theme);

[LifecycleScope("theme", "主题", false)]
[LifecycleService(LifecycleState.WindowCreating)]
public sealed partial class ThemeService
{
    [AnyConfigItem<ToneProfileConfig>("UiToneProfiles", ConfigSource.Local)]
    public static partial ToneProfileConfig ToneProfiles { get; set; }

    [RegisterConfigEvent]
    public static ConfigEventRegistry OnColorModeConfigChanged => new(
        scope: Config.Preference.Theme.ColorModeConfig,
        trigger: ConfigEvent.Update,
        handler: _ => RefreshColorMode()
    );

    [RegisterConfigEvent]
    public static ConfigEventRegistry OnColorThemeConfigChanged => new(
        scope: [Config.Preference.Theme.DarkColorConfig, Config.Preference.Theme.LightColorConfig],
        trigger: ConfigEvent.Update,
        handler: e =>
        {
            // ignore no change or non-current color theme change
            if (e.OldValue == e.Value) return;
            if (IsDarkMode) { if (e.Item == Config.Preference.Theme.LightColorConfig) return; }
            else { if (e.Item == Config.Preference.Theme.DarkColorConfig) return; }
            // trigger color refresh
            if (Lifecycle.CurrentState > LifecycleState.Loading)
            {
                Lifecycle.CurrentApplication.Dispatcher.BeginInvoke(() =>
                {
                    ApplyColorResources();
                    ColorThemeChanged?.Invoke(CurrentTheme);
                    _AprilFoolLogic();
                });
            }
        }
    );

    [RegisterConfigEvent]
    public static ConfigEventRegistry OnCustomColorConfigChanged => new(
        scope: Config.Preference.Theme.CustomColorConfig,
        trigger: ConfigEvent.Update,
        handler: e =>
        {
            if (e.OldValue == e.Value) return;
            if (CurrentTheme != ColorTheme.Custom) return;
            if (Lifecycle.CurrentState > LifecycleState.Loading)
            {
                Lifecycle.CurrentApplication.Dispatcher.BeginInvoke(() =>
                {
                    ApplyColorResources();
                    ColorThemeChanged?.Invoke(CurrentTheme);
                });
            }
        }
    );

    [LifecycleStart]
    private static void _Start()
    {
        IsDarkMode = _IsDarkMode();
        _LogStatus();
        _RefreshAll();
    }

    private static void _LogStatus()
    {
        Context.Debug($"当前状态: {(IsDarkMode ? "Dark" : "Light")}, {CurrentTheme}");
    }

    private static bool _IsDarkMode() => Config.Preference.Theme.ColorMode switch
    {
        ColorMode.Light => false,
        ColorMode.Dark => true,
        ColorMode.System => SystemThemeHelper.IsSystemInDarkMode(),
        _ => false
    };

    /// <summary>
    /// 当前是否为深色主题。
    /// </summary>
    public static bool IsDarkMode { get; private set; }

    /// <summary>
    /// 配色模式更改事件。
    /// </summary>
    public static event ColorModeChangedEvent? ColorModeChanged;

    /// <summary>
    /// 配色主题更改事件。
    /// </summary>
    public static event ColorThemeChangedEvent? ColorThemeChanged;

    private static void _RefreshAll()
    {
        ApplyGrayResources();
        ApplyColorResources();
        ColorModeChanged?.Invoke(IsDarkMode, CurrentTheme);
        _AprilFoolLogic();
    }

    private static void _AprilFoolLogic()
    {
        // for HMCL theme on April Fools' Day
        if (Basics.IsAprilFool) Config.Preference.WindowTitleTypeConfig.TriggerEvent(ConfigEvent.Changed, null);
    }

    /// <summary>
    /// 刷新配色模式，若检测到当前配色模式有实际更改，则会触发主题刷新。
    /// </summary>
    public static void RefreshColorMode()
    {
        var isDarkMode = _IsDarkMode();
        if (IsDarkMode == isDarkMode) return;
        Context.Info("正在更改配色模式");
        IsDarkMode = isDarkMode;
        _LogStatus();
        if (Lifecycle.CurrentState > LifecycleState.Loading)
        {
            Lifecycle.CurrentApplication.Dispatcher.BeginInvoke(_RefreshAll);
        }
    }

    /// <summary>
    /// 当前使用的色彩属性。
    /// </summary>
    public static ToneProfile CurrentTone => IsDarkMode ? ToneProfiles.Dark : ToneProfiles.Light;

    /// <summary>
    /// 当前使用的主题色。
    /// </summary>
    public static ColorTheme CurrentTheme
    {
        get
        {
            var theme = Config.Preference.Theme;
            return IsDarkMode ? theme.DarkColor : theme.LightColor;
        }
        set
        {
            var theme = Config.Preference.Theme;
            var config = IsDarkMode ? theme.DarkColorConfig : theme.LightColorConfig;
            config.SetValue(value);
        }
    }

    /// <summary>
    /// 获取当前色彩主题对应的各种参数。
    /// </summary>
    public static (int Hue, double LightAdjust, double ChromaAdjust) GetCurrentThemeArgs() => GetThemeArgs(CurrentTheme);

    /// <summary>
    /// 获取指定色彩主题对应的各种参数（不依赖当前配色模式）。
    /// </summary>
    public static (int Hue, double LightAdjust, double ChromaAdjust) GetThemeArgs(ColorTheme theme)
    {
        return theme switch
        {
            ColorTheme.SkyBlue => (235, 0.36, 0.2),
            ColorTheme.CatBlue => (255, 0, -0.2),
            ColorTheme.DeathBlue => (268, -0.05, -0.1),
            ColorTheme.HmclBlue => (275, -0.03, -0.35),
            ColorTheme.Pink => (345, 0.05, 0.1),
            ColorTheme.Violet => (290, 0.05, 0.15),
            ColorTheme.Green => (145, 0.1, 0.1),
            ColorTheme.Orange => (30, 0.1, 0.2),
            ColorTheme.Red => (10, 0.08, 0.2),
            ColorTheme.Browns => (25, -0.1, 0.4),
            // ---- 社区版扩展主题（Hue 色相 / LightAdjust 明度修正 / ChromaAdjust 彩度修正）----
            ColorTheme.Cyan => (190, 0.02, 0.12),
            ColorTheme.Mint => (165, 0.08, 0.18),
            ColorTheme.Indigo => (228, -0.04, 0.05),
            ColorTheme.Amber => (48, 0.06, 0.25),
            ColorTheme.Sakura => (322, 0.08, 0.18),
            ColorTheme.Steel => (212, -0.12, -0.45),
            ColorTheme.Olive => (80, 0.02, 0.05),
            ColorTheme.Crimson => (352, 0.02, 0.15),
            ColorTheme.Neptune => (205, -0.02, 0.1),
            ColorTheme.Custom => _GetCustomThemeArgs(),
#if DEBUG
            _ => ((int)theme, 0, 0)
#else
            _ => throw new IndexOutOfRangeException($"Invalid theme index: {(int)theme}")
#endif
        };
    }

    /// <summary>
    /// 从用户自定义的十六进制颜色解析主题参数 (色相 Hue)。
    /// </summary>
    private static (int Hue, double LightAdjust, double ChromaAdjust) _GetCustomThemeArgs()
    {
        if (!TryParseHexColor(Config.Preference.Theme.CustomColor, out var r, out var g, out var b))
            return (_ParseHueFromRgb(255, 255, 255), 0, 0.2);
        var hue = _ParseHueFromRgb(r, g, b);
        // 根据亮度调整，保证暗色模式下依然有足够对比度
        var luma = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        var lightAdjust = (luma > 0.6 ? -0.08 : 0.28) * luma;
        return (hue, lightAdjust, 0.2);
    }

    private static bool TryParseHexColor(string hex, out double r, out double g, out double b)
    {
        r = g = b = 0;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        var text = hex.Trim().TrimStart('#');
        if (text.Length != 6) return false;
        for (var i = 0; i < 6; i++)
            if (!Uri.IsHexDigit(text[i]))
                return false;
        r = Convert.ToInt32(text.Substring(0, 2), 16);
        g = Convert.ToInt32(text.Substring(2, 2), 16);
        b = Convert.ToInt32(text.Substring(4, 2), 16);
        return true;
    }

    private static int _ParseHueFromRgb(double r, double g, double b)
    {
        var rn = r / 255.0;
        var gn = g / 255.0;
        var bn = b / 255.0;
        var max = Math.Max(rn, Math.Max(gn, bn));
        var min = Math.Min(rn, Math.Min(gn, bn));
        var delta = max - min;
        if (Math.Abs(delta) < 1e-6) return 0;
        double hue;
        if (max == rn)
            hue = ((gn - bn) / delta) % 6.0;
        else if (max == gn)
            hue = ((bn - rn) / delta) + 2.0;
        else
            hue = ((rn - gn) / delta) + 4.0;
        hue *= 60.0;
        if (hue < 0) hue += 360.0;
        return (int)Math.Round(hue);
    }

    private static double _AdjustLinear(double value, double adjustment)
    {
        if (adjustment == 0) return value;
        // 确保输入在合理范围内
        value = Math.Clamp(value, 0.0, 1.0);
        adjustment = Math.Clamp(adjustment, -1.0, 1.0);
        // 非对称线性插值
        return adjustment switch
        {
            > 0 => value + (1.0 - value) * adjustment,
            _ => value + value * adjustment
        };
    }

    private static CatColorResource[] _CalculateGrays(ToneProfile tone) => [
        LabColor.FromLch(tone.L1).ToCatColor("Gray1"),
        LabColor.FromLch(tone.L2).ToCatColor("Gray2"),
        LabColor.FromLch(tone.L3).ToCatColor("Gray3"),
        LabColor.FromLch(tone.L4).ToCatColor("Gray4"),
        LabColor.FromLch(tone.L5).ToCatColor("Gray5"),
        LabColor.FromLch(tone.L6).ToCatColor("Gray6"),
        LabColor.FromLch(tone.L7).ToCatColor("Gray7"),
        LabColor.FromLch(tone.L8).ToCatColor("Gray8"),
        LabColor.FromLch(tone.LWhite, alpha:tone.AHalfWhite).ToCatColor("HalfWhite", false),
        LabColor.FromLch(tone.LWhite, alpha:tone.ASemiWhite).ToCatColor("SemiWhite", false),
        LabColor.FromLch(tone.LWhite).ToCatColor("White", false),
        LabColor.FromLch(tone.LWhite, alpha:tone.ATransparent).ToCatColor("Transparent", false),
        LabColor.FromLch(tone.LBackground, alpha:tone.ABackground).ToCatColor("TransparentBackground", false),
        LabColor.FromLch(tone.LBackground).ToCatColor("Background", false),
        LabColor.FromLch(tone.LBackground, alpha:tone.AToolTip).ToCatColor("ToolTip", false),
        LabColor.FromLch(tone.L7, 0.25, 30, tone.AHalfTransparent).ToCatColor("RedBack", false),
        LabColor.FromLch(tone.LForeground).ToCatColor("Memory", false),
    ];

    private static CatColorResource[] _CalculateColors(ToneProfile tone, (int hue, double lightAdj, double chromaAdj) args) => [
        LabColor.FromLch(_AdjustLinear(tone.L1, args.lightAdj * 0.1), _AdjustLinear(tone.C1, args.chromaAdj * 0.25), args.hue).ToCatColor("1"),
        LabColor.FromLch(_AdjustLinear(tone.L2, args.lightAdj), _AdjustLinear(tone.C2, args.chromaAdj), args.hue).ToCatColor("2"),
        LabColor.FromLch(_AdjustLinear(tone.L3, args.lightAdj), _AdjustLinear(tone.C3, args.chromaAdj), args.hue).ToCatColor("3"),
        LabColor.FromLch(_AdjustLinear(tone.L4, args.lightAdj), _AdjustLinear(tone.C4, args.chromaAdj), args.hue).ToCatColor("4"),
        LabColor.FromLch(_AdjustLinear(tone.L5, args.lightAdj), _AdjustLinear(tone.C5, args.chromaAdj), args.hue).ToCatColor("5"),
        LabColor.FromLch(_AdjustLinear(tone.L6, args.lightAdj), _AdjustLinear(tone.C6, args.chromaAdj), args.hue).ToCatColor("6"),
        LabColor.FromLch(_AdjustLinear(tone.L7, args.lightAdj), _AdjustLinear(tone.C7, args.chromaAdj), args.hue).ToCatColor("7"),
        LabColor.FromLch(_AdjustLinear(tone.L8, args.lightAdj), _AdjustLinear(tone.C8, args.chromaAdj), args.hue).ToCatColor("8"),
        LabColor.FromLch(_AdjustLinear(tone.L8, args.lightAdj), _AdjustLinear(tone.C8, args.chromaAdj), args.hue, tone.ASemiTransparent).ToCatColor("SemiTransparent", false),
        LabColor.FromLch(_AdjustLinear(tone.L5, args.lightAdj), _AdjustLinear(tone.C5, args.chromaAdj), args.hue).ToCatColor("Bg0"),
        LabColor.FromLch(_AdjustLinear(tone.L7, args.lightAdj), _AdjustLinear(tone.C7, args.chromaAdj), args.hue, tone.ASemiWhite).ToCatColor("Bg1"),
    ];

    private static CatColorResource[] LightGrayCache { get => field ??= _CalculateGrays(ToneProfiles.Light); set; } = null!;

    private static CatColorResource[] DarkGrayCache { get => field ??= _CalculateGrays(ToneProfiles.Dark); set; } = null!;

    /// <summary>
    /// 清除灰度配色的计算缓存。
    /// </summary>
    public static void InvalidateGrayCache()
    {
        LightGrayCache = null!;
        DarkGrayCache = null!;
    }

    /// <summary>
    /// 应用灰度配色到 WPF 资源字典。
    /// </summary>
    public static void ApplyGrayResources()
    {
        var cache = IsDarkMode ? DarkGrayCache : LightGrayCache;
        foreach (var c in cache) c.Apply();
    }

    /// <summary>
    /// 计算指定主题在指定配色模式下的代表色（即 Color2 主色），用于设置界面中的色块预览。
    /// </summary>
    /// <param name="theme">要预览的色彩主题</param>
    /// <param name="isDark">是否按深色模式的色调计算</param>
    public static System.Windows.Media.Color GetThemePreviewColor(ColorTheme theme, bool isDark)
    {
        var tone = isDark ? ToneProfiles.Dark : ToneProfiles.Light;
        var (hue, lightAdjust, chromaAdjust) = GetThemeArgs(theme);
        // 与 _CalculateColors 中 Color2 的算法保持一致，确保预览色与实际主色相同
        return LabColor
            .FromLch(_AdjustLinear(tone.L2, lightAdjust), _AdjustLinear(tone.C2, chromaAdjust), hue)
            .ToWpfColor();
    }

    /// <summary>
    /// 应用彩色配色到 WPF 资源字典。
    /// </summary>
    public static void ApplyColorResources()
    {
        var colors = _CalculateColors(CurrentTone, GetCurrentThemeArgs());
        foreach (var c in colors) c.Apply();
    }
}
