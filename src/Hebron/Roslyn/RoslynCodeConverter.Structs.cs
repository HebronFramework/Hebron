using ClangSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Hebron.Roslyn
{
	partial class RoslynCodeConverter
	{
		public void ConvertStructs()
		{
			Logger.Info("Processing structs...");

			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				if (cursor.CursorKind != ClangSharp.Interop.CXCursorKind.CXCursor_StructDecl)
				{
					continue;
				}

				var name = cursor.Spelling;
				Logger.Info("Processing struct {0}", name);

				TypeDeclarationSyntax typeDecl = StructDeclaration(name);
				foreach(FieldDecl child in cursor.CursorChildren)
				{
					var variableDecl = child.Type.VariableDeclaration(child.Name);
					typeDecl = typeDecl.AddMembers(FieldDeclaration(variableDecl));
				}

				Result.Structs[name] = typeDecl;
			}
		}
	}
}
