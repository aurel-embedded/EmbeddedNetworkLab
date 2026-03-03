

using EmbeddedNetworkLab.Core;

namespace EmbeddedNetworkLab.Infrastructure.Services
{
	public class TcpClientServiceMock : ITcpClientService
	{
		public event Action<double>? RateUpdated;

		private bool _running;

		public void Configure(string address, int port)
		{
			// No configuration needed for this dummy service
		}

		public void Start()
		{
			_running = true;

			Task.Run(async () =>
			{
				var rnd = new Random();

				while (_running)
				{
					await Task.Delay(200);
					RateUpdated?.Invoke(rnd.NextDouble() * 100);
				}
			});
		}

		public void Stop()
		{
			_running = false;
		}
	}
}

