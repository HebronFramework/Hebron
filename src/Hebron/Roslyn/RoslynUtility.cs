using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Hebron.Roslyn
{
	internal static class RoslynUtility
	{
		public static EnumDeclarationSyntax MakePublic(this EnumDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.PublicKeyword));

		public static FieldDeclarationSyntax MakePublic(this FieldDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.PublicKeyword));

		public static MethodDeclarationSyntax MakePublic(this MethodDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.PublicKeyword));

		public static DelegateDeclarationSyntax MakePublic(this DelegateDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.PublicKeyword));

		public static TypeDeclarationSyntax MakePublic(this TypeDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.PublicKeyword));

		public static ConstructorDeclarationSyntax MakePublic(this ConstructorDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.PublicKeyword));

		public static MethodDeclarationSyntax MakeStatic(this MethodDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.StaticKeyword));

		public static FieldDeclarationSyntax MakeConst(this FieldDeclarationSyntax decl) => decl.AddModifiers(Token(SyntaxKind.ConstKeyword));


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

		public static string ApplyCast(this string expr, string type)
		{
			if (string.IsNullOrEmpty(expr))
			{
				return expr;
			}

			var lastCast = string.Empty;
			var dexpr = expr.Deparentize();

			var m = Regex.Match(dexpr, @"^\((\w+)\)(\(.+\))$");
			if (m.Success)
			{
				lastCast = m.Groups[1].Value;
				var val = m.Groups[2].Value;

				if (!val.CorrectlyParentized())
				{
					lastCast = string.Empty;
				}
			}

			if (!string.IsNullOrEmpty(lastCast) && string.CompareOrdinal(lastCast, type) == 0)
			{
				return expr;
			}

			return type.Parentize() + expr.Parentize();
		}

		internal static string GetExpression(this CursorProcessResult cursorProcessResult)
		{
			return cursorProcessResult != null ? cursorProcessResult.Expression : string.Empty;
		}

		public static string UpdateNativeCall(this string functionName)
		{
			if (functionName.IsNativeFunctionName())
			{
				return "CRuntime." + functionName;
			}

			return functionName;
		}
	}
}