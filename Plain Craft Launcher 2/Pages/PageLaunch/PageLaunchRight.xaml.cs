using System.IO;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageLaunchRight : IRefreshable
{
    public PageLaunchRight()
    {
        InitializeComponent();
        onlineLoader = new ModLoader.LoaderTask<string, int>(
            Lang.Text("Launch.Homepage.Task.Download"),
            OnlineLoaderSub)
        {
            reloadTimeout = 10 * 60 * 1000
        };
        Loaded += (_, _) => Init();
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => _DisposeHomepageLiveWatcher();
        Loaded += (_, _) => RefreshInstanceList();
        ModInstanceList.mcInstanceListLoader.LoadingStateChanged += (_, _) => RefreshInstanceList();
        ModFolder.mcFolderListLoader.LoadingStateChanged += (_, _) => RefreshInstanceList();
    }

    private void RefreshInstanceList()
    {
        ModBase.RunInUi(() =>
        {
            // 首屏确认 Minecraft 文件夹尚未就绪时，等待 PageLaunchLeft 流程确定后再加载
            if (string.IsNullOrEmpty(ModFolder.mcFolderSelected))
                return;
            // 确保实例列表已加载（首屏未触发过加载时主动加载）
            if (ModInstanceList.mcInstanceListLoader.State != ModBase.LoadState.Finished &&
                ModInstanceList.mcInstanceListLoader.State != ModBase.LoadState.Loading)
            {
                ModLoader.LoaderFolderRun(ModInstanceList.mcInstanceListLoader, ModFolder.mcFolderSelected,
                    ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
                return;
            }
            UpdateInstanceListUI();
        });
    }

    #region 主页实例列表（按文件夹分组）

    private object _lastInstanceListSource;
    private bool _instanceListBuilding;
    private int _lastFolderCount = -1;

    /// <summary>多选模式下已选中的实例路径（忽略大小写）。</summary>
    private readonly HashSet<string> _multiSelected = new(StringComparer.OrdinalIgnoreCase);

    private bool _multiSelectMode;

    /// <summary>进入多选的时刻，用于吞掉长按松手时触发的那一次勾选。</summary>
    private DateTime _multiSelectEnteredAt = DateTime.MinValue;

    private void UpdateInstanceListUI()
    {
        try
        {
            var selectedPath = ModInstanceList.McMcInstanceSelected?.PathInstance ?? "";
            if (StackInstanceList.Children.Count == 0 ||
                !ReferenceEquals(_lastInstanceListSource, ModInstanceList.mcInstanceList) ||
                _lastFolderCount != ModFolder.mcFolderList.Count)
            {
                BuildInstanceListItems();
                return;
            }

            // 只更新勾选状态，不重建控件
            foreach (var item in StackInstanceList.Children.OfType<MyListItem>())
            {
                if (item.Tag is not McInstance instance) continue;
                var shouldCheck = _multiSelectMode
                    ? _multiSelected.Contains(instance.PathInstance)
                    : (instance.PathInstance ?? "") == selectedPath;
                if (item.Checked != shouldCheck)
                    item.SetChecked(shouldCheck, false, false);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新主页实例列表失败");
        }
    }

    /// <summary>
    ///     重建实例列表：按 Minecraft 文件夹分组，每个实例占一行。
    /// </summary>
    private void BuildInstanceListItems()
    {
        if (_instanceListBuilding) return;
        _instanceListBuilding = true;
        try
        {
            StackInstanceList.Children.Clear();
            _lastInstanceListSource = ModInstanceList.mcInstanceList;
            _lastFolderCount = ModFolder.mcFolderList.Count;

            var keyword = (TextSearchInstance?.Text ?? "").Trim();
            var total = 0;
            foreach (var folder in ModFolder.mcFolderList.ToList())
            {
                var instances = _GetInstances(folder);
                if (keyword.Length > 0)
                    instances = instances
                        .Where(i => (i.Name ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                // 搜索时隐藏没有命中项的文件夹，减少干扰
                if (keyword.Length > 0 && instances.Count == 0) continue;

                StackInstanceList.Children.Add(_CreateFolderHeader(folder, instances.Count));
                foreach (var instance in instances)
                {
                    StackInstanceList.Children.Add(_CreateInstanceItem(instance));
                    total++;
                }
            }

            LabInstanceEmpty.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
            PanMultiSelect.Visibility = _multiSelectMode ? Visibility.Visible : Visibility.Collapsed;
            RefreshMultiSelectBar();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "构建主页实例列表失败");
        }
        finally
        {
            _instanceListBuilding = false;
        }
    }

    /// <summary>
    ///     取某个 Minecraft 文件夹下的实例。当前文件夹复用已加载的列表，其余文件夹现场扫描。
    /// </summary>
    private List<McInstance> _GetInstances(ModFolder.McFolder folder)
    {
        var result = new List<McInstance>();
        try
        {
            if (IsCurrentFolder(folder) && ModInstanceList.mcInstanceList.Count > 0)
            {
                foreach (var pair in ModInstanceList.mcInstanceList)
                    result.AddRange(pair.Value);
                return result;
            }

            var versions = Path.Combine(folder.Location, "versions");
            if (!Directory.Exists(versions)) return result;
            foreach (var dir in new DirectoryInfo(versions).GetDirectories())
            {
                // 与 PCL 的实例判定保持一致：目录中需存在同名 JSON
                if (!File.Exists(Path.Combine(dir.FullName, dir.Name + ".json"))) continue;
                var instance = new McInstance(dir.FullName + "\\");
                try
                {
                    instance.Load();
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"读取实例信息失败：{dir.Name}", ModBase.LogLevel.Developer);
                }

                result.Add(instance);
            }

            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"扫描实例失败：{folder.Location}");
        }

        return result;
    }

    private static bool IsCurrentFolder(ModFolder.McFolder folder) =>
        !string.IsNullOrEmpty(ModFolder.mcFolderSelected) &&
        string.Equals(folder.Location.TrimEnd('\\'), ModFolder.mcFolderSelected.TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     文件夹分组标题行：名称、数量、当前标记，以及右侧的文件夹操作按钮。
    /// </summary>
    private FrameworkElement _CreateFolderHeader(ModFolder.McFolder folder, int count)
    {
        var grid = new Grid { Margin = new Thickness(1, 8, 1, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(new TextBlock
        {
            Text = folder.Name, FontSize = 12.5, Opacity = 0.65, VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = Lang.Text("Launch.Home.Folder.Count", Lang.Number(count, "N0")),
            FontSize = 12,
            Opacity = 0.4,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        if (IsCurrentFolder(folder))
            panel.Children.Add(new TextBlock
            {
                Text = Lang.Text("Launch.Home.Folder.Current"),
                FontSize = 11,
                Opacity = 0.55,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
        grid.Children.Add(panel);

        var btn = new MyIconButton
        {
            SvgIcon = "lucide/settings",
            Height = 22,
            Width = 22,
            LogoScale = 0.9,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(btn, 1);
        btn.Click += (_, _) => OpenFolderMenu(folder, btn);
        grid.Children.Add(btn);
        return grid;
    }

    /// <summary>
    ///     文件夹操作菜单：打开、设为当前、添加文件夹、从列表中移除。
    /// </summary>
    private void OpenFolderMenu(ModFolder.McFolder folder, FrameworkElement target)
    {
        var menu = new ContextMenu();

        void AddItem(string header, string icon, RoutedEventHandler handler)
        {
            var item = new MyMenuItem { Header = header, SvgIcon = icon, Padding = new Thickness(0) };
            item.Click += handler;
            menu.Items.Add(item);
        }

        AddItem(Lang.Text("Common.Action.Open"), "lucide/folder-open", (_, _) => ModBase.OpenExplorer(folder.Location));
        AddItem(Lang.Text("Launch.Home.Folder.SetCurrent"), "lucide/circle-check", (_, _) => SwitchFolder(folder));
        AddItem(Lang.Text("Launch.Home.Folder.Add"), "lucide/folder-plus", (_, _) => AddFolderFlow());
        if (ModFolder.mcFolderList.Count > 1)
            AddItem(Lang.Text("Launch.Home.Folder.Remove"), "lucide/list-x", (_, _) => RemoveFolder(folder));

        menu.PlacementTarget = target;
        menu.IsOpen = true;
    }

    private MyListItem _CreateInstanceItem(McInstance instance)
    {
        var item = new MyListItem
        {
            Title = instance.Name,
            Height = 42,
            Tag = instance,
            Type = _multiSelectMode ? MyListItem.CheckType.CheckBox : MyListItem.CheckType.Clickable,
            Checked = _multiSelectMode && _multiSelected.Contains(instance.PathInstance)
        };

        try
        {
            item.Info = instance.GetDefaultDescription();
        }
        catch
        {
            item.Info = "";
        }

        try
        {
            item.Logo = instance.Logo.EndsWith(@"PCL\Logo.png")
                ? instance.PathInstance + @"PCL\Logo.png"
                : instance.Logo;
        }
        catch
        {
            item.Logo = "pack://application:,,,/images/Blocks/RedstoneBlock.png";
        }

        // 左键：选中实例（多选模式下勾选状态由控件自身维护）
        item.Click += (_, _) =>
        {
            if (_multiSelectMode) return;
            SwitchInstance(instance);
        };
        // 多选模式下同步勾选集合
        item.Changed += (_, e) =>
        {
            if (!_multiSelectMode || !e.raiseByMouse) return;
            // 刚进入多选的那一次勾选由 EnterMultiSelect 处理，这里跳过避免相互抵消
            if ((DateTime.Now - _multiSelectEnteredAt).TotalMilliseconds < 400) return;
            if (item.Checked) _multiSelected.Add(instance.PathInstance);
            else _multiSelected.Remove(instance.PathInstance);
            RefreshMultiSelectBar();
        };
        // 右键：实例操作（编辑等）
        item.ContextMenu = _CreateInstanceMenu(instance);
        _AttachLongPress(item, instance);
        return item;
    }

    private ContextMenu _CreateInstanceMenu(McInstance instance)
    {
        var menu = new ContextMenu();

        void AddItem(string header, string icon, RoutedEventHandler handler)
        {
            var item = new MyMenuItem { Header = header, SvgIcon = icon, Padding = new Thickness(0) };
            item.Click += handler;
            menu.Items.Add(item);
        }

        AddItem(Lang.Text("Launch.Home.Instance.Settings"), "lucide/settings", (_, _) => EditInstance(instance));
        AddItem(Lang.Text("Common.Action.Open"), "lucide/folder-open",
            (_, _) => PageInstanceOverall.OpenVersionFolder(instance));
        AddItem(Lang.Text("Launch.Home.Instance.Delete"), "lucide/trash-2",
            (_, _) => DeleteInstances([instance]));
        return menu;
    }

    /// <summary>
    ///     长按 450ms 进入多选模式；按下后移动超过阈值则视为拖动并取消。
    /// </summary>
    private void _AttachLongPress(MyListItem item, McInstance instance)
    {
        DispatcherTimer? timer = null;
        var start = new Point();
        item.PreviewMouseLeftButtonDown += (_, e) =>
        {
            start = e.GetPosition(item);
            timer?.Stop();
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            timer.Tick += (_, _) =>
            {
                timer?.Stop();
                EnterMultiSelect(instance);
            };
            timer.Start();
        };
        item.PreviewMouseMove += (_, e) =>
        {
            if (timer is null || !timer.IsEnabled) return;
            var pos = e.GetPosition(item);
            if (Math.Abs(pos.X - start.X) > 8d || Math.Abs(pos.Y - start.Y) > 8d) timer.Stop();
        };
        item.PreviewMouseLeftButtonUp += (_, _) => timer?.Stop();
    }

    private void EnterMultiSelect(McInstance instance)
    {
        _multiSelectEnteredAt = DateTime.Now;
        _multiSelectMode = true;
        _multiSelected.Clear();
        _multiSelected.Add(instance.PathInstance);
        PanMultiSelect.Visibility = Visibility.Visible;
        BuildInstanceListItems();
        RefreshMultiSelectBar();
        HintService.Hint(Lang.Text("Launch.Home.MultiSelect.Entered"), HintType.Info);
    }

    private void ExitMultiSelect()
    {
        _multiSelectMode = false;
        _multiSelected.Clear();
        PanMultiSelect.Visibility = Visibility.Collapsed;
        BuildInstanceListItems();
    }

    private void RefreshMultiSelectBar()
    {
        if (LabMultiSelect is null) return;
        LabMultiSelect.Text = Lang.Text("Launch.Home.MultiSelect.Count", Lang.Number(_multiSelected.Count, "N0"));
    }

    private void TextSearchInstance_TextChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        BuildInstanceListItems();
    }

    private void BtnGoDownload_Click(object sender, MouseButtonEventArgs e) =>
        ModMain.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);

    private void BtnAddFolder_Click(object sender, MouseButtonEventArgs e) => AddFolderFlow();

    private void BtnMultiCancel_Click(object sender, MouseButtonEventArgs e) => ExitMultiSelect();

    private void BtnMultiDelete_Click(object sender, MouseButtonEventArgs e)
    {
        var list = StackInstanceList.Children.OfType<MyListItem>()
            .Where(item => item.Tag is McInstance inst && _multiSelected.Contains(inst.PathInstance))
            .Select(item => (McInstance)item.Tag)
            .ToList();
        DeleteInstances(list);
    }

    /// <summary>
    ///     添加已有 Minecraft 文件夹（与实例选择页的“添加文件夹”流程一致）。
    /// </summary>
    private void AddFolderFlow()
    {
        if (ModNet.HasDownloadingTask())
        {
            HintService.Hint(Lang.Text("Select.Folder.CannotAddWhileDownloading"), HintType.Error);
            return;
        }

        var path = SystemDialogs.SelectFolder();
        if (string.IsNullOrEmpty(path)) return;
        if (path.Contains('!') || path.Contains(';'))
        {
            HintService.Hint(Lang.Text("Select.Folder.InvalidPathChars"), HintType.Error);
            return;
        }

        var parts = path.TrimEnd('\\').Split('\\');
        var defaultName = parts.Last() == ".minecraft"
            ? parts.Length >= 3 ? parts[^2] : ""
            : parts.Last();
        if (defaultName.Length > 40) defaultName = defaultName[..39];
        var name = ModMain.MyMsgBoxInput(Lang.Text("Select.Folder.InputDisplayName.Title"),
            Lang.Text("Select.Folder.InputDisplayName.Message"), defaultName,
            [new NullOrWhiteSpaceValidator(), new StringLengthValidator(), new BlacklistValidator([">", "|"])]);
        if (string.IsNullOrWhiteSpace(name)) return;
        PageSelectLeft.AddFolder(path, name, true);
    }

    /// <summary>
    ///     把文件夹设为当前文件夹，并刷新实例列表。
    /// </summary>
    private void SwitchFolder(ModFolder.McFolder folder)
    {
        if (IsCurrentFolder(folder)) return;
        States.Game.SelectedFolder = folder.Location.Replace(ModBase.exePath, "$");
        ModFolder.mcFolderSelected = folder.Location;
        ModFolder.mcFolderListLoader.Start(isForceRestart: true);
        ModLoader.LoaderFolderRun(ModInstanceList.mcInstanceListLoader, folder.Location,
            ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
        BuildInstanceListItems();
    }

    /// <summary>
    ///     从列表中移除文件夹（不删除磁盘文件）。
    /// </summary>
    private void RemoveFolder(ModFolder.McFolder folder)
    {
        var folders = States.Game.Folders.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();
        var index = folders.FindIndex(f => f.EndsWith(folder.Location));
        if (index < 0) return;
        folders.RemoveAt(index);
        States.Game.Folders = string.Join("|", folders);
        // 移除的正好是当前文件夹时，改选剩下的第一个
        if (IsCurrentFolder(folder))
        {
            var next = ModFolder.mcFolderList.FirstOrDefault(f => !IsCurrentFolder(f));
            if (next is not null)
                States.Game.SelectedFolder = next.Location.Replace(ModBase.exePath, "$");
        }

        HintService.Hint(Lang.Text("Launch.Home.Folder.Removed", folder.Name), HintType.Success);
        ModFolder.mcFolderListLoader.Start(isForceRestart: true);
        BuildInstanceListItems();
    }

    private void SwitchInstance(McInstance instance)
    {
        if (instance.state == McInstanceState.Error)
        {
            PageInstanceOverall.OpenVersionFolder(instance);
            return;
        }

        try
        {
            // 实例可能属于另一个 Minecraft 文件夹，先切换文件夹，否则启动时用的根目录会错
            var folder = ModFolder.mcFolderList.FirstOrDefault(f =>
                (instance.PathInstance ?? "").StartsWith(f.Location, StringComparison.OrdinalIgnoreCase));
            if (folder is not null && !IsCurrentFolder(folder))
            {
                ModBase.Log("[Launch] 切换 Minecraft 文件夹：" + folder.Location);
                States.Game.SelectedFolder = folder.Location.Replace(ModBase.exePath, "$");
                ModFolder.mcFolderSelected = folder.Location;
                ModFolder.mcFolderListLoader.Start(isForceRestart: true);
            }

            instance.Load();
            if (!instance.Check())
            {
                PageInstanceOverall.OpenVersionFolder(instance);
                return;
            }

            ModInstanceList.McMcInstanceSelected = instance;
            States.Game.SelectedInstance = instance.Name;
            UpdateInstanceListUI();
            ModMain.frmLaunchLeft.RefreshButtonsUI();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "切换实例失败");
        }
    }

    /// <summary>
    ///     打开指定实例的实例设置页。
    /// </summary>
    private void EditInstance(McInstance instance)
    {
        try
        {
            instance.Load();
            ModInstanceList.McMcInstanceSelected = instance;
            States.Game.SelectedInstance = instance.Name;
            PageInstanceLeft.McInstance = instance;
            ModMain.frmMain.PageChange(FormMain.PageType.InstanceSetup);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "打开实例设置失败");
        }
    }

    /// <summary>
    ///     删除实例（含磁盘文件），需要用户确认。
    /// </summary>
    private void DeleteInstances(IReadOnlyCollection<McInstance> instances)
    {
        if (instances.Count == 0) return;
        var names = string.Join("\n", instances.Select(i => i.Name));
        if (ModMain.MyMsgBox(Lang.Text("Launch.Home.Instance.Delete.Warning", names),
                Lang.Text("Launch.Home.Instance.Delete.Title"),
                Lang.Text("Common.Action.Cancel"), Lang.Text("Common.Action.Confirm"),
                Lang.Text("Common.Action.Cancel")) != 2)
            return;

        var deleted = 0;
        foreach (var instance in instances)
        {
            try
            {
                if (Directory.Exists(instance.PathInstance))
                    Directory.Delete(instance.PathInstance, true);
                if ((ModInstanceList.McMcInstanceSelected?.PathInstance ?? "") == instance.PathInstance)
                    ModInstanceList.McMcInstanceSelected = null;
                deleted++;
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "删除实例失败：" + instance.Name);
            }
        }

        HintService.Hint(Lang.Text("Launch.Home.Instance.Delete.Success", Lang.Number(deleted, "N0")),
            HintType.Success);
        _multiSelectMode = false;
        _multiSelected.Clear();
        PanMultiSelect.Visibility = Visibility.Collapsed;
        ModLoader.LoaderFolderRun(ModInstanceList.mcInstanceListLoader, ModFolder.mcFolderSelected,
            ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\");
        BuildInstanceListItems();
        ModMain.frmLaunchLeft.RefreshButtonsUI();
    }

    #endregion

    private void Init()
    {
        PanBack.ScrollToHome();
        PanScroll = PanBack; // 不知道为啥不能在 XAML 设置
        PanLog.Visibility = ModBase.modeDebug ? Visibility.Visible : Visibility.Collapsed;
        _EnsureHomepageLiveWatcher();
    }

    #region 主页

    /// <summary>
    ///     刷新主页。
    /// </summary>
    private void Refresh()
    {
        ModBase.RunInNewThread(() =>
            {
                try
                {
                    lock (refreshLock)
                    {
                        RefreshReal();
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(
                        ex,
                        "加载 PCL 主页自定义信息失败",
                        ModBase.modeDebug
                            ? ModBase.LogLevel.Msgbox
                            : ModBase.LogLevel.Hint,
                        userSummary: Lang.Text("Launch.Error.OperationFailed"));
                }
            }, $"刷新主页 #{ModBase.GetUuid()}");
    }

    private void RefreshReal()
    {
        var content = "";
        string url = null;

        var uiCustomType = (int)Config.Preference.Homepage.Type;

        if (uiCustomType == 1)
        {
            // 本地文件
            LogWrapper.Info("[Page] 主页自定义数据来源：本地文件");
            content = ModBase.ReadFile(Path.Combine(ModBase.exePath, "PCL", "Custom.xaml"));
        }
        else if (uiCustomType == 2)
        {
            // 网络文件
            url = (string)Config.Preference.Homepage.CustomUrl;
            content = LoadFromNetwork(url);
        }
        else if (uiCustomType == 3)
        {
            // 预设主页
            var preset = (int)Config.Preference.Homepage.SelectedPreset;
            switch (preset)
            {
                case 0:
                    LogWrapper.Info("[Page] 主页预设：你知道吗");
                    var hintText = GetRandomHint();
                    content = $@"
    <local:MyCard Title=""{{DynamicResource Launch.Status.Trivia}}"" Margin=""0,0,0,15"">
        <TextBlock Margin=""25,38,23,15"" FontSize=""13.5"" IsHitTestVisible=""False"" Text=""{hintText}"" TextWrapping=""Wrap"" Foreground=""{{DynamicResource ColorBrush1}}"" />
        <local:MyIconButton Height=""22"" Width=""22"" Margin=""9"" VerticalAlignment=""Top"" HorizontalAlignment=""Right"" 
            EventType=""刷新主页"" EventData=""/""
            SvgIcon=""lucide/refresh-cw"" />
    </local:MyCard>";
                    break;

                case 1:
                    LogWrapper.Info("[Page] 主页预设：回声洞 已被移除");
                    ModMain.MyMsgBox(Lang.Text("Launch.Homepage.Preset.EchoCave.Removed"));
                    return;

                case 2:
                    LogWrapper.Info("[Page] 主页预设：Minecraft 新闻");
                    url = "https://news.bugjump.net";
                    content = LoadFromNetwork(url);
                    break;

                // case 3:
                //     LogWrapper.Info("[Page] 主页预设：简单主页");
                //     url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/MFn233/Custom.xaml";
                //     content = LoadFromNetwork(url);
                //     break;

                case 3:
                    LogWrapper.Info("[Page] 主页预设：每日整合包推荐");
                    url = "https://pclsub.sodamc.com/";
                    content = LoadFromNetwork(url);
                    break;

                case 4:
                    LogWrapper.Info("[Page] 主页预设：Minecraft 皮肤推荐");
                    url = "https://forgepixel.com/pcl_sub_file";
                    content = LoadFromNetwork(url);
                    break;

                case 5:
                    LogWrapper.Info("[Page] 主页预设：OpenBMCLAPI 仪表盘 Lite");
                    url = "https://pcl-bmcl.milu.ink/";
                    content = LoadFromNetwork(url);
                    break;

                // case 7:
                //     LogWrapper.Info("[Page] 主页预设：主页市场");
                //     url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/JingHai-Lingyun/Custom.xaml";
                //     content = LoadFromNetwork(url);
                //     break;

                // case 8:
                //     LogWrapper.Info("[Page] 主页预设：更新日志");
                //     url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Joker2184/UpdateHomepage.xaml";
                //     content = LoadFromNetwork(url);
                //     break;

                case 6:
                    LogWrapper.Info("[Page] 主页预设：PCL 新功能说明书");
                    url = "https://raw.gitcode.com/WForst-Breeze/WhatsNewPCL/raw/main/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                // case 10:
                //     LogWrapper.Info("[Page] 主页预设：OpenMCIM Dashboard");
                //     url = "https://files.mcimirror.top/PCL";
                //     content = LoadFromNetwork(url);
                //     break;

                case 7:
                    LogWrapper.Info("[Page] 主页预设：杂志主页");
                    url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Ext1nguisher/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 8:
                    LogWrapper.Info("[Page] 主页预设：PCL GitHub 仪表盘");
                    url = "https://ddf.pcl-community.org/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 9:
                    LogWrapper.Info("[Page] 主页预设：Minecraft 更新摘要");
                    url = "https://raw.gitcode.com/ENC_Euphony/PCL-AI-Summary-HomePage/raw/master/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;
                
                case 10:
                    LogWrapper.Info("[Page] 主页预设：今日新闻热点");
                    url = "https://pcl.wyc-w.top/index.xaml";
                    content = LoadFromNetwork(url);
                    break;
                
                case 11:
                    LogWrapper.Info("[Page] 主页预设：Minecraft 芝士站");
                    url = "https://www.xxag.top/mkss";
                    content = LoadFromNetwork(url);
                    break;
                
                case 12:
                    LogWrapper.Info("[Page] 主页预设：整合包推荐引擎");
                    url = "https://qawsedrftgyhujiko.fun/pcl2/Custom.xaml";
                    content = LoadFromNetwork(url);
                    break;

                case 13:
                    LogWrapper.Info("[Page] 主页预设：Bangumi 番剧主页");
                    url = "https://bangumi.p.kaphia.qzz.io";
                    content = LoadFromNetwork(url);
                    break;
                
                case 14:
                    LogWrapper.Info("[Page] 主页预设：PCL CE 公告栏");
                    url = "https://s3.pysio.online/pcl2-ce/apiv2/pages/announce.xaml";
                    content = LoadFromNetwork(url);
                    break;
                
                case 15:
                    LogWrapper.Info("[Page] 主页预设：Minecraft 信息流");
                    Dispatcher.Invoke(() =>
                    {
                        if (ModMain.frmHomepageNews is null)
                            ModMain.frmHomepageNews = new PageHomepageNewsView();
                        PanCustom.Children.Clear();
                        PanCustom.Children.Add(ModMain.frmHomepageNews);
                    });
                    return;
            }
        }

        ModBase.RunInUi(() => LoadContent(content));
    }

    /// <summary>
    ///     根据 URL 加载网络内容，优先使用缓存
    /// </summary>
    private string LoadFromNetwork(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";

        var cachePath = Path.Combine(ModBase.pathTemp, "Cache", "Custom.xaml");
        var cachedUrl = (string)States.UI.SavedHomepageUrl;

        if (url == cachedUrl && File.Exists(cachePath))
        {
            LogWrapper.Info("[Page] 主页自定义数据来源：联网缓存文件");
            // 后台更新缓存
            onlineLoader.Start(url);
            return ModBase.ReadFile(cachePath);
        }

        LogWrapper.Info("[Page] 主页自定义数据来源：联网全新下载");
        HintWrapper.Show(Lang.Text("Launch.Homepage.Loading"));
        ModBase.RunInUiWait(() => LoadContent("")); // 先清空页面
        States.UI.SavedHomepageVersion = "";
        onlineLoader.Start(url); // 下载完成后将会再次触发更新
        return "";
    }

    private readonly object refreshLock = new();

    public static string GetRandomHint(bool enableLengthLimit = false, bool raw = false)
    {
        string[]? lines = null;

        // 外部文件
        var externalPath = Path.Combine(ModBase.exePath, "PCL", "hints.txt");
        if (File.Exists(externalPath))
        {
            try
            {
                lines = File.ReadAllLines(externalPath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .ToArray();
            }
            catch
            {
                ModBase.Log(
                    Lang.Text("Launch.Homepage.Error.ExternalFile", externalPath),
                    ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Launch.Homepage.Error.ExternalFile", externalPath));
            }
        }

        // 嵌入式资源
        if (lines is null || lines.Length == 0)
        {
            var langCode = LocalizationService.CurrentLanguage.Code;
            lines = _LoadEmbeddedHints(langCode)
                ?? _LoadEmbeddedHints(LocalizationService.DefaultLanguageCode);
        }

        // 长度限制
        if (enableLengthLimit)
        {
            var shortLines = lines.Where(l => l.Length < 50).ToArray();
            if (shortLines.Length > 0) lines = shortLines;
        }

        // 随机返回
        var hint = lines[Random.Shared.Next(lines.Length)];
        return raw ? hint : hint.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    private static string[]? _LoadEmbeddedHints(string langCode)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/Plain Craft Launcher 2;component/Resources/hints/{langCode}.txt", UriKind.Absolute);
            using var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream is null) return null;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd()
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .ToArray();
        }
        catch
        {
            return null;
        }
    }

    // 联网获取主页文件
    private readonly ModLoader.LoaderTask<string, int> onlineLoader;

    private void OnlineLoaderSub(ModLoader.LoaderTask<string, int> task)
    {
        var address = task.input; // #3721 中连续触发两次导致内容变化
        try
        {
            // 获取版本校验地址
            string versionAddress;
            if (address.Contains(".xaml"))
            {
                versionAddress = address.Replace(".xaml", ".xaml.ini");
            }
            else
            {
                versionAddress = address.BeforeFirst("?");
                if (!versionAddress.EndsWith("/"))
                    versionAddress += "/";
                versionAddress += "version";
                if (address.Contains("?"))
                    versionAddress += "?" + address.AfterFirst("?");
            }

            // 校验版本
            var version = "";
            var needDownload = true;
            try
            {
                version = Requester.FetchString(versionAddress);
                if (version.Length > 1000)
                    throw new Exception($"获取的主页版本过长（{version.Length} 字符）");
                var currentVersion = States.UI.SavedHomepageVersion;
                if (!string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(currentVersion) &&
                    (version ?? "") == (currentVersion ?? ""))
                {
                    ModBase.Log($"[Page] 当前缓存的主页已为最新，当前版本：{version}，检查源：{versionAddress}");
                    needDownload = false;
                }
                else
                {
                    ModBase.Log($"[Page] 需要下载联网主页，当前版本：{version}，检查源：{versionAddress}");
                }
            }
            catch (Exception exx)
            {
                ModBase.Log(exx, "联网获取主页版本失败", ModBase.LogLevel.Developer);
                ModBase.Log($"[Page] 无法检查联网主页版本，将直接下载，检查源：{versionAddress}");
            }

            // 实际下载
            if (needDownload)
            {
                var fileContent = Requester.FetchString(address);
                ModBase.Log($"[Page] 已联网下载主页，内容长度：{fileContent.Length}，来源：{address}");
                States.UI.SavedHomepageUrl = address;
                States.UI.SavedHomepageVersion = version;
                ModBase.WriteFile(ModBase.pathTemp + @"Cache\Custom.xaml", fileContent);
            }

            // 要求刷新
            ModBase.RunInUi(Refresh); // 不直接调用 Refresh，以防止死循环（#6245）
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Launch.Homepage.Error.Download", address),
                ModBase.modeDebug
                    ? ModBase.LogLevel.Msgbox
                    : ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Launch.Homepage.Error.Download", address));
        }
    }

    /// <summary>
    ///     立即强制刷新主页。
    ///     必须在 UI 线程调用。
    /// </summary>
    public void ForceRefresh()
    {
        ModBase.Log("[Page] 要求强制刷新主页");
        ClearCache();
        // 实际的刷新
        if (ModMain.frmMain.pageCurrent.page == FormMain.PageType.Launch)
        {
            PanBack.ScrollToHome();
            Refresh();
        }
        else
        {
            ModMain.frmMain.PageChange(FormMain.PageType.Launch);
        }
    }

    void IRefreshable.Refresh()
    {
        ForceRefresh();
    }

    /// <summary>
    ///     清空主页缓存信息。
    /// </summary>
    private void ClearCache()
    {
        loadedContentHash = -1;
        onlineLoader.input = "";
        States.UI.SavedHomepageUrl = "";
        States.UI.SavedHomepageVersion = "";
        ModBase.Log("[Page] 已清空主页缓存");
    }

    /// <summary>
    ///     从文本内容中加载主页。
    ///     必须在 UI 线程调用。
    /// </summary>
    private void LoadContent(string content)
    {
        lock (loadContentLock)
        {
            // 如果加载目标内容一致则不加载
            var hash = content.GetHashCode();
            if (hash == loadedContentHash)
            {
                _ApplyHomepageLivePatchesFromFile();
                return;
            }
            loadedContentHash = hash;
            // 实际加载内容
            PanCustom.Children.Clear();
            if (string.IsNullOrWhiteSpace(content))
            {
                ModBase.Log("[Page] 实例化：清空主页 UI，来源为空");
                return;
            }

            var loadStartTime = DateTime.Now;
            try
            {
                content = ModMain.ArgumentReplace(content);
                while (content.Contains("xmlns"))
                    content = content.RegexReplace("xmlns[^\"']*(\"|')[^\"']*(\"|')", "").Replace("xmlns", "");
                content =
                    $"<StackPanel xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:local=\"clr-namespace:PCL;assembly=Plain Craft Launcher 2\">{content}</StackPanel>";
                ModBase.Log($"[Page] 实例化：加载主页 UI 开始，最终内容长度：{content.Count()}");
                PanCustom.Children.Add((UIElement)ModBase.GetObjectFromXML(content, out var sanitizeResult));
                _ShowSanitizeHints(sanitizeResult);
                _ApplyHomepageLivePatchesFromFile();
            }
            catch (Exception ex)
            {
                if (ModBase.modeDebug)
                {
                    ModBase.Log(ex, $"加载失败的主页内容：\r\n{content}");
                    if (ModMain.MyMsgBox(
                            ex is UnauthorizedAccessException
                                ? ex.Message
                                : Lang.Text("Launch.Homepage.LoadFailed.Message", ex),
                            Lang.Text("Launch.Homepage.LoadFailed.Title"),
                            Lang.Text("Launch.Homepage.LoadFailed.Retry"),
                            Lang.Text("Common.Action.Cancel")) ==
                        1) goto Refresh; // 防止 SyncLock 死锁
                }
                else
                {
                    ModBase.Log(
                        ex,
                        Lang.Text("Launch.Homepage.LoadFailed.Title"),
                        ModBase.LogLevel.Hint,
                        userSummary: Lang.Text("Launch.Homepage.LoadFailed.Title"));
                }

                return;
            }

            var loadCostTime = (DateTime.Now - loadStartTime).Milliseconds;
            ModBase.Log($"[Page] 实例化：加载主页 UI 完成，耗时 {loadCostTime}ms");
            if (loadCostTime > 3000)
                HintService.Hint(Lang.Text("Launch.Homepage.SlowWarning", Lang.Number(Math.Round(loadCostTime / 1000d, 1), "N1")));
        }

        return;
        Refresh: ;

        ForceRefresh();
    }

    private int loadedContentHash = -1;
    private readonly object loadContentLock = new();
    private static void _ShowSanitizeHints(XamlEventSanitizer.SanitizeResult result)
    {
        foreach (var unsupported in result.UnsupportedTypesFound)
            HintService.Hint(Lang.Text("Event.Sanitize.UnsupportedTypeHint", unsupported), HintType.Error);

        foreach (var unknown in result.UnrecognizedTypes)
            HintService.Hint(Lang.Text("Event.Sanitize.UnknownTypeHint", unknown), HintType.Error);
    }

    private const string homepageLivePatchFileName = "CustomLive.json";
    private const string homepageLiveSupportFileName = "CustomLive.supported.json";
    // Keep the reflection patch surface explicit because patch files are written by external tools.
    private static readonly Dictionary<string, string> _homepageLiveAllowedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = "Text",
        ["title"] = "Title",
        ["info"] = "Info",
        ["tooltip"] = "ToolTip",
        ["visibility"] = "Visibility",
        ["isEnabled"] = "IsEnabled",
        ["opacity"] = "Opacity"
    };
    private FileSystemWatcher? _homepageLiveWatcher;
    private DispatcherTimer? _homepageLivePatchTimer;

    private void _EnsureHomepageLiveWatcher()
    {
        if (_homepageLiveWatcher != null) return;
        if ((int)Config.Preference.Homepage.Type != 1) return;

        try
        {
            var directory = _GetHomepageLiveDirectory();
            Directory.CreateDirectory(directory);
            _WriteHomepageLiveSupportMarker(directory);

            _homepageLiveWatcher = new FileSystemWatcher(directory, homepageLivePatchFileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
            };
            _homepageLiveWatcher.Changed += (_, _) => _QueueHomepageLivePatchApply();
            _homepageLiveWatcher.Created += (_, _) => _QueueHomepageLivePatchApply();
            _homepageLiveWatcher.Renamed += (_, _) => _QueueHomepageLivePatchApply();
            _homepageLiveWatcher.EnableRaisingEvents = true;
            _QueueHomepageLivePatchApply();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Page] Failed to start custom homepage live patch watcher", ModBase.LogLevel.Developer);
        }
    }

    private void _DisposeHomepageLiveWatcher()
    {
        try
        {
            _homepageLiveWatcher?.Dispose();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Page] Failed to dispose custom homepage live patch watcher", ModBase.LogLevel.Developer);
        }

        _homepageLiveWatcher = null;

        try
        {
            if (_homepageLivePatchTimer != null)
            {
                _homepageLivePatchTimer.Stop();
                _homepageLivePatchTimer.Tick -= _HomepageLivePatchTimerTick;
                _homepageLivePatchTimer = null;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Page] Failed to dispose custom homepage live patch debounce timer", ModBase.LogLevel.Developer);
        }

        _DeleteHomepageLiveSupportMarker();
    }

    private void _QueueHomepageLivePatchApply()
    {
        ModBase.RunInUi(() =>
        {
            _homepageLivePatchTimer ??= new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _homepageLivePatchTimer.Tick -= _HomepageLivePatchTimerTick;
            _homepageLivePatchTimer.Tick += _HomepageLivePatchTimerTick;
            _homepageLivePatchTimer.Stop();
            _homepageLivePatchTimer.Start();
        });
    }

    private void _HomepageLivePatchTimerTick(object? sender, EventArgs e)
    {
        _homepageLivePatchTimer?.Stop();
        _ApplyHomepageLivePatchesFromFile();
    }

    private void _ApplyHomepageLivePatchesFromFile()
    {
        if (PanCustom.Children.Count == 0) return;
        if ((int)Config.Preference.Homepage.Type != 1) return;

        var file = Path.Combine(_GetHomepageLiveDirectory(), homepageLivePatchFileName);
        if (!File.Exists(file)) return;

        try
        {
            var token = JsonNode.Parse(_ReadHomepageLivePatchFile(file),
                new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true });
            foreach (var patch in _EnumerateHomepageLivePatches(token))
                _ApplyHomepageLivePatch(patch);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Page] Failed to apply custom homepage live patches", ModBase.LogLevel.Developer);
        }
    }

    private static string _ReadHomepageLivePatchFile(string file)
    {
        Exception? lastException = null;
        for (var i = 0; i < 3; i++)
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                lastException = ex;
                Thread.Sleep(50);
            }
        }

        throw lastException ?? new IOException("Unable to read custom homepage live patch file.");
    }

    private static string _GetHomepageLiveDirectory()
    {
        return Path.Combine(ModBase.exePath, "PCL");
    }

    private static void _WriteHomepageLiveSupportMarker(string directory)
    {
        try
        {
            var marker = new JsonObject(new JsonNodeOptions { PropertyNameCaseInsensitive = true })
            {
                ["processId"] = Environment.ProcessId,
                ["processPath"] = Environment.ProcessPath ?? "",
                ["patchFile"] = homepageLivePatchFileName,
                ["startedAt"] = DateTime.Now.ToString("O", CultureInfo.InvariantCulture)
            };
            File.WriteAllText(Path.Combine(directory, homepageLiveSupportFileName), marker.ToJsonString());
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Page] Failed to write custom homepage live patch support marker", ModBase.LogLevel.Developer);
        }
    }

    private static void _DeleteHomepageLiveSupportMarker()
    {
        try
        {
            var file = Path.Combine(_GetHomepageLiveDirectory(), homepageLiveSupportFileName);
            if (!File.Exists(file)) return;

            var marker = (JsonObject)JsonNode.Parse(_ReadHomepageLivePatchFile(file),
                new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true })!;
            if (marker["processId"]?.GetValue<int>() == Environment.ProcessId)
                File.Delete(file);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Page] Failed to delete custom homepage live patch support marker", ModBase.LogLevel.Developer);
        }
    }

    private static IEnumerable<JsonObject> _EnumerateHomepageLivePatches(JsonNode token)
    {
        if (token is JsonObject obj)
        {
            if (obj["patches"] is JsonArray patches)
            {
                foreach (var patch in patches.OfType<JsonObject>())
                    yield return patch;
                yield break;
            }

            if (_TryGetString(obj, "target", "tag", "name") != null)
            {
                yield return obj;
                yield break;
            }

            foreach (var property in obj)
            {
                if (property.Value is not JsonObject patch) continue;
                patch = (JsonObject)JsonNode.Parse(patch.ToJsonString(),
                    new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                    new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true })!;
                patch["target"] ??= property.Key;
                yield return patch;
            }
        }
        else if (token is JsonArray array)
        {
            foreach (var patch in array.OfType<JsonObject>())
                yield return patch;
        }
    }

    private void _ApplyHomepageLivePatch(JsonObject patch)
    {
        var target = _TryGetString(patch, "target", "tag", "name");
        if (string.IsNullOrWhiteSpace(target)) return;

        foreach (var element in _FindElementsByTag(PanCustom, target))
            _ApplyHomepageLivePatchToElement(element, patch);
    }

    private void _ApplyHomepageLivePatchToElement(FrameworkElement element, JsonObject patch)
    {
        _SetPropertyIfPresent(element, patch, "text", "Text");
        _SetPropertyIfPresent(element, patch, "title", "Title");
        _SetPropertyIfPresent(element, patch, "info", "Info");
        _SetPropertyIfPresent(element, patch, "tooltip", "ToolTip");
        _SetPropertyIfPresent(element, patch, "toolTip", "ToolTip");
        _SetPropertyIfPresent(element, patch, "visibility", "Visibility");
        _SetPropertyIfPresent(element, patch, "isEnabled", "IsEnabled");
        _SetPropertyIfPresent(element, patch, "opacity", "Opacity");

        if (patch["properties"] is JsonObject properties)
        {
            foreach (var property in properties)
                _TrySetElementProperty(element, property.Key, property.Value?.ToString() ?? "");
        }

        var childrenXaml = _TryGetString(patch, "childrenXaml", "ChildrenXaml");
        if (!string.IsNullOrEmpty(childrenXaml) && element is Panel panel)
            _ReplacePanelChildren(panel, childrenXaml);
    }

    private static void _SetPropertyIfPresent(FrameworkElement element, JsonObject patch, string jsonName, string propertyName)
    {
        if (patch.TryGetPropertyValue(jsonName, out var value))
            _TrySetElementProperty(element, propertyName, value?.ToString() ?? "");
    }

    private static bool _TrySetElementProperty(FrameworkElement element, string propertyName, string value)
    {
        if (!_homepageLiveAllowedProperties.TryGetValue(propertyName, out var allowedPropertyName))
        {
            ModBase.Log($"[Page] Skipped unsupported live patch property {propertyName}", ModBase.LogLevel.Developer);
            return false;
        }

        propertyName = allowedPropertyName;
        var property = element.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property == null || !property.CanWrite) return false;

        try
        {
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var trimmedValue = value.Trim();
            object convertedValue;
            if (propertyType == typeof(string))
                convertedValue = value;
            else if (propertyType == typeof(object))
                convertedValue = value;
            else if (propertyType == typeof(bool) && bool.TryParse(trimmedValue, out var boolValue))
                convertedValue = boolValue;
            else if (propertyType == typeof(int) && int.TryParse(trimmedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                convertedValue = intValue;
            else if (propertyType == typeof(double) && double.TryParse(trimmedValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
                convertedValue = doubleValue;
            else if (propertyType == typeof(Visibility))
            {
                if (!Enum.TryParse(trimmedValue, true, out Visibility visibilityValue))
                    return false;
                convertedValue = visibilityValue;
            }
            else if (propertyType.IsEnum && Enum.TryParse(propertyType, trimmedValue, true, out var enumValue))
                convertedValue = enumValue;
            else
                return false;

            property.SetValue(element, convertedValue);
            return true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"[Page] Failed to set live patch property {propertyName}", ModBase.LogLevel.Developer);
            return false;
        }
    }

    private static void _ReplacePanelChildren(Panel panel, string childrenXaml)
    {
        var content = ModMain.ArgumentReplace(childrenXaml);
        while (content.Contains("xmlns"))
            content = content.RegexReplace("xmlns[^\"']*(\"|')[^\"']*(\"|')", "").Replace("xmlns", "");

        var wrapped =
            $"<StackPanel xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:local=\"clr-namespace:PCL;assembly=Plain Craft Launcher 2\">{content}</StackPanel>";

        if (ModBase.GetObjectFromXML(wrapped) is not Panel parsedPanel) return;

        var children = parsedPanel.Children.OfType<UIElement>().ToList();
        parsedPanel.Children.Clear();
        panel.Children.Clear();
        foreach (var child in children)
            panel.Children.Add(child);
    }

    private static IEnumerable<FrameworkElement> _FindElementsByTag(DependencyObject root, string tag)
    {
        if (root is FrameworkElement element &&
            string.Equals(element.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            yield return element;

        int count;
        try
        {
            count = VisualTreeHelper.GetChildrenCount(root);
        }
        catch
        {
            yield break;
        }

        for (var i = 0; i < count; i++)
        {
            foreach (var child in _FindElementsByTag(VisualTreeHelper.GetChild(root, i), tag))
                yield return child;
        }
    }

    private static string? _TryGetString(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetPropertyValue(name, out var value))
                return value?.ToString();
        }

        return null;
    }

    #endregion
}
