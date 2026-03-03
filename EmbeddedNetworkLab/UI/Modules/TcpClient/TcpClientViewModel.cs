using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace EmbeddedNetworkLab.UI.Modules.TcpClient
{
	public partial class TcpClientViewModel : ModuleViewModel
	{
		private readonly ITcpThroughputService _service;
		private readonly ITcpReachabilityService _reachService;

		private CancellationTokenSource? _reachCts;

		public override string Name => "Tcp Client";

		[ObservableProperty]
		private string? targetAddress = "192.168.1.110";

		[ObservableProperty]
		private int targetPort = 8080;

		[ObservableProperty]
		private double currentRate;

		[ObservableProperty]
		private string? targetPortText = "8080";

		[ObservableProperty]
		private bool isReaching;

		[ObservableProperty]
		private string reachedStatus = "";

		public bool IsConfigurationEditable => !IsRunning && !IsReaching;

		public bool IsCmdEnabled => !IsReaching;

		public string StartStopLabel => IsRunning ? "Stop" : "Start";

		public TcpClientViewModel(ITcpThroughputService service, ITcpReachabilityService reachService)
		{
			_service = service ?? throw new ArgumentNullException(nameof(service));
			_reachService = reachService ?? throw new ArgumentNullException(nameof(reachService));

			_service.RateUpdated += rate =>
			{
				Application.Current.Dispatcher.Invoke(() =>
				{
					CurrentRate = rate;
				});
			};
		}

		//------------------------------------------------------------------------------
		/// \brief Reach server (simple TCP connect with timeout)
		//------------------------------------------------------------------------------
		[RelayCommand(CanExecute = nameof(CanReachServer))]
		private async Task ReachedServerAsync()
		{
			if (!TryGetTarget(out var ip, out var port))
			{
				ReachedStatus = "Invalid target";
				return;
			}

			IsReaching = true;
			ReachedStatus = "Reaching...";

			// Add a short delay so the UI shows the "Reaching..." status before proceeding.
			await Task.Delay(TimeSpan.FromSeconds(1));

			_reachCts?.Cancel();
			_reachCts?.Dispose();
			_reachCts = new CancellationTokenSource();

			try
			{
				bool ok = await _reachService.TryConnectAsync(ip, port, TimeSpan.FromSeconds(2), _reachCts.Token);
				ReachedStatus = ok ? "OK" : "FAIL";
			}
			finally
			{
				IsReaching = false;
				RefreshCommandStates();
			}
		}

		private bool CanReachServer()
		{
			if (IsReaching)
				return false;

			return TryGetTarget(out _, out _);
		}

		//------------------------------------------------------------------------------
		/// \brief Start throughput (send data continuously)
		//------------------------------------------------------------------------------
		[RelayCommand(CanExecute = nameof(CanStart))]
		private void Start()
		{
			if (!TryGetTarget(out var ip, out var port))
			{
				MessageBox.Show("Port invalide. Entrez un entier entre 1 et 65535.", "Validation",
					MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (!TryStart())
				return;

			TargetPort = port;

			_service.Configure(ip, port);
			_service.Start();

			RefreshCommandStates();
		}

		private bool CanStart()
		{
			if (IsRunning || IsReaching)
				return false;

			return TryGetTarget(out _, out _);
		}

		//------------------------------------------------------------------------------
		/// \brief Stop throughput
		//------------------------------------------------------------------------------
		[RelayCommand(CanExecute = nameof(CanStop))]
		private void Stop()
		{
			_service.Stop();
			StopExecution();
			CurrentRate = 0d;

			RefreshCommandStates();
		}

		private bool CanStop()
		{
			return IsRunning && !IsReaching;
		}

		//------------------------------------------------------------------------------
		/// \brief Toggle start/stop
		//------------------------------------------------------------------------------
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
			if (IsReaching)
				return false;

			return IsRunning || CanStart();
		}

		private bool TryGetTarget(out string ip, out int port)
		{
			ip = TargetAddress?.Trim() ?? string.Empty;
			port = 0;

			if (string.IsNullOrWhiteSpace(ip))
				return false;

			if (string.IsNullOrWhiteSpace(TargetPortText))
				return false;

			if (!int.TryParse(TargetPortText, out port))
				return false;

			return port is >= 1 and <= 65535;
		}

		private void RefreshCommandStates()
		{
			StartCommand.NotifyCanExecuteChanged();
			StopCommand.NotifyCanExecuteChanged();
			ToggleCommand.NotifyCanExecuteChanged();
			ReachedServerCommand.NotifyCanExecuteChanged();

			OnPropertyChanged(nameof(StartStopLabel));
			OnPropertyChanged(nameof(IsConfigurationEditable));
			OnPropertyChanged(nameof(IsCmdEnabled));
		}

		partial void OnTargetAddressChanged(string? value) => RefreshCommandStates();
		partial void OnTargetPortTextChanged(string? value) => RefreshCommandStates();
		partial void OnIsReachingChanged(bool value) => RefreshCommandStates();
		protected override void OnRunningStateChanged(bool isRunning) => RefreshCommandStates();
	}
}