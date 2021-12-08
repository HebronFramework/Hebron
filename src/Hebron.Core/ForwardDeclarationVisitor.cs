using System;
using ClangSharp;

namespace Hebron
{
	internal sealed class ForwardDeclarationVisitor
	{
		private readonly CXCursor beginningCursor;

		private bool beginningCursorReached;

		public ForwardDeclarationVisitor(CXCursor beginningCursor)
		{
			this.beginningCursor = beginningCursor;
		}

		public CXCursor ForwardDeclarationCursor { get; private set; }

		public CXChildVisitResult Visit(CXCursor cursor, CXCursor parent, IntPtr data)
		{
			if (cursor.IsInSystemHeader())
			{
				return CXChildVisitResult.CXChildVisit_Continue;
			}

			if (clang.equalCursors(cursor, this.beginningCursor) != 0)
			{
				this.beginningCursorReached = true;
				return CXChildVisitResult.CXChildVisit_Continue;
			}

			if (this.beginningCursorReached)
			{
				this.ForwardDeclarationCursor = cursor;
				return CXChildVisitResult.CXChildVisit_Break;
			}

			return CXChildVisitResult.CXChildVisit_Recurse;
		}
	}
}