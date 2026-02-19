using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Core.Services;
using EmbeddedNetworkLab.Infrastructure.Services;
using System.Windows;



namespace EmbeddedNetworkLab.UI.ViewModel
{
	public partial class MainViewModel : ObservableObject
	{
		private readonly IThroughputService _throughputService;

		public MainViewModel()
		{
			_throughputService = new ThroughputService();

			_throughputService.RateUpdated += rate =>
			{
				// Update the UI on the main thread since this event may be raised from a background thread
				Application.Current.Dispatcher.Invoke(() =>
				{
					SelectedModuleTitle = $"Throughput: {rate:F2} Mbps";
				});
			};
		}

		[ObservableProperty]
		private string selectedModuleTitle = "Select a module";

		[ObservableProperty]
		private string consoleText = "Console initialized...";

		[RelayCommand]
		private void OpenThroughput()
		{
			AppendLog("Throughput test started");
			_throughputService.Start();
		}

		// Method to append log messages to the console
		private void AppendLog(string message)
		{
			ConsoleText += $"\n[{DateTime.Now:HH:mm:ss}] {message}";
		}

	}

}

