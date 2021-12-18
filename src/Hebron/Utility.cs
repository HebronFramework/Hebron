using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;
using Type = ClangSharp.Type;

namespace Hebron
{
	public enum PrimitiveType
	{
		Boolean,
		Byte,
		Sbyte,
		UShort,
		Short,
		Float,
		Double,
		Int,
		Uint,
		Long,
		ULong,
		Void
	}

	public abstract class BaseTypeInfo
	{
		private string _fullTypeString;

		public int PointerCount { get; private set; }
		public int? ConstantArraySize { get; private set; }

		public string TypeString
		{
			get
			{
				if (string.IsNullOrEmpty(_fullTypeString))
				{
					var sb = new StringBuilder();
					sb.Append(BuildTypeString());

					if (PointerCount > 0)
					{
						sb.Append(" ");
					}

					for(var i = 0; i < PointerCount; ++i)
					{
						sb.Append("*");
					}

					_fullTypeString = sb.ToString();
				}

				return _fullTypeString;
			}
		}

		public BaseTypeInfo(int pointerCount, int? constantArraySize)
		{
			if (pointerCount < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(pointerCount));
			}

			if (constantArraySize != null && constantArraySize.Value < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(constantArraySize));
			}

			PointerCount = pointerCount;
			ConstantArraySize = constantArraySize;
		}

		public abstract string BuildTypeString();

		public override string ToString() => TypeString;
	}

	public class PrimitiveTypeInfo : BaseTypeInfo
	{
		public PrimitiveType PrimitiveType { get; private set; }

		public PrimitiveTypeInfo(PrimitiveType primiveType, int pointerCount, int? constantArraySize): base(pointerCount, constantArraySize)
		{
			PrimitiveType = primiveType;
		}

		public override string BuildTypeString() => PrimitiveType.ToString();
	}

	public class StructTypeInfo : BaseTypeInfo
	{
		public string StructName { get; private set; }

		public StructTypeInfo(string structName, int pointerCount, int? constantArraySize) : base(pointerCount, constantArraySize)
		{
			StructName = structName;
		}

		public override string BuildTypeString() => StructName;
	}

	public class FunctionPointerTypeInfo : BaseTypeInfo
	{
		public BaseTypeInfo ReturnType { get; private set; }
		public BaseTypeInfo[] Arguments { get; private set; }

		public FunctionPointerTypeInfo(BaseTypeInfo returnType, BaseTypeInfo[] arguments,
			int pointerCount, int? constantArraySize) : base(pointerCount, constantArraySize)
		{
			ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
			Arguments = arguments;
		}

		public override string BuildTypeString()
		{
			var sb = new StringBuilder();

			sb.Append(ReturnType.TypeString);
			sb.Append("(");

			if (Arguments != null)
			{
				for(var i = 0; i < Arguments.Length; ++i)
				{
					sb.Append(Arguments[0]);

					if (i < Arguments.Length - 1)
					{
						sb.Append(", ");
					}
				}
			}

			sb.Append(")");

			return sb.ToString();
		}
	}

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

		public static bool IsArray(this Type type)
		{
			return type.Kind == CXTypeKind.CXType_ConstantArray ||
				   type.Kind == CXTypeKind.CXType_DependentSizedArray ||
				   type.Kind == CXTypeKind.CXType_VariableArray;
		}

		private static PrimitiveType? ToPrimitiveType(this CXTypeKind kind)
		{
			switch (kind)
			{
				case CXTypeKind.CXType_Bool:
					return PrimitiveType.Boolean;
				case CXTypeKind.CXType_UChar:
				case CXTypeKind.CXType_Char_U:
					return PrimitiveType.Byte;
				case CXTypeKind.CXType_SChar:
				case CXTypeKind.CXType_Char_S:
					return PrimitiveType.Sbyte;
				case CXTypeKind.CXType_UShort:
					return PrimitiveType.UShort;
				case CXTypeKind.CXType_Short:
					return PrimitiveType.Short;
				case CXTypeKind.CXType_Float:
					return PrimitiveType.Float;
				case CXTypeKind.CXType_Double:
					return PrimitiveType.Double;
				case CXTypeKind.CXType_Long:
				case CXTypeKind.CXType_Int:
					return PrimitiveType.Int;
				case CXTypeKind.CXType_ULong:
				case CXTypeKind.CXType_UInt:
					return PrimitiveType.Uint;
				case CXTypeKind.CXType_LongLong:
					return PrimitiveType.Long;
				case CXTypeKind.CXType_ULongLong:
					return PrimitiveType.ULong;
				case CXTypeKind.CXType_Void:
					return PrimitiveType.Void;
			}

			return null;
		}

		public static BaseTypeInfo ToTypeInfo(this CXType type)
		{
			var run = true;
			int typeEnum = 0;
			int pointerCount = 0;
			int? constantArraySize = null;

			while (run)
			{
				type = type.CanonicalType;

				var primitiveType = type.kind.ToPrimitiveType();
				if (primitiveType != null)
				{
					break;
				}

				switch (type.kind)
				{
					case CXTypeKind.CXType_Record:
						{
							typeEnum = 1;
							run = false;
							break;
						}

					case CXTypeKind.CXType_IncompleteArray:
					case CXTypeKind.CXType_ConstantArray:
						constantArraySize = (int)type.ArraySize;
						type = clang.getArrayElementType(type);
						++pointerCount;
						continue;
					case CXTypeKind.CXType_Pointer:
						type = clang.getPointeeType(type);
						++pointerCount;
						continue;
					case CXTypeKind.CXType_FunctionProto:
						typeEnum = 2;
						run = false;
						break;
					default:
						typeEnum = 1;
						run = false;
						break;
				}
			}

			BaseTypeInfo result = null;

			switch (typeEnum)
			{
				case 0:
					result = new PrimitiveTypeInfo(type.kind.ToPrimitiveType().Value, pointerCount, constantArraySize);
					break;
				case 1:
					{
						var name = clang.getTypeSpelling(type).ToString();
						var isConstQualifiedType = clang.isConstQualifiedType(type) != 0;
						if (isConstQualifiedType)
						{
							name = name.Replace("const ", string.Empty);
						}

						name = name.Replace("struct ", string.Empty);
						result = new StructTypeInfo(name, pointerCount, constantArraySize);
					}
					break;
				case 2:
					var args = new List<BaseTypeInfo>();
					for(var i = 0; i < type.NumArgTypes; ++i)
					{
						var arg = type.GetArgType((uint)i);
						args.Add(arg.ToTypeInfo());
					}

					result = new FunctionPointerTypeInfo(type.ResultType.ToTypeInfo(), args.ToArray(), pointerCount, constantArraySize);
					break;
			}

			return result;
		}

		public static BaseTypeInfo ToTypeInfo(this Type type) => type.Handle.ToTypeInfo();
	}
}