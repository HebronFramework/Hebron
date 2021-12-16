using ClangSharp;
using ClangSharp.Interop;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

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

		public static string ToRoslynTypeName(this TypeInfo type)
		{
			if (type.PrimitiveType != null)
			{
				switch (type.PrimitiveType.Value)
				{
					case PrimitiveType.Boolean:
						return "bool";
					case PrimitiveType.Byte:
						return "byte";
					case PrimitiveType.Sbyte:
						return "sbyte";
					case PrimitiveType.UShort:
						return "ushort";
					case PrimitiveType.Short:
						return "short";
					case PrimitiveType.Float:
						return "float";
					case PrimitiveType.Double:
						return "double";
					case PrimitiveType.Int:
						return "int";
					case PrimitiveType.Uint:
						return "uint";
					case PrimitiveType.Long:
						return "long";
					case PrimitiveType.ULong:
						return "ulong";
					case PrimitiveType.Void:
						return "void";
				}
			}

			return type.StructName;
		}

		public static string ToRoslynTypeName(this CXType type) => type.ToTypeInfo().ToRoslynTypeName();
		public static string ToRoslynTypeName(this Type type) => type.Handle.ToRoslynTypeName();

		public static string ToRoslynString(this TypeInfo type)
		{
			var sb = new StringBuilder();
			sb.Append(type.ToRoslynTypeName());

			for (var i = 0; i < type.PointerCount; ++i)
			{
				sb.Append("*");
			}

			return sb.ToString();
		}

		public static string ToRoslynString(this CXType type) => type.ToTypeInfo().ToRoslynString();
		public static string ToRoslynString(this Type type) => type.Handle.ToRoslynString();

		public static VariableDeclarationSyntax VariableDeclaration(this Type type, string name)
		{
			return SyntaxFactory.VariableDeclaration(ParseTypeName(type.ToRoslynString())).AddVariables(VariableDeclarator(name));
		}
	}
}