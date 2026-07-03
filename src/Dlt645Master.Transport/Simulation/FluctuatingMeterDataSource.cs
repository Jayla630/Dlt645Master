using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;

namespace Dlt645Master.Transport.Simulation;

/// <summary>
/// 波动数据源：包装一份基准值数据源（通常为预置演示值的 <see cref="FixedMeterDataSource"/>），
/// 每次 <see cref="TryGetValue"/> 在上一次返回值附近做有界随机游走，使趋势图与卡片数值真正动起来。
/// 波动参数按 <see cref="DataItemCategory"/> 标定（slice-06），保证窄量程物理量不出荒谬数值：
/// 频率 ±0.01 Hz 钳 [49.90, 50.10]；功率因数 ±0.005 钳 [0.93, 0.99]；电压 ±0.5 V 钳 [209, 235]；
/// 电流/有功功率步长为基准 ±2%、带宽基准 ±5% 且恒大于 0；电能只增不减、每次读取小步累加（模拟真实走字）。
/// 数值精度对齐目录项的小数位（BCD 编码会截断），游走步长不小于最小分辨率，保证连续读取值确实在变。
/// 可注入 Random 种子以便测试可复现。
/// </summary>
public sealed class FluctuatingMeterDataSource : IMeterDataSource
{
    // ---- 标定表（类别 → 游走步长 / 硬钳制值域）。相对参数针对电流/功率（量级随基准而变），
    // 绝对参数针对电压/频率/功率因数（电网物理量有固定合理区间）。----
    private const decimal FrequencyStep = 0.01m;
    private const decimal FrequencyMin = 49.90m;
    private const decimal FrequencyMax = 50.10m;
    private const decimal PowerFactorStep = 0.005m;
    private const decimal PowerFactorMin = 0.93m;
    private const decimal PowerFactorMax = 0.99m;
    private const decimal VoltageStep = 0.5m;
    private const decimal VoltageMin = 209m;
    private const decimal VoltageMax = 235m;
    private const decimal CurrentPowerStepRatio = 0.02m;
    private const decimal CurrentPowerBandRatio = 0.05m;

    /// <summary>未登记 DI 的兜底带宽比例（维持 slice-05 的 ±5% 行为）。</summary>
    private const decimal DefaultBandRatio = 0.05m;

    private readonly IMeterDataSource _baseline;
    private readonly Random _random;

    /// <summary>各数据标识的当前游走值（首次读取时从基准值初始化）。</summary>
    private readonly List<(byte[] DataId, decimal Value)> _current = [];

    /// <param name="baseline">基准值数据源，提供每个数据标识的中心值；查不到的数据标识本源同样查不到。</param>
    /// <param name="seed">随机种子。传入固定值可使波动序列可复现（测试用）；省略则取时间相关种子。</param>
    public FluctuatingMeterDataSource(IMeterDataSource baseline, int? seed = null)
    {
        _baseline = baseline;
        _random = seed is { } value ? new Random(value) : new Random();
    }

    public bool TryGetValue(byte[] dataId, out decimal value)
    {
        if (!_baseline.TryGetValue(dataId, out decimal baseValue))
        {
            value = default;
            return false;
        }

        DataItemDefinition? definition = DataItemCatalog.Find(dataId);
        decimal resolution = Resolution(definition?.DecimalPlaces ?? 2);
        decimal previous = FindCurrent(dataId) ?? baseValue;

        decimal next = definition?.Category == DataItemCategory.Energy
            ? NextEnergy(previous, resolution)
            : NextWalk(previous, ResolveProfile(definition?.Category, baseValue, resolution), resolution);

        SetCurrent(dataId, next);
        value = next;
        return true;
    }

    /// <summary>按类别查标定表，换算出本次游走的（步长, 下界, 上界）；统一保障步长不小于分辨率、下界恒正。</summary>
    private static (decimal Step, decimal Lower, decimal Upper) ResolveProfile(
        DataItemCategory? category, decimal baseValue, decimal resolution)
    {
        decimal magnitude = Math.Abs(baseValue);
        (decimal step, decimal lower, decimal upper) = category switch
        {
            DataItemCategory.Frequency => (FrequencyStep, FrequencyMin, FrequencyMax),
            DataItemCategory.PowerFactor => (PowerFactorStep, PowerFactorMin, PowerFactorMax),
            DataItemCategory.Voltage => (VoltageStep, VoltageMin, VoltageMax),
            DataItemCategory.Current or DataItemCategory.ActivePower =>
                (magnitude * CurrentPowerStepRatio, magnitude * (1m - CurrentPowerBandRatio), magnitude * (1m + CurrentPowerBandRatio)),
            // 未登记 DI（目录查不到类别）：带宽 ±5%、步长取带宽 1/4 的兜底行为。
            _ => (magnitude * DefaultBandRatio / 4m, magnitude * (1m - DefaultBandRatio), magnitude * (1m + DefaultBandRatio)),
        };

        step = Math.Max(step, resolution);
        lower = Math.Max(lower, resolution); // 硬钳制恒大于 0
        upper = Math.Max(upper, lower);      // 基准过小（带宽塌缩）时退化为定值，绝不越带或变负
        return (step, lower, upper);
    }

    /// <summary>电能走字：只增不减，每次累加 1~3 个最小分辨率。</summary>
    private decimal NextEnergy(decimal previous, decimal resolution)
        => previous + resolution * _random.Next(1, 4);

    /// <summary>
    /// 有界随机游走：在上一次值附近小步移动（而非每次独立随机，曲线才像真实电网波动而不是噪声），
    /// 越界截断到钳制值域内。若截断/舍入后与上一次相同，则随机选一侧在带内挪一格——方向必须随机，
    /// 否则窄量程项（频率步长 = 分辨率，约半数步长被舍入吞掉）会被系统性推向一侧、贴边抖动。
    /// </summary>
    private decimal NextWalk(decimal previous, (decimal Step, decimal Lower, decimal Upper) profile, decimal resolution)
    {
        (decimal step, decimal lower, decimal upper) = profile;
        decimal signedStep = step * (decimal)(_random.NextDouble() * 2.0 - 1.0);
        decimal next = RoundTo(Clamp(previous + signedStep, lower, upper), resolution);

        if (next == previous)
        {
            decimal up = previous + resolution;
            decimal down = previous - resolution;
            bool preferUp = _random.Next(2) == 0;
            next = preferUp
                ? (up <= upper ? up : down >= lower ? down : previous)
                : (down >= lower ? down : up <= upper ? up : previous);
        }

        return next;
    }

    private static decimal Resolution(int decimalPlaces)
    {
        decimal resolution = 1m;
        for (int i = 0; i < decimalPlaces; i++)
        {
            resolution /= 10m;
        }

        return resolution;
    }

    private static decimal Clamp(decimal value, decimal lower, decimal upper)
        => Math.Min(Math.Max(value, lower), upper);

    private static decimal RoundTo(decimal value, decimal resolution)
        => Math.Round(value / resolution, MidpointRounding.AwayFromZero) * resolution;

    private decimal? FindCurrent(byte[] dataId)
    {
        foreach ((byte[] entryDataId, decimal entryValue) in _current)
        {
            if (entryDataId.SequenceEqual(dataId))
            {
                return entryValue;
            }
        }

        return null;
    }

    private void SetCurrent(byte[] dataId, decimal value)
    {
        int index = _current.FindIndex(entry => entry.DataId.SequenceEqual(dataId));
        if (index >= 0)
        {
            _current[index] = (dataId, value);
        }
        else
        {
            _current.Add((dataId, value));
        }
    }
}
