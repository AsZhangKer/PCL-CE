using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCL;

/// <summary>
/// HSV 取色器：左侧为饱和度/明度色板，中间为色相条，右侧为十六进制输入框。
/// 只负责“选出一个颜色”，具体用途（主题色 / 渐变终点色）由调用方在 <see cref="ColorChanged" /> 中决定。
/// </summary>
public partial class MyColorPicker
{
    /// <summary>色相，0~360。</summary>
    private double _hue = 200;

    /// <summary>饱和度，0~1。</summary>
    private double _sat = 1;

    /// <summary>明度，0~1。</summary>
    private double _val = 1;

    private Color _color = Colors.White;

    /// <summary>程序内回填界面时置位，避免控件事件被误判为用户操作。</summary>
    private bool _uiUpdating;

    private bool _draggingSv;
    private bool _draggingHue;

    public MyColorPicker()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshUi();
        TextHex.ValidatedTextChanged += (_, _) => OnHexInput();
        // 输入非法时退出编辑回显当前颜色，避免留下看不见的错值
        TextHex.LostKeyboardFocus += (_, _) => RefreshUi();
    }

    /// <summary>用户改变颜色后触发（程序回填不会触发）。</summary>
    public event EventHandler? ColorChanged;

    public Color Color => _color;

    /// <summary>
    /// 设置当前颜色。外部回填界面时请保持 <paramref name="raiseEvent" /> 为 false。
    /// </summary>
    public void SetColor(Color color, bool raiseEvent = false)
    {
        _color = color;
        var (h, s, v) = ColorToHsv(color);
        // 纯灰/纯黑时色相没有意义，沿用旧值以免拖动滑块时色相跳回 0
        if (s > 0.001) _hue = h;
        _sat = s;
        _val = v;
        RefreshUi();
        if (raiseEvent) ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 由当前 HSV 状态推回 RGB，并刷新界面与事件。
    /// </summary>
    private void CommitHsv()
    {
        _color = HsvToColor(_hue, _sat, _val);
        RefreshUi();
        if (!_uiUpdating) ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshUi()
    {
        if (!IsLoaded) return;
        _uiUpdating = true;
        try
        {
            RectHue.Fill = new SolidColorBrush(HsvToColor(_hue, 1, 1));
            var width = CanvasSv.ActualWidth > 0 ? CanvasSv.ActualWidth : 164;
            var height = CanvasSv.ActualHeight > 0 ? CanvasSv.ActualHeight : 104;
            Canvas.SetLeft(ShapeCursor, _sat * width - 5.5);
            Canvas.SetTop(ShapeCursor, (1 - _val) * height - 5.5);
            ShapeHueMark.Margin = new Thickness(-1, Math.Clamp(_hue / 360d, 0, 1) * (height - 3), -1, 0);
            if (!TextHex.IsKeyboardFocusWithin)
                TextHex.Text = $"#{_color.R:X2}{_color.G:X2}{_color.B:X2}";
        }
        finally
        {
            _uiUpdating = false;
        }
    }

    #region 饱和度 / 明度色板

    private void PanSv_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingSv = true;
        PanSv.CaptureMouse();
        UpdateSvFromPoint(e.GetPosition(CanvasSv));
    }

    private void PanSv_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingSv) UpdateSvFromPoint(e.GetPosition(CanvasSv));
    }

    private void PanSv_MouseUp(object sender, MouseEventArgs e)
    {
        if (!_draggingSv) return;
        _draggingSv = false;
        PanSv.ReleaseMouseCapture();
    }

    private void UpdateSvFromPoint(Point point)
    {
        var width = CanvasSv.ActualWidth > 0 ? CanvasSv.ActualWidth : 164;
        var height = CanvasSv.ActualHeight > 0 ? CanvasSv.ActualHeight : 104;
        _sat = Math.Clamp(point.X / width, 0, 1);
        _val = 1 - Math.Clamp(point.Y / height, 0, 1);
        CommitHsv();
    }

    #endregion

    #region 色相条

    private void PanHue_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingHue = true;
        PanHue.CaptureMouse();
        UpdateHueFromPoint(e.GetPosition(PanHue));
    }

    private void PanHue_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingHue) UpdateHueFromPoint(e.GetPosition(PanHue));
    }

    private void PanHue_MouseUp(object sender, MouseEventArgs e)
    {
        if (!_draggingHue) return;
        _draggingHue = false;
        PanHue.ReleaseMouseCapture();
    }

    private void UpdateHueFromPoint(Point point)
    {
        var height = PanHue.ActualHeight > 0 ? PanHue.ActualHeight : 104;
        // 取 360 会让色相绕回红色，这里压到 359.9 保留连续感
        _hue = Math.Clamp(point.Y / height, 0, 1) * 360;
        if (_hue >= 360) _hue = 359.9;
        CommitHsv();
    }

    #endregion

    #region 十六进制输入

    private void OnHexInput()
    {
        if (_uiUpdating) return;
        var text = (TextHex.Text ?? "").Trim().TrimStart('#');
        if (text.Length != 6) return;
        foreach (var c in text)
            if (!Uri.IsHexDigit(c))
                return;
        var r = Convert.ToInt32(text[..2], 16);
        var g = Convert.ToInt32(text.Substring(2, 2), 16);
        var b = Convert.ToInt32(text.Substring(4, 2), 16);
        SetColor(Color.FromRgb((byte)r, (byte)g, (byte)b), true);
    }

    #endregion

    #region 颜色空间转换

    private static Color HsvToColor(double hue, double sat, double val)
    {
        hue = ((hue % 360) + 360) % 360;
        var c = val * sat;
        var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = val - c;
        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x)
        };
        return Color.FromRgb(
            (byte)Math.Round(Math.Clamp(r + m, 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(g + m, 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(b + m, 0, 1) * 255));
    }

    private static (double Hue, double Sat, double Val) ColorToHsv(Color color)
    {
        double r = color.R / 255d, g = color.G / 255d, b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        double hue = 0;
        if (delta > 1e-6)
        {
            if (Math.Abs(max - r) < 1e-6) hue = 60 * (((g - b) / delta) % 6);
            else if (Math.Abs(max - g) < 1e-6) hue = 60 * ((b - r) / delta + 2);
            else hue = 60 * ((r - g) / delta + 4);
        }

        if (hue < 0) hue += 360;
        var sat = max <= 1e-6 ? 0 : delta / max;
        return (hue, sat, max);
    }

    #endregion
}
