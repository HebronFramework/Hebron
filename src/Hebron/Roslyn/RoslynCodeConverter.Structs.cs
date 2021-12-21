using ClangSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Text;
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

		private TypeDeclarationSyntax FillTypeDeclaration(Cursor cursor, string name, TypeDeclarationSyntax typeDecl)
		{
			typeDecl = typeDecl.MakePublic();

			foreach (NamedDecl child in cursor.CursorChildren)
			{
				if (child is RecordDecl)
				{
					continue;
				}

				var asField = (FieldDecl)child;
				var childType = asField.Type;
				var childName = asField.Name.FixSpecialWords();
				var typeInfo = asField.Type.ToTypeInfo();

				FieldDeclarationSyntax fieldDecl = null;
				if (typeInfo.ConstantArraySizes != null && typeInfo.ConstantArraySizes.Length > 0)
				{
					if (!IsClass(name) && typeInfo is PrimitiveTypeInfo && typeInfo.ConstantArraySizes.Length == 1)
					{
						var expr = "public fixed " + ToRoslynTypeName(typeInfo) + " " +
							childName + "[" + typeInfo.ConstantArraySizes[0] + "];";

						fieldDecl = (FieldDeclarationSyntax)ParseMemberDeclaration(expr);
					}
					else
					{
						if (typeInfo.TypeString.Contains("unnamed "))
						{
							// unnamed struct
							var subName = "unnamed1";
							TypeDeclarationSyntax subTypeDecl = StructDeclaration(subName);
							subTypeDecl = FillTypeDeclaration(child.CursorChildren[0], subName, subTypeDecl);
							typeDecl = typeDecl.AddMembers(subTypeDecl);

							typeInfo = new StructTypeInfo(subName, typeInfo.PointerCount, typeInfo.ConstantArraySizes);
						}

						var arrayTypeName = ToRoslynString(typeInfo, treatArrayAsPointer: false);

						var sb = new StringBuilder();
						for(var i = 0; i < typeInfo.ConstantArraySizes.Length; ++i)
						{
							sb.Append(typeInfo.ConstantArraySizes[i]);
							if (i < typeInfo.ConstantArraySizes.Length - 1)
							{
								sb.Append(",");
							}
						}

						if(arrayTypeName.Contains("UnsafeArray1D<"))
						{
							sb.Append(", sizeof(" + typeInfo.TypeName + ")");
						}


						var expr = "public " + arrayTypeName + " " + childName +
							" = new " + arrayTypeName + "(" + sb.ToString() + ");";

						fieldDecl = (FieldDeclarationSyntax)ParseMemberDeclaration(expr);
					}
				}
				else
				{
					var vd = VariableDeclarator(childName);
					var variableDecl = VariableDeclaration(ParseTypeName(ToRoslynString(asField.Type, true))).AddVariables(vd);

					fieldDecl = FieldDeclaration(variableDecl).MakePublic();
				}

				typeDecl = typeDecl.AddMembers(fieldDecl);
			}

			return typeDecl;
		}

		public void ConvertStructs()
		{
			Logger.Info("Processing structs...");

			_state = State.Structs;

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
				foreach (NamedDecl child in cursor.CursorChildren)
				{
					if (child is RecordDecl)
					{
						continue;
					}

					var asField = (FieldDecl)child;
					var typeInfo = asField.Type.ToTypeInfo();
					var asStruct = typeInfo as StructTypeInfo;
					if (asStruct != null)
					{
						List<string> names;
						if (!dependencyTree.TryGetValue(name, out names))
						{
							names = new List<string>();
							dependencyTree[name] = names;
						}

						names.Add(asStruct.StructName);
						if (asStruct.IsArray)
						{
							Logger.Info("Marking struct {0} as class since it contains array of type {1}", name, asStruct.StructName);
							Classes.Add(name);
							break;
						}
					}

					var asFunctionPointer = typeInfo as FunctionPointerTypeInfo;
					if (asFunctionPointer != null)
					{
						Logger.Info("Marking struct {0} as class since it contains function pointers", name);
						Classes.Add(name);
						break;
					}

					if (typeInfo.IsArray && typeInfo.ConstantArraySizes.Length > 1)
					{
						Logger.Info("Marking struct {0} as class since it contains multidimensional arrays", name);
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

				TypeDeclarationSyntax typeDecl;
				if (Classes.Contains(name))
				{
					typeDecl = ClassDeclaration(name);
				}
				else
				{
					typeDecl = StructDeclaration(name);
				}

				typeDecl = FillTypeDeclaration(cursor, name, typeDecl);

				Result.Structs[name] = typeDecl;
			}
		}
	}
}