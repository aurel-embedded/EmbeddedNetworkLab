using System.Windows;

namespace EmbeddedNetworkLab.UI.Windows
{
	public partial class SerialCommandsWindow : Window
	{
		public SerialCommandsWindow(SerialCommandsViewModel viewModel)
		{
			InitializeComponent();
			DataContext = viewModel;
		}

		private void Window_Closed(object sender, System.EventArgs e)
		{
			((SerialCommandsViewModel)DataContext).Cleanup();
		}
	}
}
