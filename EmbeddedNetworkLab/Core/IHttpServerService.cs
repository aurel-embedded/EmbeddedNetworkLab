using EmbeddedNetworkLab.Core.Models;

namespace EmbeddedNetworkLab.Core
{
	public interface IHttpServerService
	{
		Task StartAsync(string bindIp, int httpPort, bool httpsEnabled, int httpsPort);
		Task StopAsync();

		bool IsRunning { get; }
		IReadOnlyCollection<string> ListeningUrls { get; }

		event EventHandler<string>? RequestReceived;
		event EventHandler<string>? ServerEventTriggered;
		event EventHandler<ReceivedVideo>? VideoReceived;
		event EventHandler<UploadProgress>? UploadProgressChanged;
	}
}
