

namespace EmbeddedNetworkLab.Core.Services
{
	public interface IThroughputService
	{
		event Action<double>? RateUpdated;

		void Start();
		void Stop();
	}
}
