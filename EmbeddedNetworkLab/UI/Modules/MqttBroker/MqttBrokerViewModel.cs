using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Core;
using System.Collections.ObjectModel;
using System.Windows;

namespace EmbeddedNetworkLab.UI.Modules.MqttBroker
{

	public partial class MqttBrokerViewModel : ModuleViewModel
	{
		private readonly IMqttBrokerService _brokerService;

		public override string Name => "MQTT Broker";

		public ObservableCollection<string> NetworkInterfaces { get; } = new();
		public ObservableCollection<string> BrokerMessages { get; } = new();
		public ObservableCollection<string> BrokerEvents { get; } = new();

		[ObservableProperty]
		private int brokerPort = 1883;

		[ObservableProperty]
		private string? selectedBindIp;

		[ObservableProperty]
		private string? brokerUsername;

		[ObservableProperty]
		private string? brokerPassword;

		[ObservableProperty]
		private string brokerStatus = "Stopped";

		public MqttBrokerViewModel(IMqttBrokerService brokerService)
		{
			_brokerService = brokerService;

			_brokerService.MessageIntercepted += (s, msg) =>
			{
				Application.Current.Dispatcher.Invoke(() =>
					BrokerMessages.Add(msg));
			};

			_brokerService.BrokerEvent += (s, evt) =>
			{
				Application.Current.Dispatcher.Invoke(() =>
					BrokerEvents.Add(evt));
			};

			LoadNetworkInterfaces();
		}

		[RelayCommand(CanExecute = nameof(CanStart))]
		private async Task Start()
		{
			if (!TryStart())
				return;

			string? bindIp = SelectedBindIp == "0.0.0.0"
				? null
				: SelectedBindIp;

			await _brokerService.StartAsync(
				BrokerPort,
				bindIp,
				BrokerUsername,
				BrokerPassword);

			if (_brokerService.IsRunning)
			{
				var addresses = string.Join(", ", _brokerService.ListeningAddresses);
				BrokerStatus = $"Running (on {addresses})";
			}
			else
			{
				BrokerStatus = "Failed";
				StopExecution();
			}
		}

		[RelayCommand]
		private async Task Stop()
		{
			await _brokerService.StopAsync();
			BrokerStatus = "Stopped";
			StopExecution();

			BrokerMessages.Clear();
			BrokerEvents.Clear();
		}

		private bool CanStart() => !IsRunning;

		protected override void OnRunningStateChanged(bool isRunning)
		{
			StartCommand.NotifyCanExecuteChanged();
		}

		private void LoadNetworkInterfaces()
		{
			NetworkInterfaces.Clear();

			NetworkInterfaces.Add("0.0.0.0");

			var ips = System.Net.NetworkInformation.NetworkInterface
				.GetAllNetworkInterfaces()
				.Where(ni =>
					ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
					ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
				.SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
				.Where(ip => ip.Address.AddressFamily ==
							 System.Net.Sockets.AddressFamily.InterNetwork)
				.Select(ip => ip.Address.ToString())
				.Distinct();

			foreach (var ip in ips)
				NetworkInterfaces.Add(ip);

			SelectedBindIp = NetworkInterfaces.FirstOrDefault();
		}
	}
}
