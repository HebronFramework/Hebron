using ClangSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Hebron.Roslyn
{
	partial class RoslynCodeConverter
	{
		private readonly List<string> TopTypes = new List<string>();
		private int _unnamedCounter;

		private bool CheckIsClass(string name, 
			Dictionary<string, List<string>> dependencyTree,
			HashSet<string> parsedNames)
		{
			parsedNames.Add(name);

			if (IsClass(name))
			{
				return true;
			}

			List<string> dependencyNames;
			if (dependencyTree.TryGetValue(name, out dependencyNames))
			{
				foreach (var dependencyName in dependencyNames)
				{
					if (parsedNames.Contains(dependencyName))
					{
						continue;
					}

					if (CheckIsClass(dependencyName, dependencyTree, parsedNames))
					{
						return true;
					}
				}
			}

			return false;
		}

		private TypeDeclarationSyntax FillTypeDeclaration(Cursor cursor, string name, TypeDeclarationSyntax typeDecl, bool isUnion)
		{
			typeDecl = typeDecl.MakePublic().MakeUnsafe();

			var constructorStatements = new List<StatementSyntax>();
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

				if ((typeInfo.TypeDescriptor is StructTypeInfo && !TopTypes.Contains(typeInfo.TypeName)) ||
					typeInfo.TypeString.Contains("unnamed "))
				{
					// unnamed struct
					string subName;
					if (typeInfo.TypeString.Contains("unnamed "))
					{
						// Unnamed subtype
						subName = "unnamed" + (_unnamedCounter + 1);
					} else
					{
						// Named subtype
						subName = typeInfo.TypeName;
					}
					 
					++_unnamedCounter;

					var sb = new StringBuilder();

					var subIsUnion = false;
					if (asField.Type.AsString.Contains("union "))
					{
						subIsUnion = true;
						subName = subName.Replace("union ", string.Empty); ;
						sb.Append("[StructLayout(LayoutKind.Explicit)]");
					}

					sb.Append("struct ");
					sb.Append(subName);
					sb.Append(" {}");


					var subTypeDecl = (TypeDeclarationSyntax)ParseMemberDeclaration(sb.ToString());

					subTypeDecl = FillTypeDeclaration(child.CursorChildren[0], subName, subTypeDecl, subIsUnion);
					typeDecl = typeDecl.AddMembers(subTypeDecl);

					typeInfo = new TypeInfo(new StructTypeInfo(subName), typeInfo.PointerCount, typeInfo.ConstantArraySizes);
				}

				var typeName = ToRoslynTypeName(typeInfo);

				string fieldDecl = null;

				var isFixedField = !IsClass(name) && 
					typeInfo.TypeDescriptor is PrimitiveTypeInfo &&
					typeInfo.ConstantArraySizes.Length == 1;
				if (isFixedField)
				{
					fieldDecl = "public fixed " + ToRoslynTypeName(typeInfo) + " " +
						childName + "[" + typeInfo.ConstantArraySizes[0] + "];";
				}
				else if (!IsClass(typeInfo.TypeDescriptor.TypeName) ||
					!typeInfo.IsArray)
				{
					fieldDecl = "public " + ToRoslynString(typeInfo, true) + " " + childName;

					if (typeInfo.ConstantArraySizes.Length > 0)
					{
						// Declare array field
						var arrayTypeName = BuildUnsafeArrayTypeName(typeInfo);
						var dimensions = typeInfo.ConstantArraySizes.BuildArrayDimensionsString();
						var expr = "public " + arrayTypeName + " " + childName +
							"Array = new " + arrayTypeName + "(" + dimensions + ");";
						var arrayFieldDecl = (FieldDeclarationSyntax)ParseMemberDeclaration(expr);

						var stmt = ParseStatement(childName + " = " + ToRoslynString(typeInfo).Parentize() + childName + "Array;");
						constructorStatements.Add(stmt);

						typeDecl = typeDecl.AddMembers(arrayFieldDecl);
					}
				} else
				{
					// Class array
					var arrayTypeName = ToRoslynTypeName(typeInfo);
					var dimensions = typeInfo.ConstantArraySizes.BuildArrayDimensionsString();
					fieldDecl = "public " + arrayTypeName + "[] " + childName +
						" = new " + arrayTypeName + "[" + dimensions + "];";

					var d = typeInfo.ConstantArraySizes[0];
					var stmt = ParseStatement(
						$"for (var i = 0; i < {d}; ++i) {{" +
						$"	{childName}[i] = new {arrayTypeName}();" +
						"}");
						
					constructorStatements.Add(stmt);
				}

				if (isUnion)
				{
					fieldDecl = "[FieldOffset(0)]" + fieldDecl;
				}

				var fieldDecl2 = (FieldDeclarationSyntax)ParseMemberDeclaration(fieldDecl.EnsureStatementFinished());
				typeDecl = typeDecl.AddMembers(fieldDecl2);
			}

			if (constructorStatements.Count > 0)
			{
				var constructor = ConstructorDeclaration(name).MakePublic();

				foreach(var stmt in constructorStatements)
				{
					constructor = constructor.AddBodyStatements(stmt);
				}

				typeDecl = typeDecl.AddMembers(constructor);
			}

			return typeDecl;
		}

		public void ConvertStructs()
		{
			if (!Parameters.ConversionEntities.HasFlag(ConversionEntities.Structs))
			{
				return;
			}

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
				var name = recordDecl.GetName().FixSpecialWords();
				foreach (NamedDecl child in cursor.CursorChildren)
				{
					if (child is RecordDecl)
					{
						continue;
					}

					var asField = (FieldDecl)child;
					var typeInfo = asField.Type.ToTypeInfo();
					var asStruct = typeInfo.TypeDescriptor as StructTypeInfo;
					if (asStruct != null)
					{
						List<string> names;
						if (!dependencyTree.TryGetValue(name, out names))
						{
							names = new List<string>();
							dependencyTree[name] = names;
						}

						names.Add(asStruct.StructName);
						if (typeInfo.IsArray)
						{
							Logger.Info("Marking struct {0} as class since it contains array of type {1}", name, asStruct.StructName);
							Classes.Add(name);
							break;
						}
					}

					var asFunctionPointer = typeInfo.TypeDescriptor as FunctionPointerTypeInfo;
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

				TopTypes.Add(name);
			}

			// Second run - use dependency tree to determine which structs reference classes and mark it as classes too
			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				if (cursor.CursorKind != ClangSharp.Interop.CXCursorKind.CXCursor_StructDecl)
				{
					continue;
				}

				var recordDecl = (RecordDecl)cursor;
				var name = recordDecl.GetName().FixSpecialWords();
				if (!IsClass(name) && CheckIsClass(name, dependencyTree, new HashSet<string>()))
				{
					Logger.Info("Marking struct {0} as class since it references other class", name);
					Classes.Add(name);
				}
			}

			// Third run - generate actual code
			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				_unnamedCounter = 0;
				if (cursor.CursorKind != ClangSharp.Interop.CXCursorKind.CXCursor_StructDecl)
				{
					continue;
				}

				var recordDecl = (RecordDecl)cursor;
				var name = recordDecl.GetName().FixSpecialWords();
				if (Parameters.SkipStructs.Contains(name))
				{
					Logger.Info("Skipping.");
					continue;
				}

				Logger.Info("Generating code for struct {0}", name);

				TypeDeclarationSyntax typeDecl;
				if (IsClass(name))
				{
					typeDecl = ClassDeclaration(name);
				}
				else
				{
					typeDecl = StructDeclaration(name);
				}

				typeDecl = FillTypeDeclaration(cursor, name, typeDecl, false);

				Result.Structs[name] = typeDecl;
			}

			// First delegate run - add missing return and argument types
			foreach (var pair in DelegateMap.ToArray())
			{
				var functionInfo = pair.Value.FunctionInfo;
				ToRoslynString(functionInfo.ReturnType, true);

				if (functionInfo.Arguments != null)
				{
					for (var i = 0; i < functionInfo.Arguments.Length; ++i)
					{
						var arg = functionInfo.Arguments[i];
						ToRoslynString(arg, true);
					}
				}
			}

			// Second delegate run: declare actual delegates
			foreach (var pair in DelegateMap)
			{
				var name = pair.Value.Name;
				var functionInfo = pair.Value.FunctionInfo;
				var decl = DelegateDeclaration(ParseTypeName(ToRoslynString(functionInfo.ReturnType, true)), name)
					.MakePublic();

				if (functionInfo.Arguments != null)
				{
					for (var i = 0; i < functionInfo.Arguments.Length; ++i)
					{
						var arg = functionInfo.Arguments[i];
						var argName = "arg" + i;
						decl = decl.AddParameterListParameters(Parameter(Identifier(argName)).WithType(ParseTypeName(ToRoslynString(arg, true))));
					}
				}

				Result.Delegates[name] = decl;
			}
		}
	}
}