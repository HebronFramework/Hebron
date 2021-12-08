using System;
using System.IO;
using ClangSharp;
using SealangSharp;

namespace Hebron
{
	public class DumpProcessor : BaseProcessor
	{
		private TextWriter _writer;

		protected override TextWriter Writer
		{
			get { return _writer; }
		}

		public DumpProcessor(CXTranslationUnit translationUnit, TextWriter writer)
			: base(translationUnit)
		{
			_writer = writer;
		}

		private CXChildVisitResult DumpCursor(CXCursor cursor, CXCursor parent, IntPtr data)
		{
			DumpCursor(cursor);

			return CXChildVisitResult.CXChildVisit_Continue;
		}

		protected void DumpCursor(CXCursor cursor)
		{
			var cursorKind = clang.getCursorKind(cursor);

			var line = string.Format("// {0}- {1} - {2}", clang.getCursorKindSpelling(cursorKind),
				clang.getCursorSpelling(cursor),
				clang.getTypeSpelling(clang.getCursorType(cursor)));

			var addition = string.Empty;

			switch (cursorKind)
			{
				case CXCursorKind.CXCursor_UnaryExpr:
				case CXCursorKind.CXCursor_UnaryOperator:
					addition = string.Format("Unary Operator: {0} ({1})",
						sealang.cursor_getUnaryOpcode(cursor),
						sealang.cursor_getOperatorString(cursor));
					break;
				case CXCursorKind.CXCursor_BinaryOperator:
					addition = string.Format("Binary Operator: {0} ({1})",
						sealang.cursor_getBinaryOpcode(cursor),
						sealang.cursor_getOperatorString(cursor));
					break;
				case CXCursorKind.CXCursor_IntegerLiteral:
				case CXCursorKind.CXCursor_FloatingLiteral:
				case CXCursorKind.CXCursor_CharacterLiteral:
				case CXCursorKind.CXCursor_StringLiteral:
					addition = string.Format("Literal: {0}",
						sealang.cursor_getLiteralString(cursor));
					break;
			}

			if (!string.IsNullOrEmpty(addition))
			{
				line += " [" + addition + "]";
			}

			IndentedWriteLine(line);

			_indentLevel++;
			clang.visitChildren(cursor, DumpCursor, new CXClientData(IntPtr.Zero));
			_indentLevel--;
		}

		public void Run()
		{
			clang.visitChildren(clang.getTranslationUnitCursor(_translationUnit), DumpCursor, new CXClientData(IntPtr.Zero));
		}
	}
}