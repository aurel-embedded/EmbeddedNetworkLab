using EmbeddedNetworkLab.Core.Logging;
using System.Windows;
using System.Windows.Controls;

namespace EmbeddedNetworkLab.UI.Modules.MqttBroker;

public class BrokerEventTemplateSelector : DataTemplateSelector
{
	public DataTemplate? SystemTemplate { get; set; }
	public DataTemplate? MessageTemplate { get; set; }

	public override DataTemplate SelectTemplate(object item, DependencyObject container)
	{
		if (item is BrokerEvent evt)
		{
			return evt.Category switch
			{
				BrokerEventCategory.System => SystemTemplate!,
				BrokerEventCategory.Message => MessageTemplate!,
				_ => SystemTemplate!
			};
		}

		return base.SelectTemplate(item, container);
	}
}

