using ClangSharp;
using System;

namespace Hebron.Roslyn
{
	public static class RoslynCodeConverter
	{
		public static RoslynConversionResult Convert(RoslynConversionParameters parameters)
		{
			if (parameters == null)
			{
				throw new ArgumentNullException(nameof(parameters));
			}

			var translationUnit = Utility.Compile(parameters.InputPath, parameters.Defines);

			foreach (var cursor in translationUnit.TranslationUnitDecl.CursorChildren)
			{
				var decl = cursor as Decl;
				if (decl == null)
				{
					continue;
				}

				if (decl.SourceRange.Start.IsInSystemHeader)
				{
					continue;
				}

				if (decl.CursorKind == ClangSharp.Interop.CXCursorKind.CXCursor_FunctionDecl)
				{
					var k = 5;
				}
			}

			return null;
		}
	}
}
