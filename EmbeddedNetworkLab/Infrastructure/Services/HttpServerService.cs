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

				// Upload endpoint
				_app.MapPost("/upload", async context =>
				{
					var form = await context.Request.ReadFormAsync();
					var file = form.Files.FirstOrDefault();
					if (file == null)
					{
						context.Response.StatusCode = 400;
						await context.Response.WriteAsync("{\"error\":\"no file\"}");
						return;
					}

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
					var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "?";
					var sizeKb = file.Length / 1024.0;
					ServerEventTriggered?.Invoke(this, $"[{ts}] [UPLOAD] {file.FileName} — {sizeKb:F1} KB — from {clientIp} — saved to {savePath}");
				});

				// Single catch-all route
				_app.Map("/{**path}", async context =>
				{
					var req = context.Request;
					var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "?";
					var ts = DateTime.Now.ToString("HH:mm:ss");
					var logLine = $"[{ts}] {req.Method} {req.Path}{req.QueryString} from {clientIp}";

					context.Response.ContentType = "application/json";
					context.Response.StatusCode = 200;
					await context.Response.WriteAsync("{\"status\":\"ok\",\"server\":\"EmbeddedNetworkLab\"}");

					RequestReceived?.Invoke(this, $"{logLine} → 200");
				});

				_listeningUrls.Add($"http://{bindIp}:{httpPort}/");
				if (httpsEnabled)
					_listeningUrls.Add($"https://{bindIp}:{httpsPort}/");

				await _app.StartAsync();

				IsRunning = true;
				ServerEventTriggered?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] [START] Listening on {string.Join(", ", _listeningUrls)}");
			}
			catch (Exception ex)
			{
				ServerEventTriggered?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] [ERROR] Failed to start: {ex.Message}");
				await TryCleanupAsync();
			}
		}

		public async Task StopAsync()
		{
			if (!IsRunning) return;

			await TryCleanupAsync();
			ServerEventTriggered?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] [STOP] Server stopped");
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
	}
}
