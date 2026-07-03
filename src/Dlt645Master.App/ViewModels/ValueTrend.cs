namespace Dlt645Master.App.ViewModels;

/// <summary>卡片数值的涨跌趋势（相对上一次成功读数）。</summary>
public enum ValueTrend
{
    /// <summary>持平或尚无可比较的上一次值；电能类恒为此值（恒升箭头是视觉噪音，刻意不显示）。</summary>
    Flat,

    /// <summary>较上一次上升。</summary>
    Up,

    /// <summary>较上一次下降。</summary>
    Down,
}
