using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Modules;
using System.Windows;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace EmbeddedNetworkLab.UI.Modules.SimulatorCentrale
{
	public partial class SimulatorCentraleViewModel : ModuleViewModel
	{
		public override string Name => "Central Simulator";

		[ObservableProperty]
		private string? statusText = "";

		// Collections exposed for binding
		public ObservableCollection<SerialPortItem> SerialPortItems { get; } = new();
		public ObservableCollection<int> BaudRates { get; } = new();

		[ObservableProperty]
		private SerialPortItem? selectedPortItem;

		[ObservableProperty]
		private int selectedBaud = 460800;

		// Indicates the real state of the serial port opened by this module
		[ObservableProperty]
		private bool isPortOpen;

		// Convenience property to enable/disable configuration in the UI
		public bool IsConfigurationEditable => !IsPortOpen;

		// Label for the toggle button ("Open" or "Close")
		public string ToggleLabel => IsPortOpen ? "Close" : "Open";

		// Event used to emit raw log messages to the shell (no timestamp)
		public event EventHandler<string>? LogEmitted;

		private SerialPort? _serialPort;
		private readonly DispatcherTimer _portScanTimer;

		// Helper to emit logs
		private void EmitLog(string message) => LogEmitted?.Invoke(this, message);

		public SimulatorCentraleViewModel()
		{
			// Populate common baud rates
			var commonBauds = new[]
			{
				110, 300, 600, 1200, 2400, 4800, 9600,
				14400, 19200, 38400, 57600, 115200,
				230400, 460800, 921600
			};

			foreach (var b in commonBauds)
				BaudRates.Add(b);

			SelectedBaud = 460800;

			RefreshSerialPorts();

			_portScanTimer = new DispatcherTimer
			{
				Interval = System.TimeSpan.FromSeconds(2)
			};
			_portScanTimer.Tick += async (_, __) => await ScanPortsAsync();
			_portScanTimer.Start();
		}

		// Port item model with availability flags
		public partial class SerialPortItem : ObservableObject
		{
			public SerialPortItem(string name)
			{
				Name = name;
				Present = true;
				IsUsable = false;
			}

			public string Name { get; }

			[ObservableProperty]
			private bool present;

			[ObservableProperty]
			private bool isUsable;

			public override string ToString() => Name;
		}

		// Initial refresh: build items from current system ports (and test usability asynchronously)
		public void RefreshSerialPorts()
		{
			var current = SerialPort.GetPortNames().OrderBy(p => p).ToArray();

			// Keep existing items where possible; add new ones
			var known = SerialPortItems.ToDictionary(i => i.Name, i => i);

			// Mark all known as not present initially
			foreach (var item in SerialPortItems)
			{
				item.Present = false;
				item.IsUsable = false;
			}

			foreach (var name in current)
			{
				if (known.TryGetValue(name, out var existing))
				{
					existing.Present = true;
				}
				else
				{
					var it = new SerialPortItem(name) { Present = true, IsUsable = false };
					SerialPortItems.Add(it);
				}
			}

			// Remove items that were never present and not the currently selected one (optional)
			// (We keep missing items so they appear greyed; do not remove them)

			// Start asynchronous usability tests for present ports
			_ = ScanPortsAsync();
		}

		// Manual refresh command (source generator produces RefreshPortsCommand)
		[RelayCommand]
		private void RefreshPorts() => RefreshSerialPorts();

		// Periodic or manual scan that verifies presence and usability.
		private async Task ScanPortsAsync()
		{
			var currentNames = SerialPort.GetPortNames().OrderBy(n => n).ToArray();

			// Mark presence flags and add new items
			var existingDict = SerialPortItems.ToDictionary(i => i.Name, i => i);

			// Mark all present=false before re-marking
			foreach (var item in SerialPortItems)
			{
				item.Present = false;
			}

			foreach (var name in currentNames)
			{
				if (!existingDict.TryGetValue(name, out var it))
				{
					it = new SerialPortItem(name) { Present = true, IsUsable = false };
					// add on UI thread
					Application.Current?.Dispatcher.BeginInvoke(new Action(() => SerialPortItems.Add(it)));
				}
				else
				{
					// mark present
					Application.Current?.Dispatcher.BeginInvoke(new Action(() => it.Present = true));
				}

				// Test usability asynchronously and set IsUsable accordingly
				_ = Task.Run(async () =>
				{
					var usable = await TestPortUsableAsync(name, SelectedBaud);
					Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
					{
						// update the corresponding item (it may have been added in the meantime)
						var item = SerialPortItems.FirstOrDefault(i => i.Name == name);
						if (item != null)
							item.IsUsable = usable;
					}));
				});
			}

			// For items not present in currentNames, ensure IsUsable=false
			foreach (var item in SerialPortItems.Where(i => !currentNames.Contains(i.Name)).ToArray())
			{
				Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
				{
					item.Present = false;
					item.IsUsable = false;
				}));
			}
		}

		// Quick test whether the port can be opened. Non-blocking because called from Task.Run.
		private static Task<bool> TestPortUsableAsync(string portName, int baud)
		{
			return Task.Run(() =>
			{
				try
				{
					using var sp = new SerialPort(portName, baud)
					{
						ReadTimeout = 200,
						WriteTimeout = 200
					};
					sp.Open();
					// If open succeeded, close immediately
					sp.Close();
					return true;
				}
				catch
				{
					return false;
				}
			});
		}

		// Toggle command: open if closed, close if open
		[RelayCommand(CanExecute = nameof(CanToggle))]
		private void Toggle()
		{
			if (IsPortOpen)
				PerformClose();
			else
				PerformOpen();
		}

		private bool CanToggle()
		{
			// Allow close when port is open.
			if (IsPortOpen)
				return true;

			// Allow open when a usable port is selected and module not running.
			return !IsRunning && SelectedPortItem != null && SelectedPortItem.Present && SelectedPortItem.IsUsable;
		}

		// Helper: open selected serial port and start module
		private void PerformOpen()
		{
			// Open the serial port first
			if (SelectedPortItem == null || string.IsNullOrWhiteSpace(SelectedPortItem.Name))
			{
				EmitLog("Select a serial port before opening.");
				return;
			}

			try
			{
				_serialPort = new SerialPort(SelectedPortItem.Name, SelectedBaud)
				{
					ReadTimeout = 1000,
					WriteTimeout = 1000
				};

				_serialPort.DataReceived += SerialPort_DataReceived;
				_serialPort.Open();
				IsPortOpen = true;
			}
			catch (Exception ex)
			{
				var msg = $"Failed to open port {SelectedPortItem.Name}: {ex.Message}";
				EmitLog(msg);
				// cleanup...
				if (_serialPort != null)
				{
					try { _serialPort.DataReceived -= SerialPort_DataReceived; } catch { }
					try { _serialPort.Dispose(); } catch { }
				}
				_serialPort = null;
				IsPortOpen = false;
				return;
			}

			// Then start the module logic
			if (!TryStart())
				return;

			var runningMsg = $"Running on {SelectedPortItem.Name} @ {SelectedBaud}";
			EmitLog(runningMsg);
		}

		// Helper: close port and stop module
		private void PerformClose()
		{
			// Stop module logic
			StopExecution();

			// Close the serial port cleanly if open
			CloseSerialPort();

			EmitLog("Closed");
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
			}
			catch
			{
				// ignore
			}
			finally
			{
				try { _serialPort.DataReceived -= SerialPort_DataReceived; } catch { }
				try { _serialPort.Dispose(); } catch { }
				_serialPort = null;
				IsPortOpen = false;
			}
		}

		// Incoming data handler: simple example that feeds StatusText (UI thread)
		private void SerialPort_DataReceived(object? sender, SerialDataReceivedEventArgs e)
		{
			try
			{
				if (sender is not SerialPort sp)
					return;

				var incoming = sp.ReadExisting();
				if (string.IsNullOrEmpty(incoming))
					return;

				Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
				{
					StatusText = incoming.Length > 200 ? incoming[..200] + "…" : incoming;
				}));

				// Also emit the raw incoming data to shell console
				EmitLog(incoming);
			}
			catch (Exception ex)
			{
				var err = $"[ERROR reading serial port: {ex.Message}]";
				Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
				{
					StatusText = err;
				}));
				EmitLog(err);
			}
		}

		// Invoked automatically by the source generator when IsPortOpen changes
		partial void OnIsPortOpenChanged(bool value)
		{
			// Update commands and UI
			ToggleCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(IsConfigurationEditable));
			OnPropertyChanged(nameof(ToggleLabel));
		}

		// Notify toggle availability when selected port changes
		partial void OnSelectedPortItemChanged(SerialPortItem? value)
		{
			ToggleCommand.NotifyCanExecuteChanged();
		}

		protected override void OnRunningStateChanged(bool isRunning)
		{
			// Update command availability when running state changes
			ToggleCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(StatusText));

			// Ensure the port is closed when stopping the module
			if (!isRunning)
				CloseSerialPort();
		}
	}
}