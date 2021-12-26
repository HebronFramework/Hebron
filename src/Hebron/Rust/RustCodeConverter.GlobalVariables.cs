using ClangSharp;
using System;
using System.Linq;

namespace Hebron.Rust
{
	partial class RustCodeConverter
	{
		public void ConvertGlobalVariables()
		{
			if (!Parameters.ConversionEntities.HasFlag(ConversionEntities.GlobalVariables))
			{
				return;
			}

			_state = State.GlobalVariables;
			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				var varDecl = cursor as VarDecl;
				if (varDecl == null)
				{
					continue;
				}

				var name = cursor.Spelling.FixSpecialWords();
				Logger.Info("Processing global variable {0}", name);

				if (Parameters.SkipGlobalVariables.Contains(name))
				{
					Logger.Info("Skipped.");
					continue;
				}

				var res = Process(cursor);

				var varName = res.Expression.Split(':')[0];
				var expr = res.Expression.EnsureStatementEndWithSemicolon();
				Result.GlobalVariables[varName] = expr;
			}
		}
	}
}