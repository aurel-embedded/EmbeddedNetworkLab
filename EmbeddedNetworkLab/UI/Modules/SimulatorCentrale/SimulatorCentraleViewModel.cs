using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Modules;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.ComponentModel;

namespace EmbeddedNetworkLab.UI.Modules.SimulatorCentrale
{
	public partial class SimulatorCentraleViewModel : ModuleViewModel
	{
		public override string Name => "Central Simulator";

		[ObservableProperty]
		private string? statusText = "";

		// Ports & baudrates
		public ObservableCollection<SerialPortItem> SerialPortItems { get; } = new();
		public ObservableCollection<int> BaudRates { get; } = new();

		[ObservableProperty]
		private SerialPortItem? selectedPortItem;

		[ObservableProperty]
		private int selectedBaud = 460800;

		[ObservableProperty]
		private bool isPortOpen;

		public bool IsConfigurationEditable => !IsPortOpen;
		// Toggle label now follows IsRunning
		public string ToggleLabel => IsRunning ? "Close" : "Open";

		// Commands area: 10 command items
		public ObservableCollection<CommandItem> CommandItems { get; } = new();

		// Event used to emit raw log messages to the shell (no timestamp)
		public event EventHandler<string>? LogEmitted;
		private void EmitLog(string message) => LogEmitted?.Invoke(this, message);

		private SerialPort? _serialPort;
		private readonly DispatcherTimer _portScanTimer;

		// Save debounce timer (1s) and file path
		private readonly DispatcherTimer _saveTimer;
		private readonly string _commandsFilePath;

		// persistence helper
		private readonly CommandStore _commandStore;

		public SimulatorCentraleViewModel()
		{
			// path next to executable
			_commandsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "simulator_centrale_commands.json");
			_commandStore = new CommandStore(_commandsFilePath);

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

			// Create 10 command items and subscribe to changes
			for (int i = 0; i < 10; i++)
			{
				var ci = new CommandItem { Name = $"Cmd {i + 1}", Text = string.Empty, Index = i };
				ci.PropertyChanged += CommandItem_PropertyChanged;
				CommandItems.Add(ci);
			}

			// Debounced save timer
			_saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
			_saveTimer.Tick += (_, __) =>
			{
				_saveTimer.Stop();
				_ = SaveCommandItemsAsync();
			};

			// Load saved commands (or create default file)
			try
			{
				LoadCommandItems();
			}
			catch (Exception ex)
			{
				EmitLog($"[ERROR] loading commands file: {ex.Message}");
			}

			RefreshSerialPorts();

