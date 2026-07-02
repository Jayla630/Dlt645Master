using System.Collections.ObjectModel;
using Dlt645Master.App.Tests.Fakes;
using Dlt645Master.App.ViewModels;
using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;
using FluentAssertions;

namespace Dlt645Master.App.Tests;

/// <summary>slice-05 图表扩展：电压历史缓冲（定长滚动窗口）与三条趋势线的追加/裁剪/空态口径。</summary>
public class MainWindowViewModelChartTests
{
    private static readonly byte[] TestAddress = [0x00, 0x00, 0x72, 0x00, 0x72, 0x01];

    private static (MainWindowViewModel Vm, FakePollingService Service, SyncUiDispatcher Dispatcher) Create()
    {
        var service = new FakePollingService();
        var vm = new MainWindowViewModel(new FakeTransport(), service, new SyncUiDispatcher());
        return (vm, service, new SyncUiDispatcher());
    }

    private static MeterReadResult SuccessResult(byte[] dataId, decimal value)
    {
        DataItemDefinition? def = DataItemCatalog.Find(dataId);
        return new MeterReadResult
        {
            IsSuccess = true,
            ControlCode = 0x91,
            Address = TestAddress,
            DataId = dataId,
            ItemName = def?.Name,
            Value = value,
            Unit = def?.Unit,
        };
    }

    /// <summary>取第 index 条电压趋势线挂的滚动缓冲（0=A 相，1=B 相，2=C 相）。</summary>
    private static ObservableCollection<double> BufferOf(MainWindowViewModel vm, int index)
        => (ObservableCollection<double>)vm.VoltageSeries[index].Values!;

    [Fact]
    public void VoltageSeries_HasThreeLineSeriesWithEmptyBuffers()
    {
        (MainWindowViewModel vm, _, _) = Create();

        vm.VoltageSeries.Should().HaveCount(3);
        BufferOf(vm, 0).Should().BeEmpty();
        BufferOf(vm, 1).Should().BeEmpty();
        BufferOf(vm, 2).Should().BeEmpty();
        vm.VoltageYAxes.Should().ContainSingle().Which.MinLimit.Should().Be(180);
        vm.VoltageYAxes[0].MaxLimit.Should().Be(260);
        vm.VoltageSections.Should().ContainSingle().Which.Yi.Should().Be(MainWindowViewModel.VoltageAlarmLimit);
    }

    [Fact]
    public void ReadCompleted_VoltagePhaseA_AppendsPointToPhaseABufferOnly()
    {
        (MainWindowViewModel vm, FakePollingService service, _) = Create();

        service.RaiseReadCompleted(SuccessResult(DataItemCatalog.VoltagePhaseA.DataId, 220.5m));

        BufferOf(vm, 0).Should().ContainSingle().Which.Should().Be(220.5);
        BufferOf(vm, 1).Should().BeEmpty();
        BufferOf(vm, 2).Should().BeEmpty();
    }

    [Fact]
    public void ReadCompleted_AllThreePhases_AppendToRespectiveBuffers()
    {
        (MainWindowViewModel vm, FakePollingService service, _) = Create();

        service.RaiseReadCompleted(SuccessResult(DataItemCatalog.VoltagePhaseA.DataId, 220.1m));
        service.RaiseReadCompleted(SuccessResult(DataItemCatalog.VoltagePhaseB.DataId, 219.8m));
        service.RaiseReadCompleted(SuccessResult(DataItemCatalog.VoltagePhaseC.DataId, 221.3m));

        BufferOf(vm, 0).Should().Equal(220.1);
        BufferOf(vm, 1).Should().Equal(219.8);
        BufferOf(vm, 2).Should().Equal(221.3);
    }

    [Fact]
    public void ReadCompleted_BeyondMaxVoltagePoints_CapsWindowAndTrimsOldest()
    {
        (MainWindowViewModel vm, FakePollingService service, _) = Create();
        byte[] dataId = DataItemCatalog.VoltagePhaseA.DataId;
        int total = MainWindowViewModel.MaxVoltagePoints + 10;

        for (int i = 0; i < total; i++)
        {
            service.RaiseReadCompleted(SuccessResult(dataId, 200m + i * 0.1m));
        }

        ObservableCollection<double> buffer = BufferOf(vm, 0);
        buffer.Should().HaveCount(MainWindowViewModel.MaxVoltagePoints);
        // 共发 130 点（200.0、200.1、…），封顶 120 → 最旧保留第 10 点（200 + 10*0.1 = 201.0）。
        buffer[0].Should().Be(201.0);
        buffer[^1].Should().Be(200.0 + (total - 1) * 0.1);
    }

    [Fact]
    public void ReadCompleted_NonVoltageDataId_DoesNotTouchVoltageBuffers()
    {
        (MainWindowViewModel vm, FakePollingService service, _) = Create();

        service.RaiseReadCompleted(SuccessResult(DataItemCatalog.ForwardActiveEnergy.DataId, 12.34m));
        service.RaiseReadCompleted(SuccessResult(DataItemCatalog.CurrentPhaseA.DataId, 1.234m));

        BufferOf(vm, 0).Should().BeEmpty();
        BufferOf(vm, 1).Should().BeEmpty();
        BufferOf(vm, 2).Should().BeEmpty();
        vm.Readings.Should().HaveCount(2, "非电压项仍照常进卡片墙");
    }

    [Fact]
    public void ReadCompleted_VoltageFailure_DoesNotAppendPoint()
    {
        (MainWindowViewModel vm, FakePollingService service, _) = Create();
        byte[] dataId = DataItemCatalog.VoltagePhaseA.DataId;

        service.RaiseReadCompleted(new MeterReadResult
        {
            IsSuccess = false,
            ControlCode = 0xD1,
            Address = TestAddress,
            DataId = dataId,
            ErrorMessage = "从站异常应答",
        });

        BufferOf(vm, 0).Should().BeEmpty("失败读数没有可信数值，不能进趋势图");
        vm.Readings.Should().ContainSingle(r => !r.IsSuccess, "但失败仍要体现在卡片上");
    }

    [Fact]
    public void IsVoltageChartUnsubscribed_TogglesWithVoltageSelection()
    {
        (MainWindowViewModel vm, _, _) = Create();
        vm.IsVoltageChartUnsubscribed.Should().BeFalse("默认全选含三相电压");

        List<string> raised = [];
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        foreach (DataItemOption option in vm.DataItems.Where(o => o.Unit == "V"))
        {
            option.IsSelected = false;
        }

        vm.IsVoltageChartUnsubscribed.Should().BeTrue("三相电压全部取消勾选后图表区应显示空态提示");
        raised.Should().Contain(nameof(MainWindowViewModel.IsVoltageChartUnsubscribed));

        vm.DataItems.First(o => o.Unit == "V").IsSelected = true;
        vm.IsVoltageChartUnsubscribed.Should().BeFalse("勾回任意一相即恢复订阅");
    }
}
