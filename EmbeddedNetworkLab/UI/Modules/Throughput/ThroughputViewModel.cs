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

		protected override void OnRunningStateChanged(bool isRunning)
		{
			StartCommand.NotifyCanExecuteChanged();
		}
	}
}
