using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EmbeddedNetworkLab.UI.Modules.Serial
{
	public partial class SerialViewModel : ObservableObject
	{
		private SerialPort? _serialPort;

		// Event emitted for external listeners (e.g., MainViewModel) when a serial log message is produced.
		public event EventHandler<string>? LogEmitted;

		[ObservableProperty]
		private string title = "Serial";

		[ObservableProperty]
		private string serialText = string.Empty;

		// Collection of common baud rates for the ComboBox
		public ObservableCollection<int> BaudRates { get; } = new();

		[ObservableProperty]
		private int selectedBaud;

		// Collection of available serial ports
		public ObservableCollection<string> SerialPorts { get; } = new();

		[ObservableProperty]
		private string? selectedPort;

		[ObservableProperty]
		private bool isPortOpen;

		public string TogglePortButtonText => IsPortOpen ? "Close" : "Open";

		// VT100 processor (separated into its own class)
		private readonly SerialVt100Processor _vt100Processor = new();

		public SerialViewModel()
		{
			var commonBauds = new[]
			{
				110, 300, 600, 1200, 2400, 4800, 9600,
				14400, 19200, 38400, 57600, 115200,
				230400, 460800, 921600
			};

			foreach (var b in commonBauds)
				BaudRates.Add(b);

			// sensible default
			SelectedBaud = 460800;

			RefreshSerialPorts();
		}

		public void RefreshSerialPorts()
		{
			var previouslySelected = SelectedPort;

			SerialPorts.Clear();

			var ports = SerialPort.GetPortNames()
								  .OrderBy(p => p);

			foreach (var port in ports)
				SerialPorts.Add(port);

			if (!string.IsNullOrWhiteSpace(previouslySelected) && SerialPorts.Contains(previouslySelected))
				SelectedPort = previouslySelected;
			else
				SelectedPort = SerialPorts.FirstOrDefault();

			TogglePortCommand.NotifyCanExecuteChanged();
		}

		[RelayCommand]
		private void RefreshPorts() => RefreshSerialPorts();

		[RelayCommand(CanExecute = nameof(CanTogglePort))]
		private void TogglePort()
		{
			if (IsPortOpen)
				CloseSerialPort();
			else
				OpenSerialPort();
		}

		private bool CanTogglePort() => !string.IsNullOrWhiteSpace(SelectedPort);

		private void OpenSerialPort()
		{
			if (string.IsNullOrWhiteSpace(SelectedPort))
			{
				AppendSerialLog("Select a serial port before opening.");
				return;
			}

			try
			{
				_serialPort = new SerialPort(SelectedPort, SelectedBaud)
				{
					ReadTimeout = 1000,
					WriteTimeout = 1000
				};

				_serialPort.DataReceived += SerialPort_DataReceived;
				_serialPort.Open();
				IsPortOpen = true;
				AppendSerialLog($"Opened {SelectedPort} @ {SelectedBaud} baud.");
			}
			catch (Exception ex)
			{
				AppendSerialLog($"Failed to open {SelectedPort}: {ex.Message}");
				if (_serialPort != null)
				{
					_serialPort.DataReceived -= SerialPort_DataReceived;
					_serialPort.Dispose();
				}
				_serialPort = null;
				IsPortOpen = false;
			}
		}

		private void CloseSerialPort()
		{
			if (_serialPort == null)
			{
				IsPortOpen = false;
				return;
			}

			try
			{
				if (_serialPort.IsOpen)
					_serialPort.Close();

				AppendSerialLog($"Closed {_serialPort.PortName}.");
			}
			catch (Exception ex)
			{
				AppendSerialLog($"Failed to close {_serialPort.PortName}: {ex.Message}");
			}
			finally
			{
				try
				{
					_serialPort.DataReceived -= SerialPort_DataReceived;
				}
				catch
				{
					// ignore if already removed or port disposed
				}

				_serialPort.Dispose();
				_serialPort = null;
				IsPortOpen = false;

				// reset logical screen
				_vt100Processor.Reset();
				Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
				{
					SerialText = string.Empty;
				}));
			}
		}

		// DataReceived handler - read incoming data, process VT100 and update SerialText (UI thread).
		private void SerialPort_DataReceived(object? sender, SerialDataReceivedEventArgs e)
		{
			try
			{
				if (sender is not SerialPort sp)
					return;

				// Read whatever is available right now
				var incoming = sp.ReadExisting();
				if (string.IsNullOrEmpty(incoming))
					return;

				// Process VT100 sequences and obtain the logical screen text
				var text = _vt100Processor.Process(incoming);

				// Update UI on dispatcher
				Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
				{
					SerialText = text;
				}));

				// Emit the raw message (no timestamp) so MainViewModel can append to the console with its own timestamp.
				AppendSerialLog(incoming);
			}
			catch (Exception ex)
			{
				// Avoid throwing from IO thread: surface error to UI/log
				Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
				{
					SerialText += $"[ERROR reading serial port: {ex.Message}]";
				}));
				AppendSerialLog($"[ERROR reading serial port: {ex.Message}]");
			}
		}

		// emit a raw message (without timestamp) so the shell console can timestamp it.
		private void AppendSerialLog(string message)
		{
			// Emit the raw message (no timestamp) so MainViewModel can append to the console with its own timestamp.
			LogEmitted?.Invoke(this, message);
		}

		partial void OnSelectedPortChanged(string? value)
		{
			TogglePortCommand.NotifyCanExecuteChanged();

			if (IsPortOpen && !string.Equals(_serialPort?.PortName, value, StringComparison.OrdinalIgnoreCase))
				CloseSerialPort();
		}

		partial void OnSelectedBaudChanged(int value)
		{
			if (_serialPort is { IsOpen: true })
			{
				try
				{
					_serialPort.BaudRate = value;
					AppendSerialLog($"Baudrate updated to {value} baud.");
				}
				catch (Exception ex)
				{
					AppendSerialLog($"Failed to update baudrate: {ex.Message}");
				}
			}
		}

		partial void OnIsPortOpenChanged(bool value) => OnPropertyChanged(nameof(TogglePortButtonText));
	}
}