using ClangSharp;
using ClangSharp.Interop;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
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

		public static DelegateDeclarationSyntax MakePublic(this DelegateDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.PublicKeyword));

		public static TypeDeclarationSyntax MakePublic(this TypeDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.PublicKeyword));

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
	}
}