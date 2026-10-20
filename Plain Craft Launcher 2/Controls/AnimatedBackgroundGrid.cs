using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL;

public class AnimatedBackgroundGrid : Grid
{
    public static readonly DependencyProperty BackgroundBrushProperty = DependencyProperty.Register("BackgroundBrush",
        typeof(Brush), typeof(AnimatedBackgroundGrid),
        new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), _BackgroundBrushChanged));

    private readonly DependencyProperty _animatableBrushProperty;

    public readonly int uuid = ModBase.GetUuid();

    public AnimatedBackgroundGrid(DependencyProperty brushDp)
    {
        _animatableBrushProperty = brushDp;
        Loaded += (_, _) => Init();
    }

    public AnimatedBackgroundGrid() : this(BackgroundProperty)
    {
    }

    protected virtual FrameworkElement AnimatableElement => this;

    /// <summary>
    /// 参与动画的画刷。类型为 Brush 而非 SolidColorBrush，因为主题启用渐变时这里会被赋值为渐变画刷。
    /// </summary>
    protected virtual Brush AnimatableBrush
    {
        get => AnimatableElement.GetValue(_animatableBrushProperty) as Brush ?? Brushes.Transparent;
        set => AnimatableElement.SetValue(_animatableBrushProperty, value);
    }

    protected bool IsAnimating
    {
        get => field;
        private set => field = value;
    }

    public Brush BackgroundBrush
    {
        get => (Brush)GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    private static void _BackgroundBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var grid = (AnimatedBackgroundGrid)d;
        var newBrush = e.NewValue as Brush ?? Brushes.Transparent;
        // 渐变画刷（以及其它非纯色画刷）无法做颜色补间，直接应用，避免强转 SolidColorBrush 时抛异常
        if (newBrush is not SolidColorBrush brush || grid.AnimatableBrush is not SolidColorBrush oldBrush)
        {
            grid.AnimatableBrush = newBrush;
            return;
        }

        if (!(grid.IsLoaded && grid.IsVisible))
        {
            grid.AnimatableBrush = newBrush;
            return;
        }

        grid.Dispatcher.BeginInvoke(new Func<Task>(async () =>
        {
            grid.IsAnimating = true;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(grid.AnimatableElement, grid._animatableBrushProperty,
                        new ModBase.MyColor(brush) - new ModBase.MyColor(oldBrush), 300)
                }, "MyCard Theme " + grid.uuid);
            await Task.Delay(300);
            grid.AnimatableBrush = newBrush;
            grid.IsAnimating = false;
        }));
    }

    private void Init()
    {
        AnimatableBrush = BackgroundBrush;
    }
}
