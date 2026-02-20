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

	public sealed record BrokerEvent(
		DateTime Timestamp,
		BrokerEventLevel Level,
		string Message
	);
}
