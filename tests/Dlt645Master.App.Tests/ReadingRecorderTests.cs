using Dlt645Master.App.Services;
using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;
using FluentAssertions;

namespace Dlt645Master.App.Tests;

public class ReadingRecorderTests
{
    private static MeterReadResult SuccessResult(decimal value) => new()
    {
        IsSuccess = true,
        ControlCode = 0x91,
        DataId = DataItemCatalog.VoltagePhaseA.DataId,
        ItemName = DataItemCatalog.VoltagePhaseA.Name,
        Value = value,
        Unit = DataItemCatalog.VoltagePhaseA.Unit,
    };

    // ---- 用例 1：成功读数的字段映射 ----
    [Fact]
    public void Append_SuccessResult_MapsAllFields()
    {
        var recorder = new ReadingRecorder();
        var timestamp = new DateTimeOffset(2026, 7, 3, 8, 47, 12, 345, TimeSpan.FromHours(8));

        recorder.Append(SuccessResult(220.5m), timestamp);

        recorder.Count.Should().Be(1);
        ReadingRecord record = recorder.Snapshot()[0];
        record.Timestamp.Should().Be(timestamp);
        record.ItemName.Should().Be("A 相电压");
        record.DataIdText.Should().Be("02 01 01 00");
        record.Value.Should().Be(220.5m);
        record.Unit.Should().Be("V");
        record.IsSuccess.Should().BeTrue();
        record.ErrorMessage.Should().BeEmpty();
    }

    // ---- 用例 2：无 DataId 的失败（超时/校验和错）也要记录，名称与 DI 留空 ----
    [Fact]
    public void Append_FailureWithoutDataId_RecordsErrorWithEmptyNameAndDataId()
    {
        var recorder = new ReadingRecorder();

        recorder.Append(MeterReadResult.Failure("等待应答超时"), DateTimeOffset.Now);

        ReadingRecord record = recorder.Snapshot()[0];
        record.IsSuccess.Should().BeFalse();
        record.ErrorMessage.Should().Be("等待应答超时");
        record.ItemName.Should().BeEmpty();
        record.DataIdText.Should().BeEmpty();
        record.Value.Should().BeNull();
    }

    // ---- 用例 3：结果未带 ItemName 时按目录补齐名称 ----
    [Fact]
    public void Append_ResultWithoutItemName_FallsBackToCatalogName()
    {
        var recorder = new ReadingRecorder();
        var result = new MeterReadResult
        {
            IsSuccess = true,
            DataId = DataItemCatalog.GridFrequency.DataId,
            Value = 50.01m,
        };

        recorder.Append(result, DateTimeOffset.Now);

        recorder.Snapshot()[0].ItemName.Should().Be("电网频率");
    }

    // ---- 用例 4：封顶 20000 条，满则裁最旧 ----
    [Fact]
    public void Append_BeyondCap_TrimsOldestAndKeepsCap()
    {
        var recorder = new ReadingRecorder();
        int total = ReadingRecorder.MaxRecords + 30;

        for (int i = 0; i < total; i++)
        {
            recorder.Append(SuccessResult(i), DateTimeOffset.Now);
        }

        recorder.Count.Should().Be(ReadingRecorder.MaxRecords);
        // 共 20030 条（值 = i），裁掉最旧 30 条 → 首条值应为 30。
        recorder.Snapshot()[0].Value.Should().Be(30m);
    }

    // ---- 用例 5：Snapshot 是拷贝，后续追加不影响已取快照 ----
    [Fact]
    public void Snapshot_IsIndependentCopy()
    {
        var recorder = new ReadingRecorder();
        recorder.Append(SuccessResult(1m), DateTimeOffset.Now);

        IReadOnlyList<ReadingRecord> snapshot = recorder.Snapshot();
        recorder.Append(SuccessResult(2m), DateTimeOffset.Now);

        snapshot.Should().HaveCount(1);
        recorder.Count.Should().Be(2);
    }

    // ---- 用例 6：并发追加不丢记录（记录发生在后台事件线程，缓冲必须线程安全）----
    [Fact]
    public void Append_ConcurrentWriters_LosesNoRecordBelowCap()
    {
        var recorder = new ReadingRecorder();
        const int writers = 8;
        const int perWriter = 500;

        Parallel.For(0, writers, _ =>
        {
            for (int i = 0; i < perWriter; i++)
            {
                recorder.Append(SuccessResult(i), DateTimeOffset.Now);
            }
        });

        recorder.Count.Should().Be(writers * perWriter);
    }
}
