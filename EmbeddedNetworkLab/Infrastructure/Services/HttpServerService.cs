using EmbeddedNetworkLab.Core;
using EmbeddedNetworkLab.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EmbeddedNetworkLab.Infrastructure.Services
{
	public class HttpServerService : IHttpServerService
	{
		private WebApplication? _app;

		private readonly List<string> _listeningUrls = new();
		public IReadOnlyCollection<string> ListeningUrls => _listeningUrls;

		public bool IsRunning { get; private set; }

		public event EventHandler<string>? RequestReceived;
		public event EventHandler<string>? ServerEventTriggered;
		public event EventHandler<ReceivedVideo>? VideoReceived;
		public event EventHandler<UploadProgress>? UploadProgressChanged;

		public async Task StartAsync(string bindIp, int httpPort, bool httpsEnabled, int httpsPort)
		{
			if (IsRunning) return;

			_listeningUrls.Clear();

			try
			{
				var builder = WebApplication.CreateBuilder();

				// Suppress ASP.NET Core console logs — we forward events ourselves
				builder.Logging.ClearProviders();

				var ip = bindIp == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(bindIp);

				builder.WebHost.ConfigureKestrel(options =>
				{
					options.Listen(ip, httpPort);
					if (httpsEnabled)
						options.Listen(ip, httpsPort, o => o.UseHttps());
				});

				_app = builder.Build();

				// Multipart upload endpoint
				_app.MapPost("/upload", HandleMultipartUpload);

				// Raw stream upload endpoint
				_app.MapPost("/upload/raw", HandleRawUpload);

				// Single catch-all route
				_app.Map("/{**path}", HandleDefault);

				_listeningUrls.Add($"http://{bindIp}:{httpPort}/");
				if (httpsEnabled)
					_listeningUrls.Add($"https://{bindIp}:{httpsPort}/");

				await _app.StartAsync();

				IsRunning = true;
				ServerEventTriggered?.Invoke(this, 
					$"[{DateTime.Now:HH:mm:ss}] [START] Listening on {string.Join(", ", _listeningUrls)}");
				ServerEventTriggered?.Invoke(this,
					$"[{DateTime.Now:HH:mm:ss}] [CONFIG] HTTP port={httpPort} HTTPS={httpsEnabled}");
			}
			catch (Exception ex)
			{
				ServerEventTriggered?.Invoke(this, 
					$"[{DateTime.Now:HH:mm:ss}] [ERROR] Failed to start: {ex.Message}");
				await TryCleanupAsync();
			}
		}

		public async Task StopAsync()
		{
			if (!IsRunning) return;
			ServerEventTriggered?.Invoke(this,
				$"[{DateTime.Now:HH:mm:ss}] [STOP REQUEST]");

			await TryCleanupAsync();

			ServerEventTriggered?.Invoke(this, 
				$"[{DateTime.Now:HH:mm:ss}] [STOP] Server stopped");
		}

		private async Task TryCleanupAsync()
		{
			try
			{
				if (_app != null)
				{
					await _app.StopAsync();
					await _app.DisposeAsync();
					_app = null;
				}
			}
			catch { }
			finally
			{
				IsRunning = false;
				_listeningUrls.Clear();
			}
		}

		private async Task HandleMultipartUpload(HttpContext context)
		{
			var form = await context.Request.ReadFormAsync();
			var file = form.Files.FirstOrDefault();
			if (file == null)
			{
				context.Response.StatusCode = 400;
				await context.Response.WriteAsync("{\"error\":\"no file\"}");
				return;
			}

			var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "?";
			ServerEventTriggered?.Invoke(this,
				$"[{DateTime.Now:HH:mm:ss}] [MULTIPART START] {file.FileName} from {clientIp}");

			var saveDir = Path.Combine(AppContext.BaseDirectory, "received_videos");
			Directory.CreateDirectory(saveDir);
			var savePath = Path.Combine(saveDir, file.FileName);
			using (var stream = File.Create(savePath))
				await file.CopyToAsync(stream);

			var receivedAt = DateTime.Now;
			var video = new ReceivedVideo(file.FileName, savePath, receivedAt);
			VideoReceived?.Invoke(this, video);

			context.Response.ContentType = "application/json";
			context.Response.StatusCode = 200;
			await context.Response.WriteAsync("{\"status\":\"uploaded\"}");

			var ts = receivedAt.ToString("HH:mm:ss");
			var sizeKb = file.Length / 1024.0;
			ServerEventTriggered?.Invoke(this, 
				$"[{ts}] [UPLOAD] {file.FileName} — {sizeKb:F1} KB — from {clientIp} — saved to {savePath}");
		}

		private async Task HandleRawUpload(HttpContext context)
		{
			var saveDir = Path.Combine(AppContext.BaseDirectory, "received_videos");
			Directory.CreateDirectory(saveDir);

			var fileName = $"video_{DateTime.Now:HHmmss}.mp4";
			var savePath = Path.Combine(saveDir, fileName);

			var expected = context.Request.ContentLength ?? -1;
			foreach (var header in context.Request.Headers)
			{
				ServerEventTriggered?.Invoke(this,
					$"[{DateTime.Now:HH:mm:ss}] [HEADER] {header.Key}: {header.Value}");
			}

			var totalRead = 0L;

			var buffer = new byte[8192];

			var start = DateTime.UtcNow;

			var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "?";

			ServerEventTriggered?.Invoke(this,
				$"[{DateTime.Now:HH:mm:ss}] [UPLOAD HEADER] Content-Type={context.Request.ContentType} Length={expected}");

			ServerEventTriggered?.Invoke(this,
				$"[{DateTime.Now:HH:mm:ss}] [UPLOAD START] {fileName} from {clientIp} size={expected} bytes");

			using var fs = File.Create(savePath);

			while (true)
			{
				var read = await context.Request.Body.ReadAsync(buffer);

				if (read == 0)
					break;

				await fs.WriteAsync(buffer.AsMemory(0, read));
				totalRead += read;

				if (expected > 0)
				{
					var percent = (double)totalRead / expected * 100.0;

					UploadProgressChanged?.Invoke(this,
						new UploadProgress(totalRead, expected, percent));
				}
				else
				{
					ServerEventTriggered?.Invoke(this,
						$"[{DateTime.Now:HH:mm:ss}] [UPLOAD PROGRESS] {totalRead} bytes");
				}

			}

			var duration = Math.Max((DateTime.UtcNow - start).TotalSeconds, 0.001); // Avoid division by zero
			var rateMbps = (totalRead * 8.0 / 1_000_000.0) / duration;

			ServerEventTriggered?.Invoke(this,
				$"[{DateTime.Now:HH:mm:ss}] [UPLOAD DONE] {fileName} {totalRead} bytes in {duration:F2}s ({rateMbps:F2} Mbps)");

			VideoReceived?.Invoke(this,
				new ReceivedVideo(fileName, savePath, DateTime.Now));

			context.Response.StatusCode = 200;
			await context.Response.WriteAsync("{\"status\":\"uploaded\"}");

		}

		private async Task HandleDefault(HttpContext context)
		{
			var req = context.Request;
			var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "?";
			var ts = DateTime.Now.ToString("HH:mm:ss");
			var logLine = $"[{ts}] {req.Method} {req.Path}{req.QueryString} from {clientIp}";

			context.Response.ContentType = "application/json";
			context.Response.StatusCode = 200;
			ServerEventTriggered?.Invoke(this,
				$"[{ts}] [REQUEST] {req.Method} {req.Path}{req.QueryString} from {clientIp}");

			await context.Response.WriteAsync("{\"status\":\"ok\",\"server\":\"EmbeddedNetworkLab\"}");

			RequestReceived?.Invoke(this, $"{logLine} → 200");
		}
	}
}
