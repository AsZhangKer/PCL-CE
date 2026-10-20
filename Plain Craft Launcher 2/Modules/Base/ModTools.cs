using System.Diagnostics;
using System.Globalization;
using System.IO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

/// <summary>
///     原「百宝箱」页面被移除后，保留其被自定义主页事件与占位符引用的静态功能。
/// </summary>
public static class ModTools
{
    /// <summary>
    ///     触发一次自定义文件下载（供 {launch} 类事件与 Custom.xaml 使用）。
    /// </summary>
    public static void StartCustomDownload(string url, string fileName, string folder = null, string userAgent = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.CustomDownload.SelectLocation"), fileName);
                if (!folder.Contains(@"\")) return;
                if (folder.EndsWith(fileName)) folder = folder[..^fileName.Length];
            }

            folder = folder.Replace("/", @"\").TrimEnd('\\') + @"\";
            try
            {
                Directory.CreateDirectory(folder);
                ModBase.CheckPermissionWithException(folder);
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    $"访问文件夹失败（{folder}）",
                    ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
                return;
            }

            ModBase.Log("[Download] 自定义下载文件名：" + fileName);
            ModBase.Log("[Download] 自定义下载文件目标：" + folder);
            var uuid = ModBase.GetUuid();
            ModLoader.LoaderBase loaderdownload;
            if (new HttpValidator().Validate(url).IsValid)
                loaderdownload = new LoaderDownload(Lang.Text("Tools.Test.CustomDownload.LoaderName", fileName),
                    new List<DownloadFile> { new(new[] { url }, folder + fileName, null, true, userAgent) });
            else // UNC 路径
                loaderdownload = new LoaderDownloadUnc(Lang.Text("Tools.Test.CustomDownload.LoaderName", fileName),
                    new Tuple<string, string>(url, folder + fileName));
            var loaderCombo =
                new ModLoader.LoaderCombo<int>(Lang.Text("Tools.Test.CustomDownload.LoaderTitle", uuid),
                    new[] { loaderdownload })
                    { OnStateChanged = a => DownloadState((ModLoader.LoaderCombo<int>)a) };
            loaderCombo.Start();
            ModLoader.LoaderTaskbarAdd(loaderCombo);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "开始自定义下载失败",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
        }
    }

    private static void DownloadState(ModLoader.LoaderCombo<int> loader)
    {
        try
        {
            switch (loader.State)
            {
                case ModBase.LoadState.Finished:
                    HintService.Hint(Lang.Text("Tools.Test.CustomDownload.Finished", loader.name), HintType.Success);
                    Console.Beep();
                    break;
                case ModBase.LoadState.Failed:
                    ModBase.Log(
                        loader.Error,
                        $"{loader.name}失败",
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
                    Console.Beep();
                    break;
                case ModBase.LoadState.Aborted:
                    HintService.Hint(Lang.Text("Tools.Test.CustomDownload.Aborted", loader.name));
                    break;
            }
        }
        catch
        {
            // 忽略界面提示时的异常
        }
    }

    /// <summary>
    ///     今日人品（按日期固定种子，同一天结果一致）。
    /// </summary>
    public static void Jrrp()
    {
        var random = new Random(GenerateDailySeed());
        var luckValue = random.Next(0, 101);
        var rating = GetRating(luckValue);
        var title = Lang.Text("Tools.Test.Luck.MsgboxTitle", Lang.Date(DateTime.Now, "d"));

        if (luckValue >= 60)
            ModMain.MyMsgBox(Lang.Text("Tools.Test.Luck.MessageGood", luckValue, rating), title);
        else
            ModMain.MyMsgBox(Lang.Text("Tools.Test.Luck.MessageBad", luckValue, rating), title, isWarn: luckValue <= 30);
    }

    /// <summary>
    ///     清理游戏与启动器产生的垃圾文件。
    /// </summary>
    public static void RubbishClear()
    {
        ModBase.RunInNewThread(() =>
        {
            try
            {
                if (ModWatcher.hasRunningMinecraft || ModLaunch.mcLaunchLoader.State == ModBase.LoadState.Loading)
                {
                    HintService.Hint(Lang.Text("Tools.Test.Clean.CloseGameFirst"));
                    return;
                }

                if (ModNet.HasDownloadingTask())
                {
                    HintService.Hint(Lang.Text("Tools.Test.Clean.WaitForDownload"));
                    return;
                }

                if (!ModFolder.mcFolderList.Any()) ModFolder.mcFolderListLoader.Start();
                if (States.Hint.CleanJunkFile <= 2)
                {
                    if (ModMain.MyMsgBox(
                            Lang.Text("Tools.Test.Clean.ConfirmMessage"),
                            Lang.Text("Tools.Test.Clean.ConfirmTitle"),
                            Lang.Text("Common.Action.Confirm"),
                            Lang.Text("Common.Action.Cancel")
                        ) == 2) return;
                    States.Hint.CleanJunkFile += 1;
                }

                var num = 0;
                var cleanMcFolderList = new List<DirectoryInfo>();
                if (!ModFolder.mcFolderList.Any()) ModFolder.mcFolderListLoader.WaitForExit();
                foreach (var mcFolder in ModFolder.mcFolderList)
                {
                    cleanMcFolderList.Add(new DirectoryInfo(mcFolder.Location));
                    var dirInfo = new DirectoryInfo(mcFolder.Location + "versions");
                    if (dirInfo.Exists)
                        foreach (var item in dirInfo.EnumerateDirectories())
                            cleanMcFolderList.Add(item);
                }

                foreach (var dirInfo in cleanMcFolderList)
                {
                    var basePath = dirInfo.FullName + (dirInfo.FullName.EndsWith(@"\") ? "" : @"\");
                    num += ModBase.DeleteDirectory(basePath + @"crash-reports\", true);
                    num += ModBase.DeleteDirectory(basePath + @"logs\", true);
                    foreach (var fileInfo in dirInfo.EnumerateFiles("*"))
                        if (fileInfo.Name.StartsWith("hs_err_pid") || fileInfo.Name.EndsWith(".log") ||
                            fileInfo.Name == "WailaErrorOutput.txt")
                        {
                            fileInfo.Delete();
                            num += 1;
                        }

                    foreach (var dirInfo2 in dirInfo.EnumerateDirectories())
                        if (dirInfo2.Name.EndsWith("-natives") || dirInfo2.Name == "natives-windows-x86_64")
                            num += ModBase.DeleteDirectory(dirInfo2.FullName, true);
                }

                num += ModBase.DeleteDirectory(ModBase.pathTemp, true);
                num += ModBase.DeleteDirectory(Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL"), true);
                if (num != 0)
                {
                    ModMain.MyMsgBox(Lang.Text("Tools.Test.Clean.ClearedMessage", num),
                        Lang.Text("Tools.Test.Clean.Cleared"), Lang.Text("Common.Action.Confirm"), "", "", false, true, true);
                    Process.Start(new ProcessStartInfo(Basics.ExecutablePath));
                    FormMain.EndProgramForce();
                }
                else
                {
                    HintService.Hint(Lang.Text("Tools.Test.Clean.NoFiles"));
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    "清理垃圾失败",
                    ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
            }
        }, "Rubbish Clear");
    }

    public static string GetRandomCave() => Lang.Text("Tools.Test.CeNotice");

    public static string GetRandomHint() => Lang.Text("Tools.Test.CeNotice");

    public static string GetRandomPresetHint() => Lang.Text("Tools.Test.CeNotice");

    public static int GenerateDailySeed()
    {
        var datePart = DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return DJB2Hash(datePart + Identify.LauncherId);
    }

    private static int DJB2Hash(string str)
    {
        var hash = 5381L;
        const long prime = 33L;
        foreach (var c in str)
        {
            long charValue = c;
            hash = (hash * prime + charValue) % 0x100000000L;
        }

        return (int)(hash & 0x7FFFFFFFL);
    }

    public static string GetRating(int luckValue)
    {
        var key = luckValue switch
        {
            100 => "Tools.Test.Luck.Rating100",
            >= 95 => "Tools.Test.Luck.Rating95",
            >= 90 => "Tools.Test.Luck.Rating90",
            >= 60 => "Tools.Test.Luck.Rating60",
            >= 40 => "Tools.Test.Luck.Rating40",
            >= 30 => "Tools.Test.Luck.Rating30",
            >= 10 => "Tools.Test.Luck.Rating10",
            _ => "Tools.Test.Luck.Rating0"
        };

        return Lang.Text(key);
    }
}
