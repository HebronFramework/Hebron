using ClangSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hebron
{
	partial class CodeConverter
	{
		public void ConvertEnums()
		{
			Logger.Info("Processing enums...");

			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				if (cursor.CursorKind != ClangSharp.Interop.CXCursorKind.CXCursor_EnumDecl)
				{
					continue;
				}

				if (string.IsNullOrEmpty(cursor.Spelling))
				{
					Logger.Info("Processing unnamed enum");

					int value = 0;
					foreach (var child in cursor.CursorChildren)
					{
						if (child.CursorChildren.Count > 0)
						{
							value = int.Parse(child.CursorChildren[0].GetLiteralString());
						}

						Output.IntegerConstant(child.Spelling, value);

						++value;
					}
				}
				else
				{
					Logger.Info("Processing enum {0}", cursor.Spelling);

					Dictionary<string, int?> values = new Dictionary<string, int?>();
					foreach (var child in cursor.CursorChildren)
					{
						int? value = null;
						if (child.CursorChildren.Count > 0)
						{
							value = int.Parse(child.CursorChildren[0].GetLiteralString());
						}

						values[child.Spelling] = value;
					}

					Output.Enum(cursor.Spelling, values);
				}

				if (Parameters.SkipEnums.Contains(cursor.Spelling))
				{
					Logger.Info("Skipping");
					continue;
				}
			}
		}
	}
}