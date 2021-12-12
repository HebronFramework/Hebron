using ClangSharp;
using ClangSharp.Interop;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Type = ClangSharp.Type;

namespace Hebron.Roslyn
{
	internal static class RoslynUtility
	{
		public enum RecordType
		{
			None,
			Struct,
			Class
		}

		public static EnumDeclarationSyntax MakePublic(this EnumDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.PublicKeyword));

		public static FieldDeclarationSyntax MakePublic(this FieldDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.PublicKeyword));

		public static MethodDeclarationSyntax MakePublic(this MethodDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.PublicKeyword));

		public static MethodDeclarationSyntax MakeStatic(this MethodDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.StaticKeyword));

		public static FieldDeclarationSyntax MakeConst(this FieldDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.ConstKeyword));

		private static string ToRoslynTypeInternal(this Type type)
		{
			if (type.IsPointerType)
			{
				return type.PointeeType.ToRoslynTypeInternal() + "*";
			}

			switch (type.CanonicalType.Kind)
			{
				case CXTypeKind.CXType_Bool:
					return "bool";
				case CXTypeKind.CXType_UChar:
				case CXTypeKind.CXType_Char_U:
					return "byte";
				case CXTypeKind.CXType_SChar:
				case CXTypeKind.CXType_Char_S:
					return "sbyte";
				case CXTypeKind.CXType_UShort:
					return "ushort";
				case CXTypeKind.CXType_Short:
					return "short";
				case CXTypeKind.CXType_Float:
					return "float";
				case CXTypeKind.CXType_Double:
					return "double";
				case CXTypeKind.CXType_Int:
					return "int";
				case CXTypeKind.CXType_UInt:
					return "uint";
				case CXTypeKind.CXType_Pointer:
				case CXTypeKind.CXType_NullPtr: // ugh, what else can I do?
					return "IntPtr";
				case CXTypeKind.CXType_Long:
					return "int";
				case CXTypeKind.CXType_ULong:
					return "int";
				case CXTypeKind.CXType_LongLong:
					return "long";
				case CXTypeKind.CXType_ULongLong:
					return "ulong";
				case CXTypeKind.CXType_Void:
					return "void";
				case CXTypeKind.CXType_Unexposed:
					if (type.CanonicalType.Kind == CXTypeKind.CXType_Unexposed)
					{
						return type.CanonicalType.KindSpelling;
					}

					return type.CanonicalType.ToRoslynTypeInternal();
				default:
					return type.ToString();
			}
		}

		public static string ToRoslynType(this Type type)
		{
			return type.ToRoslynTypeInternal().Replace("const ", string.Empty);
		}

		public unsafe static string[] Tokenize(this Cursor cursor, TranslationUnit translationUnit)
		{
			CXToken *tokens = null;
			uint numTokens;
			clang.tokenize(translationUnit.Handle, cursor.Extent, &tokens, &numTokens);

			var result = new List<string>();
			for (uint i = 0; i < numTokens; ++i)
			{
				var name = clang.getTokenSpelling(translationUnit.Handle, tokens[i]).ToString();
				result.Add(name);
			}

			return result.ToArray();
		}

		private static readonly HashSet<string> _specialWords = new HashSet<string>(new[]
		{
			"out", "in", "base", "null", "string"
		});

		public static string FixSpecialWords(this string name)
		{
			if (_specialWords.Contains(name))
			{
				name = "_" + name + "_";
			}

			return name;
		}

		public static bool IsStruct(this Type type)
		{
			bool isStruct;
			string name;
			type.Handle.ResolveRecord(out isStruct, out name);

			return isStruct;
		}

		public static bool IsClass(this Type type, string[] classes)
		{
			bool isStruct;
			string name;
			type.Handle.ResolveRecord(out isStruct, out name);

			if (!isStruct)
			{
				return false;
			}

			return classes != null && classes.Contains(name);
		}
	}
}