using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;

namespace PCL;

/// <summary>
///     设置 -「管理」页：游戏与下载管理 + 启动器杂项（系统 / 网络 / 调试）+ 查看日志。
///     原「杂项」与「查看日志」两个独立页面已并入本页。
/// </summary>
public partial class PageSetupGameManage
{
    private new bool isLoaded;

    public PageSetupGameManage()
    {
        InitializeComponent();
        Loaded += PageSetupSystem_Loaded;
    }

    private void PageSetupSystem_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();
        LoadLogList(); // 日志列表每次进入都刷新

        // 非重复加载部分
        if (isLoaded)
            return;
        isLoaded = true;

        ModAnimation.AniControlEnabled += 1;
        Reload();
        SliderLoad();

        if (!Lang.IsChineseMainland)
        {
            TextFilenameFormat.Visibility = Visibility.Collapsed;
            ComboDownloadTranslateV2.Visibility = Visibility.Collapsed;
            TextModManageStyle.Visibility = Visibility.Collapsed;
            ComboModLocalNameStyle.Visibility = Visibility.Collapsed;

            RowFilenameFormat.Height = new GridLength(0);
            RowFilenameFormatGap.Height = new GridLength(0);
            RowModManageStyle.Height = new GridLength(0);
            RowModManageStyleGap.Height = new GridLength(0);
        }

