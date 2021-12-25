using ClangSharp;
using System;

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
						value = int.Parse(child.CursorChildren[0].GetLiteralString());
					}

					var expr = "pub const " + name + ": i32 = " + value + ";" + Environment.NewLine;

					Result.UnnamedEnumValues[child.Spelling] = expr;

					++value;
				}
			}
		}
	}
}
