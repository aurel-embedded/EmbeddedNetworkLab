using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmbeddedNetworkLab.Core.Logging
{
	public enum BrokerEventLevel
	{
		Info,
		Warning,
		Error,
		Debug
	}
	public enum BrokerEventCategory
	{
		System,
		Message
	}

	public sealed record BrokerEvent(
		DateTime Timestamp,
		BrokerEventLevel Level,
		BrokerEventCategory Category,
		string Message
	);
}
