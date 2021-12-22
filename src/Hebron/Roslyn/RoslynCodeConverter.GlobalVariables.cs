using ClangSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Hebron.Roslyn
{
	partial class RoslynCodeConverter
	{
		private Stack<string> GetVariableInfos(string name)
		{
			Stack<string> infos;
			if (!_variables.TryGetValue(name, out infos))
			{
				infos = new Stack<string>();
				_variables[name] = infos;
			}

			return infos;
		}

		private void PushVariableInfo(string name, string csType)
		{
			var infos = GetVariableInfos(name);
			infos.Push(csType);
		}

		private string PopVariableInfo(string name)
		{
			var infos = GetVariableInfos(name);
			return infos.Pop();
		}

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

				if (Parameters.SkipGlobalVariables.Contains(name))
				{
					Logger.Info("Skipped.");
					continue;
				}

				try
				{
					var res = Process(cursor);

					_variables[name] = new Stack<string>();
					_variables[name].Push(res.CsType);

					var expr = ("public static " + res.Expression).EnsureStatementEndWithSemicolon();

					var stmt = (FieldDeclarationSyntax)ParseMemberDeclaration(expr);
					Result.GlobalVariables[varDecl.Spelling] = stmt;
				}
				catch(Exception)
				{
				}
			}
		}
	}
}
