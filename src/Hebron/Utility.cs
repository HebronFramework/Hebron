using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;
using Type = ClangSharp.Type;

namespace Hebron
{
	public enum TypeEnum
	{
		Primitive,
		Struct,
		FunctionPointer
	}

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

	public class TypeInfo
	{
		public TypeEnum Type;
		public PrimitiveType? PrimitiveType;
		public string StructName;
		public string FunctionPointerName;
		public int PointerCount;

		public bool IsStruct => !string.IsNullOrEmpty(StructName);
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

		private static PrimitiveType ToPrimitiveType(this CXType type)
		{
			switch (type.kind)
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

			throw new Exception(string.Format("Could not convert {0] to primitive type", type.ToString()));
		}

		public static TypeInfo ToTypeInfo(this CXType type)
		{
			var result = new TypeInfo();
			var run = true;

			var typeEnum = TypeEnum.Primitive;
			while (run)
			{
				type = type.CanonicalType;

				switch (type.kind)
				{
					case CXTypeKind.CXType_Record:
						{
							typeEnum = TypeEnum.Struct;
							run = false;
							break;
						}

					case CXTypeKind.CXType_IncompleteArray:
					case CXTypeKind.CXType_ConstantArray:
						type = clang.getArrayElementType(type);
						++result.PointerCount;
						continue;
					case CXTypeKind.CXType_Pointer:
						type = clang.getPointeeType(type);
						++result.PointerCount;
						continue;
					case CXTypeKind.CXType_FunctionProto:
						typeEnum = TypeEnum.FunctionPointer;
						run = false;
						break;
					default:
						typeEnum = TypeEnum.Struct;
						run = false;
						break;
				}
			}

			switch (typeEnum)
			{
				case TypeEnum.Primitive:
					result.Type = TypeEnum.Primitive;
					result.PrimitiveType = type.ToPrimitiveType();
					break;
				case TypeEnum.Struct:
					{
						var name = clang.getTypeSpelling(type).ToString();
						var isConstQualifiedType = clang.isConstQualifiedType(type) != 0;
						if (isConstQualifiedType)
						{
							name = name.Replace("const ", string.Empty);
						}

						name = name.Replace("struct ", string.Empty);

						result.Type = TypeEnum.Struct;
						result.StructName = name;
					}
					break;
				case TypeEnum.FunctionPointer:
					result.Type = TypeEnum.FunctionPointer;
					result.FunctionPointerName = clang.getTypeSpelling(type).ToString();
					break;
			}

			return result;
		}

		public static TypeInfo ToTypeInfo(this Type type) => type.Handle.ToTypeInfo();
	}
}