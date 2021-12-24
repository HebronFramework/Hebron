using ClangSharp;
using ClangSharp.Interop;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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

			if (string.IsNullOrEmpty(trimmed))
			{
				return ";";
			}

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
		};

		public static string UpdateNativeCall(this string functionName)
		{
			if (NativeFunctions.Contains(functionName))
			{
				return "CRuntime." + functionName;
			}

			return functionName;
		}
	}
}