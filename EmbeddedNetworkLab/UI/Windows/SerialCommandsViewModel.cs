using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Core;
using EmbeddedNetworkLab.Core.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Windows;

namespace EmbeddedNetworkLab.UI.Windows
{
	public partial class SerialCommandsViewModel : ObservableObject
	{
		private readonly IAppConfigService _configService;
		private SerialPort? _serialPort;

		private const int CommandCount = 10;

		public ObservableCollection<string> AvailablePorts { get; } = new();
		public List<int> AvailableBaudRates { get; } = [460800, 115200, 57600, 38400, 19200, 9600];
		public ObservableCollection<SerialCommandRowViewModel> Commands { get; } = new();

		[ObservableProperty]
		private string? selectedPort;

		[ObservableProperty]
		private int selectedBaudRate = 115200;

		[ObservableProperty]
		private bool isConnected;

		[ObservableProperty]
		private string statusText = "Disconnected";

		[ObservableProperty]
		private string receptionText = string.Empty;

		public string ConnectButtonLabel => IsConnected ? "Close" : "Open";

		public SerialCommandsViewModel(IAppConfigService configService)
		{
			_configService = configService;

			for (int i = 0; i < CommandCount; i++)
				Commands.Add(new SerialCommandRowViewModel(SendCommand));

			RefreshPorts();

			var path = _configService.EnsureAndGetCommandsFile();
			LoadCommandsFromPath(path);
		}

		public bool IsNotConnected => !IsConnected;

		partial void OnIsConnectedChanged(bool value)
		{
			OnPropertyChanged(nameof(ConnectButtonLabel));
			OnPropertyChanged(nameof(IsNotConnected));
			foreach (var cmd in Commands)
				cmd.CanSend = value;
			ConnectCommand.NotifyCanExecuteChanged();
		}

		//------------------------------------------------------------------------------
		// Commands
		//------------------------------------------------------------------------------

		[RelayCommand]
		private void RefreshPorts()
		{
			AvailablePorts.Clear();
			try
			{
				var ports = SerialPort.GetPortNames()
					.OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
				foreach (var p in ports)
					AvailablePorts.Add(p);

				if (AvailablePorts.Count > 0)
				{
					SelectedPort ??= AvailablePorts[0];
					StatusText = "Ready";
				}
				else
				{
					StatusText = "No COM ports found";
				}
			}
			catch
			{
				StatusText = "Error listing COM ports";
			}
		}

		[RelayCommand]
		private void Connect()
		{
			if (IsConnected)
				Disconnect();
			else
				OpenPort();
		}

		[RelayCommand]
		private void LoadCommands()
		{
			var dlg = new OpenFileDialog
			{
				Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
				DefaultExt = "json",
				Multiselect = false
			};

			if (dlg.ShowDialog() != true) return;

			try
			{
				var list = _configService.LoadCommandsFromPath(dlg.FileName);
				ApplyCommandList(list);
				StatusText = $"Loaded {list.Count} entries";
			}
			catch (Exception ex)
			{
				StatusText = $"Load failed: {ex.Message}";
			}
		}

		[RelayCommand]
		private void SaveCommands()
		{
			var dlg = new SaveFileDialog
			{
				Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
				DefaultExt = "json",
				FileName = "serial_commands.json"
			};

			if (dlg.ShowDialog() != true) return;

			try
			{
				var list = Commands
					.Select(c => new CommandEntry { Name = c.Name, Value = c.Value })
					.ToList();
				_configService.SaveCommandsToPath(list, dlg.FileName);
				StatusText = $"Saved {list.Count} entries";
			}
			catch (Exception ex)
			{
				StatusText = $"Save failed: {ex.Message}";
			}
		}

		//------------------------------------------------------------------------------
		// Serial port
		//------------------------------------------------------------------------------

