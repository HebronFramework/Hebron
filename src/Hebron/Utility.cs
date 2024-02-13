using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Index = ClangSharp.Index;
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

	public abstract class BaseTypeDescriptor
	{
		private string _typeName;

		public string TypeName
		{
			get
			{
				if (string.IsNullOrEmpty(_typeName))
				{
					_typeName = BuildTypeString();
				}

				return _typeName;
			}
		}

		protected BaseTypeDescriptor()
		{
		}

		public abstract string BuildTypeString();

		public override string ToString() => TypeName;
	}

	public class PrimitiveTypeInfo : BaseTypeDescriptor
	{
		public PrimitiveType PrimitiveType { get; private set; }

		public PrimitiveTypeInfo(PrimitiveType primiveType)
		{
			PrimitiveType = primiveType;
		}

		public override string BuildTypeString() => PrimitiveType.ToString();
	}

	public class StructTypeInfo : BaseTypeDescriptor
	{
		public string StructName { get; private set; }

		public StructTypeInfo(string structName)
		{
			StructName = structName;
		}

		public override string BuildTypeString() => StructName;
	}

	public class FunctionPointerTypeInfo : BaseTypeDescriptor
	{
		public TypeInfo ReturnType { get; private set; }
		public TypeInfo[] Arguments { get; private set; }

		public FunctionPointerTypeInfo(TypeInfo returnType, TypeInfo[] arguments)
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
				for (var i = 0; i < Arguments.Length; ++i)
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

	public class TypeInfo
	{
		private string _typeString;

		public BaseTypeDescriptor TypeDescriptor { get; private set; }

		public int PointerCount { get; private set; }

		public int[] ConstantArraySizes { get; private set; }

		public bool IsArray => ConstantArraySizes != null && ConstantArraySizes.Length > 0;
		public bool IsPointer => PointerCount > 0;

		public string TypeName => TypeDescriptor.TypeName;

		public string TypeString
		{
			get
			{
				if (string.IsNullOrEmpty(_typeString))
				{
					var sb = new StringBuilder();
					sb.Append(TypeDescriptor.TypeName);

					if (PointerCount > 0)
					{
						sb.Append(" ");
					}

					for (var i = 0; i < PointerCount; ++i)
					{
						sb.Append("*");
					}

					_typeString = sb.ToString();
				}

				return _typeString;
			}
		}

		public TypeInfo(BaseTypeDescriptor typeDescriptor, int pointerCount, int[] constantArraySizes)
		{
			if (pointerCount < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(pointerCount));
			}

			TypeDescriptor = typeDescriptor ?? throw new ArgumentNullException(nameof(typeDescriptor));
			ConstantArraySizes = constantArraySizes;
			PointerCount = pointerCount;
		}

		public override string ToString() => TypeString;
	}

	internal static class Utility
	{
		public static unsafe TranslationUnit Compile(string inputPath, string[] defines, string[] additionalIncludeFolders)
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

			foreach (var i in additionalIncludeFolders)
			{
				var d = "-I\"" + i + "\"";
				arr.Add(d);
			}

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
			switch (cursor.Kind)
			{
				case CXCursorKind.CXCursor_IntegerLiteral:
					return cursor.GetTokenLiteral();
				case CXCursorKind.CXCursor_FloatingLiteral:
					return cursor.GetTokenLiteral();
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

		public static string GetOperatorString(this CXCursor cursor)
		{
			if (cursor.kind == CXCursorKind.CXCursor_BinaryOperator ||
				cursor.kind == CXCursorKind.CXCursor_CompoundAssignOperator)
			{
				return cursor.BinaryOperatorKindSpelling.CString;
			}

			if (cursor.kind == CXCursorKind.CXCursor_UnaryOperator)
			{
				return cursor.UnaryOperatorKindSpelling.CString;
			}

			return string.Empty;
		}

		public static string GetOperatorString(this Cursor cursor) => GetOperatorString(cursor.Handle);

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

		public static TypeInfo ToTypeInfo(this CXType type)
		{
			var run = true;
			int typeEnum = 0;
			int pointerCount = 0;
			var constantArraySizes = new List<int>();

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
						constantArraySizes.Add((int)type.ArraySize);
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

			TypeInfo result = null;

			switch (typeEnum)
			{
				case 0:
					result = new TypeInfo(new PrimitiveTypeInfo(type.kind.ToPrimitiveType().Value), pointerCount, constantArraySizes.ToArray());
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
						result = new TypeInfo(new StructTypeInfo(name), pointerCount, constantArraySizes.ToArray());
					}
					break;
				case 2:
					var args = new List<TypeInfo>();
					for (var i = 0; i < type.NumArgTypes; ++i)
					{
						var arg = type.GetArgType((uint)i);
						args.Add(arg.ToTypeInfo());
					}

					result = new TypeInfo(new FunctionPointerTypeInfo(type.ResultType.ToTypeInfo(), args.ToArray()), pointerCount, constantArraySizes.ToArray());
					break;
			}

			return result;
		}

		public static TypeInfo ToTypeInfo(this Type type) => type.Handle.ToTypeInfo();
		public static TypeInfo ToTypeInfo(this CXCursor cursor) => cursor.Type.ToTypeInfo();
		public static TypeInfo ToTypeInfo(this Cursor cursor) => cursor.Handle.ToTypeInfo();

		public static bool IsLogicalBooleanOperator(this CXBinaryOperatorKind op)
		{
			return op == CXBinaryOperatorKind.CXBinaryOperator_LAnd || op == CXBinaryOperatorKind.CXBinaryOperator_LOr ||
				op == CXBinaryOperatorKind.CXBinaryOperator_EQ || op == CXBinaryOperatorKind.CXBinaryOperator_GE ||
				op == CXBinaryOperatorKind.CXBinaryOperator_GE || op == CXBinaryOperatorKind.CXBinaryOperator_LE ||
				op == CXBinaryOperatorKind.CXBinaryOperator_GT || op == CXBinaryOperatorKind.CXBinaryOperator_LT;
		}

		public static bool IsLogicalBinaryOperator(this CXBinaryOperatorKind op)
		{
			return op == CXBinaryOperatorKind.CXBinaryOperator_LAnd || op == CXBinaryOperatorKind.CXBinaryOperator_LOr;
		}

		public static bool IsBinaryOperator(this CXBinaryOperatorKind op)
		{
			return op == CXBinaryOperatorKind.CXBinaryOperator_And || op == CXBinaryOperatorKind.CXBinaryOperator_Or;
		}

		public static bool IsAssign(this CXBinaryOperatorKind op)
		{
			return op == CXBinaryOperatorKind.CXBinaryOperator_AddAssign || op == CXBinaryOperatorKind.CXBinaryOperator_AndAssign ||
				   op == CXBinaryOperatorKind.CXBinaryOperator_Assign || op == CXBinaryOperatorKind.CXBinaryOperator_DivAssign ||
				   op == CXBinaryOperatorKind.CXBinaryOperator_MulAssign || op == CXBinaryOperatorKind.CXBinaryOperator_OrAssign ||
				   op == CXBinaryOperatorKind.CXBinaryOperator_RemAssign || op == CXBinaryOperatorKind.CXBinaryOperator_ShlAssign ||
				   op == CXBinaryOperatorKind.CXBinaryOperator_ShrAssign || op == CXBinaryOperatorKind.CXBinaryOperator_SubAssign ||
				   op == CXBinaryOperatorKind.CXBinaryOperator_XorAssign;
		}

		public static bool IsBooleanOperator(this CXBinaryOperatorKind op)
		{
			return op == CXBinaryOperatorKind.CXBinaryOperator_LAnd || op == CXBinaryOperatorKind.CXBinaryOperator_LOr ||
				   op == CXBinaryOperatorKind.CXBinaryOperator_EQ || op == CXBinaryOperatorKind.CXBinaryOperator_NE ||
				   op == CXBinaryOperatorKind.CXBinaryOperator_GE || op == CXBinaryOperatorKind.CXBinaryOperator_LE ||
				   op == CXBinaryOperatorKind.CXBinaryOperator_GT || op == CXBinaryOperatorKind.CXBinaryOperator_LT ||
				   op == CXBinaryOperatorKind.CXBinaryOperator_And || op == CXBinaryOperatorKind.CXBinaryOperator_Or;
		}

		public static bool IsUnaryOperatorPre(this CXUnaryOperatorKind type)
		{
			switch (type)
			{
				case CXUnaryOperatorKind.CXUnaryOperator_PreInc:
				case CXUnaryOperatorKind.CXUnaryOperator_PreDec:
				case CXUnaryOperatorKind.CXUnaryOperator_Plus:
				case CXUnaryOperatorKind.CXUnaryOperator_Minus:
				case CXUnaryOperatorKind.CXUnaryOperator_Not:
				case CXUnaryOperatorKind.CXUnaryOperator_LNot:
				case CXUnaryOperatorKind.CXUnaryOperator_AddrOf:
				case CXUnaryOperatorKind.CXUnaryOperator_Deref:
					return true;
			}

			return false;
		}

		public static bool IsPrimitiveNumericType(this TypeInfo typeInfo)
		{
			var asPrimitive = typeInfo.TypeDescriptor as PrimitiveTypeInfo;
			return asPrimitive != null && asPrimitive.PrimitiveType != PrimitiveType.Void;
		}

		public static string GetTokenLiteral(this CXCursor cursor)
		{
			var tokens = cursor.TranslationUnit.Tokenize(cursor.SourceRange);

			Debug.Assert(tokens.Length == 1);
			Debug.Assert(tokens[0].Kind == CXTokenKind.CXToken_Literal);

			var spelling = tokens[0].GetSpelling(cursor.TranslationUnit).ToString();
			spelling = spelling.Trim('\\', '\r', '\n');
			return spelling;
		}

		public unsafe static string[] Tokenize(this CXCursor cursor)
		{
			var range = clang.getCursorExtent(cursor);
			CXToken *nativeTokens;
			uint numTokens;
			clang.tokenize(cursor.TranslationUnit, range, &nativeTokens, &numTokens);

			var result = new List<string>();
			for (uint i = 0; i < numTokens; ++i)
			{
				var name = clang.getTokenSpelling(cursor.TranslationUnit, nativeTokens[i]).ToString();
				result.Add(name);
			}

			return result.ToArray();
		}

		public static string[] Tokenize(this Cursor cursor) => cursor.Handle.Tokenize();

		public static string UppercaseFirstLetter(this string s)
		{
			if (string.IsNullOrEmpty(s) || char.IsUpper(s[0]))
			{
				return s;
			}

			return char.ToUpper(s[0]) + s.Substring(1);
		}

		public static string Depoint(this string s)
		{
			s = s.Trim();
			if (s.EndsWith("*"))
			{
				s = s.Substring(0, s.Length - 1);
			}

			return s;
		}

		public static string PointerToArray(this string s)
		{
			var sb = new StringBuilder();
			var arrayCount = s.Count(c => c == '*') - 1;

			sb.Append(s.Replace("*", string.Empty));
			sb.Append("[");

			if (arrayCount > 0)
			{
				sb.Append(new string(',', arrayCount));
			}
			sb.Append("]");

			return sb.ToString();
		}

		public static string GetName(this RecordDecl decl) =>
			decl.TypeForDecl.AsString.Replace("struct ", string.Empty);

		public static bool IsVoid(this TypeInfo typeInfo)
		{
			if (typeInfo.IsPointer)
			{
				return false;
			}

			var asPrimitiveType = typeInfo.TypeDescriptor as PrimitiveTypeInfo;
			if (asPrimitiveType == null)
			{
				return false;
			}

			return asPrimitiveType.PrimitiveType == PrimitiveType.Void;
		}

		public static bool CorrectlyParentized(this string expr)
		{
			if (string.IsNullOrEmpty(expr))
			{
				return false;
			}

			expr = expr.Trim();
			if (expr.StartsWith("(") && expr.EndsWith(")"))
			{
				var pcount = 1;
				for (var i = 1; i < expr.Length - 1; ++i)
				{
					var c = expr[i];

					if (c == '(')
					{
						++pcount;
					}
					else if (c == ')')
					{
						--pcount;
					}

					if (pcount == 0)
					{
						break;
					}
				}

				if (pcount > 0)
				{
					return true;
				}
			}

			return false;
		}

		public static string Parentize(this string expr)
		{
			if (expr.CorrectlyParentized())
			{
				return expr;
			}

			return "(" + expr + ")";
		}

		public static string Deparentize(this string expr)
		{
			if (string.IsNullOrEmpty(expr))
			{
				return expr;
			}

			// Remove white space
			expr = Regex.Replace(expr, @"\s+", "");

			while (expr.CorrectlyParentized())
			{
				expr = expr.Substring(1, expr.Length - 2);
			}

			return expr;
		}

		public static string EnsureStatementFinished(this string statement)
		{
			var trimmed = statement.Trim();

			if (string.IsNullOrEmpty(trimmed))
			{
				return trimmed;
			}

			if (!trimmed.EndsWith(";") && !trimmed.EndsWith("}"))
			{
				return statement + ";";
			}

			return statement;
		}

		public static string EnsureStatementEndWithSemicolon(this string statement)
		{
			var trimmed = statement.Trim();

			if (!trimmed.EndsWith(";"))
			{
				return statement + ";";
			}

			return statement;
		}

		public static string Curlize(this string expr)
		{
			expr = expr.Trim();

			if (expr.StartsWith("{") && expr.EndsWith("}"))
			{
				return expr;
			}

			return "{" + expr + "}";
		}

		public static string Decurlize(this string expr)
		{
			expr = expr.Trim();

			if (expr.StartsWith("{") && expr.EndsWith("}"))
			{
				return expr.Substring(1, expr.Length - 2).Trim();
			}

			return expr;
		}

		private static readonly HashSet<string> NativeFunctions = new HashSet<string>
		{
			"malloc",
			"free",
			"abs",
			"strcmp",
			"strtol",
			"strncmp",
			"memset",
			"realloc",
			"pow",
			"memcpy",
			"_lrotl",
			"ldexp",
			"fabs",
			"fmod",
			"strlen",
			"sqrt",
			"cos",
			"sin",
			"acos",
			"floor",
			"ceil",
			"memmove",
		};

		public static bool IsNativeFunctionName(this string name) => NativeFunctions.Contains(name);

		public static bool IsCaseStatement(Cursor parent0, Cursor parent1)
		{
			if (parent0 == null)
			{
				return false;
			}

			if (parent0.CursorKind == CXCursorKind.CXCursor_CaseStmt ||
				parent0.CursorKind == CXCursorKind.CXCursor_DefaultStmt)
			{
				// First parent is case statement
				return true;
			}

			if (parent1 == null)
			{
				return false;
			}

			if (parent0.CursorKind == CXCursorKind.CXCursor_CompoundStmt &&
				(parent1.CursorKind == CXCursorKind.CXCursor_CaseStmt ||
				parent1.CursorKind == CXCursorKind.CXCursor_DefaultStmt ||
				parent1.CursorKind == CXCursorKind.CXCursor_SwitchStmt))
			{
				return true;
			}

			return false;
		}
	}
}