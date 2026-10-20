using System.Windows;
using System.Windows.Controls;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupLeft
{
    private bool isLoad;
    private bool isPageSwitched; // 如果在 Loaded 前切换到其他页面，会导致触发 Loaded 时再次切换一次

    private void PageSetupLeft_Loaded(object sender, RoutedEventArgs e)
    {
        // 设置页已精简为「启动 / Java / 管理 / 个性化」四项且始终可见，这里只负责首次选中第一项
        if (isLoad || isPageSwitched)
            return;
        isLoad = true;
        // 选择第一个子页面
        ItemLaunch.SetChecked(true, false, false);
    }

    private void PageOtherLeft_Unloaded(object sender, RoutedEventArgs e)
    {
        isPageSwitched = false;
    }

    public void Reset(object sender, EventArgs e)
    {
        switch (ModBase.Val(((MyIconButton)sender).Tag))
        {
            case (double)FormMain.PageSubType.SetupLaunch:
            {
                if (ModMain.MyMsgBox(Lang.Text("Setup.Left.Reset.Launch.Message"), Lang.Text("Setup.Left.Reset.Title"), button2: Lang.Text("Common.Action.Cancel"), isWarn: true) == 1)
                {
                    if (ModMain.frmSetupLaunch is null)
                        ModMain.frmSetupLaunch = new PageSetupLaunch();
                    ModMain.frmSetupLaunch.Reset();
                    ItemLaunch.Checked = true;
                }

                break;
            }
            case (double)FormMain.PageSubType.SetupUI:
            {
                if (ModMain.MyMsgBox(Lang.Text("Setup.Left.Reset.Ui.Message"),
                        Lang.Text("Setup.Left.Reset.Title"), button2: Lang.Text("Common.Action.Cancel"), isWarn: true) == 1)
                {
                    if (ModMain.frmSetupUI is null)
                        ModMain.frmSetupUI = new PageSetupUI();
                    ModMain.frmSetupUI.Reset();
                    ItemUI.Checked = true;
                }

                break;
            }
            case (double)FormMain.PageSubType.SetupGameManage:
            {
                if (ModMain.MyMsgBox(Lang.Text("Setup.Left.Reset.GameManage.Message"), Lang.Text("Setup.Left.Reset.Title"), button2: Lang.Text("Common.Action.Cancel"), isWarn: true) == 1)
                {
                    if (ModMain.frmSetupGameManage is null)
                        ModMain.frmSetupGameManage = new PageSetupGameManage();
                    ModMain.frmSetupGameManage.Reset();
                    ItemGameManage.Checked = true;
                }

                break;
            }
        }
    }

    public void Refresh(object sender, EventArgs e) // 由边栏按钮匿名调用
    {
        if (ModBase.Val(((MyIconButton)sender).Tag) == (double)FormMain.PageSubType.SetupJava)
        {
            if (ModMain.frmSetupJava is not null) ModMain.frmSetupJava.loader.Start(isForceRestart: true);
            ItemJava.Checked = true;
        }

        HintService.Hint(Lang.Text("Setup.Left.Refreshing"), log: false);
    }

    #region 页面切换

    /// <summary>
    ///     当前页面的编号。从左往右从 0 开始计算。
    /// </summary>
    public FormMain.PageSubType pageID;

    public PageSetupLeft()
    {
        InitializeComponent();
        pageID = FormMain.PageSubType.SetupLaunch;
        AnimatedControl = PanItem;
        Loaded += PageSetupLeft_Loaded;
        Unloaded += PageOtherLeft_Unloaded;
    }

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object senderRaw, ModBase.RouteEventArgs e)
    {
        var sender = (MyListItem)senderRaw;
        // 尚未初始化控件属性时，sender.Tag 为 Nothing，会跳过切换，且由于 PageID 默认为 0 而切换到第一个页面
        // 若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        if (sender.Tag is not null)
            PageChange((FormMain.PageSubType)ModBase.Val(sender.Tag));
    }

    /// <summary>
    ///     获取当前导航指定的右页面。
    /// </summary>
    public object PageGet(FormMain.PageSubType? id = null)
    {
        var targetID = id ?? pageID;
        switch (id)
        {
            case FormMain.PageSubType.SetupLaunch:
            {
                if (ModMain.frmSetupLaunch is null)
                    ModMain.frmSetupLaunch = new PageSetupLaunch();
                return ModMain.frmSetupLaunch;
            }
            case FormMain.PageSubType.SetupUI:
            {
                if (ModMain.frmSetupUI is null)
                    ModMain.frmSetupUI = new PageSetupUI();
                return ModMain.frmSetupUI;
            }
            case FormMain.PageSubType.SetupGameManage:
            {
                if (ModMain.frmSetupGameManage is null)
                    ModMain.frmSetupGameManage = new PageSetupGameManage();
                return ModMain.frmSetupGameManage;
            }
            case FormMain.PageSubType.SetupJava:
            {
                if (ModMain.frmSetupJava is null)
                    ModMain.frmSetupJava = new PageSetupJava();
                return ModMain.frmSetupJava;
            }

            default:
            {
                throw new Exception("未知的设置子页面种类：" + (int)id);
            }
        }
    }

    /// <summary>
    ///     切换现有页面。
    /// </summary>
    public void PageChange(FormMain.PageSubType id)
    {
        if (pageID == id)
            return;
        ModAnimation.AniControlEnabled += 1;
        isPageSwitched = true;
        try
        {
            PageChangeRun((MyPageRight)PageGet(id));
            pageID = id;
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                $"切换分页面失败（ID {(int)id}）",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    private static void PageChangeRun(MyPageRight target)
    {
        ModAnimation.AniStop("FrmMain PageChangeRight"); // 停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        if (target.Parent is not null)
            target.SetValue(ContentPresenter.ContentProperty, null);
        ModMain.frmMain.pageRight = target;
        ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnExit();
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnForceExit();
                ModMain.frmMain.PanMainRight.Child = ModMain.frmMain.pageRight;
                ModMain.frmMain.pageRight.Opacity = 0d;
            }, 130),
            ModAnimation.AaCode(() =>
            {
                // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                ModMain.frmMain.pageRight.Opacity = 1d;
                ModMain.frmMain.pageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }

    #endregion
}