        ModAnimation.AniControlEnabled -= 1;
    }

    public void Reload()
    {
        // 下载
        SliderDownloadThread.Value = Config.Download.ThreadLimit;
        SliderDownloadSpeed.Value = Config.Download.SpeedLimit;
        ComboDownloadSource.SelectedIndex = Config.Download.FileSource;
        ComboDownloadVersion.SelectedIndex = Config.Download.VersionListSource;
        CheckDownloadAutoSelectVersion.Checked = Config.Download.AutoSelectInstance;
        CheckFixAuthlib.Checked = Config.Download.FixAuthLib;

        // Mod 与整合包
        ComboDownloadTranslateV2.SelectedIndex = Config.Download.Comp.NameFormatV2;
        ComboDownloadMod.SelectedIndex = Config.Download.Comp.CompSourceSolution;
        ComboModLocalNameStyle.SelectedIndex = Config.Download.Comp.UiCompNameSolution;
        ComboDownloadQuickBehavior.SelectedIndex = Config.Download.Comp.QuickDownloadBehavior;
        CheckDownloadIgnoreQuilt.Checked = Config.Download.Comp.IgnoreQuilt;
        CheckDownloadAutoInstallDependencies.Checked = Config.Download.Comp.AutoInstallDependencies;
        CheckDownloadClipboard.Checked = Config.Download.Comp.ReadClipboard;

        // Minecraft 更新提示
        CheckUpdateRelease.Checked = Config.Tool.ReleaseNotification;
        CheckUpdateSnapshot.Checked = Config.Tool.SnapshotNotification;

        // 辅助设置
        CheckHelpLauncherLanguage.Checked = Config.Tool.AutoChangeLanguage;

        // 系统（原杂项页）
        CheckSystemDisableHardwareAcceleration.Checked = Config.System.DisableHardwareAcceleration;
        SliderAniFPS.Value = Config.System.AnimationFpsLimit;
        SliderMaxLog.Value = Config.System.MaxGameLog;
        CheckSystemTelemetry.Checked = Config.System.Telemetry;

        // 网络（原杂项页）
        TextSystemHttpProxy.Text = Config.Network.HttpProxy.CustomAddress;
        TextSystemHttpProxyCustomUsername.Text = Config.Network.HttpProxy.CustomUsername;
        TextSystemHttpProxyCustomPassword.Text = Config.Network.HttpProxy.CustomPassword;
        ((MyRadioBox)FindName($"RadioHttpProxyType{Config.Network.HttpProxy.Type}")).SetChecked(true, false);
        CheckNetDohEnable.Checked = Config.Network.EnableDoH;

        // 调试选项（原杂项页）
        SliderDebugAnim.Value = Config.Debug.AnimationSpeed;
        CheckDebugSkipCopy.Checked = Config.Debug.DontCopy;
        CheckDebugMode.Checked = Config.Debug.Enabled;
        CheckDebugDelay.Checked = Config.Debug.AddRandomDelay;
    }

    // 初始化
    public void Reset()
    {
        try
        {
            Config.Download.Reset();
            Config.Tool.Reset();
            Config.System.Reset();
            Config.Network.Reset();
            Config.Debug.Reset();
            ModBase.Log("[Setup] 已初始化「管理」页设置");
            HintService.Hint(Lang.Text("Setup.GameManage.Initialized"), HintType.Success, false);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Setup.GameManage.Error.InitFailed"),
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.GameManage.Error.InitFailed"));
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void CheckBoxChange(object senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw;
        if (ModAnimation.AniControlEnabled == 0)
            SetByTag(sender.Tag?.ToString(), sender.Checked);
    }

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

    private void RadioBoxChange(object senderRaw, ModBase.RouteEventArgs e)
    {
        var sender = (MyRadioBox)senderRaw;
        var gotCfg = sender.Tag?.ToString()?.Split("/") ?? Array.Empty<string>();
        if (ModAnimation.AniControlEnabled == 0 && gotCfg.Length >= 2)
            SetByTag(gotCfg[0], int.Parse(gotCfg[1]));
    }

    private static void SetByTag(string tag, object value)
        => ConfigService.TrySetValue(tag, value);

    // 滑动条
    private void SliderLoad()
    {
        SliderDownloadThread.getHintText = new Func<object, object>(v => (int)v + 1);
        SliderDownloadSpeed.getHintText = new Func<object, object>(v =>
        {
            int value = (int)v;
            switch (value)
            {
                case <= 14:
                    return Lang.Number((value + 1) * 0.1d, "N1") + " MiB/s";
                case <= 31:
                    return Lang.Number((value - 11) * 0.5d, "N1") + " MiB/s";
                case <= 41:
                    return Lang.Number(value - 21, "N0") + " MiB/s";
                default:
                    return Lang.Text("Setup.GameManage.Download.Unlimited");
            }
        });

        // 系统（原杂项页）
        SliderDebugAnim.getHintText = new Func<object, object>(v =>
            (int)v > 29
                ? Lang.Text("Common.Action.Close")
                : Lang.Number(Math.Round(Convert.ToDouble(v) / 10 + 0.1d, 1), "N1") + "x");
        SliderAniFPS.getHintText = new Func<object, string>(v => Lang.Number(Convert.ToInt32(v) + 1, "N0") + " FPS");
        // y = 10x + 50 (0 <= x <= 5, 50 <= y <= 100)
        // y = 50x - 150 (5 < x <= 13, 100 < y <= 500)
        // y = 100x - 800 (13 < x <= 28, 500 < y <= 2000)
        SliderMaxLog.getHintText = new Func<object, object>(v =>
        {
            var val = Convert.ToInt32(v);
            return val switch
            {
                <= 5 => val * 10 + 50,
                <= 13 => val * 50 - 150,
                <= 28 => val * 100 - 800,
                _ => Lang.Text("Setup.Misc.System.MaxLogLines.Unlimited")
            };
        });
    }

    private void SliderDownloadThread_PreviewChange(object sender, ModBase.RouteEventArgs e)
    {
        if (SliderDownloadThread.Value < 100)
            return;
        if (!States.Hint.LargeDownloadThread)
        {
            States.Hint.LargeDownloadThread = true;
            ModMain.MyMsgBox(
                Lang.Text("Setup.GameManage.Download.Threads.TooManyWarning.Message"),
                Lang.Text("Common.Dialog.Warning"),
                Lang.Text("Setup.GameManage.Download.Threads.TooManyWarning.Confirm"), isWarn: true);
        }
    }

    #region 系统 / 网络 / 调试（原「杂项」页）

    // 网络
    private void ApplyHttpProxyBtn_OnClicked(object sender, MouseButtonEventArgs e)
    {
        Config.Network.HttpProxy.CustomAddress = TextSystemHttpProxy.Text;
        Config.Network.HttpProxy.CustomUsername = TextSystemHttpProxyCustomUsername.Text;
        Config.Network.HttpProxy.CustomPassword = TextSystemHttpProxyCustomPassword.Text;
    }

    // 硬件加速
    private void Check_DisableHardwareAcceleration(object _, bool __)
    {
        HintService.Hint(Lang.Text("Setup.Misc.HardwareAcceleration.RestartNotice"));
    }

    // 调试模式
    private void CheckDebugMode_Change(object _, bool __)
    {
        if (ModAnimation.AniControlEnabled == 0)
            HintService.Hint(Lang.Text("Setup.Misc.Debug.Mode.Hint"), log: false);
    }

    private void CheckDebugMode_OnChange(object sender, bool user)
    {
        CheckBoxChange(sender, user);
        CheckDebugMode_Change(sender, user);
    }

    private void CheckSystemDisableHardwareAcceleration_OnChange(object sender, bool user)
    {
        CheckBoxChange(sender, user);
        Check_DisableHardwareAcceleration(sender, user);
    }

    #region 导出 / 导入设置

    private void BtnSystemSettingExp_Click(object sender, MouseButtonEventArgs e)
    {
        var savePath =
            SystemDialogs.SelectSaveFile(Lang.Text("Setup.Misc.System.ExportSettings.SaveTitle"), "PCL 全局配置.json", Lang.Text("Setup.Misc.System.ExportSettings.Filter"), ModBase.exePath);
        if (string.IsNullOrWhiteSpace(savePath))
            return;
        File.Copy(ConfigService.SharedConfigPath, savePath, true);
        HintService.Hint(Lang.Text("Setup.Misc.System.ExportSettings.Success"), HintType.Success);
        ModBase.OpenExplorer(savePath);
    }

    private void BtnSystemSettingImp_Click(object sender, MouseButtonEventArgs e)
    {
        var sourcePath = SystemDialogs.SelectFile(Lang.Text("Setup.Misc.System.ExportSettings.Filter"), Lang.Text("Setup.Misc.System.ImportSettings.SelectTitle"));
        if (string.IsNullOrWhiteSpace(sourcePath))
            return;
        File.Copy(sourcePath, ConfigService.SharedConfigPath, true);
        ModMain.MyMsgBox(Lang.Text("Setup.Misc.System.ImportSettings.Success.Message"), button1: Lang.Text("Setup.Misc.System.ImportSettings.Success.Restart"), forceWait: true);
        Process.Start(new ProcessStartInfo(Basics.ExecutablePath));
        FormMain.EndProgramForce();
    }

    #endregion

    #endregion

    #region 查看日志（原「查看日志」页）

    private static string LogDirectory => LogService.Logger.Configuration.StoreFolder;

    private static List<string> CurrentLogs
    {
        get
        {
            var logs = LogService.Logger.CurrentLogFiles;
            return logs.Select(item => Path.GetFullPath(item)).ToList();
        }
    }

    public void LoadLogList()
    {
        if (PanLogList is null)
            return;
        PanLogList.Children.Clear();
        if (!Directory.Exists(LogDirectory))
            return;
        var current = CurrentLogs;
        var logFiles = Directory.GetFiles(LogDirectory).OrderByDescending(f => File.GetLastWriteTime(f)).ToArray();
        foreach (var item in logFiles)
        {
            var fullPath = Path.GetFullPath(item);
            var title = Path.GetFileName(item);
            if (title.StartsWith("Launch"))
            {
                title = title.Substring(7, title.Length - 11);
                if (DateTime.TryParseExact(title, "yyyy-M-d-HHmmssfff", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dt))
                    title = Lang.Date(dt, "G");
                if (current.Any(log => log.Equals(fullPath)))
                    title = Lang.Text("Setup.Log.CurrentSuffix", title);
            }
            else if (title.StartsWith("LastPending"))
            {
                title = title.Substring(11, title.Length - 15);
                title = title.Length > 1
                    ? Lang.Text("Setup.Log.TempStored", title.Substring(1))
                    : Lang.Text("Setup.Log.TempUnoutput");
            }

            var ele = new MyListItem
            {
                Type = MyListItem.CheckType.Clickable,
                Title = title,
                Info = fullPath,
                Tag = fullPath
            };
            ele.Click += (s, _) => Basics.OpenPath((string)((MyListItem)s).Tag);
            PanLogList.Children.Add(ele);
        }
    }

    private static void ExportLog(IEnumerable<string> sourceFiles)
    {
        var filter = Lang.Text("Setup.Log.ExportFilter");
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var baseName = "PCL_Logs_" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var tempDirName = baseName + ".tmp";
        var fileName = baseName + ".zip";
        var selectedPath = SystemDialogs.SelectSaveFile(Lang.Text("Setup.Log.ExportSaveTitle"), fileName, filter, desktopPath);
        if (string.IsNullOrEmpty(selectedPath))
            return;
        try
        {
            Directory.CreateDirectory(tempDirName);
            if (File.Exists(selectedPath))
                File.Delete(selectedPath);
            using (var zip = ZipFile.Open(selectedPath, ZipArchiveMode.Create))
            {
                foreach (var item in sourceFiles)
                {
                    var itemFileName = Path.GetFileName(item);
                    var tempPath = Path.Combine(tempDirName, itemFileName);
                    File.Copy(item, tempPath);
                    zip.CreateEntryFromFile(tempPath, itemFileName, CompressionLevel.Fastest);
                    File.Delete(tempPath);
                }
            }

            HintService.Hint(Lang.Text("Setup.Log.ExportSuccess"), HintType.Success);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Setup.Log.ExportFailed"),
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Setup.Log.ExportFailed"));
        }
        finally
        {
            if (Directory.Exists(tempDirName))
                Directory.Delete(tempDirName);
        }
    }

    private void ButtonOpenDir_OnClick(object sender, MouseButtonEventArgs e)
    {
        Basics.OpenPath(LogDirectory);
    }

    private void ButtonClean_OnClick(object sender, MouseButtonEventArgs e)
    {
        var r = ModMain.MyMsgBox(Lang.Text("Setup.Log.Clear.Confirm.Message"), Lang.Text("Setup.Log.Clear.Confirm.Title"), Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel"), isWarn: true);
        if (r != 1)
            return;
        var currentSet = new HashSet<string>(CurrentLogs);
        foreach (var item in Directory.GetFiles(LogDirectory))
            if (!currentSet.Contains(item))
                File.Delete(item);
        HintService.Hint(Lang.Text("Setup.Log.Clear.Success"), HintType.Success);
        LoadLogList();
    }

    private void ButtonExportAll_OnClick(object sender, MouseButtonEventArgs e)
    {
        ExportLog(Directory.GetFiles(LogDirectory));
    }

    private void ButtonExport_OnClick(object sender, MouseButtonEventArgs e)
    {
        var pendingLogs = Array.FindAll(Directory.GetFiles(LogDirectory),
            s => s.IsMatch(RegexPatterns.LastPendingLogPath));
        ExportLog(CurrentLogs.Concat(pendingLogs));
    }

    #endregion
}
