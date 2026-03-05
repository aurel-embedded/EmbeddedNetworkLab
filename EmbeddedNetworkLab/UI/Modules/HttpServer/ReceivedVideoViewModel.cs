using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Core.Models;
using System;

namespace EmbeddedNetworkLab.UI.Modules.HttpServer
{
	public partial class ReceivedVideoViewModel : ObservableObject
	{
		private readonly Action<ReceivedVideoViewModel> _playCallback;

		public ReceivedVideo Video { get; }
		public string FileName => Video.FileName;
		public string ReceivedAt => Video.ReceivedAt.ToString("yyyy-MM-dd HH:mm:ss");
		public IRelayCommand PlayCommand { get; }

		public ReceivedVideoViewModel(ReceivedVideo video, Action<ReceivedVideoViewModel> playCallback)
		{
			Video = video;
			_playCallback = playCallback;
			PlayCommand = new RelayCommand(() => _playCallback(this));
		}
	}
}
