namespace Dlt645Master.Core.Models;

/// <summary>
/// 数据项的物理量类别。作为仿真波动标定（Transport 层）与界面卡片分组（App 层）的单一分类来源，
/// 避免两处按单位或 DI 前缀各自映射产生漂移。纯领域概念，不含任何界面或仿真语义。
/// </summary>
public enum DataItemCategory
{
    /// <summary>电能（kWh），累计量，物理上单调不减。</summary>
    Energy,

    /// <summary>相电压（V）。</summary>
    Voltage,

    /// <summary>相电流（A）。</summary>
    Current,

    /// <summary>有功功率（kW）。</summary>
    ActivePower,

    /// <summary>功率因数（无量纲，0~1）。</summary>
    PowerFactor,

    /// <summary>电网频率（Hz），窄量程物理量。</summary>
    Frequency,
}
