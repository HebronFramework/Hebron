using ClangSharp;
using System;
using System.Globalization;

namespace Hebron.Rust
{
	partial class RustCodeConverter
	{
		public void ConvertEnums()
		{
			if (!Parameters.ConversionEntities.HasFlag(ConversionEntities.Enums))
			{
				return;
			}

			Logger.Info("Processing enums...");

			_state = State.Enums;
			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				if (cursor.CursorKind != ClangSharp.Interop.CXCursorKind.CXCursor_EnumDecl)
				{
					continue;
				}

				int value = 0;
				foreach (var child in cursor.CursorChildren)
				{
					var name = child.Spelling;
					if (child.CursorChildren.Count > 0)
					{
						var str = child.CursorChildren[0].GetLiteralString();
						if (str.StartsWith("0x"))
						{
							value = int.Parse(str.Substring(2), NumberStyles.HexNumber);
						}
						else
						{
							value = int.Parse(str);
						}
					}

					var expr = "pub const " + name + ": i32 = " + value + ";" + Environment.NewLine;

					Result.UnnamedEnumValues[child.Spelling] = expr;

					++value;
				}
			}
		}
	}
}
