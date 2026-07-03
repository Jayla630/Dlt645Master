using Dlt645Master.App.Tests.Fakes;
using Dlt645Master.App.ViewModels;
using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;
using Dlt645Master.Core.Services;
using FluentAssertions;

namespace Dlt645Master.App.Tests;

/// <summary>
/// 导出命令的状态机测试：可执行条件只看缓冲是否非空（与连接状态无关，断开后仍可导出）。
/// 磁盘写入与真实对话框不在测试范围（格式化逻辑另有纯函数测试）。
/// </summary>
public class MainWindowViewModelExportTests
{
    private static (MainWindowViewModel Vm, FakePollingService Service, FakeSaveFileDialogService Dialog) Create()
    {
        var service = new FakePollingService();
        var dialog = new FakeSaveFileDialogService();
        var vm = new MainWindowViewModel(new FakeTransport(), service, new SyncUiDispatcher(), dialog);
        return (vm, service, dialog);
    }

    private static MeterReadResult SuccessResult()
    {
        DataItemDefinition def = DataItemCatalog.VoltagePhaseA;
        return new MeterReadResult
        {
            IsSuccess = true,
            ControlCode = 0x91,
            DataId = def.DataId,
            ItemName = def.Name,
            Value = 220.5m,
            Unit = def.Unit,
        };
    }

    // ---- 用例 1：空缓冲时两个导出命令均不可执行 ----
    [Fact]
    public void InitialState_BothExportCommandsDisabled()
    {
        (MainWindowViewModel vm, _, _) = Create();

        vm.ExportReadingsCommand.CanExecute().Should().BeFalse();
        vm.ExportFrameLogCommand.CanExecute().Should().BeFalse();
    }

    // ---- 用例 2：收到读数后导出读数可执行，且与连接状态无关 ----
    [Fact]
    public void ExportReadings_ExecutableAfterReadCompleted_EvenWhenDisconnected()
    {
        (MainWindowViewModel vm, FakePollingService service, _) = Create();

        service.RaiseReadCompleted(SuccessResult());

        vm.IsConnected.Should().BeFalse("导出可执行条件与连接状态无关");
        vm.ExportReadingsCommand.CanExecute().Should().BeTrue();
    }

    // ---- 用例 3：失败读数（含超时）同样进记录缓冲 ----
    [Fact]
    public void ExportReadings_ExecutableAfterFailureResult()
    {
        (MainWindowViewModel vm, FakePollingService service, _) = Create();

        service.RaiseReadCompleted(MeterReadResult.Failure("等待应答超时"));

        vm.ExportReadingsCommand.CanExecute().Should().BeTrue();
    }

    // ---- 用例 4：收到报文后导出报文可执行 ----
    [Fact]
    public void ExportFrameLog_ExecutableAfterFrameTransferred()
    {
        (MainWindowViewModel vm, FakePollingService service, _) = Create();

        service.RaiseFrameTransferred(FrameDirection.Tx, [0x68, 0x16]);

        vm.ExportFrameLogCommand.CanExecute().Should().BeTrue();
    }

    // ---- 用例 5：用户取消对话框 → 不写文件不报错，默认文件名带时间戳 ----
    [Fact]
    public void ExportReadings_DialogCancelled_DoesNotThrow_DefaultFileNameHasTimestamp()
    {
        (MainWindowViewModel vm, FakePollingService service, FakeSaveFileDialogService dialog) = Create();
        service.RaiseReadCompleted(SuccessResult());

        Action act = () => vm.ExportReadingsCommand.Execute();

        act.Should().NotThrow();
        dialog.PromptCount.Should().Be(1);
        dialog.LastDefaultFileName.Should().MatchRegex(@"^读数记录_\d{8}_\d{6}\.csv$");
    }

    [Fact]
    public void ExportFrameLog_DialogCancelled_DoesNotThrow_DefaultFileNameHasTimestamp()
    {
        (MainWindowViewModel vm, FakePollingService service, FakeSaveFileDialogService dialog) = Create();
        service.RaiseFrameTransferred(FrameDirection.Rx, [0x68, 0x16]);

        Action act = () => vm.ExportFrameLogCommand.Execute();

        act.Should().NotThrow();
        dialog.PromptCount.Should().Be(1);
        dialog.LastDefaultFileName.Should().MatchRegex(@"^报文日志_\d{8}_\d{6}\.txt$");
    }

    // ---- 用例 6：写文件失败（非法路径）→ 异常兜底进状态栏，不打穿界面 ----
    [Fact]
    public void ExportReadings_WriteFailure_IsCaughtAndReportedInStatus()
    {
        (MainWindowViewModel vm, FakePollingService service, FakeSaveFileDialogService dialog) = Create();
        service.RaiseReadCompleted(SuccessResult());
        dialog.PathToReturn = @"Z:\不存在的目录\不可写\读数.csv";

        Action act = () => vm.ExportReadingsCommand.Execute();

        act.Should().NotThrow();
        vm.StatusMessage.Should().Contain("导出");
    }
}
