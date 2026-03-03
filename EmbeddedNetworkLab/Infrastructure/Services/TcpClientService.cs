using EmbeddedNetworkLab.Core;
using System.Diagnostics;
using System.Net.Sockets;

public class TcpClientService : ITcpClientService
{
	public event Action<double>? RateUpdated;

	private CancellationTokenSource? _cts;
	private Task? _task;

	private string _address = "127.0.0.1";
	private int _port = 8080;

	public void Configure(string address, int port)
	{
		_address = address;
		_port = port;
	}

	public void Start()
	{
		if (_task != null)
			return;

		_cts = new CancellationTokenSource();
		_task = Task.Run(() => RunAsync(_cts.Token));
	}

	public void Stop()
	{
		_cts?.Cancel();
		_task = null;
	}

	private async Task RunAsync(CancellationToken token)
	{
		try
		{
			using var client = new TcpClient();
			client.NoDelay = true; // Important for throughput tests

			await client.ConnectAsync(_address, _port);

			using NetworkStream stream = client.GetStream();

			byte[] buffer = new byte[4096]; // test chunk size
			new Random().NextBytes(buffer);

			var sw = Stopwatch.StartNew();
			long bytesSent = 0;
			long lastBytes = 0;
			long lastTime = 0;

			while (!token.IsCancellationRequested)
			{
				await stream.WriteAsync(buffer, 0, buffer.Length, token);
				bytesSent += buffer.Length;

				long currentTime = sw.ElapsedMilliseconds;

				// Update every 500 ms
				if (currentTime - lastTime >= 500)
				{
					long deltaBytes = bytesSent - lastBytes;
					double seconds = (currentTime - lastTime) / 1000.0;
					double rate = deltaBytes / seconds; // Bytes per second

					RateUpdated?.Invoke(rate);

					lastBytes = bytesSent;
					lastTime = currentTime;
				}
			}
		}
		catch
		{
			RateUpdated?.Invoke(0);
		}
	}
}