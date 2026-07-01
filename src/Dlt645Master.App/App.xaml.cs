using System.Windows;
using Dlt645Master.App.Views;
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
    }
}

