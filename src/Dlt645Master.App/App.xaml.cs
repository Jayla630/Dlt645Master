using System.Windows;
using Dlt645Master.App.Services;
using Dlt645Master.App.Simulation;
using Dlt645Master.App.Views;
using Dlt645Master.Core.Protocol;
using Dlt645Master.Core.Services;
using Dlt645Master.Core.Transport;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Prism.Ioc;

namespace Dlt645Master.App;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // LiveCharts2 全局初始化：深色主题（图例/提示等默认画笔随深色 HMI），须在首个图表渲染前执行一次。
        LiveCharts.Configure(config => config.AddDarkTheme());

        base.OnStartup(e);
    }

    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // slice-04：仿真模式装配（全部单例，使视图模型、服务、传输共享同一实例）。
        // 真实串口模式（SerialPortTransport + 串口/波特率选择）留待 slice-05 随界面切换接入。
        containerRegistry.RegisterSingleton<IMeterProtocol, Dlt645Protocol>();
        containerRegistry.RegisterSingleton<IUiDispatcher, WpfUiDispatcher>();
        containerRegistry.RegisterSingleton<ISaveFileDialogService, SaveFileDialogService>();
        containerRegistry.RegisterSingleton<IMeterPollingService, MeterPollingService>();

        // ITransport 需工厂构造（LoopbackTransport 依赖仿真从站 + 预置数据源，非容器可解析的参数）。
        containerRegistry.RegisterSingleton<ITransport>(() => SimulatedTransportFactory.Create());
    }
}
