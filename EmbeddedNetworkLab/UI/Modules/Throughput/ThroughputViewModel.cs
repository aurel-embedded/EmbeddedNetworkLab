using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Core;
using System.Windows;

namespace EmbeddedNetworkLab.UI.Modules.Throughput
{
	public partial class ThroughputViewModel : ModuleViewModel
	{
		private readonly IThroughputService _service;

		public override string Name => "Throughput Test";

		[ObservableProperty]
		private double currentRate;

		public ThroughputViewModel(IThroughputService service)
		{
			_service = service;

			_service.RateUpdated += rate =>
			{
				Application.Current.Dispatcher.Invoke(() =>
				{
					CurrentRate = rate;
				});
			};
		}

		[RelayCommand(CanExecute = nameof(CanStart))]
		private void Start()
		{
			if (!TryStart())
				return;

			_service.Start();
		}

		private bool CanStart()
		{
			return !IsRunning;
		}

		[RelayCommand(CanExecute = nameof(CanStop))]
		private void Stop()
		{
			_service.Stop();
			StopExecution();
			CurrentRate = 0d;
		}
		private bool CanStop()
		{
			return IsRunning;
		}

		[RelayCommand]
		private void Toggle()
		{
			if (IsRunning)
				Stop();
			else
				Start();
		}
		public string StartStopLabel => IsRunning ? "Stop" : "Start";

		protected override void OnRunningStateChanged(bool isRunning)
		{
			StartCommand.NotifyCanExecuteChanged();
			StopCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(StartStopLabel));
		}
	}
}
