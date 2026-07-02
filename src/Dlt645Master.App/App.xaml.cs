using System.Windows;
using Dlt645Master.App.Services;
using Dlt645Master.App.Simulation;
using Dlt645Master.App.Views;
using Dlt645Master.Core.Protocol;
using Dlt645Master.Core.Services;
using Dlt645Master.Core.Transport;
using Prism.Ioc;

namespace Dlt645Master.App;

public partial class App
{
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
        containerRegistry.RegisterSingleton<IMeterPollingService, MeterPollingService>();

        // ITransport 需工厂构造（LoopbackTransport 依赖仿真从站 + 预置数据源，非容器可解析的参数）。
        containerRegistry.RegisterSingleton<ITransport>(SimulatedTransportFactory.Create);
    }
}
