using Dlt645Master.App.Tests.Fakes;
using Dlt645Master.App.ViewModels;
using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;
using Dlt645Master.Core.Services;
using FluentAssertions;

namespace Dlt645Master.App.Tests;

public class MainWindowViewModelTests
{
    // 显示序地址（高位在前），与仿真从站一致：ReverseBytes([01 72 00 72 00 00]) == [00 00 72 00 72 01]。
    private static readonly byte[] TestAddress = [0x00, 0x00, 0x72, 0x00, 0x72, 0x01];

    private static (MainWindowViewModel Vm, FakeTransport Transport, FakePollingService Service, SyncUiDispatcher Dispatcher) CreateViewModel()
    {
        var transport = new FakeTransport();
        var service = new FakePollingService();
        var dispatcher = new SyncUiDispatcher();
        var vm = new MainWindowViewModel(transport, service, dispatcher);
        return (vm, transport, service, dispatcher);
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

    // ---- 用例 1：初始状态 ----
    [Fact]
    public void InitialState_OnlyConnectCommandIsExecutable()
    {
        (MainWindowViewModel vm, _, _, _) = CreateViewModel();

        vm.ConnectCommand.CanExecute().Should().BeTrue();
        vm.DisconnectCommand.CanExecute().Should().BeFalse();
        vm.StartPollingCommand.CanExecute().Should().BeFalse();
        vm.StopPollingCommand.CanExecute().Should().BeFalse();
        vm.ReadOnceCommand.CanExecute().Should().BeFalse();
        vm.IsConnected.Should().BeFalse();
        vm.IsPolling.Should().BeFalse();
    }

    // ---- 用例 2：连接后 ----
    [Fact]
    public void AfterConnect_StartAndReadOnceExecutable_ConnectNotExecutable()
    {
        (MainWindowViewModel vm, FakeTransport transport, _, _) = CreateViewModel();

        vm.ConnectCommand.Execute();

        vm.IsConnected.Should().BeTrue();
        transport.IsOpen.Should().BeTrue();
        vm.ConnectCommand.CanExecute().Should().BeFalse();
        vm.DisconnectCommand.CanExecute().Should().BeTrue();
        vm.StartPollingCommand.CanExecute().Should().BeTrue();
        vm.ReadOnceCommand.CanExecute().Should().BeTrue();
        vm.StopPollingCommand.CanExecute().Should().BeFalse();
    }

    // ---- 用例 3：轮询中 ----
    [Fact]
    public void WhilePolling_StopExecutable_StartAndReadOnceNotExecutable()
    {
        (MainWindowViewModel vm, _, FakePollingService service, _) = CreateViewModel();
        vm.ConnectCommand.Execute();

        vm.StartPollingCommand.Execute();

        vm.IsPolling.Should().BeTrue();
        service.IsPolling.Should().BeTrue();
        vm.StartPollingCommand.CanExecute().Should().BeFalse();
        vm.ReadOnceCommand.CanExecute().Should().BeFalse();
        vm.StopPollingCommand.CanExecute().Should().BeTrue();
        vm.DisconnectCommand.CanExecute().Should().BeTrue();
    }

    // ---- 用例 4：轮询中断开 ----
    [Fact]
    public void Disconnect_WhilePolling_StopsPollingThenClosesTransport_NoThrow()
    {
        (MainWindowViewModel vm, FakeTransport transport, FakePollingService service, _) = CreateViewModel();
        vm.ConnectCommand.Execute();
        vm.StartPollingCommand.Execute();

        Action act = () => vm.DisconnectCommand.Execute();

        act.Should().NotThrow();
        service.StopCallCount.Should().BeGreaterThanOrEqualTo(1);
        vm.IsPolling.Should().BeFalse();
        vm.IsConnected.Should().BeFalse();
        transport.IsOpen.Should().BeFalse();
    }

    // ---- 用例 5：ReadCompleted 更新 Readings，同 DataId 就地更新 ----
    [Fact]
    public void ReadCompleted_SameDataId_UpdatesExistingReadingInsteadOfAppending()
    {
        (MainWindowViewModel vm, _, FakePollingService service, _) = CreateViewModel();
        byte[] dataId = DataItemCatalog.ForwardActiveEnergy.DataId;

        service.RaiseReadCompleted(SuccessResult(dataId, 12.34m));

        vm.Readings.Should().HaveCount(1);
        vm.Readings[0].Value.Should().Be(12.34m);
        vm.Readings[0].IsSuccess.Should().BeTrue();
        vm.Readings[0].ItemName.Should().Be(DataItemCatalog.ForwardActiveEnergy.Name);

        service.RaiseReadCompleted(SuccessResult(dataId, 56.78m));

        vm.Readings.Should().HaveCount(1);
        vm.Readings[0].Value.Should().Be(56.78m);
    }

    [Fact]
    public void ReadCompleted_DistinctDataIds_ProduceSeparateReadings()
    {
        (MainWindowViewModel vm, _, FakePollingService service, _) = CreateViewModel();

        service.RaiseReadCompleted(SuccessResult(DataItemCatalog.ForwardActiveEnergy.DataId, 1m));
        service.RaiseReadCompleted(SuccessResult(DataItemCatalog.VoltagePhaseA.DataId, 220.1m));

        vm.Readings.Should().HaveCount(2);
    }

    [Fact]
    public void ReadCompleted_FailureWithoutDataId_DoesNotAddReadingButSetsStatus()
    {
        (MainWindowViewModel vm, _, FakePollingService service, _) = CreateViewModel();

        service.RaiseReadCompleted(MeterReadResult.Failure("等待应答超时"));

        vm.Readings.Should().BeEmpty();
        vm.StatusMessage.Should().Contain("超时");
    }

    // ---- 用例 6：报文上限裁剪 ----
    [Fact]
    public void FrameTransferred_BeyondCap_CapsCountAndTrimsOldest()
    {
        (MainWindowViewModel vm, _, FakePollingService service, _) = CreateViewModel();
        int total = MainWindowViewModel.MaxFrameLogEntries + 50;

        for (int i = 0; i < total; i++)
        {
            service.RaiseFrameTransferred(FrameDirection.Tx, [(byte)i, 0x68]);
        }

        vm.FrameLog.Should().HaveCount(MainWindowViewModel.MaxFrameLogEntries);
        // 共发 550 条（首字节 = i），封顶 500 → 保留 i=50..549，最旧一条对应 i=50 (0x32)。
        vm.FrameLog[0].HexText.Should().Be("32 68");
    }

    // ---- 用例 7：三事件均经 IUiDispatcher.Post ----
    [Fact]
    public void ServiceEvents_AreAllMarshaledThroughUiDispatcher()
    {
        (MainWindowViewModel vm, _, FakePollingService service, SyncUiDispatcher dispatcher) = CreateViewModel();
        dispatcher.PostCount.Should().Be(0);

        service.RaiseReadCompleted(SuccessResult(DataItemCatalog.ForwardActiveEnergy.DataId, 1m));
        service.RaiseFrameTransferred(FrameDirection.Tx, [0x68]);
        service.RaiseStatisticsChanged(new CommStatistics { TxFrameCount = 1 });

        dispatcher.PostCount.Should().Be(3);
    }

    // ---- 用例 8：非法地址启动被拒 ----
    [Fact]
    public void StartPolling_WithInvalidAddress_IsRejected_ServiceStartNotCalled()
    {
        (MainWindowViewModel vm, _, FakePollingService service, _) = CreateViewModel();
        vm.ConnectCommand.Execute();
        vm.MeterAddressText = "XYZ-not-hex";

        vm.StartPollingCommand.Execute();

        service.StartCallCount.Should().Be(0);
        vm.IsPolling.Should().BeFalse();
        vm.StatusMessage.Should().NotBeNullOrWhiteSpace();
    }

    // ---- 补充：默认地址 / 默认选择 / 统计刷新 / ReadOnce 更新路径 / 连接失败兜底 ----
    [Fact]
    public void DefaultAddressText_MatchesSimulatedSlaveAddress()
    {
        (MainWindowViewModel vm, _, _, _) = CreateViewModel();

        vm.MeterAddressText.Should().Be("000072007201");
        vm.DataItems.Should().NotBeEmpty();
        vm.DataItems.Should().OnlyContain(o => o.IsSelected);
    }

    [Fact]
    public void StartPolling_ComposesOptionsWithAddressAndSelectedDataIds()
    {
        (MainWindowViewModel vm, _, FakePollingService service, _) = CreateViewModel();
        vm.ConnectCommand.Execute();
        // 只留下前两项被选中。
        for (int i = 2; i < vm.DataItems.Count; i++)
        {
            vm.DataItems[i].IsSelected = false;
        }

        vm.StartPollingCommand.Execute();

        service.StartCallCount.Should().Be(1);
        service.LastStartOptions!.MeterAddress.Should().Equal(TestAddress);
        service.LastStartOptions.DataIds.Should().HaveCount(2);
    }

    [Fact]
    public void StartPolling_WithNoSelectedDataItem_IsRejected()
    {
        (MainWindowViewModel vm, _, FakePollingService service, _) = CreateViewModel();
        vm.ConnectCommand.Execute();
        foreach (DataItemOption option in vm.DataItems)
        {
            option.IsSelected = false;
        }

        vm.StartPollingCommand.CanExecute().Should().BeFalse();
        vm.StartPollingCommand.Execute();

        service.StartCallCount.Should().Be(0);
        vm.StatusMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void StatisticsChanged_RefreshesAllFiveStatisticProperties()
    {
        (MainWindowViewModel vm, _, FakePollingService service, _) = CreateViewModel();

        service.RaiseStatisticsChanged(new CommStatistics
        {
            TxFrameCount = 5,
            RxFrameCount = 4,
            TimeoutCount = 1,
            ErrorCount = 2,
            LastRoundTripMs = 12.5,
        });

        vm.TxCount.Should().Be(5);
        vm.RxCount.Should().Be(4);
        vm.TimeoutCount.Should().Be(1);
        vm.ErrorCount.Should().Be(2);
        vm.LastRoundTripMs.Should().Be(12.5);
    }

    [Fact]
    public void ReadOnce_AppliesReturnedResultThroughSameReadingsPath()
    {
        (MainWindowViewModel vm, _, FakePollingService service, _) = CreateViewModel();
        vm.ConnectCommand.Execute();
        byte[] dataId = DataItemCatalog.ForwardActiveEnergy.DataId; // 默认全选，第一个即正向有功
        service.ReadOnceResult = SuccessResult(dataId, 99.99m);

        vm.ReadOnceCommand.Execute();

        service.ReadOnceCallCount.Should().Be(1);
        service.LastReadOnceDataId.Should().Equal(dataId);
        vm.Readings.Should().ContainSingle(r => r.DataId.SequenceEqual(dataId) && r.Value == 99.99m);
    }

    [Fact]
    public void Connect_WhenTransportThrows_IsCaughtAndReportedInStatus()
    {
        (MainWindowViewModel vm, FakeTransport transport, _, _) = CreateViewModel();
        transport.ThrowOnOpen = true;

        Action act = () => vm.ConnectCommand.Execute();

        act.Should().NotThrow();
        vm.IsConnected.Should().BeFalse();
        vm.StatusMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Dispose_UnsubscribesFromServiceEvents()
    {
        (MainWindowViewModel vm, _, FakePollingService service, _) = CreateViewModel();

        vm.Dispose();
        service.RaiseReadCompleted(SuccessResult(DataItemCatalog.ForwardActiveEnergy.DataId, 1m));

        vm.Readings.Should().BeEmpty("退订后事件不应再改动集合");
    }
}
