using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Core;
using EmbeddedNetworkLab.Core.Models;
using EmbeddedNetworkLab.UI.Modules;
using EmbeddedNetworkLab.UI.Windows;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;

namespace EmbeddedNetworkLab.UI.Modules.HttpServer
{
	public partial class HttpServerViewModel : ModuleViewModel
	{
		private readonly IHttpServerService _service;

		public override string Name => "HTTP Server";

		public ObservableCollection<string> NetworkInterfaces { get; } = new();
		public ObservableCollection<ReceivedVideoViewModel> Videos { get; } = new();
		public ObservableCollection<string> EventLog { get; } = new();

		[ObservableProperty]
		private string? selectedBindIp;

		[ObservableProperty]
		private string httpPortText = "8081";

		[ObservableProperty]
		private bool httpsEnabled;

		[ObservableProperty]
		private string httpsPortText = "8443";

		[ObservableProperty]
		private string serverStatus = "Stopped";

		public bool IsConfigurationEditable => !IsRunning;

		public HttpServerViewModel(IHttpServerService service)
		{
			_service = service;

			_service.VideoReceived += (_, video) =>
				Application.Current.Dispatcher.Invoke(() =>
					Videos.Insert(0, new ReceivedVideoViewModel(video, OnPlayVideo)));

			_service.ServerEventTriggered += (_, msg) =>
				Application.Current.Dispatcher.Invoke(() => EventLog.Insert(0, msg));

			LoadNetworkInterfaces();
		}

		[RelayCommand(CanExecute = nameof(CanStart))]
		private async Task Start()
		{
			if (!TryParseConfig(out var ip, out var httpPort, out var httpsPort)) return;
			if (!TryStart()) return;

			await _service.StartAsync(ip, httpPort, HttpsEnabled, httpsPort);

			if (_service.IsRunning)
			{
				var urls = string.Join(", ", _service.ListeningUrls);
				ServerStatus = $"Running ({urls})";
			}
			else
			{
				ServerStatus = "Failed to start";
				StopExecution();
			}
		}

		private bool CanStart() => !IsRunning;

		[RelayCommand(CanExecute = nameof(CanStop))]
		private async Task Stop()
		{
			await _service.StopAsync();
			ServerStatus = "Stopped";
			StopExecution();
		}

		private bool CanStop() => IsRunning;

		[RelayCommand]
		private void ClearVideos() => Videos.Clear();

		[RelayCommand]
		private void ClearEventLog() => EventLog.Clear();

		private void OnPlayVideo(ReceivedVideoViewModel vm)
		{
			var win = new VideoPlayerWindow(vm.Video.FilePath, vm.FileName)
			{
				Owner = Application.Current.MainWindow
			};
			win.Show();
		}

		protected override void OnRunningStateChanged(bool isRunning)
		{
			StartCommand.NotifyCanExecuteChanged();
			StopCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(IsConfigurationEditable));
		}

		private void LoadNetworkInterfaces()
		{
			NetworkInterfaces.Clear();
			NetworkInterfaces.Add("0.0.0.0");

			var ips = System.Net.NetworkInformation.NetworkInterface
				.GetAllNetworkInterfaces()
				.Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
							 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
				.SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
				.Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)
				.Select(ua => ua.Address.ToString())
				.Distinct();

			foreach (var ip in ips)
				NetworkInterfaces.Add(ip);

			NetworkInterfaces.Add("127.0.0.1");
			SelectedBindIp = "127.0.0.1";
		}

		private bool TryParseConfig(out string ip, out int httpPort, out int httpsPort)
		{
			ip = SelectedBindIp ?? "127.0.0.1";
			httpPort = 0;
			httpsPort = 0;

			if (!int.TryParse(HttpPortText, out httpPort) || httpPort is < 1 or > 65535)
				return false;

			if (HttpsEnabled && (!int.TryParse(HttpsPortText, out httpsPort) || httpsPort is < 1 or > 65535))
				return false;

			return true;
		}
	}
}
