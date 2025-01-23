using System;

namespace Test {
	class ConsoleTerminal {
		private static string inputText = string.Empty;
		private static int inputPos = 0;
		private static List<string> history = new List<string>() { string.Empty };
		private static int historyPos = 0;

		private static void Bain() {
			Console.WriteLine(Console.BufferWidth);
			Console.WriteLine(Console.BufferHeight);
			Console.Write("> ");
			while (true) {
				ConsoleKeyInfo keyInfo = Console.ReadKey(true);
				Console.Title = (!Char.IsControl(keyInfo.KeyChar)).ToString();
				if (!Char.IsControl(keyInfo.KeyChar)) //((keyn > 47 && keyn < 65) || (keyn > 57 && keyn < 91) || keyn == 32)
					AddChar(keyInfo.KeyChar);
				else switch(keyInfo.Key) {
				  case ConsoleKey.Backspace:
					Remove(true);
					break;
				  case ConsoleKey.Delete:
					Remove(false);
					break;
				  case ConsoleKey.Enter:
					Handle();
					break;
				  case ConsoleKey.LeftArrow:
					Move(true);
					break;
				  case ConsoleKey.RightArrow:
					Move(false);
					break;
				  case ConsoleKey.UpArrow:
					if (historyPos > 0)
						SetHistory(historyPos--);
					break;
				  case ConsoleKey.DownArrow:
					if (historyPos+1 < history.Count)
						SetHistory(historyPos++);
					break;
				  case ConsoleKey.Home:
					MoveCursor(-inputPos);
					inputPos = 0;
					break;
				  case ConsoleKey.End:
					MoveCursor(inputText.Length-inputPos);
					inputPos = inputText.Length;
					break;
				}
			}
		}

		private static void SetHistory(int i) {
			MoveCursor(-inputPos);
			Console.Write(new String(' ', inputText.Length));
			MoveCursor(-inputText.Length);
			Console.Write(history[i]);
			inputText = history[i]; 
			inputPos = history[i].Length;
		}
		private static void Remove(bool rev) {
			if ((inputPos == inputText.Length && !rev) || (inputPos == 0 && rev)) return;
			int i = rev?-1:0;
			inputPos += i;
			inputText = inputText.Remove(inputPos, 1);

			MoveCursor(i);
			Console.Write(inputText.Substring(inputPos)+" ");
			MoveCursor(-inputText.Substring(inputPos).Length-1);
		}
		private static void Move(bool rev) {
			if ((inputPos == inputText.Length && !rev) || (inputPos == 0 && rev)) return;
			int i = rev?-1:1;
			MoveCursor(i);
			inputPos += i;
		}
		private static void MoveCursor(int val) {
			int newLeft = Console.CursorLeft+val;
			int rows = 0;
			while (newLeft+rows*Console.BufferWidth < 0) rows++;
			while (newLeft+rows*Console.BufferWidth >= Console.BufferWidth) rows--;
			Console.CursorTop -= rows;
			Console.CursorLeft = newLeft+rows*Console.BufferWidth;
		}
		private static void AddChar(char ch) {
			inputText = inputText.Insert(inputPos, ch+string.Empty);
			inputPos++;
			Console.Write(ch);
			Console.Write(inputText.Substring(inputPos));
			MoveCursor(-inputText.Substring(inputPos).Length);
		}
		private static void Handle() {
			MoveCursor(inputText.Length-inputPos);
			Console.WriteLine();

			string[] cmd = System.Text.RegularExpressions.Regex.Matches(inputText, @"[\""].+?[\""]|[^ ]+")
			.Cast<System.Text.RegularExpressions.Match>()
			.Select(m => m.Value)
			.ToArray();

			foreach (string a in cmd) Console.WriteLine(a);

			history.Insert(history.Count-2, inputText);
			historyPos = history.Count-1;
			inputPos = 0;
			inputText = string.Empty;
			Console.Write("> ");
		}
	}
}