using ClangSharp;
using ClangSharp.Interop;
using Hebron;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Type = ClangSharp.Type;

namespace Roslyn
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

		public static string ToRoslynString(this TypeInfo type)
		{
			var sb = new StringBuilder();

			if (type.PrimitiveType != null)
			{
				switch (type.PrimitiveType.Value)
				{
					case PrimitiveType.Boolean:
						sb.Append("bool");
						break;
					case PrimitiveType.Byte:
						sb.Append("byte");
						break;
					case PrimitiveType.Sbyte:
						sb.Append("sbyte");
						break;
					case PrimitiveType.UShort:
						sb.Append("ushort");
						break;
					case PrimitiveType.Short:
						sb.Append("short");
						break;
					case PrimitiveType.Float:
						sb.Append("float");
						break;
					case PrimitiveType.Double:
						sb.Append("double");
						break;
					case PrimitiveType.Int:
						sb.Append("int");
						break;
					case PrimitiveType.Uint:
						sb.Append("uint");
						break;
					case PrimitiveType.Long:
						sb.Append("long");
						break;
					case PrimitiveType.ULong:
						sb.Append("ulong");
						break;
					case PrimitiveType.Void:
						sb.Append("void");
						break;
				}
			}
			else
			{
				sb.Append(type.StructName);
			}

			for (var i = 0; i < type.PointerCount; ++i)
			{
				sb.Append("*");
			}

			return sb.ToString();
		}

		/*		public static bool IsStruct(this Type type)
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
				}*/
	}
}