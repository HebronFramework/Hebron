using ClangSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Hebron.Roslyn
{
	partial class RoslynCodeConverter
	{
		public void ConvertGlobalVariables()
		{
			_state = State.GlobalVariables;
			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				var varDecl = cursor as VarDecl;
				if (varDecl == null)
				{
					continue;
				}

				var name = cursor.Spelling.FixSpecialWords();

				Logger.Info("Processing global variable {0}", name);

				if (name == "stbi__bmask")
				{
					var k = 5;
				}

				var res = Process(cursor);
				var expr = ("public static " + res.Expression).EnsureStatementEndWithSemicolon();

				var stmt = (FieldDeclarationSyntax)ParseMemberDeclaration(expr);
				Result.GlobalVariables[varDecl.Spelling] = stmt;
			}
		}
	}
}
