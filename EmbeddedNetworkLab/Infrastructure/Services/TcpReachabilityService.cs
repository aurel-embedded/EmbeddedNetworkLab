using EmbeddedNetworkLab.Core;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedNetworkLab.Infrastructure.Services
{
	public sealed class TcpReachabilityService : ITcpReachabilityService
	{
		public async Task<bool> TryConnectAsync(
			string address,
			int port,
			TimeSpan timeout,
			CancellationToken ct)
		{
			using var client = new TcpClient();

			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

			linkedCts.CancelAfter(timeout);

			try
			{
				await client.ConnectAsync(address, port, linkedCts.Token);

				using var stream = client.GetStream();

				// Protocol byte: reach
				await stream.WriteAsync(new byte[] { 0x01 }, linkedCts.Token);

				return true;
			}
			catch (OperationCanceledException)
			{
				return false;
			}
			catch
			{
				return false;
			}
		}
	}
}
