﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LanguageExt;
using LanguageExt.SomeHelp;

namespace AsciiUml {

	public class Range<T> {
		public T Min, Max;

		public Range(T min, T max) {
			Min = min;
			Max = max;
		}

		public override string ToString() {
			return $"{Min} - {Max}";
		}
	}

	public static class CommandParser {
		public static Option<int> ReadInt(Range<int> range, string text) {
			var input = TryReadLineWithCancel(text);
			return input.Match(x => {
				int result;
				if (!int.TryParse(x, out result))
					return ReadInt(range, "\nNot a number. Try again: ");

				if (result < range.Min || result > range.Max)
					return ReadInt(range, $"\nNot in range {range}: ");

				return result.ToSome();
			}, () => Option<int>.None);
		}

		public static Option<string> TryReadLineWithCancel(string explanation) {
            Console.WriteLine("ESC to abort input. RETURN to finish input. SHIFT+RETURN for muliline input");
            Console.Write(explanation);

            var sb = new StringBuilder();
            do {
				var key = Console.ReadKey(true);
				if (key.Key == ConsoleKey.Enter)
                {
                    if (key.Modifiers == ConsoleModifiers.Shift)
                        sb.Append("\n");
                    else
                        return sb.ToString();
                }
                else if (key.Key == ConsoleKey.Escape)
					return null;
				else if (key.Key == ConsoleKey.Backspace) {
					if (sb.Length > 0) {
						sb.Remove(sb.Length - 1, 1);
						Console.Write("\b \b");
					}
				}
				else {
					Console.Write(key.KeyChar);
					sb.Append(key.KeyChar);
				}
			} while (true);
		}
	}
}