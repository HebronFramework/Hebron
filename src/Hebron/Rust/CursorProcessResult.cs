using ClangSharp;
using System;

namespace Hebron.Rust
{
	public class CursorProcessResult
	{
		private readonly Cursor _info;
		private readonly TypeInfo _typeInfo;
		private readonly string _rustType;

		public Cursor Info => _info;

		public TypeInfo TypeInfo => _typeInfo;

		public string Expression { get; set; }

		public bool IsPointer => _typeInfo.IsPointer;

		public bool IsArray => _typeInfo.IsArray;

		public string RustType => _rustType;

		public CursorProcessResult(RustCodeConverter codeConverter, Cursor cursor)
		{
			_info = cursor ?? throw new ArgumentNullException(nameof(cursor));
			_typeInfo = _info.Handle.Type.ToTypeInfo();
			_rustType = codeConverter.ToRustString(_typeInfo);
		}
	}
}
