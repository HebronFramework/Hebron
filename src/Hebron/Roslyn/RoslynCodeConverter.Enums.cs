using ClangSharp;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Hebron.Roslyn
{
	public static partial class RoslynCodeConverter
	{
		private static void ConvertEnums(this TranslationUnit translationUnit, string[] skipEnums, 
			out Dictionary<string, EnumDeclarationSyntax> namedEnums,
			out Dictionary<string, AssignmentExpressionSyntax> unnamedEnumValues)
		{
			if (skipEnums == null)
			{
				skipEnums = new string[0];
			}

			namedEnums = new Dictionary<string, EnumDeclarationSyntax>();
			unnamedEnumValues = new Dictionary<string, AssignmentExpressionSyntax>();
			foreach (var cursor in translationUnit.EnumerateCursors())
			{
				if (cursor.CursorKind != ClangSharp.Interop.CXCursorKind.CXCursor_EnumDecl)
				{
					continue;
				}

				if (string.IsNullOrEmpty(cursor.Spelling))
				{
					Logger.Info("Processing unnamed enum");

					int value = 0;
					foreach(var child in cursor.CursorChildren)
					{
						if (child.CursorChildren.Count > 0)
						{
							value = int.Parse(child.CursorChildren[0].GetLiteralString());
						}

						var expr = AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName(child.Spelling), IdentifierName(value.ToString()));
						unnamedEnumValues[child.Spelling] = expr;

						++value;
					}
				}
				else
				{
					Logger.Info("Processing enum {0}", cursor.Spelling);

					var expr = EnumDeclaration(cursor.Spelling).AddModifiers(Token(SyntaxKind.PublicKeyword));

					foreach (var child in cursor.CursorChildren)
					{
						EnumMemberDeclarationSyntax enumMemberDeclaration = EnumMemberDeclaration(child.Spelling);
						if (child.CursorChildren.Count > 0)
						{
							var value = int.Parse(child.CursorChildren[0].GetLiteralString());
							enumMemberDeclaration = enumMemberDeclaration.WithEqualsValue(EqualsValueClause(IdentifierName(value.ToString())));
						}

						expr = expr.AddMembers(enumMemberDeclaration);

					}

					namedEnums[cursor.Spelling] = expr;
				}

				if (skipEnums.Contains(cursor.Spelling))
				{
					Logger.Info("Skipping");
					continue;
				}


			}
		}
	}
}
