using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;

namespace Dlt645Master.App.Services;

/// <summary>
/// 读数记录缓冲：为 CSV 导出留存历史读数（卡片墙只保留每项最新值，没有历史可导）。
/// 定长滚动窗口，上限 <see cref="MaxRecords"/> 条，满则裁最旧——与报文监视/电压趋势的口径一致。
/// 追加发生在轮询服务的后台事件线程、快照读取发生在界面线程，因此内部加锁保证线程安全。
/// </summary>
public sealed class ReadingRecorder
{
    /// <summary>记录上限：1 秒轮询 × 11 个数据项 ≈ 可留存半小时数据，演示绰绰有余。</summary>
    public const int MaxRecords = 20000;

    private readonly object _gate = new();
    private readonly Queue<ReadingRecord> _records = new();

    /// <summary>当前记录条数（线程安全）。</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _records.Count;
            }
        }
    }

    /// <summary>
    /// 把一次读取结果追加为记录。名称缺失时按 <see cref="DataItemCatalog"/> 补齐；
    /// 无 DI 的失败（超时/校验和错）名称与 DI 留空串，只记时间戳与失败原因。
    /// </summary>
    public void Append(MeterReadResult result, DateTimeOffset timestamp)
    {
        DataItemDefinition? definition = result.DataId is { } dataId ? DataItemCatalog.Find(dataId) : null;
        var record = new ReadingRecord(
            timestamp,
            result.ItemName ?? definition?.Name ?? string.Empty,
            result.DataId is { } id ? HexFormat.Spaced(id) : string.Empty,
            result.Value,
            result.Unit ?? definition?.Unit ?? string.Empty,
            result.IsSuccess,
            result.ErrorMessage ?? string.Empty);

        lock (_gate)
        {
            _records.Enqueue(record);
            while (_records.Count > MaxRecords)
            {
                _records.Dequeue();
            }
        }
    }

    /// <summary>取当前全部记录的独立拷贝（线程安全），供导出在界面线程读取。</summary>
    public IReadOnlyList<ReadingRecord> Snapshot()
    {
        lock (_gate)
        {
            return [.. _records];
        }
    }
}
