using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.UI;
using PCL.Core.UI.Theme;
using PCL.Core.Utils;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupUI
{
    public string[] ThemeColors => _CreateThemeColorNames();

    private static bool _themeColorMismatchLogged;

    /// <summary>
    /// 构造主题名称列表。索引必须与 <see cref="ColorTheme"/> 枚举值严格一一对应，
    /// 因为设置页的下拉框是直接用 SelectedIndex 当枚举值来存取的，错位会导致用户配置串味。
    /// 注意不能缓存结果：切换语言后名称需要重新取本地化文本。
    /// </summary>
    private static string[] _CreateThemeColorNames()
    {
        string[] names =
        [
            Lang.Text("Setup.Ui.Theme.Color.SkyBlue"), Lang.Text("Setup.Ui.Theme.Color.CatBlue"),
            Lang.Text("Setup.Ui.Theme.Color.CrashBlue"), Lang.Text("Setup.Ui.Theme.Color.Hmcl"),
            Lang.Text("Setup.Ui.Theme.Color.Pink"), Lang.Text("Setup.Ui.Theme.Color.Violet"),
            Lang.Text("Setup.Ui.Theme.Color.Green"), Lang.Text("Setup.Ui.Theme.Color.Orange"),
            Lang.Text("Setup.Ui.Theme.Color.Red"), Lang.Text("Setup.Ui.Theme.Color.Browns"),
            Lang.Text("Setup.Ui.Theme.Color.Custom"),
            // ---- 扩展主题（索引必须与 ColorTheme 枚举值一一对应）----
            Lang.Text("Setup.Ui.Theme.Color.Cyan"), Lang.Text("Setup.Ui.Theme.Color.Mint"),
            Lang.Text("Setup.Ui.Theme.Color.Indigo"), Lang.Text("Setup.Ui.Theme.Color.Amber"),
            Lang.Text("Setup.Ui.Theme.Color.Sakura"), Lang.Text("Setup.Ui.Theme.Color.Steel"),
            Lang.Text("Setup.Ui.Theme.Color.Olive"), Lang.Text("Setup.Ui.Theme.Color.Crimson"),
            Lang.Text("Setup.Ui.Theme.Color.Neptune")
        ];
        // 防御性检查：增删主题时若忘了同步枚举或此数组，这里会留下日志线索（只记一次，避免刷屏）
        if (!_themeColorMismatchLogged)
        {
            var expected = System.Enum.GetValues<ColorTheme>().Length;
            if (names.Length != expected)
            {
                _themeColorMismatchLogged = true;
                ModBase.Log($"[UI] 主题名称数量 {names.Length} 与 ColorTheme 枚举数量 {expected} 不一致，主题选择可能错位",
                    ModBase.LogLevel.Debug);
            }
        }
        return names;
    }
    
    public new bool isLoaded;

    public PageSetupUI()
    {
        InitializeComponent();
        Loaded += PageSetupUI_Loaded;
        Loaded += (_, _) => HiddenRefresh();
        // 取色器事件在代码里挂接，避免 XAML 中声明自定义事件处理器导致设计器报错
        PickerCustomColor.ColorChanged += PickerCustomColor_ColorChanged;
        PickerCustomColor2.ColorChanged += PickerCustomColor2_ColorChanged;
    }

    private void PageSetupUI_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        ModAnimation.AniControlEnabled += 1;
        Reload(); // #4826，在每次进入页面时都刷新一下
        ModAnimation.AniControlEnabled -= 1;

        // 非重复加载部分
        if (isLoaded)
            return;
        isLoaded = true;

        SliderLoad();

    }

    public void Reload()
    {
        try
        {
            // 启动器
            SliderLauncherOpacity.Value = Config.Preference.Theme.WindowOpacity;
            CheckLauncherLogo.Checked = Config.Preference.ShowStartupLogo;
            ComboDarkMode.SelectedIndex = (int)Config.Preference.Theme.ColorMode;
            ComboDarkColor.SelectedIndex = (int)Config.Preference.Theme.DarkColor;
            ComboLightColor.SelectedIndex = (int)Config.Preference.Theme.LightColor;
            RefreshThemeColorPreview();
            RefreshCustomColorPickers();
            if (TextMsClientId is not null)
            {
                _isReloadingMsClientId = true;
                TextMsClientId.Text = Config.Preference.Theme.MsClientId;
                _isReloadingMsClientId = false;
            }
            UpdateCustomColorVisibility();
            CheckShowLaunchingHint.Checked = Config.Preference.ShowLaunchingHint;

            // 字体设置
            ComboUiFont.SelectedFontTag = Config.Preference.Font;
            ComboUiMotdFont.SelectedFontTag = Config.Preference.MotdFont;

            CheckBlur.Checked = Config.Preference.Blur.IsEnabled;
            SliderBlurValue.Value = Config.Preference.Blur.Radius;
            SliderBlurSamplingRate.Value = Config.Preference.Blur.SamplingRate;
            ComboBlurType.SelectedIndex = Config.Preference.Blur.KernelType;
            PanBlurValue.Visibility = CheckBlur.Checked == true ? Visibility.Visible : Visibility.Collapsed;
            CheckLockWindowSize.Checked = Config.Preference.LockWindowSize;

            // 背景图片
            SliderBackgroundOpacity.Value = Config.Preference.Background.WallpaperOpacity;
            SliderBackgroundBlur.Value = Config.Preference.Background.WallpaperBlurRadius;
            ComboBackgroundSuit.SelectedIndex = Config.Preference.Background.WallpaperSuitMode;
            CheckBackgroundColorful.Checked = Config.Preference.Background.BackgroundColorful;
            var autoPauseVideo = Config.Preference.Background.AutoPauseVideo;
            CheckAutoPauseVideo.Checked = autoPauseVideo;
            if (ModVideoBack.IsGaming)
                if (autoPauseVideo)
                    BtnBackgroundRefresh.IsEnabled = false;

            BackgroundRefresh(false, false);

            // 标题栏
            ((MyRadioBox)FindName("RadioLogoType" + (int)Config.Preference.WindowTitleType))
                .Checked = true;
            CheckLogoLeft.Visibility = RadioLogoType0.Checked ? Visibility.Visible : Visibility.Collapsed;
            PanLogoText.Visibility = RadioLogoType2.Checked ? Visibility.Visible : Visibility.Collapsed;
            PanLogoChange.Visibility = RadioLogoType3.Checked ? Visibility.Visible : Visibility.Collapsed;
            TextLogoText.Text = Config.Preference.WindowTitleCustomText;
            CheckLogoLeft.Checked = Config.Preference.TopBarLeftAlign;

            // 主页
            try
            {
                ComboCustomPreset.SelectedIndex = Config.Preference.Homepage.SelectedPreset;
            }
            catch
            {
                Config.Preference.Homepage.SelectedPresetConfig.Reset();
            }

            ((MyRadioBox)FindName("RadioCustomType" + Config.Preference.Homepage.Type)).Checked = true;
            TextCustomNet.Text = Config.Preference.Homepage.CustomUrl;
            ModSetup.UiCustomType(Config.Preference.Homepage.Type);

        }
        catch (NullReferenceException ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Setup.Ui.Error.ConfigReset"),
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Ui.Error.ConfigReset"));
            Reset();
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Setup.Ui.Error.LoadFailed"),
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Ui.Error.LoadFailed"));
        }
    }

    // 初始化
    public void Reset()
    {
        try
        {
            Config.Preference.Reset();
            ModBase.Log("[Setup] 已初始化个性化设置！");
            HintService.Hint(Lang.Text("Setup.Ui.Initialized"), HintType.Success, false);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Setup.Ui.Error.InitFailed"),
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Ui.Error.InitFailed"));
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void SliderChange(object senderRaw, bool user)
    {
        var sender = (MySlider)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.Value);
    }

    private void ComboChange(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.SelectedIndex);
    }

    private void CheckBoxChange(object senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.Checked);
    }

    private void TextBoxChange(object senderRaw, RoutedEventArgs e)
    {
        var sender = (MyTextBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.Text);
    }

    private void RadioBoxChange(object senderRaw, ModBase.RouteEventArgs e)
    {
        var sender = (MyRadioBox)senderRaw;
        var gotCfg = sender.Tag?.ToString()?.Split("/") ?? Array.Empty<string>();
        if (ModAnimation.AniControlEnabled == 0 && gotCfg.Length >= 2)
            SetByTag(gotCfg[0], int.Parse(gotCfg[1]));
    }

    private static void SetByTag(string tag, object value)
    {
        ConfigService.TrySetValue(tag, value);
    }

    private void ComboFontChange(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled == 0) Config.Preference.Font = ComboUiFont.SelectedFontTag;
    }

    private void ComboMotdFontChange(object sender, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniControlEnabled == 0) Config.Preference.MotdFont = ComboUiMotdFont.SelectedFontTag;
    }

    // 背景图片
    private void BtnUIBgOpen_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenExplorer(ModBase.exePath + @"PCL\Pictures\");
    }

    private void BtnBackgroundRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        BackgroundRefresh(true, true);
    }

    public void BackgroundRefreshUI(bool show, int count)
    {
        if (PanBackgroundOpacity is null)
            return;
        if (show)
        {
            PanBackgroundOpacity.Visibility = Visibility.Visible;
            PanBackgroundBlur.Visibility = Visibility.Visible;
            PanBackgroundSuit.Visibility = Visibility.Visible;
            BtnBackgroundClear.Visibility = Visibility.Visible;
            CheckAutoPauseVideo.Visibility = Visibility.Visible;
            CardBackground.Title = Lang.Text("Setup.Ui.Background.TitleWithCount", count);
        }
        else
        {
            PanBackgroundOpacity.Visibility = Visibility.Collapsed;
            PanBackgroundBlur.Visibility = Visibility.Collapsed;
            PanBackgroundSuit.Visibility = Visibility.Collapsed;
            BtnBackgroundClear.Visibility = Visibility.Collapsed;
            CheckAutoPauseVideo.Visibility = Visibility.Collapsed;
            CardBackground.Title = Lang.Text("Setup.Ui.Background.TitleDefault");
        }

        CardBackground.TriggerForceResize();
    }

    private void BtnBackgroundClear_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModMain.MyMsgBox(Lang.Text("Setup.Ui.Background.Clear.Confirm.Message"),
                Lang.Text("Common.Dialog.Warning"), button2: Lang.Text("Common.Action.Cancel"),
                isWarn: true) == 1)
        {
            ModBase.DeleteDirectory(ModBase.exePath + @"PCL\Pictures");
            BackgroundRefresh(false, true);
            HintService.Hint(Lang.Text("Setup.Ui.Background.Clear.Success"), HintType.Success);
        }
    }

    /// <summary>
    ///     刷新背景图片及设置页 UI。
    /// </summary>
    /// <param name="isHint">是否显示刷新提示。</param>
    /// <param name="refresh">是否刷新图片显示。</param>
    public static void BackgroundRefresh(bool isHint, bool refresh)
    {
        try
        {
            // 获取可用的图片文件
            Directory.CreateDirectory(ModBase.exePath + @"PCL\Pictures\");
            var pic = ModBase.EnumerateFiles(ModBase.exePath + @"PCL\Pictures\").Where(file =>
                    !(file.Extension.Equals(".ini", StringComparison.OrdinalIgnoreCase) ||
                      file.Extension.Equals(".db", StringComparison.OrdinalIgnoreCase))).Select(file => file.FullName)
                .ToList();

            // 视频加载异常处理

            EventHandler<ExceptionRoutedEventArgs> videoHandler = (sender, e) =>
            {
                var videoEx = e.ErrorException;
                var videoAddress = ModMain.frmMain.VideoBack.Source.ToString();
                if (ModMain.frmMain.VideoBack.Source is not null)
                {
                    ModVideoBack.VideoStop();

                    if (videoEx.Message.Contains("0xC00D109B"))
                        ModBase.Log(
                            $"""
                             刷新背景内容失败，该视频文件可能并非 H.264（AVC）格式。
                             你可以尝试使用视频转码工具打开视频文件并设定目标格式为 H.264（AVC），然后转码该视频。
                             文件：{videoAddress}
                             """,
                            ModBase.LogLevel.Msgbox,
                            userSummary: Lang.Text("Setup.Ui.Error.BackgroundVideoUnsupported"));
                    else
                        ModBase.Log(
                            videoEx,
                            $"刷新背景内容失败（{videoAddress}）",
                            ModBase.LogLevel.Msgbox,
                            userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
                }
            };
            ModMain.frmMain.VideoBack.MediaFailed -= videoHandler;
            ModVideoBack.GamingStateChanged -= ModVideoBack.OnGamingStateChanged;
            ModVideoBack.ForcePlayChanged -= ModVideoBack.OnForcePlayChanged;
            ModVideoBack.GamingStateChanged += ModVideoBack.OnGamingStateChanged;
            ModVideoBack.ForcePlayChanged += ModVideoBack.OnForcePlayChanged;
            if (!Config.Preference.Background.AutoPauseVideo)
                ModVideoBack.ForcePlay = true;
            // 加载
            if (pic.Count == 0)
            {
                if (refresh)
                {
                    if (ModMain.frmMain.ImgBack.Visibility == Visibility.Collapsed)
                    {
                        if (isHint)
                            HintService.Hint(Lang.Text("Setup.Ui.Background.NoAvailableContent"), HintType.Error);
                    }
                    else
                    {
                        ModMain.frmMain.ImgBack.Visibility = Visibility.Collapsed;
                        if (isHint)
                            HintService.Hint(Lang.Text("Setup.Ui.Background.Cleared"), HintType.Success);
                    }
                }

                if (ModMain.frmSetupUI is not null)
                    ModMain.frmSetupUI.BackgroundRefreshUI(false, 0);
            }
            else
            {
                if (refresh)
                {
                    var address = RandomUtils.PickRandom(pic);
                    try
                    {
                        ModMain.frmMain.ImgBack.Background = null;
                        ModVideoBack.VideoStop();
                        ModBase.Log("[UI] 加载背景内容：" + address);
                        ModMain.frmMain.ImgBack.Background = new MyBitmap(address);
                        _ = Config.Preference.Background.WallpaperSuitMode;
                        ModMain.frmMain.ImgBack.Visibility = Visibility.Visible;
                        if (isHint)
                                HintService.Hint(Lang.Text("Setup.Ui.Background.Refresh.Success", ModBase.GetFileNameFromPath(address)), HintType.Success,
                                false);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            ModMain.frmMain.VideoBack.MediaFailed += videoHandler;
                            ModBase.Log(ex, "[UI] 加载背景图片失败" + address);
                            if (ModBase.modeDebug)
                                HintService.Hint(Lang.Text("Setup.Ui.Background.ImageLoadFailed", address));
                            ModMain.frmMain.ImgBack.Visibility = Visibility.Visible;
                            ModMain.frmMain.VideoBack.Source = new Uri(address, UriKind.Absolute);
                            ModVideoBack.VideoPlay();
                            if (isHint)
                            HintService.Hint(Lang.Text("Setup.Ui.Background.Refresh.Success", ModBase.GetFileNameFromPath(address)), HintType.Success,
                                    false);
                        }
                        catch (Exception playEx)
                        {
                            ModBase.Log(playEx, "播放背景内容时出现未知错误：");
                        }
                    }
                }

                if (ModMain.frmSetupUI is not null)
                    ModMain.frmSetupUI.BackgroundRefreshUI(true, pic.Count);
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "刷新背景内容时出现未知错误",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    // 顶部栏
    private void BtnLogoChange_Click(object sender, MouseButtonEventArgs e)
    {
        var fileName = SystemDialogs.SelectFile(
            Lang.Text("Setup.Ui.ImageFile.Filter"),
            Lang.Text("Setup.Ui.ImageFile.SelectTitle"));
        if (string.IsNullOrEmpty(fileName))
            return;
        try
        {
            // 拷贝文件
            File.Delete(ModBase.exePath + @"PCL\Logo.png");
            ModBase.CopyFile(fileName, ModBase.exePath + @"PCL\Logo.png");
            // 设置当前显示
            ModMain.frmMain.ImageTitleLogo.Source = null; // 防止因为 Source 属性前后的值相同而不更新 (#5628)
            ModMain.frmMain.ImageTitleLogo.Source = ModBase.exePath + @"PCL\Logo.png";
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("参数无效"))
                ModBase.Log(
                    """
                    改变标题栏图片失败，该图片文件可能并非标准格式。
                    你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。
                    """,
                    ModBase.LogLevel.Msgbox,
                    userSummary: Lang.Text("Setup.Ui.Error.TitleImageInvalidFormat"));
            else
                ModBase.Log(
                    ex,
                    "设置标题栏图片失败",
                    ModBase.LogLevel.Msgbox,
                    userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
            ModMain.frmMain.ImageTitleLogo.Source = null;
        }
    }

    private void RadioLogoType3_Check(object sender, ModBase.RouteEventArgs e)
    {
        if (!(ModAnimation.AniControlEnabled == 0 && e.raiseByMouse))
            return;
        Refresh: ;

        // 已有图片则不再选择
        if (File.Exists(ModBase.exePath + @"PCL\Logo.png"))
        {
            try
            {
                ModMain.frmMain.ImageTitleLogo.Source = null; // 防止因为 Source 属性前后的值相同而不更新 (#5628)
                ModMain.frmMain.ImageTitleLogo.Source = ModBase.exePath + @"PCL\Logo.png";
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("参数无效"))
                    ModBase.Log(
                        """
                        调整标题栏图片失败，该图片文件可能并非标准格式。
                        你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。
                        """,
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Setup.Ui.Error.TitleImageResizeInvalidFormat"));
                else
                    ModBase.Log(
                        ex,
                        "调整标题栏图片失败",
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
                ModMain.frmMain.ImageTitleLogo.Source = null;
                e.handled = true;
                try
                {
                    File.Delete(ModBase.exePath + @"PCL\Logo.png");
                }
                catch (Exception exx)
                {
                    ModBase.Log(
                        exx,
                        "清理错误的标题栏图片失败",
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
                }
            }

            return;
        }

        // 没有图片则要求选择
        var fileName = SystemDialogs.SelectFile(Lang.Text("Setup.Ui.ImageFile.Filter"), Lang.Text("Setup.Ui.ImageFile.SelectTitle"));
        if (string.IsNullOrEmpty(fileName))
        {
            ModMain.frmMain.ImageTitleLogo.Source = null;
            e.handled = true;
        }
        else
        {
            try
            {
                // 拷贝文件
                File.Delete(ModBase.exePath + @"PCL\Logo.png");
                ModBase.CopyFile(fileName, ModBase.exePath + @"PCL\Logo.png");
                goto Refresh;
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    "复制标题栏图片失败",
                    ModBase.LogLevel.Msgbox,
                    userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
            }
        }
    }

    private void BtnLogoDelete_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            File.Delete(ModBase.exePath + @"PCL\Logo.png");
            RadioLogoType1.SetChecked(true, true);
            HintService.Hint(Lang.Text("Setup.Ui.Logo.Clear.Success"), HintType.Success);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "清空标题栏图片失败",
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    // 主页

    private void BtnCustomRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.frmLaunchRight.ForceRefresh();
        HintService.Hint(Lang.Text("Setup.Ui.Homepage.Refresh.Success"), HintType.Success);
    }

    private void BtnCustomTutorial_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenWebsite("https://docs.pclc.cc/ce/customization/xaml-format");
    }

    // 主题
    private void ThemeColor_Change(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        SetByTag(sender.Tag?.ToString(), sender.SelectedIndex);
        UpdateCustomColorVisibility();
        // 配置已同步写入，直接按最新配置刷新色块预览
        RefreshThemeColorPreview();
        ThemeManager.ThemeRefresh();
    }

    /// <summary>
    /// 刷新主题色块预览：浅色/深色两列分别按对应模式的色调计算所选主题的主色。
    /// </summary>
    private void RefreshThemeColorPreview()
    {
        if (BorderLightThemeColor is not null)
            BorderLightThemeColor.Background =
                ThemeService.GetThemePreviewBrush(Config.Preference.Theme.LightColor, false);
        if (BorderDarkThemeColor is not null)
            BorderDarkThemeColor.Background =
                ThemeService.GetThemePreviewBrush(Config.Preference.Theme.DarkColor, true);
    }

    // 自定义主题色输入
    private void UpdateCustomColorVisibility()
    {
        var isCustom = ThemeService.CurrentTheme == ColorTheme.Custom;
        if (PanCustomColor is not null)
            PanCustomColor.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool _isReloadingCustomColor;

    /// <summary>
    /// 把配置中的自定义颜色与渐变设置回填到控件。
    /// </summary>
    private void RefreshCustomColorPickers()
    {
        if (PickerCustomColor is null) return;
        _isReloadingCustomColor = true;
        try
        {
            PickerCustomColor.SetColor(_ParseColor(Config.Preference.Theme.CustomColor, Colors.DeepSkyBlue));
            PickerCustomColor2.SetColor(_ParseColor(Config.Preference.Theme.CustomColor2, Colors.MediumPurple));
            CheckGradient.Checked = !string.IsNullOrWhiteSpace(Config.Preference.Theme.CustomColor2);
            ComboGradientDirection.SelectedIndex = (int)Config.Preference.Theme.GradientDirection;
            SliderGradientRatio.Value = Config.Preference.Theme.GradientRatio;
            var gradientEnabled = CheckGradient.Checked == true;
            ComboGradientDirection.IsEnabled = gradientEnabled;
            SliderGradientRatio.IsEnabled = gradientEnabled;
        }
        finally
        {
            _isReloadingCustomColor = false;
        }
    }

    /// <summary>
    /// 解析十六进制颜色，失败时回退到给定颜色。
    /// </summary>
    private static Color _ParseColor(string hex, Color fallback)
    {
        var text = (hex ?? "").Trim().TrimStart('#');
        if (text.Length != 6) return fallback;
        foreach (var c in text)
            if (!Uri.IsHexDigit(c))
                return fallback;
        return Color.FromRgb(
            (byte)Convert.ToInt32(text[..2], 16),
            (byte)Convert.ToInt32(text.Substring(2, 2), 16),
            (byte)Convert.ToInt32(text.Substring(4, 2), 16));
    }

    private static string _ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>
    /// 主色取色器改变。
    /// </summary>
    private void PickerCustomColor_ColorChanged(object sender, EventArgs e)
    {
        if (_isReloadingCustomColor) return;
        Config.Preference.Theme.CustomColor = _ToHex(PickerCustomColor.Color);
        _AfterCustomColorChanged();
    }

    /// <summary>
    /// 渐变终点色取色器改变。
    /// </summary>
    private void PickerCustomColor2_ColorChanged(object sender, EventArgs e)
    {
        if (_isReloadingCustomColor) return;
        // 只有启用渐变时才写入配置，否则保持为空（表示不使用渐变）
        if (CheckGradient.Checked == true)
            Config.Preference.Theme.CustomColor2 = _ToHex(PickerCustomColor2.Color);
        _AfterCustomColorChanged();
    }

    private void _AfterCustomColorChanged()
    {
        // 配置变更事件会自动刷新资源，但仅在当前主题为自定义时；否则手动触发一次
        if (ThemeService.CurrentTheme != ColorTheme.Custom)
            ThemeManager.ThemeRefresh();
        RefreshThemeColorPreview();
    }

    private void Gradient_Change(object sender, bool user)
    {
        if (_isReloadingCustomColor) return;
        var enabled = CheckGradient.Checked == true;
        ComboGradientDirection.IsEnabled = enabled;
        SliderGradientRatio.IsEnabled = enabled;
        Config.Preference.Theme.CustomColor2 = enabled ? _ToHex(PickerCustomColor2.Color) : "";
        _AfterCustomColorChanged();
    }

    private void GradientDirection_Change(object sender, SelectionChangedEventArgs e)
    {
        if (_isReloadingCustomColor) return;
        var index = ComboGradientDirection.SelectedIndex;
        if (index < 0) return;
        Config.Preference.Theme.GradientDirection = (ThemeGradientDirection)index;
        _AfterCustomColorChanged();
    }

    private void GradientRatio_Change(object sender, bool user)
    {
        if (_isReloadingCustomColor) return;
        Config.Preference.Theme.GradientRatio = SliderGradientRatio.Value;
        _AfterCustomColorChanged();
    }

    // 自定义微软 Client ID 输入
    private bool _isReloadingMsClientId;

    private void MsClientId_TextChanged(object sender, RoutedEventArgs e)
    {
        if (_isReloadingMsClientId)
            return;
        var text = TextMsClientId?.Text?.Trim();
        Config.Preference.Theme.MsClientId = text ?? "";
    }

    // 滑动条
    private void SliderLoad()
    {
        SliderLauncherOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number(Math.Round(40 + Convert.ToDouble(v) * 0.1d) / 100d, "P0"));
        SliderBackgroundOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number(Math.Round(Convert.ToDouble(v) * 0.1d) / 100d, "P0"));
        SliderBackgroundBlur.getHintText = new Func<object, object>(v => Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(v), "N0")));
        SliderBlurValue.getHintText = new Func<object, object>(v => Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(v), "N0")));
        SliderBlurSamplingRate.getHintText = new Func<object, object>(v => Lang.Number(Convert.ToDouble(v) / 100d, "P0"));
        // 渐变占比：直接以百分比显示，100% 等于纯色
        SliderGradientRatio.getHintText = new Func<object, object>(v => Lang.Number(Convert.ToDouble(v), "N0") + "%");
    }

    #region 功能隐藏（已移除）

    /// <summary>
    ///     功能隐藏设置已移除。保留该属性仅供其他页面判断“是否强制显示被隐藏项”，永远为 false。
    /// </summary>
    public static bool HiddenForceShow { get; set; }

    /// <summary>
    ///     功能隐藏设置已移除，保留空实现以兼容原有调用点。
    /// </summary>
    public static void HiddenRefresh()
    {
    }

    #endregion
}
