using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmbeddedNetworkLab.UI.Modules.Serial
{
	public sealed class SerialVt100Processor
	{
		private readonly object _screenLock = new();
		private readonly List<StringBuilder> _screenLines = new();
		private int _cursorRow = 0;
		private int _cursorCol = 0;

		public int MaxLines { get; }
		public int MaxCols { get; }

		public SerialVt100Processor(int maxLines = 2000, int maxCols = 1024)
		{
			MaxLines = Math.Max(1, maxLines);
			MaxCols = Math.Max(1, maxCols);
			_screenLines.Add(new StringBuilder());
		}

		/// <summary>
		/// Process incoming raw data (may contain VT100/CSI sequences) and return the current screen text.
		/// Thread-safe.
		/// </summary>
		public string Process(string data)
		{
			if (string.IsNullOrEmpty(data))
				return BuildScreenText();

			lock (_screenLock)
			{
				int i = 0;
				while (i < data.Length)
				{
					char c = data[i];

					// ESC sequences (CSI)
					if (c == '\x1b')
					{
						// try to parse CSI: ESC [
						if (i + 1 < data.Length && data[i + 1] == '[')
						{
							i += 2;
							var paramSb = new StringBuilder();
							char final = '\0';
							// collect until final byte (letters @ A-Z a-z and other final bytes up to '~')
							while (i < data.Length)
							{
								char ch = data[i];
								if ((ch >= '@' && ch <= '~')) // final byte of CSI
								{
									final = ch;
									i++;
									break;
								}
								paramSb.Append(ch);
								i++;
							}

							var paramStr = paramSb.ToString();
							HandleCsi(final, paramStr);
							continue;
						}
						else
						{
							// unknown/unsupported escape - skip it
							i++;
							continue;
						}
					}

					// Controls
					if (c == '\r')
					{
						_cursorCol = 0;
						i++;
						continue;
					}

					if (c == '\n')
					{
						_cursorRow++;
						_cursorCol = 0;
						EnsureScreenHasRow(_cursorRow);
						i++;
						continue;
					}

					if (c == '\b')
					{
						if (_cursorCol > 0)
						{
							_cursorCol--;
							var sb = _screenLines[_cursorRow];
							if (_cursorCol < sb.Length)
								sb[_cursorCol] = ' ';
						}
						else if (_cursorRow > 0)
						{
							_cursorRow--;
							_cursorCol = _screenLines[_cursorRow].Length;
						}
						i++;
						continue;
					}

					// printable
					if (!char.IsControl(c))
					{
						EnsureScreenHasRow(_cursorRow);
						var sb = _screenLines[_cursorRow];

						// Ensure capacity up to column
						if (_cursorCol >= MaxCols) _cursorCol = MaxCols - 1;
						if (sb.Length <= _cursorCol)
						{
							if (_cursorCol > sb.Length)
								sb.Append(' ', _cursorCol - sb.Length);
							sb.Append(c);
						}
						else
						{
							sb[_cursorCol] = c;
						}

						_cursorCol++;
					}

					i++;
				}

				// Trim excessive lines from top
				while (_screenLines.Count > MaxLines)
					_screenLines.RemoveAt(0);

				return BuildScreenText();
			}
		}

		public void Reset()
		{
			lock (_screenLock)
			{
				_screenLines.Clear();
				_screenLines.Add(new StringBuilder());
				_cursorRow = 0;
				_cursorCol = 0;
			}
		}

		private void HandleCsi(char final, string paramStr)
		{
			int[] ParseParams()
			{
				if (string.IsNullOrEmpty(paramStr))
					return Array.Empty<int>();
				return paramStr.Split(';').Select(s =>
				{
					if (int.TryParse(s, out var v)) return v;
					return 0;
				}).ToArray();
			}

			var pars = ParseParams();

			switch (final)
			{
				// SGR - colors/attributes: ignore but consume
				case 'm':
					// do nothing for now
					break;

				// Cursor up/down/right/left
				case 'A': // CUU
					_cursorRow = Math.Max(0, _cursorRow - (pars.Length > 0 && pars[0] > 0 ? pars[0] : 1));
					EnsureScreenHasRow(_cursorRow);
					break;
				case 'B': // CUD
					_cursorRow += (pars.Length > 0 && pars[0] > 0 ? pars[0] : 1);
					EnsureScreenHasRow(_cursorRow);
					break;
				case 'C': // CUF
					_cursorCol += (pars.Length > 0 && pars[0] > 0 ? pars[0] : 1);
					if (_cursorCol >= MaxCols) _cursorCol = MaxCols - 1;
					break;
				case 'D': // CUB
					_cursorCol = Math.Max(0, _cursorCol - (pars.Length > 0 && pars[0] > 0 ? pars[0] : 1));
					break;

				// Cursor position
				case 'H':
				case 'f':
					{
						int row = 1, col = 1;
						if (pars.Length >= 1 && pars[0] > 0) row = pars[0];
						if (pars.Length >= 2 && pars[1] > 0) col = pars[1];
						_cursorRow = Math.Max(0, row - 1);
						_cursorCol = Math.Max(0, col - 1);
						EnsureScreenHasRow(_cursorRow);
					}
					break;

				// Erase in line
				case 'K':
					{
						EnsureScreenHasRow(_cursorRow);
						var sb = _screenLines[_cursorRow];
						var p = pars.Length > 0 ? pars[0] : 0;
						if (p == 0)
						{
							// erase from cursor to end
							if (_cursorCol < sb.Length)
								sb.Remove(_cursorCol, sb.Length - _cursorCol);
						}
						else if (p == 1)
						{
							// erase from start to cursor
							if (_cursorCol >= 0 && _cursorCol <= sb.Length)
								sb.Remove(0, _cursorCol);
						}
						else // p == 2
						{
							// erase entire line
							sb.Clear();
						}
					}
					break;

				// Erase in display
				case 'J':
					{
						var p = pars.Length > 0 ? pars[0] : 0;
						if (p == 2)
						{
							// clear screen
							_screenLines.Clear();
							_screenLines.Add(new StringBuilder());
							_cursorRow = 0;
							_cursorCol = 0;
						}
						else if (p == 0)
						{
							// clear from cursor to end of screen
							for (int r = _cursorRow + 1; r < _screenLines.Count; r++)
								_screenLines[r].Clear();
							var sb = _screenLines[_cursorRow];
							if (_cursorCol < sb.Length)
								sb.Remove(_cursorCol, sb.Length - _cursorCol);
						}
						// other variants ignored
					}
					break;

				// Unsupported/ignored - just consume
				default:
					break;
			}
		}

		private void EnsureScreenHasRow(int row)
		{
			while (_screenLines.Count <= row)
				_screenLines.Add(new StringBuilder());
			// enforce max size by trimming the top if needed
			while (_screenLines.Count > MaxLines)
				_screenLines.RemoveAt(0);
		}

		private string BuildScreenText()
		{
			var sb = new StringBuilder();
			for (int i = 0; i < _screenLines.Count; i++)
			{
				sb.Append(_screenLines[i].ToString());
				if (i < _screenLines.Count - 1)
					sb.Append('\n');
			}
			return sb.ToString();
		}
	}
}