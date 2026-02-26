using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Core;
using EmbeddedNetworkLab.Infrastructure.Services;
using EmbeddedNetworkLab.Modules;
using EmbeddedNetworkLab.UI.Modules.MqttBroker;
using EmbeddedNetworkLab.UI.Modules.Throughput;
using EmbeddedNetworkLab.UI.Modules.Serial;
using EmbeddedNetworkLab.UI.Modules.SimulatorCentrale;



namespace EmbeddedNetworkLab.UI.Shell
{
	public partial class MainViewModel : ObservableObject
	{
		private readonly IThroughputService _throughputService;
		private readonly IMqttBrokerService _mqttBrokerService;

		private readonly ThroughputViewModel _throughputModule;
		private readonly MqttBrokerViewModel _mqttBrokerModule;
		private readonly SimulatorCentraleViewModel _simulatorCentraleModule;

		private readonly SerialViewModel _leftSerialModel;


        public MainViewModel()
		{
			_throughputService = new ThroughputService();
			_throughputModule = new ThroughputViewModel(_throughputService);

			_mqttBrokerService = new MqttNetBrokerService();
			_mqttBrokerModule = new MqttBrokerViewModel(_mqttBrokerService);

			_simulatorCentraleModule = new SimulatorCentraleViewModel();

			// Subscribe simulator logs to the shell console, include module name
			_simulatorCentraleModule.LogEmitted += (s, msg) =>
				AppendLog(msg, (s as IModule)?.Name ?? _simulatorCentraleModule.Name);

			_leftSerialModel = new SerialViewModel { Title = "THW", SerialText = "" };

			// Subscribe serial VM log events to the shell console, use Title as module name
			_leftSerialModel.LogEmitted += (s, msg) =>
				AppendLog(msg, _leftSerialModel.Title ?? "Serial");

			LeftSerial = _leftSerialModel;
        }

		[ObservableProperty]
		private string selectedModuleTitle = "Select a module";

		[ObservableProperty]
		private string consoleText = "Console initialized...";

		[ObservableProperty]
		private IModule currentModule;

		// Expose the single serial view model for binding in MainWindow
		[ObservableProperty]
		private SerialViewModel leftSerial;

		[RelayCommand]
		private void OpenThroughput()
		{
			CurrentModule = _throughputModule;
			AppendLog("selected", _throughputModule.Name);
		}

        [RelayCommand]
        private void OpenMqttBroker()
        {
            CurrentModule = _mqttBrokerModule;
            AppendLog("selected", _mqttBrokerModule.Name);
        }

        [RelayCommand]
		private void OpenSimulatorCentrale()
		{
			CurrentModule = _simulatorCentraleModule;
			AppendLog("selected", _simulatorCentraleModule.Name);
		}

        // Method to append log messages to the console (adds its own timestamp).
        // moduleName is optional; when present it is shown as [Module].
        private void AppendLog(string message, string? moduleName = null)
		{
			var modulePart = string.IsNullOrWhiteSpace(moduleName) ? "Shell" : moduleName;
			ConsoleText += $"\n[{DateTime.Now:HH:mm:ss}] [{modulePart}] {message}";
		}

	}

}

