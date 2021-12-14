using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn;
using System.Collections.Generic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Hebron.Roslyn
{
	public class RoslynOutput : IOutput
	{
		public RoslynConversionResult Result { get; } = new RoslynConversionResult();

		public void Enum(string name, Dictionary<string, int?> values)
		{
			var expr = EnumDeclaration(name).MakePublic();

			foreach (var pair in values)
			{
				EnumMemberDeclarationSyntax enumMemberDeclaration = EnumMemberDeclaration(pair.Key);
				if (pair.Value != null)
				{
					enumMemberDeclaration = enumMemberDeclaration.WithEqualsValue(EqualsValueClause(IdentifierName(pair.Value.Value.ToString())));
				}

				expr = expr.AddMembers(enumMemberDeclaration);

			}

			Result.NamedEnums[name] = expr;
		}

		public void IntegerConstant(string name, int value)
		{
			var assignmentExpr = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));

			var expr = FieldDeclaration(VariableDeclaration(IdentifierName("int"),
				SeparatedList(new[] {
								VariableDeclarator(Identifier(name),
									null,
									EqualsValueClause(assignmentExpr))
				})))
				.MakePublic()
				.MakeConst();

			Result.UnnamedEnumValues[name] = expr;
		}

		public void Function(string name, TypeInfo returnType, Dictionary<string, TypeInfo> parameters)
		{
			name = name.FixSpecialWords();
			var md = MethodDeclaration(ParseTypeName(returnType.ToRoslynString()), name)
				.MakePublic()
				.MakeStatic();

			foreach (var p in parameters)
			{
				var pname = p.Key.FixSpecialWords();
				md = md.AddParameterListParameters(Parameter(Identifier(pname)).WithType(ParseTypeName(p.Value.ToRoslynString())));
			}

/*			foreach (var child in funcDecl.Body.Children)
			{
				_functionStatements.Clear();
				Process(child);

				if (_functionStatements.Count > 0)
				{
					md = md.AddBodyStatements(_functionStatements.ToArray());
				}
			}*/

			md = md.AddBodyStatements();

			Result.Functions[name] = md;
		}
	}
}
