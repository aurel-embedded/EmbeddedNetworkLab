using EmbeddedNetworkLab.Core;
using System.Configuration;
using System.Data;
using System.Windows;

namespace EmbeddedNetworkLab
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IAppConfigService AppConfigService { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // initialize shared services
            AppConfigService = new Infrastructure.Services.AppConfigService();
        }
    }

}
