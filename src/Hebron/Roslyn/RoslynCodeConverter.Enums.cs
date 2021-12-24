using ClangSharp;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Hebron.Roslyn
{
	partial class RoslynCodeConverter
	{
		public void ConvertEnums()
		{
			if (!Parameters.ConversionEntities.HasFlag(ConversionEntities.Enums))
			{
				return;
			}

			Logger.Info("Processing enums...");

			_state = State.Enums;
			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				if (cursor.CursorKind != ClangSharp.Interop.CXCursorKind.CXCursor_EnumDecl)
				{
					continue;
				}

				if (string.IsNullOrEmpty(cursor.Spelling))
				{
					Logger.Info("Processing unnamed enum");

					int value = 0;
					foreach (var child in cursor.CursorChildren)
					{
						if (child.CursorChildren.Count > 0)
						{
							value = int.Parse(child.CursorChildren[0].GetLiteralString());
						}

						var assignmentExpr = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));

						var expr = FieldDeclaration(VariableDeclaration(IdentifierName("int"),
							SeparatedList(new[] {
								VariableDeclarator(Identifier(child.Spelling),
									null,
									EqualsValueClause(assignmentExpr))
							})))
							.MakePublic()
							.MakeConst();

						Result.UnnamedEnumValues[child.Spelling] = expr;

						++value;
					}
				}
				else
				{
					Logger.Info("Processing enum {0}", cursor.Spelling);

					var expr = EnumDeclaration(cursor.Spelling).MakePublic();

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

					Result.NamedEnums[cursor.Spelling] = expr;
				}

				if (Parameters.SkipEnums.Contains(cursor.Spelling))
				{
					Logger.Info("Skipping");
					continue;
				}
			}
		}
	}
}
