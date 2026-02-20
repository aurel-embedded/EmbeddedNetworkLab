

namespace EmbeddedNetworkLab.Core
{
	public interface IThroughputService
	{
		event Action<double>? RateUpdated;

		void Start();
		void Stop();
	}
}
