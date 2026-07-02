using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;

namespace Dlt645Master.Transport.Simulation;

/// <summary>
/// 波动数据源：包装一份基准值数据源（通常为预置演示值的 <see cref="FixedMeterDataSource"/>），
/// 每次 <see cref="TryGetValue"/> 在上一次返回值附近做有界随机游走，使趋势图与卡片数值真正动起来。
/// 波动规则按目录项单位区分：
/// 电压类（单位 V）基准 ±2% 且始终大于 0；电能类（单位 kWh）只增不减、每次读取小步累加（模拟真实走字）；
/// 其余（电流/功率/频率/功率因数等）基准 ±5%。
/// 数值精度对齐目录项的小数位（BCD 编码会截断），游走步长不小于最小分辨率，保证连续读取值确实在变。
/// 可注入 Random 种子以便测试可复现。
/// </summary>
public sealed class FluctuatingMeterDataSource : IMeterDataSource
{
    private const decimal VoltageAmplitude = 0.02m;
    private const decimal DefaultAmplitude = 0.05m;
    private const string VoltageUnit = "V";
    private const string EnergyUnit = "kWh";

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

        decimal next = definition?.Unit == EnergyUnit
            ? NextEnergy(previous, resolution)
            : NextWalk(previous, baseValue, resolution, definition?.Unit == VoltageUnit ? VoltageAmplitude : DefaultAmplitude);

        SetCurrent(dataId, next);
        value = next;
        return true;
    }

    /// <summary>电能走字：只增不减，每次累加 1~3 个最小分辨率。</summary>
    private decimal NextEnergy(decimal previous, decimal resolution)
        => previous + resolution * _random.Next(1, 4);

    /// <summary>
    /// 有界随机游走：在上一次值附近小步移动（而非每次独立随机，曲线才像真实电网波动而不是噪声），
    /// 并夹取到基准值 ±amplitude 的带内、保证大于 0。步长向上对齐到最小分辨率，若夹取后与上一次相同则
    /// 在带内强制挪一格，保证连续读取值不同。
    /// </summary>
    private decimal NextWalk(decimal previous, decimal baseValue, decimal resolution, decimal amplitude)
    {
        decimal band = Math.Abs(baseValue) * amplitude;
        decimal lower = Math.Max(baseValue - band, resolution);
        decimal upper = Math.Max(baseValue + band, lower); // 基准过小（带宽塌缩）时退化为定值，绝不越带或变负

        // 单步幅度取带宽的 1/4 与最小分辨率的较大者，映射 [-1,1) 的随机数为有符号步长。
        decimal maxStep = Math.Max(band / 4m, resolution);
        decimal step = maxStep * (decimal)(_random.NextDouble() * 2.0 - 1.0);

        decimal next = RoundTo(Clamp(previous + step, lower, upper), resolution);
        if (next == previous)
        {
            // 贴边或步长被舍入吞掉时强制挪一格，方向选带内可行的一侧；两侧都不可行（带宽塌缩）则保持原值。
            next = previous + resolution <= upper ? previous + resolution
                : previous - resolution >= lower ? previous - resolution
                : previous;
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
