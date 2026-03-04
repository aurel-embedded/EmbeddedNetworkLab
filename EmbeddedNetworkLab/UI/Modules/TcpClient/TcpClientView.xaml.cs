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
		public TcpClientView()
		{
			InitializeComponent();
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

	}
}
