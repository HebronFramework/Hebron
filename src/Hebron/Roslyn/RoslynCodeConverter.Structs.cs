using ClangSharp;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Hebron.Roslyn
{
	partial class RoslynCodeConverter
	{
		private bool CheckIsClass(string name, Dictionary<string, List<string>> dependencyTree)
		{
			if (IsClass(name))
			{
				return true;
			}

			List<string> dependencyNames;
			if (dependencyTree.TryGetValue(name, out dependencyNames))
			{
				foreach (var dependencyName in dependencyNames)
				{
					if (CheckIsClass(dependencyName, dependencyTree))
					{
						return true;
					}
				}
			}

			return false;
		}

		public void ConvertStructs()
		{
			Logger.Info("Processing structs...");

			// First run - build structs dependency tree and mark structs with function pointers as classes
			var dependencyTree = new Dictionary<string, List<string>>();
			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				if (cursor.CursorKind != ClangSharp.Interop.CXCursorKind.CXCursor_StructDecl)
				{
					continue;
				}

				var recordDecl = (RecordDecl)cursor;
				var name = recordDecl.TypeForDecl.AsString;
				foreach (FieldDecl child in cursor.CursorChildren)
				{
					var asStruct = child.Type.ToTypeInfo() as StructTypeInfo;
					if (asStruct != null)
					{
						List<string> names;
						if (!dependencyTree.TryGetValue(name, out names))
						{
							names = new List<string>();
							dependencyTree[name] = names;
						}

						names.Add(asStruct.StructName);
					}

					if (asStruct != null && asStruct.ConstantArraySize != null)
					{
						Logger.Info("Marking struct {0} as class since it contains array of type {1}", name, asStruct.StructName);
						Classes.Add(name);
						break;
					}

					var asFunctionPointer = child.Type.ToTypeInfo() as FunctionPointerTypeInfo;
					if (asFunctionPointer != null)
					{
						Logger.Info("Marking struct {0} as class since it contains function pointers", name);
						Classes.Add(name);
						break;
					}
				}
			}

			// Second run - use dependency tree to determine which structs reference classes and mark it as classes too
			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				if (cursor.CursorKind != ClangSharp.Interop.CXCursorKind.CXCursor_StructDecl)
				{
					continue;
				}

				var recordDecl = (RecordDecl)cursor;
				var name = recordDecl.TypeForDecl.AsString;
				if (!IsClass(name) && CheckIsClass(name, dependencyTree))
				{
					Logger.Info("Marking struct {0} as class since it references other class", name);
					Classes.Add(name);
				}
			}

			// Third run - generate actual code
			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				if (cursor.CursorKind != ClangSharp.Interop.CXCursorKind.CXCursor_StructDecl)
				{
					continue;
				}

				var recordDecl = (RecordDecl)cursor;

				var name = recordDecl.TypeForDecl.AsString.FixSpecialWords();
				Logger.Info("Generating code for struct {0}", name);

				if (name == "stbi__jpeg")
				{
					var k = 5;
				}

				TypeDeclarationSyntax typeDecl;
				if (Classes.Contains(name))
				{
					typeDecl = ClassDeclaration(name);
				}
				else
				{
					typeDecl = StructDeclaration(name);
				}

				typeDecl = typeDecl.MakePublic();

				var destructorStatements = new List<StatementSyntax>();
				foreach (FieldDecl child in cursor.CursorChildren)
				{
					var childType = child.Type;
					var childName = child.Name.FixSpecialWords();
					var typeInfo = child.Type.ToTypeInfo();

					FieldDeclarationSyntax fieldDecl = null;
					if (typeInfo.ConstantArraySize != null)
					{
						if (!IsClass(name) && typeInfo is PrimitiveTypeInfo)
						{
							var expr = "public fixed " + ToRoslynTypeName(typeInfo) + " " +
								childName + "[" + typeInfo.ConstantArraySize.Value + "];";

							fieldDecl = (FieldDeclarationSyntax)ParseMemberDeclaration(expr);
						}
						else
						{
							var roslynString = ToRoslynString(typeInfo);
							var expr = "public " + roslynString + " " + childName +
								" = (" + roslynString + ")CRuntime.malloc(" + typeInfo.ConstantArraySize.Value + ");";

							fieldDecl = (FieldDeclarationSyntax)ParseMemberDeclaration(expr);

							expr = "CRuntime.free(" + childName + ");";
							var destructorStatement = ParseStatement(expr);
							destructorStatements.Add(destructorStatement);
						}
					}
					else
					{
						var variableDecl = VariableDeclaration2(child.Type, childName);
						fieldDecl = FieldDeclaration(variableDecl);
					}

					typeDecl = typeDecl.AddMembers(fieldDecl);
				}

				if (destructorStatements.Count > 0)
				{
					var destructor = DestructorDeclaration(Identifier(name));
					foreach (var stmt in destructorStatements)
					{
						destructor = destructor.AddBodyStatements(stmt);
					}

					typeDecl = typeDecl.AddMembers(destructor);
				}

				Result.Structs[name] = typeDecl;
			}
		}
	}
}