		private void OpenPort()
		{
			if (string.IsNullOrWhiteSpace(SelectedPort))
			{
				StatusText = "No port selected";
				return;
			}

			try
			{
				_serialPort = new SerialPort(SelectedPort, SelectedBaudRate)
				{
					NewLine = "\r\n",
					ReadTimeout = 500,
					WriteTimeout = 500
				};
				_serialPort.DataReceived += OnDataReceived;
				_serialPort.Open();

				IsConnected = true;
				StatusText = $"Opened {SelectedPort} @ {SelectedBaudRate}";
			}
			catch (Exception ex)
			{
				StatusText = $"Open failed: {ex.Message}";
				_serialPort?.Dispose();
				_serialPort = null;
			}
		}

		private void Disconnect()
		{
			try
			{
				if (_serialPort != null)
				{
					_serialPort.DataReceived -= OnDataReceived;
					if (_serialPort.IsOpen)
						_serialPort.Close();
					_serialPort.Dispose();
					_serialPort = null;
				}
			}
			catch { }

			IsConnected = false;
			StatusText = "Disconnected";
		}

		private void OnDataReceived(object? sender, SerialDataReceivedEventArgs e)
		{
			try
			{
				if (sender is SerialPort sp)
				{
					var data = sp.ReadExisting();
					if (!string.IsNullOrEmpty(data))
					{
						Application.Current.Dispatcher.BeginInvoke(() =>
							ReceptionText += data);
					}
				}
			}
			catch { }
		}

		// Called by each SerialCommandRowViewModel via delegate
		private void SendCommand(SerialCommandRowViewModel row)
		{
			if (_serialPort is null || !_serialPort.IsOpen)
			{
				StatusText = "Port not open";
				return;
			}

			var bytes = ParseHexStringToBytes(row.Value);
			if (bytes is null || bytes.Length == 0)
			{
				StatusText = "Invalid hex input";
				return;
			}

			try
			{
				_serialPort.Write(bytes, 0, bytes.Length);
				ReceptionText += $"[Sent {DateTime.Now:HH:mm:ss}] {BitConverter.ToString(bytes)}\n";
				StatusText = $"Sent {bytes.Length} bytes";
			}
			catch (Exception ex)
			{
				StatusText = $"Send failed: {ex.Message}";
			}
		}

		//------------------------------------------------------------------------------
		// Helpers
		//------------------------------------------------------------------------------

		private void LoadCommandsFromPath(string path)
		{
			try
			{
				var list = _configService.LoadCommandsFromPath(path);
				ApplyCommandList(list);
			}
			catch { }
		}

		private void ApplyCommandList(List<CommandEntry> list)
		{
			for (int i = 0; i < Commands.Count; i++)
			{
				if (i < list.Count)
				{
					Commands[i].Name = list[i].Name ?? string.Empty;
					Commands[i].Value = list[i].Value ?? string.Empty;
				}
				else
				{
					Commands[i].Name = string.Empty;
					Commands[i].Value = string.Empty;
				}
			}
		}

		private static byte[]? ParseHexStringToBytes(string input)
		{
			var cleaned = input.Trim();

			if (cleaned.IndexOfAny([' ', ',', ';', '-']) >= 0)
			{
				var tokens = cleaned
					.Split([' ', ',', ';', '-'], StringSplitOptions.RemoveEmptyEntries)
					.Select(t => t.Trim())
					.ToArray();

				var bytes = new byte[tokens.Length];
				for (int i = 0; i < tokens.Length; i++)
				{
					var tok = tokens[i];
					if (tok.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
						tok = tok[2..];
					if (tok.Length == 0 || tok.Length > 2) return null;
					if (!byte.TryParse(tok, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b)) return null;
					bytes[i] = b;
				}
				return bytes;
			}

			if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
				cleaned = cleaned[2..];
			if ((cleaned.Length & 1) != 0) return null;

			var outBytes = new byte[cleaned.Length / 2];
			for (int i = 0; i < outBytes.Length; i++)
			{
				var pair = cleaned.Substring(i * 2, 2);
				if (!byte.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b)) return null;
				outBytes[i] = b;
			}
			return outBytes;
		}

		public void Cleanup()
		{
			Disconnect();
		}
	}
}
