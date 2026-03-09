using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EmbeddedNetworkLab.UI.Modules.HttpServer
{
	public partial class HttpServerView : UserControl
	{
		private ScrollViewer? _scrollViewer;

		public HttpServerView()
		{
			InitializeComponent();

			NotifyCollectionChangedEventHandler? handler = null;
			HttpServerViewModel? prevVm = null;

			Loaded += (_, _) =>
			{
				_scrollViewer = FindScrollViewer(EventLogList);
			};

			DataContextChanged += (_, e) =>
			{
				if (prevVm is not null)
					((INotifyCollectionChanged)prevVm.EventLog).CollectionChanged -= handler;

				prevVm = e.NewValue as HttpServerViewModel;

				if (prevVm is not null)
				{
					handler = (_, _) =>
					{
						Dispatcher.BeginInvoke(() =>
						{
							if (_scrollViewer == null)
								return;

							bool isAtBottom =
								_scrollViewer.VerticalOffset >=
								_scrollViewer.ScrollableHeight - 1;

							if (isAtBottom && EventLogList.Items.Count > 0)
							{
								EventLogList.ScrollIntoView(
									EventLogList.Items[^1]);
							}
						});
					};

					((INotifyCollectionChanged)prevVm.EventLog).CollectionChanged += handler;
				}
			};
		}

		private ScrollViewer? FindScrollViewer(DependencyObject obj)
		{
			if (obj is ScrollViewer viewer)
				return viewer;

			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
			{
				var result = FindScrollViewer(VisualTreeHelper.GetChild(obj, i));
				if (result != null)
					return result;
			}

			return null;
		}
	}
}