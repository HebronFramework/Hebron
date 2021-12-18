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
		private enum State
		{
			Structs,
			GlobalVariables,
			Enums,
			Constants,
			Functions
		}

		private class FieldInfo
		{
			public string Name;
			public Type Type;
		}

		private List<FieldInfo> _currentStructInfo;
		private readonly Dictionary<string, List<FieldInfo>> _visitedStructs = new Dictionary<string, List<FieldInfo>>();
		private State _state = State.Functions;


		public void ConvertFunctions()
		{
			Logger.Info("Processing functions...");

			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				var funcDecl = cursor as FunctionDecl;
				if (funcDecl == null || !funcDecl.HasBody)
				{
					continue;
				}

				Logger.Info("Processing function {0}", cursor.Spelling);

				var md = MethodDeclaration(ParseTypeName(ToRoslynString(funcDecl.ReturnType)), cursor.Spelling)
					.MakePublic()
					.MakeStatic();

				foreach(var p in funcDecl.Parameters)
				{
					md = md.AddParameterListParameters(Parameter(Identifier(p.Name)).WithType(ParseTypeName(ToRoslynString(p.Type))));
				}

				foreach(var child in funcDecl.Body.Children)
				{
					var result = Process(child);
					if (result == null)
					{
						continue;
					}
					foreach (var stmt in Process(child))
					{
						if (stmt == null)
						{
							continue;
						}

						md = md.AddBodyStatements((StatementSyntax)stmt);
					}
				}

				md = md.AddBodyStatements();

				Result.Functions[cursor.Spelling] = md;
			}
		}


		private IEnumerable<SyntaxNode> ProcessPossibleChildByIndex(Cursor child, int index)
		{
			if (child.CursorChildren.Count <= index)
			{
				return null;
			}

			return Process(child.CursorChildren[index]);
		}

		private IEnumerable<SyntaxNode> Process(IEnumerable<Cursor> children)
		{
			var result = new List<SyntaxNode>();
			foreach(var child in children)
			{
				result.AddRange(Process(child));
			}

			return result;
		}

		private IEnumerable<SyntaxNode> Process(Cursor child)
		{
			switch (child)
			{
				case DeclStmt declStmt:
					return Process(declStmt.CursorChildren);
				case VarDecl varDecl:
					{
						return new[] 
						{
							LocalDeclarationStatement(VariableDeclaration2(varDecl.Type, varDecl.Name))
						};
					}
			}

			return null;
		}

/*		private void ProcessDeclaration(VarDecl info, out SyntaxNode left, out SyntaxNode right)
		{
			SyntaxNode rvalue;

			var size = info.CursorChildren.Count;
			var name = info.Spelling.FixSpecialWords();

			var tt = info.Type.ToTypeInfo();
			if (tt.IsStruct)
			{
				_visitedStructs.TryGetValue(tt.ToRoslynTypeName(), out _currentStructInfo);
			}

			if (size > 0)
			{
				rvalue = ProcessPossibleChildByIndex(info, size - 1);

				if (info.Type.IsArray())
				{
					var t = info.Type.GetPointeeType().ToCSharpTypeString();

					if (rvalue.Info.Kind == CXCursorKind.CXCursor_TypeRef || rvalue.Info.Kind == CXCursorKind.CXCursor_IntegerLiteral ||
						rvalue.Info.Kind == CXCursorKind.CXCursor_BinaryOperator)
					{
						string sizeExp;
						if (rvalue.Info.Kind == CXCursorKind.CXCursor_TypeRef ||
							rvalue.Info.Kind == CXCursorKind.CXCursor_IntegerLiteral)
						{
							sizeExp = info.Type.GetArraySize().ToString();
						}
						else
						{
							sizeExp = rvalue.Expression;
						}

						if (_state != State.Functions || info.Type.GetPointeeType().IsClass())
						{
							rvalue.Expression = "new " + t + "[" + sizeExp + "]";
						}
						else
						{
							rvalue.Expression = "stackalloc " + arrayType + "[" + sizeExp + "]";
						}
					}
				}
			}

			string type = info.CsType;
			left = type + " " + name;
			right = string.Empty;

			if (rvalue != null && !string.IsNullOrEmpty(rvalue.Expression))
			{
				if (!info.IsPointer)
				{
					if (rvalue.Info.Kind == CXCursorKind.CXCursor_BinaryOperator)
					{
						var op = sealang.cursor_getBinaryOpcode(rvalue.Info.Cursor);
						if (op.IsLogicalBooleanOperator())
						{
							rvalue.Expression = rvalue.Expression + "?1:0";
						}
					}

					right = rvalue.Expression.ApplyCast(info.CsType);
				}
				else
				{
					var t = info.Type.GetPointeeType().ToCSharpTypeString();
					if (rvalue.Info.Kind == CXCursorKind.CXCursor_InitListExpr)
					{
						if (_state != State.Functions || info.Type.GetPointeeType().IsClass())
						{
							rvalue.Expression = rvalue.Expression;
						}
						else
						{
							var arrayType = info.Type.GetPointeeType().ToCSharpTypeString();

							rvalue.Expression = "stackalloc " + arrayType + "[" + info.Type.GetArraySize() + "];\n";
							var size2 = rvalue.Info.Cursor.GetChildrenCount();
							for (var i = 0; i < size2; ++i)
							{
								var exp = ProcessChildByIndex(rvalue.Info.Cursor, i);

								if (!exp.Info.IsPointer)
								{
									exp.Expression = exp.Expression.ApplyCast(exp.Info.CsType);
								}

								rvalue.Expression += name + "[" + i + "] = " + exp.Expression + ";\n";
							}
						}
					}

					if (info.IsPointer && !info.IsArray && rvalue.Info.IsArray &&
						rvalue.Info.Type.GetPointeeType().kind.IsPrimitiveNumericType() &&
						rvalue.Info.Kind != CXCursorKind.CXCursor_StringLiteral)
					{
						rvalue.Expression = "((" + info.Type.GetPointeeType().ToCSharpTypeString() + "*)" + rvalue.Expression + ")";
					}

					if (info.IsPointer && !info.IsArray && rvalue.Expression == "0")
					{
						rvalue.Expression = "null";
					}

					right = rvalue.Expression;
				}
			}
			else if (info.RecordType != RecordType.None && !info.IsPointer)
			{
				right = "new " + info.CsType + "()";
			}
			else if (!info.IsPointer)
			{
				right = "0";
			}

			_currentStructInfo = null;
		}*/
	}
}
