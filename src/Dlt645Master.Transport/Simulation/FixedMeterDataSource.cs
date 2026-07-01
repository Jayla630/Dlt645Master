using Dlt645Master.Core.Configuration;

namespace Dlt645Master.Transport.Simulation;

/// <summary>固定值数据源（测试确定性用）。默认含：正向有功总电能 DI=00 01 00 00 → 0.00m。</summary>
public sealed class FixedMeterDataSource : IMeterDataSource
{
    private readonly List<(byte[] DataId, decimal Value)> _entries = [];

    public FixedMeterDataSource()
    {
        Set(DataItemCatalog.ForwardActiveEnergy.DataId, 0.00m);
    }

    /// <summary>链式配置某个数据标识的当前值，便于测试。</summary>
    public FixedMeterDataSource Set(byte[] dataId, decimal value)
    {
        int index = _entries.FindIndex(entry => entry.DataId.SequenceEqual(dataId));
        if (index >= 0)
        {
            _entries[index] = (dataId, value);
        }
        else
        {
            _entries.Add((dataId, value));
        }

        return this;
    }

    public bool TryGetValue(byte[] dataId, out decimal value)
    {
        foreach ((byte[] entryDataId, decimal entryValue) in _entries)
        {
            if (entryDataId.SequenceEqual(dataId))
            {
                value = entryValue;
                return true;
            }
        }

        value = default;
        return false;
    }
}
