using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Core;
using System.Windows;

namespace EmbeddedNetworkLab.UI.Modules.TcpClient
{
	public partial class TcpClientViewModel : ModuleViewModel
	{
		private readonly ITcpClientService _service;

		public override string Name => "Tcp Client Test";

		[ObservableProperty]
		private double currentRate;

		[ObservableProperty]
		private string? targetAddress = "127.0.0.1";

		[ObservableProperty]
		private int targetPort = 8080;

		// Text backing for the port input so we can validate parsing
		[ObservableProperty]
		private string? targetPortText = "8080";

		public bool IsConfigurationEditable => !IsRunning;

		public bool IsCmdEnabled => IsRunning || IsPortValid();

		public TcpClientViewModel(ITcpClientService service)
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
			// Additional safety: validate again before starting
			if (string.IsNullOrWhiteSpace(TargetPortText) ||
				!int.TryParse(TargetPortText, out var port) ||
				port < 1 || port > 65535)
			{
				MessageBox.Show("Port invalide. Entrez un entier entre 1 et 65535.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (!TryStart())
				return;

			TargetPort = port;

			_service.Configure(TargetAddress!, TargetPort);
			_service.Start();
		}

		private bool CanStart()
		{
			// Prevent start when already running or when port text is invalid
			if (IsRunning)
				return false;

			return IsPortValid();
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

		// Toggle: can start only if CanStart, can stop if IsRunning
		[RelayCommand(CanExecute = nameof(CanToggle))]
		private void Toggle()
		{
			if (IsRunning)
				Stop();
			else
				Start();
		}

		private bool CanToggle()
		{
			// Allow stopping while running; allow starting only when CanStart is true
			return IsRunning || CanStart();
		}

		public string StartStopLabel => IsRunning ? "Stop" : "Start";

		private bool IsPortValid()
		{
			if (string.IsNullOrWhiteSpace(TargetPortText))
				return false;
			if (!int.TryParse(TargetPortText, out var port))
				return false;
			return port >= 1 && port <= 65535;
		}

		// Notify command availability and the toggle enabled state when relevant inputs change
		partial void OnTargetPortTextChanged(string? value)
		{
			StartCommand.NotifyCanExecuteChanged();
			StopCommand.NotifyCanExecuteChanged();
			ToggleCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(IsCmdEnabled));
		}

		protected override void OnRunningStateChanged(bool isRunning)
		{
			StartCommand.NotifyCanExecuteChanged();
			StopCommand.NotifyCanExecuteChanged();
			ToggleCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(StartStopLabel));
			OnPropertyChanged(nameof(IsConfigurationEditable));
			OnPropertyChanged(nameof(IsCmdEnabled));
		}
	}
}
