using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EmbeddedNetworkLab.UI.Modules.TcpClient
{
	/// <summary>
	/// Interaction logic for TcpClientView.xaml
	/// </summary>
	public partial class TcpClientView : UserControl
	{
		private const int HistorySize = 60;

		private double[] _values = new double[HistorySize];
		private int _index = 0;

		private Signal? _signal;

		public TcpClientView()
		{
			InitializeComponent();

			Loaded += (_, _) =>
			{
				_signal = ThroughputPlot.Plot.Add.Signal(_values);

				ThroughputPlot.Plot.Title("Throughput (Mbps)");
				ThroughputPlot.Plot.YLabel("Mbps");
				ThroughputPlot.Plot.XLabel("Samples");

				ThroughputPlot.Refresh();
			};

			DataContextChanged += (_, _) =>
			{
				if (DataContext is TcpClientViewModel vm)
					vm.PropertyChanged += Vm_PropertyChanged;
			};

		}

		private void Port_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			// Allow only digits
			e.Handled = !IsTextNumeric(e.Text);
		}

		private void Port_Pasting(object sender, DataObjectPastingEventArgs e)
		{
			if (e.DataObject.GetDataPresent(DataFormats.Text))
			{
				var text = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
				if (!IsTextNumeric(text))
					e.CancelCommand();
			}
			else
			{
				e.CancelCommand();
			}
		}

		private static bool IsTextNumeric(string text)
		{
			return !string.IsNullOrEmpty(text) && text.All(char.IsDigit);
		}

		private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName != nameof(TcpClientViewModel.CurrentRate))
				return;

			Dispatcher.Invoke(() =>
			{
				var vm = (TcpClientViewModel)DataContext;

				_values[_index] = vm.CurrentRate;
				_index = (_index + 1) % HistorySize;

				// Rien d’autre à faire
				ThroughputPlot.Plot.Axes.AutoScale();
				ThroughputPlot.Refresh();
			});
		}

	}
}
