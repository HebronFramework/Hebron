using ClangSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Hebron
{
	partial class CodeConverter
	{
		private class FieldInfo
		{
			public string Name;
			public Type Type;
		}

		private readonly List<StatementSyntax> _functionStatements = new List<StatementSyntax>();
		private List<FieldInfo> _currentStructInfo;
		private readonly Dictionary<string, List<FieldInfo>> _visitedStructs = new Dictionary<string, List<FieldInfo>>();


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

				var name = cursor.Spelling;
				Logger.Info("Processing function {0}", name);

				var parameters = new Dictionary<string, TypeInfo>();
				foreach (var p in funcDecl.Parameters)
				{
					parameters[p.Name] = p.Type.ToTypeInfo();
				}

				Output.Function(name, funcDecl.ReturnType.ToTypeInfo(), parameters);
			}
		}


		private void ProcessPossibleChildByIndex(Stmt child, int index)
		{
			if (child.Children.Count <= index)
			{
				return;
			}

			Process(child.Children[index]);
		}

		private void Process(IEnumerable<Cursor> children)
		{
			foreach(var child in children)
			{
				Process(child);
			}
		}

		private void Process(Cursor child)
		{
			switch (child)
			{
				case DeclStmt declStmt:
					Process(declStmt.CursorChildren);
					break;
				case VarDecl varDecl:

					break;
			}
		}

/*		private void ProcessDeclaration(VarDecl info, out string left, out string right)
		{
			var size = info.CursorChildren.Count;
			var name = info.Spelling.FixSpecialWords();

			var tt = info.Type;
			if (tt.IsArray())
			{
				tt = info.Type.PointeeType;
			}


			if (tt.IsClass(Parameters.Classes) || tt.IsStruct())
			{
				_visitedStructs.TryGetValue(tt.ToCSharpTypeString(), out _currentStructInfo);
			}

			if (size > 0)
			{
				rvalue = ProcessPossibleChildByIndex(info.Cursor, size - 1);

				if (info.Type.IsArray())
				{
					var arrayType = info.Type.GetPointeeType().ToCSharpTypeString();
					if (_state == State.Functions && !info.Type.GetPointeeType().IsClass())
					{
						info.CsType = info.Type.ToCSharpTypeString(true);
					}

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
