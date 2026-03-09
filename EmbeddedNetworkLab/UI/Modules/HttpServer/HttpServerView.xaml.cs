using System.Collections.Specialized;
using System.Windows.Controls;

namespace EmbeddedNetworkLab.UI.Modules.HttpServer
{
	public partial class HttpServerView : UserControl
	{
		public HttpServerView()
		{
			InitializeComponent();
			NotifyCollectionChangedEventHandler? handler = null;
			HttpServerViewModel? prevVm = null;

			DataContextChanged += (_, e) =>
			{
				if (prevVm is not null)
					((INotifyCollectionChanged)prevVm.EventLog).CollectionChanged -= handler;

				prevVm = e.NewValue as HttpServerViewModel;

				if (prevVm is not null)
				{
					handler = (_, _) =>
					{
						// Schedule after the ItemsControl has processed the change
						Dispatcher.BeginInvoke(new Action(() =>
						{
							if (EventLogList.Items.Count > 0)
								EventLogList.ScrollIntoView(EventLogList.Items[^1]);
						}), System.Windows.Threading.DispatcherPriority.Background);
					};
					((INotifyCollectionChanged)prevVm.EventLog).CollectionChanged += handler;
				}
			};
		}
	}
}
