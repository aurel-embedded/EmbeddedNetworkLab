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
using System.Reflection.PortableExecutable;
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

		// Standardized upload logging helpers
		private void EmitUploadStart(string type, string fileName, string clientIp, long expected)
		{
			ServerEventTriggered?.Invoke(this,
				$"[{DateTime.Now:HH:mm:ss}] [UPLOAD {type}] START {fileName} from {clientIp} size={expected} bytes");
		}

		private void EmitUploadDone(string type, string fileName, long totalBytes, TimeSpan duration, string clientIp, string savePath)
		{
			var rateMbps = (totalBytes * 8.0 / 1_000_000.0) / Math.Max(duration.TotalSeconds, 0.001);
			ServerEventTriggered?.Invoke(this,
				$"[{DateTime.Now:HH:mm:ss}] [UPLOAD {type}] DONE {fileName} {totalBytes} bytes in {duration.TotalSeconds:F2}s ({rateMbps:F2} Mbps) — from {clientIp} — saved to {savePath}");
		}

        private async Task HandleMultipartUpload(HttpContext context)
        {
            // Try to stream the multipart request so we can report progress while the body is being received.
            // Fall back to the buffered approach if the content type is missing or not multipart.
            var contentType = context.Request.ContentType;
            if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
            {
                // Fallback to previous buffered behavior
                var form = await context.Request.ReadFormAsync();
                var file = form.Files.FirstOrDefault();
                if (file == null)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("{\"error\":\"no file\"}");
                    return;
                }

                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "?";

                var saveDir = Path.Combine(AppContext.BaseDirectory, "received_videos");
                Directory.CreateDirectory(saveDir);
                var savePath = Path.Combine(saveDir, file.FileName);

                var expected = file.Length;
                EmitUploadStart("MULTIPART", file.FileName, clientIp, expected);

                var totalRead = 0L;
                var buffer = new byte[8192];
                var start = DateTime.UtcNow;

                using (var input = file.OpenReadStream())
                using (var fs = File.Create(savePath))
                {
                    while (true)
                    {
                        var read = await input.ReadAsync(buffer);
                        if (read == 0) break;
                        await fs.WriteAsync(buffer.AsMemory(0, read));
                        totalRead += read;

                        if (expected > 0)
                        {
                            var percent = (double)totalRead / expected * 100.0;
                            UploadProgressChanged?.Invoke(this, new UploadProgress(totalRead, expected, percent));
                        }
                        else
                        {
                            ServerEventTriggered?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] [UPLOAD PROGRESS] {totalRead} bytes");
                        }
                    }
                }

                var duration = DateTime.UtcNow - start;
                var receivedAt = DateTime.Now;
                var video = new ReceivedVideo(file.FileName, savePath, receivedAt);
                VideoReceived?.Invoke(this, video);

                if (expected > 0)
                {
                    UploadProgressChanged?.Invoke(this, new UploadProgress(totalRead, expected, 100.0));
                }

                EmitUploadDone("MULTIPART", file.FileName, totalRead, duration, clientIp, savePath);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("{\"status\":\"uploaded\"}");
                return;
            }

            // Streamed multipart parsing
            try
            {
                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "?";
                var saveDir = Path.Combine(AppContext.BaseDirectory, "received_videos");
                Directory.CreateDirectory(saveDir);

                // Extract boundary from content-type header (manual parse to avoid dependency on specific header types)
                string? boundary = null;
                var parts = contentType.Split(';');
                foreach (var part in parts)
                {
                    var idx = part.IndexOf('=');
                    if (idx <= 0) continue;
                    var name = part.Substring(0, idx).Trim();
                    var value = part.Substring(idx + 1).Trim().Trim('"');
                    if (name.Equals("boundary", StringComparison.OrdinalIgnoreCase))
                    {
                        boundary = value;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(boundary))
                {
                    throw new InvalidOperationException("Missing multipart boundary");
                }

                var reader = new Microsoft.AspNetCore.WebUtilities.MultipartReader(boundary, context.Request.Body);

                Microsoft.AspNetCore.WebUtilities.MultipartSection section;
                while ((section = await reader.ReadNextSectionAsync()) != null)
                {
                    var hasContentDisposition = System.Net.Http.Headers.ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var cd);
                    if (!hasContentDisposition) continue;

                    // Is this a file section?
                    if (cd.DispositionType.Equals("form-data") && (!string.IsNullOrEmpty(cd.FileName) || !string.IsNullOrEmpty(cd.FileNameStar)))
                    {
                        var rawFileName = cd.FileName ?? cd.FileNameStar;
                        var fileName = rawFileName?.Trim('"');
                        var savePath = Path.Combine(saveDir, fileName);

                        var expected = context.Request.ContentLength ?? -1;
                        EmitUploadStart("MULTIPART", fileName, clientIp, expected);

                        var totalRead = 0L;
                        var buffer = new byte[8192];
                        var start = DateTime.UtcNow;

                        using (var fs = File.Create(savePath))
                        {
                            while (true)
                            {
                                var read = await section.Body.ReadAsync(buffer);
                                if (read == 0) break;
                                await fs.WriteAsync(buffer.AsMemory(0, read));
                                totalRead += read;

                                if (expected > 0)
                                {
                                    var percent = (double)totalRead / expected * 100.0;
                                    UploadProgressChanged?.Invoke(this, new UploadProgress(totalRead, expected, percent));
                                }
                                else
                                {
                                    ServerEventTriggered?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] [UPLOAD PROGRESS] {totalRead} bytes");
                                }
                            }
                        }

                        var duration = DateTime.UtcNow - start;
                        var receivedAt = DateTime.Now;
                        var video = new ReceivedVideo(fileName, savePath, receivedAt);
                        VideoReceived?.Invoke(this, video);

                        if (expected > 0)
                        {
                            UploadProgressChanged?.Invoke(this, new UploadProgress(totalRead, expected, 100.0));
                        }

                        EmitUploadDone("MULTIPART", fileName, totalRead, duration, clientIp, savePath);
                    }
                }

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("{\"status\":\"uploaded\"}");
            }
            catch (Exception ex)
            {
                ServerEventTriggered?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] [ERROR] Multipart handling failed: {ex.Message}");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("{\"error\":\"multipart error\"}");
            }
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

			EmitUploadStart("RAW", fileName, clientIp, expected);

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

			var duration = DateTime.UtcNow - start;

			EmitUploadDone("RAW", fileName, totalRead, duration, clientIp, savePath);

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
