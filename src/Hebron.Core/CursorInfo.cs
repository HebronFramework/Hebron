using ClangSharp;

namespace Hebron
{
	public class CursorInfo
	{
		private readonly CXCursor _cursor;
		private readonly CXCursorKind _kind;
		private readonly CXType _type;
		private readonly string _spelling;
		private readonly RecordType _recordType;
		private readonly string _recordName;

		public CXCursor Cursor
		{
			get { return _cursor; }
		}

		public CXCursorKind Kind
		{
			get { return _kind; }
		}

		public CXType Type
		{
			get { return _type; }
		}

		public string Spelling
		{
			get { return _spelling; }
		}

		public string CsType { get; set; }

		public RecordType RecordType
		{
			get { return _recordType; }
		}

		public string RecordName
		{
			get { return _recordName; }
		}

		public bool IsPointer
		{
			get { return _type.IsPointer(); }
		}

		public bool IsArray
		{
			get { return _type.IsArray(); }
		}

		public bool IsPrimitiveNumericType
		{
			get { return _type.kind.IsPrimitiveNumericType(); }
		}

		public CursorInfo(CXCursor cursor)
		{
			_cursor = cursor;
			_kind = clang.getCursorKind(cursor);
			_type = clang.getCursorType(cursor).Desugar();
			_spelling = clang.getCursorSpelling(cursor).ToString();
			CsType = _type.ToCSharpTypeString();
			
			_type.ResolveRecord(out _recordType, out _recordName);
		}
	}
}