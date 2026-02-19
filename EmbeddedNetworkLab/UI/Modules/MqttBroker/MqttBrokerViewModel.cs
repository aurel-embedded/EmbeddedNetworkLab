using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Core.Services;
using MqttManager.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace EmbeddedNetworkLab.UI.Modules.MqttBroker
{
	public partial class MqttBrokerViewModel : ModuleViewModel
	{
		private readonly IMqttBrokerService _brokerService;

		public override string Name => "MQTT Broker";

		[ObservableProperty]
		private int brokerPort = 1883;

		[ObservableProperty]
		private string brokerStatus = "Stopped";

		public ObservableCollection<string> BrokerMessages { get; } = new();
		public ObservableCollection<string> BrokerEvents { get; } = new();

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
		}

		[RelayCommand(CanExecute = nameof(CanStart))]
		private async Task Start()
		{
			if (!TryStart())
				return;

			await _brokerService.StartAsync(BrokerPort);
			BrokerStatus = "Running";
		}

		[RelayCommand]
		private async Task Stop()
		{
			await _brokerService.StopAsync();
			StopExecution();
			BrokerStatus = "Stopped";
		}

		private bool CanStart() => !IsRunning;

		protected override void OnRunningStateChanged(bool isRunning)
		{
			StartCommand.NotifyCanExecuteChanged();
		}
	}
}