			_portScanTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(2)
			};
			_portScanTimer.Tick += async (_, __) => await ScanPortsAsync();
			_portScanTimer.Start();
		}

		// Called when a command item's Text or Name changes -> schedule Save
		private void CommandItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(CommandItem.Text) || e.PropertyName == nameof(CommandItem.Name))
			{
				// restart debounce timer
				_saveTimer.Stop();
				_saveTimer.Start();

				// refresh Send command availability
				SendCommand.NotifyCanExecuteChanged();
			}
		}

		// Command item model
		public partial class CommandItem : ObservableObject
		{
			[ObservableProperty]
			private string? name;

			[ObservableProperty]
			private string? text;

			// optional index for identification
			public int Index { get; set; }
		}

		// Port item model
		public partial class SerialPortItem : ObservableObject
		{
			public SerialPortItem(string name)
			{
				Name = name;
				Present = true;
				IsUsable = false;
			}
			public string Name { get; }
			[ObservableProperty] private bool present;
			[ObservableProperty] private bool isUsable;
			public override string ToString() => Name;
		}

		// DTO for JSON persistence (kept for local use)
		private record CommandDto(string Name, string Text);

		// --- Persistence methods (JSON) using CommandStore ---

		private void LoadCommandItems()
		{
			// if file missing: create default (save will create)
			if (!File.Exists(_commandsFilePath))
			{
				_ = SaveCommandItemsAsync();
				return;
			}

			var values = _commandStore.Load();

			// apply values to existing CommandItems (up to count)
			for (int i = 0; i < CommandItems.Count; i++)
			{
				if (i < values.Length)
				{
					CommandItems[i].Name = values[i].Name ?? CommandItems[i].Name;
					CommandItems[i].Text = values[i].Text ?? string.Empty;
				}
				else
				{
					// if missing in file, keep defaults
					CommandItems[i].Text = string.Empty;
				}
			}
		}

		private async Task SaveCommandItemsAsync()
		{
			try
			{
				var dtos = CommandItems.Select(ci => new CommandStore.CommandDto(ci.Name ?? string.Empty, ci.Text ?? string.Empty)).ToArray();
				await _commandStore.SaveAsync(dtos).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// ensure errors are logged to shell console
				EmitLog($"[ERROR] saving commands file: {ex.Message}");
			}
		}

		// --- existing serial-port / command logic (unchanged aside from minor integration) ---

		// Refresh ports (keeps missing items grayed) and preserve selection when possible
        public void RefreshSerialPorts()
        {
            var previousName = SelectedPortItem?.Name;

            var current = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
            var existingDict = SerialPortItems.ToDictionary(i => i.Name, i => i);

            // Update existing items' Present flag only when it changes (avoid transient false)
            foreach (var item in SerialPortItems)
            {
                var isNowPresent = current.Contains(item.Name);
                if (item.Present != isNowPresent)
                {
                    Application.Current?.Dispatcher.BeginInvoke(new Action(() => item.Present = isNowPresent));
                }
            }

            // Add any new ports
            foreach (var name in current)
            {
                if (!existingDict.ContainsKey(name))
                {
                    var it = new SerialPortItem(name) { Present = true, IsUsable = false };
                    Application.Current?.Dispatcher.BeginInvoke(new Action(() => SerialPortItems.Add(it)));
                }
            }

            // Try to restore previous selection if still present
            if (!string.IsNullOrWhiteSpace(previousName))
            {
                var restored = SerialPortItems.FirstOrDefault(i => i.Name == previousName && i.Present);
                if (restored != null)
                {
                    SelectedPortItem = restored;
                    // request a usability scan for the selected item immediately
                    _ = Task.Run(async () =>
                    {
                        var usable = await TestPortUsableAsync(restored.Name, SelectedBaud);
                        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            restored.IsUsable = usable;
                            ToggleCommand.NotifyCanExecuteChanged();
                        }));
                    });
                    return;
                }
            }

            // if no previous to restore, pick first present (usability will be determined by ScanPortsAsync)
            SelectedPortItem = SerialPortItems.FirstOrDefault(i => i.Present);
            _ = ScanPortsAsync();
        }

		[RelayCommand]
		private void RefreshPorts() => RefreshSerialPorts();

		private async Task ScanPortsAsync()
		{
			var currentNames = SerialPort.GetPortNames().OrderBy(n => n).ToArray();

			// Safe snapshot: tolerate existing duplicates by choosing first
			var existingDict = SerialPortItems
				.GroupBy(i => i.Name)
				.ToDictionary(g => g.Key, g => g.First());

			foreach (var name in currentNames)
			{
				if (!existingDict.TryGetValue(name, out var it))
				{
					// Perform check+add atomically on UI thread to avoid races
					Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
					{
						if (!SerialPortItems.Any(i => i.Name == name))
						{
							SerialPortItems.Add(new SerialPortItem(name) { Present = true, IsUsable = false });
						}
					}));
				}
				else
				{
					if (!it.Present)
						Application.Current?.Dispatcher.BeginInvoke(new Action(() => it.Present = true));
				}

				_ = Task.Run(async () =>
				{
					var usable = await TestPortUsableAsync(name, SelectedBaud);
					Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
					{
						var item = SerialPortItems.FirstOrDefault(i => i.Name == name);
						if (item != null && item.IsUsable != usable)
						{
							item.IsUsable = usable;
							ToggleCommand.NotifyCanExecuteChanged();
						}
					}));
				});
			}

            // For items not present in currentNames, ensure IsUsable=false and Present=false and notify toggle
            foreach (var item in SerialPortItems.Where(i => !currentNames.Contains(i.Name)).ToArray())
            {
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (item.Present)
                        item.Present = false;
                    if (item.IsUsable)
                        item.IsUsable = false;
                    ToggleCommand.NotifyCanExecuteChanged();
                }));
            }

            // after a scan completes, ensure the toggle state is recomputed
            Application.Current?.Dispatcher.BeginInvoke(new Action(() => ToggleCommand.NotifyCanExecuteChanged()));
		}

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
					sp.Close();
					return true;
				}
				catch
				{
					return false;
				}
			});
		}

		// Toggle command: execute based on IsRunning now
		[RelayCommand(CanExecute = nameof(CanToggle))]
		private void Toggle()
		{
			if (IsRunning)
				PerformClose();
			else
				PerformOpen();
		}

		private bool CanToggle() =>
			IsRunning || (SelectedPortItem != null && SelectedPortItem.Present && SelectedPortItem.IsUsable && !IsRunning);

		private void PerformOpen()
		{
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
				EmitLog($"Opened {SelectedPortItem.Name} @ {SelectedBaud}");
			}
			catch (Exception ex)
			{
				var msg = $"Failed to open port {SelectedPortItem.Name}: {ex.Message}";
				EmitLog(msg);
				if (_serialPort != null)
				{
					try { _serialPort.DataReceived -= SerialPort_DataReceived; } catch { }
					try { _serialPort.Dispose(); } catch { }
				}
				_serialPort = null;
				IsPortOpen = false;
				return;
			}

			if (!TryStart())
				return;
		}

		private void PerformClose()
		{
			StopExecution();
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
			catch { }
			finally
			{
				try { _serialPort.DataReceived -= SerialPort_DataReceived; } catch { }
				try { _serialPort.Dispose(); } catch { }
				_serialPort = null;
				IsPortOpen = false;
			}
		}

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

				EmitLog(incoming);
			}
			catch (Exception ex)
			{
				var err = $"[ERROR reading serial port: {ex.Message}]";
				Application.Current?.Dispatcher.BeginInvoke(new Action(() => StatusText = err));
				EmitLog(err);
			}
		}

		partial void OnIsPortOpenChanged(bool value)
		{
			// keep Send availability and configuration notifications
			SendCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(IsConfigurationEditable));
		}

		partial void OnSelectedPortItemChanged(SerialPortItem? value)
		{
			ToggleCommand.NotifyCanExecuteChanged();
			SendCommand.NotifyCanExecuteChanged();
		}

		protected override void OnRunningStateChanged(bool isRunning)
		{
			// Update command availability and label when running state changes
			ToggleCommand.NotifyCanExecuteChanged();
			SendCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(StatusText));
			OnPropertyChanged(nameof(ToggleLabel));

			// Ensure the port is closed when stopping the module
			if (!isRunning)
				CloseSerialPort();
		}

		// Send command: parameter = CommandItem
		[RelayCommand(CanExecute = nameof(CanSend))]
		private void Send(CommandItem? item)
		{
			if (item == null || string.IsNullOrWhiteSpace(item.Text))
				return;

			if (_serialPort == null || !_serialPort.IsOpen)
			{
				EmitLog("Cannot send: serial port is not open.");
				return;
			}

			try
			{
				_serialPort.WriteLine(item.Text);
				EmitLog($"Sent: {item.Text}");
			}
			catch (Exception ex)
			{
				EmitLog($"[ERROR sending]: {ex.Message}");
			}
		}

		private bool CanSend(CommandItem? item)
		{
			return item != null && !string.IsNullOrWhiteSpace(item.Text) && IsPortOpen && _serialPort != null && _serialPort.IsOpen;
		}
	}
}