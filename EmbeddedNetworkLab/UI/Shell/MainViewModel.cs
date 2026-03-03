using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Core;
using EmbeddedNetworkLab.Infrastructure.Services;
using EmbeddedNetworkLab.Modules;
using EmbeddedNetworkLab.UI.Modules.MqttBroker;
using EmbeddedNetworkLab.UI.Modules.TcpClient;
using EmbeddedNetworkLab.UI.Modules.Serial;
using EmbeddedNetworkLab.UI.Modules.SimulatorCentrale;
using EmbeddedNetworkLab.UI.Windows;
using System.Windows;

namespace EmbeddedNetworkLab.UI.Shell
{
	public partial class MainViewModel : ObservableObject
	{
		private readonly ITcpClientService _tcpClientService;
		private readonly IMqttBrokerService _mqttBrokerService;

		// concrete instances kept for wiring module-specific events
		private readonly TcpClientViewModel _tcpClientModuleInstance;
		private readonly MqttBrokerViewModel _mqttBrokerModuleInstance;
		private readonly SimulatorCentraleViewModel _simulatorCentraleModuleInstance;

		// Exposed bindable module properties (for XAML bindings)
		[ObservableProperty]
		private IModule tcpClientModule;

		[ObservableProperty]
		private IModule mqttBrokerModule;

		[ObservableProperty]
		private IModule simulatorCentraleModule;

		private readonly SerialViewModel _leftSerialModel;

        public MainViewModel(ITcpClientService tcpClientService)
		{
			_tcpClientService = tcpClientService;
			_tcpClientModuleInstance = new TcpClientViewModel(_tcpClientService);
			TcpClientModule = _tcpClientModuleInstance;

			_mqttBrokerService = new MqttNetBrokerService();
			_mqttBrokerModuleInstance = new MqttBrokerViewModel(_mqttBrokerService);
			MqttBrokerModule = _mqttBrokerModuleInstance;

			_simulatorCentraleModuleInstance = new SimulatorCentraleViewModel();
			SimulatorCentraleModule = _simulatorCentraleModuleInstance;

			// Subscribe simulator logs to the shell console, include module name
			_simulatorCentraleModuleInstance.LogEmitted += (s, msg) =>
				AppendLog(msg, (s as IModule)?.Name ?? _simulatorCentraleModuleInstance.Name);

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
		private void OpenTcpClient()
		{
			CurrentModule = _tcpClientModuleInstance;
			AppendLog("selected", _tcpClientModuleInstance.Name);
		}

        [RelayCommand]
        private void OpenMqttBroker()
        {
            CurrentModule = _mqttBrokerModuleInstance;
            AppendLog("selected", _mqttBrokerModuleInstance.Name);
        }

        [RelayCommand]
		private void OpenSimulatorCentrale()
		{
			CurrentModule = _simulatorCentraleModuleInstance;
			AppendLog("selected", _simulatorCentraleModuleInstance.Name);
		}

		// Opens the SerialCommandsWindow when the corresponding command is executed.
		[RelayCommand]
		private void OpenSerialCommands()
		{
			var win = new SerialCommandsWindow
			{
				Owner = Application.Current?.MainWindow
			};
			win.Show();
			AppendLog("opened", "SerialCommandsWindow");
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

