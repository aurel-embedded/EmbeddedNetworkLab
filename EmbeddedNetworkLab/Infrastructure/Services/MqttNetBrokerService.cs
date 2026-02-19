using MqttManager.Core;
using MQTTnet;
using MQTTnet.Diagnostics.Logger;
using MQTTnet.Server;
using MQTTnet.Server.Internal.Adapter;
using System.Net;

namespace MqttManager.Infrastructure
{

	//------------------------------------------------------------------------------
	/// \class MqttNetBrokerService
	/// \brief MQTT broker implementation using MQTTnet
	//------------------------------------------------------------------------------
	public class MqttNetBrokerService : IMqttBrokerService
	{
		private MqttServer? _server;
		public bool IsRunning { get; private set; } = false;

		private readonly List<string> _listeningAddresses = new();
		public IReadOnlyCollection<string> ListeningAddresses => _listeningAddresses;

		/// <summary>
		/// Event raised whenever a message is intercepted by the broker
		/// </summary>
		public event EventHandler<string>? MessageIntercepted;
		public event EventHandler<string>? BrokerEvent;

		//------------------------------------------------------------------------------
		/// \brief Start the broker on the given port (default 1883)
		//------------------------------------------------------------------------------
		public async Task StartAsync(int port = 1883, string? bindIp = null, string? username = null, string? password = null)
		{
			if (IsRunning)
				return;

			// Configure server options
			var optionsBuilder = new MqttServerOptionsBuilder()
				.WithDefaultEndpoint()
				.WithDefaultEndpointPort(port);
			if (!string.IsNullOrWhiteSpace(bindIp))
			{
				optionsBuilder.WithDefaultEndpointBoundIPAddress(
					IPAddress.Parse(bindIp)
				);
			}

			// Build server options
			var options = optionsBuilder.Build();

			// define local listening addresses
			_listeningAddresses.Clear();
			if (!string.IsNullOrWhiteSpace(bindIp))
			{
				_listeningAddresses.Add($"{bindIp}:{port}");
			}
			else
			{
				_listeningAddresses.Add($"0.0.0.0:{port}");
			}

			// Create adapters (needed in v5)
			new MqttTcpServerAdapter(); // basic TCP adapter

			// Remove the incorrect constructor usage and use the parameterless constructor instead
			var adapters = new List<IMqttServerAdapter>
			{
				new MqttTcpServerAdapter() // basic TCP adapter
            };

			// Logger (can be null, here we use a minimal one)
			var logger = new MqttNetEventLogger();

			_server = new MqttServer(options, adapters, logger);

			// Intercept messages
			_server.InterceptingPublishAsync += args =>
			{
				var topic = args.ApplicationMessage.Topic;
				var payload = args.ApplicationMessage.ConvertPayloadToString();
				var message = $"[{topic}] {payload}";
				var ts = DateTime.Now.ToString("HH:mm:ss");

				MessageIntercepted?.Invoke(this, $"{ts} [MSG] {topic}: {payload}");

				return Task.CompletedTask;
			};

			// Track client connections
			_server.ClientConnectedAsync += args =>
			{
				var ts = DateTime.Now.ToString("HH:mm:ss");
				BrokerEvent?.Invoke(this, $"{ts} [CONNECT] Client {args.ClientId}");
				return Task.CompletedTask;
			};

			_server.ClientDisconnectedAsync += args =>
			{
				var ts = DateTime.Now.ToString("HH:mm:ss");
				BrokerEvent?.Invoke(this, $"{ts} [DISCONNECT] Client {args.ClientId}");
				return Task.CompletedTask;
			};

			await _server.StartAsync();
			IsRunning = true;
		}


		//------------------------------------------------------------------------------
		/// \brief Stop the broker if it is running
		//------------------------------------------------------------------------------
		public async Task StopAsync()
		{
			if (_server != null && IsRunning)
			{
				await _server.StopAsync();
				_server.Dispose();
				_server = null;
				IsRunning = false;
			}
		}
	}
}
