using System;
using System.Linq;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EmbeddedNetworkLab.UI.Windows
{
	/// <summary>
	/// Interaction logic for SerialCommandsWindow.xaml
	/// </summary>
	public partial class SerialCommandsWindow : Window
	{
		private SerialPort? _serialPort;

		public SerialCommandsWindow()
		{
			InitializeComponent();
			LoadSerialPorts();
		}

		private void LoadSerialPorts()
		{
			try
			{
				var ports = SerialPort.GetPortNames()
					.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
					.ToArray();

				PortComboBox.ItemsSource = ports;

				if (ports.Length > 0)
				{
					PortComboBox.SelectedIndex = 0;
					ConnectionStatusText.Text = "Ready";
					ConnectionStatusText.Foreground = Brushes.Green;
				}
				else
				{
					ConnectionStatusText.Text = "No COM ports found";
					ConnectionStatusText.Foreground = Brushes.Red;
				}
			}
			catch (Exception)
			{
				ConnectionStatusText.Text = "Error listing COM ports";
				ConnectionStatusText.Foreground = Brushes.Red;
			}
		}

		private void RefreshPortsButton_Click(object sender, RoutedEventArgs e)
		{
			LoadSerialPorts();
		}

		private void ConnectButton_Click(object sender, RoutedEventArgs e)
		{
			if (_serialPort is null || !_serialPort.IsOpen)
			{
				OpenPort();
			}
			else
			{
				ClosePort();
			}
		}

		private void OpenPort()
		{
			var portName = PortComboBox.SelectedItem as string;
			if (string.IsNullOrWhiteSpace(portName))
			{
				ConnectionStatusText.Text = "No port selected";
				ConnectionStatusText.Foreground = Brushes.Red;
				return;
			}

			// Attempt to parse the selected baudrate; fall back to 115200
			int baud = 115200;
			if (BaudrateComboBox.SelectedItem is ComboBoxItem cbi)
			{
				int.TryParse(cbi.Content?.ToString() ?? string.Empty, out baud);
			}
			else if (BaudrateComboBox.SelectedItem is string s)
			{
				int.TryParse(s, out baud);
			}

			try
			{
				_serialPort = new SerialPort(portName, baud)
				{
					NewLine = "\r\n",
					ReadTimeout = 500,
					WriteTimeout = 500
				};
				_serialPort.DataReceived += SerialPort_DataReceived;
				_serialPort.Open();

				ConnectButton.Content = "Close";
				ConnectionStatusText.Text = $"Opened {portName} @ {baud}";
				ConnectionStatusText.Foreground = Brushes.Green;

				// Disable selectors while open
				PortComboBox.IsEnabled = false;
				BaudrateComboBox.IsEnabled = false;
			}
			catch (Exception ex)
			{
				ConnectionStatusText.Text = $"Open failed: {ex.Message}";
				ConnectionStatusText.Foreground = Brushes.Red;
				_serialPort?.Dispose();
				_serialPort = null;
			}
		}

		private void ClosePort()
		{
			try
			{
				if (_serialPort != null)
				{
					_serialPort.DataReceived -= SerialPort_DataReceived;

					if (_serialPort.IsOpen)
					{
						_serialPort.Close();
					}

					_serialPort.Dispose();
					_serialPort = null;
				}

				ConnectButton.Content = "Open";
				ConnectionStatusText.Text = "Disconnected";
				ConnectionStatusText.Foreground = Brushes.Red;

				// Re-enable selectors
				PortComboBox.IsEnabled = true;
				BaudrateComboBox.IsEnabled = true;
			}
			catch (Exception ex)
			{
				ConnectionStatusText.Text = $"Close failed: {ex.Message}";
				ConnectionStatusText.Foreground = Brushes.Red;
			}
		}

		private void SerialPort_DataReceived(object? sender, SerialDataReceivedEventArgs e)
		{
			try
			{
				if (sender is SerialPort sp)
				{
					var data = sp.ReadExisting();
					if (!string.IsNullOrEmpty(data))
					{
						Dispatcher.BeginInvoke(() =>
						{
							ReceptionTextBox.AppendText(data);
							ReceptionTextBox.ScrollToEnd();
						});
					}
				}
			}
			catch
			{
				// ignore read errors for now
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			try
			{
				if (_serialPort != null)
				{
					if (_serialPort.IsOpen)
					{
						_serialPort.Close();
					}
					_serialPort.Dispose();
					_serialPort = null;
				}
			}
			catch
			{
				// ignore cleanup errors
			}
		}
	}
}
