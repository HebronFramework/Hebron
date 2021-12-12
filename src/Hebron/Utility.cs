using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hebron
{
	internal static class Utility
	{
		public static unsafe TranslationUnit Compile(string inputPath, string[] defines)
		{
			if (string.IsNullOrEmpty(inputPath))
			{
				throw new ArgumentNullException(nameof(inputPath));
			}

			if (defines == null)
			{
				defines = new string[0];
			}

			var arr = new List<string>();

			foreach (var d in defines)
			{
				arr.Add("-D" + d);
			}

			//			arr.Add("-I" + @"D:\Develop\Microsoft Visual Studio 12.0\VC\include");

			var index = Index.Create();

			CXTranslationUnit cxTranslationUnit;
			var res = CXTranslationUnit.TryParse(index.Handle,
				inputPath,
				arr.ToArray(),
				new CXUnsavedFile[0],
				CXTranslationUnit_Flags.CXTranslationUnit_None,
				out cxTranslationUnit);

			var numDiagnostics = clang.getNumDiagnostics(cxTranslationUnit);
			for (uint i = 0; i < numDiagnostics; ++i)
			{
				var diag = clang.getDiagnostic(cxTranslationUnit, i);
				var str =
					clang.formatDiagnostic(diag,
						(uint)
							(CXDiagnosticDisplayOptions.CXDiagnostic_DisplaySourceLocation |
							 CXDiagnosticDisplayOptions.CXDiagnostic_DisplaySourceRanges)).ToString();
				Logger.LogLine(str);
				clang.disposeDiagnostic(diag);
			}

			if (res != CXErrorCode.CXError_Success)
			{
				var sb = new StringBuilder();

				sb.AppendLine(res.ToString());

				numDiagnostics = clang.getNumDiagnostics(cxTranslationUnit);
				for (uint i = 0; i < numDiagnostics; ++i)
				{
					var diag = clang.getDiagnostic(cxTranslationUnit, i);
					sb.AppendLine(clang.getDiagnosticSpelling(diag).ToString());
					clang.disposeDiagnostic(diag);
				}

				throw new Exception(sb.ToString());
			}

			return TranslationUnit.GetOrCreate(cxTranslationUnit);
		}

		public static IEnumerable<Cursor> EnumerateCursors(this TranslationUnit translationUnit)
		{
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

				yield return cursor;
			}
		}

		public static string GetLiteralString(this CXCursor cursor)
		{
			switch(cursor.Kind)
			{
				case CXCursorKind.CXCursor_IntegerLiteral:
					return clangsharp.Cursor_getIntegerLiteralValue(cursor).ToString();
				case CXCursorKind.CXCursor_FloatingLiteral:
					return clangsharp.Cursor_getFloatingLiteralValueAsApproximateDouble(cursor).ToString();
				case CXCursorKind.CXCursor_CharacterLiteral:
					return clangsharp.Cursor_getCharacterLiteralValue(cursor).ToString();
				case CXCursorKind.CXCursor_StringLiteral:
					return clangsharp.Cursor_getStringLiteralValue(cursor).ToString();
				case CXCursorKind.CXCursor_CXXBoolLiteralExpr:
					return clangsharp.Cursor_getBoolLiteralValue(cursor).ToString();
			}

			return string.Empty;
		}

		public static string GetLiteralString(this Cursor cursor) => GetLiteralString(cursor.Handle);
	}
}