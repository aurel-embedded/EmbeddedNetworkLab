using System.Collections.Specialized;
using System.Windows.Controls;

namespace EmbeddedNetworkLab.UI.Modules.HttpServer
{
	public partial class HttpServerView : UserControl
	{
		public HttpServerView()
		{
			InitializeComponent();
			DataContextChanged += (_, e) =>
			{
				if (e.NewValue is HttpServerViewModel vm)
					((INotifyCollectionChanged)vm.EventLog).CollectionChanged += (_, _) =>
						EventLogList.ScrollIntoView(EventLogList.Items[EventLogList.Items.Count - 1]);
			};
		}
	}
}
