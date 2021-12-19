using ClangSharp;
using System;

namespace Hebron.Roslyn
{
	public class CursorProcessResult
	{
		private readonly Cursor _info;
		private readonly BaseTypeInfo _typeInfo;
		private readonly string _csType;
		private bool _isClass;

		public Cursor Info => _info;

		public BaseTypeInfo TypeInfo => _typeInfo;

		public string Expression { get; set; }

		public bool IsPointer => _typeInfo.IsPointer;

		public bool IsArray => _typeInfo.IsArray;

		public string CsType => _csType;

		public bool IsClass => _isClass;

		public CursorProcessResult(RoslynCodeConverter roslynCodeConverter, Cursor cursor)
		{
			if (cursor == null)
			{
				throw new ArgumentNullException("info");
			}

			_info = cursor;
			_typeInfo = _info.Handle.Type.ToTypeInfo();
			_csType = roslynCodeConverter.ToRoslynString(_typeInfo);
			_isClass = roslynCodeConverter.IsClass(_typeInfo.TypeName);
		}
	}
}
