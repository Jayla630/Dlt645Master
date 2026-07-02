using Dlt645Master.App.Configuration;
using Dlt645Master.App.Simulation;
using Dlt645Master.App.Tests.Fakes;
using Dlt645Master.App.ViewModels;
using Dlt645Master.Core.Protocol;
using Dlt645Master.Core.Services;
using Dlt645Master.Core.Transport;
using FluentAssertions;

namespace Dlt645Master.App.Tests;

/// <summary>
/// 冒烟集成测试：用真实仿真链路（<see cref="SimulatedTransportFactory"/> + 真实
/// <see cref="MeterPollingService"/> + 真实 <see cref="Dlt645Protocol"/>）驱动真实
/// <see cref="MainWindowViewModel"/>，走完「连接 → 启动轮询 → 收到读数/报文/统计 → 停止 → 断开」，
/// 验证全程无未处理异常且数据正确落入绑定集合。此为验收标准三的可复现替代。
/// </summary>
public class MainWindowViewModelSimulationTests
{
    [Fact]
    public void SimulationChain_ConnectStartReadStopDisconnect_PopulatesReadingsFramesAndStats()
    {
        ITransport transport = SimulatedTransportFactory.Create();
        var service = new MeterPollingService(transport, new Dlt645Protocol());
        var vm = new MainWindowViewModel(transport, service, new SyncUiDispatcher());

        try
        {
            vm.MeterAddressText.Should().Be(AppDefaults.SimulatedMeterAddress);

            vm.ConnectCommand.Execute();
            vm.IsConnected.Should().BeTrue();

            vm.StartPollingCommand.Execute();
            vm.IsPolling.Should().BeTrue();

            SpinWait.SpinUntil(() => vm.Readings.Count > 0 && vm.FrameLog.Count > 0, TimeSpan.FromSeconds(5))
                .Should().BeTrue("仿真链路应在数秒内产出读数与报文");

            vm.StopPollingCommand.Execute();

            vm.Readings.Should().Contain(r => r.IsSuccess && r.Value.HasValue);
            vm.FrameLog.Should().NotBeEmpty();
            (vm.TxCount + vm.RxCount).Should().BeGreaterThan(0);
            vm.ErrorCount.Should().Be(0, "预置数据源覆盖全部默认选中数据项，不应有解析错误");

            vm.DisconnectCommand.Execute();
            vm.IsConnected.Should().BeFalse();
            vm.IsPolling.Should().BeFalse();
        }
        finally
        {
            vm.Dispose();
        }
    }
}
