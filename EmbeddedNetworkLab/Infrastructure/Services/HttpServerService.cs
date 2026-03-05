using EmbeddedNetworkLab.Core;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedNetworkLab.Infrastructure.Services
{
	public class HttpServerService : IHttpServerService
	{
		private HttpListener? _listener;
		private CancellationTokenSource? _cts;
		private Task? _listenTask;

		private readonly List<string> _listeningUrls = new();
		public IReadOnlyCollection<string> ListeningUrls => _listeningUrls;

		public bool IsRunning { get; private set; }

		public event EventHandler<string>? RequestReceived;
		public event EventHandler<string>? ServerEventTriggered;

		public async Task StartAsync(string bindIp, int httpPort, bool httpsEnabled, int httpsPort)
		{
			if (IsRunning) return;

			_listener = new HttpListener();
			_listeningUrls.Clear();

			// HttpListener prefix format: http://hostname:port/
			// Use "+" to bind to all interfaces, or specific IP for restricted binding.
			// Note: on Windows, binding to a specific IP requires the prefix to be registered
			// via "netsh http add urlacl url=http://{ip}:{port}/ user=Everyone"
			// Using "+" avoids this requirement.
			string host = bindIp == "0.0.0.0" ? "+" : bindIp;

			var httpPrefix = $"http://{host}:{httpPort}/";
			_listener.Prefixes.Add(httpPrefix);
			_listeningUrls.Add($"http://{bindIp}:{httpPort}/");

			if (httpsEnabled)
			{
				var httpsPrefix = $"https://{host}:{httpsPort}/";
				_listener.Prefixes.Add(httpsPrefix);
				_listeningUrls.Add($"https://{bindIp}:{httpsPort}/");
			}

			try
			{
				_listener.Start();
			}
			catch (Exception ex)
			{
				ServerEventTriggered?.Invoke(this, $"[ERROR] Failed to start: {ex.Message}");
				_listener.Close();
				_listener = null;
				return;
			}

			IsRunning = true;
			ServerEventTriggered?.Invoke(this, $"[START] Listening on {string.Join(", ", _listeningUrls)}");

			_cts = new CancellationTokenSource();
			_listenTask = Task.Run(() => ListenLoop(_cts.Token));

			await Task.CompletedTask;
		}

		public async Task StopAsync()
		{
			if (!IsRunning) return;

			_cts?.Cancel();

			try
			{
				_listener?.Stop();
				if (_listenTask != null)
					await _listenTask.ConfigureAwait(false);
			}
			catch { }
			finally
			{
				_listener?.Close();
				_listener = null;
				_cts?.Dispose();
				_cts = null;
				_listenTask = null;
				IsRunning = false;
			}

			ServerEventTriggered?.Invoke(this, "[STOP] Server stopped");
		}

		private async Task ListenLoop(CancellationToken token)
		{
			while (!token.IsCancellationRequested && _listener is { IsListening: true })
			{
				try
				{
					var contextTask = _listener.GetContextAsync();
					var completedTask = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, token));

					if (completedTask != contextTask) break;

					var context = await contextTask;
					_ = Task.Run(() => HandleRequest(context), token);
				}
				catch (HttpListenerException) { break; }
				catch (OperationCanceledException) { break; }
				catch (ObjectDisposedException) { break; }
				catch (Exception ex)
				{
					ServerEventTriggered?.Invoke(this, $"[ERROR] {ex.Message}");
				}
			}
		}

		private async Task HandleRequest(HttpListenerContext context)
		{
			var req = context.Request;
			var resp = context.Response;

			var clientIp = req.RemoteEndPoint?.Address?.ToString() ?? "?";
			var ts = DateTime.Now.ToString("HH:mm:ss");
			var logLine = $"[{ts}] {req.HttpMethod} {req.Url?.PathAndQuery} from {clientIp}";

			try
			{
				var body = Encoding.UTF8.GetBytes("{\"status\":\"ok\",\"server\":\"EmbeddedNetworkLab\"}");
				resp.StatusCode = 200;
				resp.ContentType = "application/json";
				resp.ContentLength64 = body.Length;
				await resp.OutputStream.WriteAsync(body);

				RequestReceived?.Invoke(this, $"{logLine} → 200");
			}
			catch (Exception ex)
			{
				RequestReceived?.Invoke(this, $"{logLine} → ERROR ({ex.Message})");
			}
			finally
			{
				resp.Close();
			}
		}
	}
}
