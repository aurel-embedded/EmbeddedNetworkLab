using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Core;
using EmbeddedNetworkLab.Infrastructure.Services;
using EmbeddedNetworkLab.Modules;
using EmbeddedNetworkLab.UI.Modules.MqttBroker;
using EmbeddedNetworkLab.UI.Modules.Throughput;



namespace EmbeddedNetworkLab.UI.Shell
{
	public partial class MainViewModel : ObservableObject
	{
		private readonly IThroughputService _throughputService;
        private readonly IMqttBrokerService _mqttBrokerService;

        private readonly ThroughputViewModel _throughputModule;
		private readonly MqttBrokerViewModel _mqttBrokerModule;


        public MainViewModel()
		{
			_throughputService = new ThroughputService();
			_throughputModule = new ThroughputViewModel(_throughputService);

			_mqttBrokerService = new MqttNetBrokerService();
			_mqttBrokerModule = new MqttBrokerViewModel(_mqttBrokerService);
        }

		[ObservableProperty]
		private string selectedModuleTitle = "Select a module";

		[ObservableProperty]
		private string consoleText = "Console initialized...";

		[ObservableProperty]
		private IModule currentModule;

		[RelayCommand]
		private void OpenThroughput()
		{
			CurrentModule = _throughputModule;
			AppendLog(CurrentModule.Name + " selected");
		}

        [RelayCommand]
        private void OpenMqttBroker()
        {
            CurrentModule = _mqttBrokerModule;
            AppendLog(CurrentModule.Name + " selected");
        }

        // Method to append log messages to the console
        private void AppendLog(string message)
		{
			ConsoleText += $"\n[{DateTime.Now:HH:mm:ss}] {message}";
		}

	}

}

