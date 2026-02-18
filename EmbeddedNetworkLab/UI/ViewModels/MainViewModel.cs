using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Core.Services;
using EmbeddedNetworkLab.Infrastructure.Services;



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
				SelectedModuleTitle = $"Throughput: {rate:F2} Mbps";
			};
		}

		[ObservableProperty]
		private string selectedModuleTitle = "Select a module";

		[ObservableProperty]
		private string consoleText = "Console initialized...";

		[RelayCommand]
		private void OpenThroughput()
		{
			_throughputService.Start();
		}
	}

}

