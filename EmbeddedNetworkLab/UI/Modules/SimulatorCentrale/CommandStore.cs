using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace EmbeddedNetworkLab.UI.Modules.SimulatorCentrale
{
	public sealed class CommandStore
	{
		private readonly string _filePath;

		public CommandStore(string filePath)
		{
			_filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
		}

		public record CommandDto(string Name, string Text);

		// Synchronous load (simple to call from ctor). Returns empty array if file missing or invalid.
		public CommandDto[] Load()
		{
			try
			{
				if (!File.Exists(_filePath))
					return Array.Empty<CommandDto>();

				var json = File.ReadAllText(_filePath);
				var values = JsonSerializer.Deserialize<CommandDto[]?>(json);
				return values ?? Array.Empty<CommandDto>();
			}
			catch
			{
				// swallow here; caller may log
				return Array.Empty<CommandDto>();
			}
		}

		// Async save with atomic write (temp file + move)
		// Exceptions are no longer swallowed so caller can log failures.
		public async Task SaveAsync(CommandDto[] items)
		{
			if (items == null) throw new ArgumentNullException(nameof(items));

			var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
			var tmp = _filePath + ".tmp";
			await File.WriteAllTextAsync(tmp, json).ConfigureAwait(false);
			// Move with overwrite
			File.Move(tmp, _filePath, overwrite: true);
		}
	}
}