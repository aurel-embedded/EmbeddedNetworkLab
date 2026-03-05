using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EmbeddedNetworkLab.UI.Windows
{
	public partial class SerialCommandRowViewModel : ObservableObject
	{
		private readonly System.Action<SerialCommandRowViewModel> _sendCallback;

		[ObservableProperty]
		private string name = string.Empty;

		[ObservableProperty]
		private string value = string.Empty;

		[ObservableProperty]
		private bool canSend;

		public IRelayCommand SendCommand { get; }

		public SerialCommandRowViewModel(System.Action<SerialCommandRowViewModel> sendCallback)
		{
			_sendCallback = sendCallback;
			SendCommand = new RelayCommand(() => _sendCallback(this), () => CanSend);
		}

		partial void OnCanSendChanged(bool value) => SendCommand.NotifyCanExecuteChanged();
	}
}
