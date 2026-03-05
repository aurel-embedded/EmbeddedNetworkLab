using LibVLCSharp.Shared;
using System;
using System.Windows;
using System.Windows.Threading;

namespace EmbeddedNetworkLab.UI.Windows
{
	public partial class VideoPlayerWindow : Window
	{
		private readonly LibVLC _libVLC;
		private readonly MediaPlayer _mediaPlayer;
		private readonly DispatcherTimer _timer;
		private readonly string _filePath;
		private bool _isDraggingSlider;
		private Media? _currentMedia;

		public VideoPlayerWindow(string filePath, string title)
		{
			InitializeComponent();
			Title = title;
			_filePath = filePath;

			_libVLC = new LibVLC();
			_mediaPlayer = new MediaPlayer(_libVLC);

			_mediaPlayer.EndReached += (_, _) =>
				Dispatcher.Invoke(() =>
				{
					ProgressSlider.Value = 0;
					PlayPauseButton.Content = "▶";
				});

			_timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
			_timer.Tick += (_, _) =>
			{
				if (_isDraggingSlider || _mediaPlayer.Length <= 0) return;
				ProgressSlider.Value = (double)_mediaPlayer.Time / _mediaPlayer.Length;
			};
			_timer.Start();

			// Assign MediaPlayer only after VideoView HWND is created
			VideoView.Loaded += OnVideoViewLoaded;
		}

		private void OnVideoViewLoaded(object sender, RoutedEventArgs e)
		{
			VideoView.Loaded -= OnVideoViewLoaded;
			VideoView.MediaPlayer = _mediaPlayer;

			_currentMedia = new Media(_libVLC, new Uri(_filePath));
			_mediaPlayer.Play(_currentMedia);
			PlayPauseButton.Content = "⏸";
		}

		private void PlayPause_Click(object sender, RoutedEventArgs e)
		{
			if (_mediaPlayer.IsPlaying)
			{
				_mediaPlayer.Pause();
				PlayPauseButton.Content = "▶";
			}
			else
			{
				_mediaPlayer.Play();
				PlayPauseButton.Content = "⏸";
			}
		}

		private void Stop_Click(object sender, RoutedEventArgs e)
		{
			_mediaPlayer.Stop();
			ProgressSlider.Value = 0;
			PlayPauseButton.Content = "▶";
		}

		private void Rewind_Click(object sender, RoutedEventArgs e)
		{
			_mediaPlayer.Time = Math.Max(0, _mediaPlayer.Time - 10_000);
		}

		private void Forward_Click(object sender, RoutedEventArgs e)
		{
			_mediaPlayer.Time = Math.Min(_mediaPlayer.Length, _mediaPlayer.Time + 10_000);
		}

		private void ProgressSlider_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			_isDraggingSlider = true;
		}

		private void ProgressSlider_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			_isDraggingSlider = false;
			if (_mediaPlayer.Length > 0)
				_mediaPlayer.Time = (long)(ProgressSlider.Value * _mediaPlayer.Length);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			_timer.Stop();
			_mediaPlayer.Stop();
			_mediaPlayer.Dispose();
			_currentMedia?.Dispose();
			_libVLC.Dispose();
		}
	}
}
