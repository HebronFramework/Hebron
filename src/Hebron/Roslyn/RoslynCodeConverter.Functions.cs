using ClangSharp;
using ClangSharp.Interop;
using System.Collections.Generic;
using System.Text;
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
		private FunctionDecl _functionDecl;


		public void ConvertFunctions()
		{
			Logger.Info("Processing functions...");

			_state = State.Functions;
			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				var funcDecl = cursor as FunctionDecl;
				if (funcDecl == null || !funcDecl.HasBody)
				{
					continue;
				}

				_functionDecl = funcDecl;
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

					var stmt = ParseStatement(result.Expression.EnsureStatementFinished());
					md = md.AddBodyStatements(stmt);
				}

				md = md.AddBodyStatements();

				Result.Functions[cursor.Spelling] = md;
			}
		}

		private void ProcessDeclaration(VarDecl info, out string left, out string right)
		{
			var size = info.CursorChildren.Count;
			var name = info.Spelling.FixSpecialWords();

			var tt = info.Type.ToTypeInfo();
			var type = ToRoslynString(tt, treatArrayAsPointer: _state == State.Functions);
			var typeName = ToRoslynTypeName(tt);

			if (tt is StructTypeInfo)
			{
				_visitedStructs.TryGetValue(tt.TypeName, out _currentStructInfo);
			}

			left = type + " " + name;
			right = string.Empty;

			if (size > 0)
			{
				var rvalue = ProcessChildByIndex(info, size - 1);

				switch(rvalue.Info.CursorKind)
				{
					case CXCursorKind.CXCursor_BinaryOperator:
						var op = clangsharp.Cursor_getBinaryOpcode(rvalue.Info.Handle);
						if (op.IsLogicalBooleanOperator())
						{
							right = rvalue.Expression + "?1:0";
						}
						break;

					case CXCursorKind.CXCursor_InitListExpr:
						if (_state != State.Functions)
						{
							right = "new " + type + "(new " + typeName + "[]" + rvalue.Expression + ");";
						}
						else
						{
							right = "stackalloc " + typeName + "[]" + rvalue.Expression + ";";
						}
						break;
				}
			}

			if (string.IsNullOrEmpty(right) && tt is StructTypeInfo)
			{
				right = "new " + type + "()";
			}

			if (string.IsNullOrEmpty(right) && !tt.IsPointer)
			{
				right = "0";
			}

			_currentStructInfo = null;
		}

		internal void AppendGZ(CursorProcessResult crp)
		{
			var info = crp.Info;

			if (info.CursorKind == CXCursorKind.CXCursor_BinaryOperator)
			{
				var type = clangsharp.Cursor_getBinaryOpcode(info.Handle);
				if (!type.IsBinaryOperator())
				{
					return;
				}
			}

			if (info.CursorKind == CXCursorKind.CXCursor_ParenExpr)
			{
				var child2 = ProcessChildByIndex(info, 0);
				if (child2.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator &&
					clangsharp.Cursor_getBinaryOpcode(child2.Info.Handle).IsBinaryOperator())
				{
					var sub = ProcessChildByIndex(crp.Info, 0);
					crp.Expression = sub.Expression.Parentize() + "!= 0";
				}
				return;
			}

			if (info.CursorKind == CXCursorKind.CXCursor_UnaryOperator)
			{
				var child = ProcessChildByIndex(info, 0);
				var type = clangsharp.Cursor_getUnaryOpcode(info.Handle);
				if (child.IsPointer)
				{
					if (type == CX_UnaryOperatorKind.CX_UO_LNot)
					{
						crp.Expression = child.Expression + "== null";
					}

					return;
				}

				if (child.Info.CursorKind == CXCursorKind.CXCursor_ParenExpr)
				{
					var child2 = ProcessChildByIndex(child.Info, 0);
					if (child2.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator &&
						clangsharp.Cursor_getBinaryOpcode(child2.Info.Handle).IsBinaryOperator())
					{
					}
					else
					{
						return;
					}
				}

				if (type == CX_UnaryOperatorKind.CX_UO_LNot)
				{
					var sub = ProcessChildByIndex(crp.Info, 0);
					crp.Expression = sub.Expression + "== 0";

					return;
				}
			}

			if (crp.TypeInfo is PrimitiveTypeInfo)
			{
				crp.Expression = crp.Expression.Parentize() + " != 0";
			}

			if (crp.IsPointer)
			{
				crp.Expression = crp.Expression.Parentize() + " != null";
			}
		}

		private CursorProcessResult ProcessChildByIndex(Cursor cursor, int index)
		{
			return Process(cursor.CursorChildren[index]);
		}

		private CursorProcessResult ProcessPossibleChildByIndex(Cursor cursor, int index)
		{
			if (cursor.CursorChildren.Count <= index)
			{
				return null;
			}

			return Process(cursor.CursorChildren[index]);
		}

		private string InternalProcess(Cursor info)
		{
			switch (info.Handle.Kind)
			{
				case CXCursorKind.CXCursor_EnumConstantDecl:
					{
						var expr = ProcessPossibleChildByIndex(info, 0);

						return info.Spelling + " = " + expr.Expression;
					}

				case CXCursorKind.CXCursor_UnaryExpr:
					{
						var opCode = clangsharp.Cursor_getUnaryOpcode(info.Handle);
						var expr = ProcessPossibleChildByIndex(info, 0);

						if ((int)opCode == 99999 && expr != null)
						{
							var op = "sizeof";
/*							if (tokens.Length > 0 && tokens[0] == "__alignof")
							{
								// 4 is default alignment
								return "4";
							}*/

							if (!string.IsNullOrEmpty(expr.Expression))
							{
								// sizeof
								return op + "(" + expr.Expression + ")";
							}

							if (expr.Info.CursorKind == CXCursorKind.CXCursor_TypeRef)
							{
								return op + "(" + expr.CsType + ")";
							}
						}

						return string.Empty;
					}
				case CXCursorKind.CXCursor_DeclRefExpr:
					{
						return info.Spelling.FixSpecialWords();
					}
				case CXCursorKind.CXCursor_CompoundAssignOperator:
				case CXCursorKind.CXCursor_BinaryOperator:
					{
						var a = ProcessChildByIndex(info, 0);
						var b = ProcessChildByIndex(info, 1);
						var type = clangsharp.Cursor_getBinaryOpcode(info.Handle);

						if (type.IsLogicalBinaryOperator())
						{
							AppendGZ(a);
							AppendGZ(b);
						}

						if (type.IsLogicalBooleanOperator())
						{
							a.Expression = a.Expression.Parentize();
							b.Expression = b.Expression.Parentize();
						}

						if (type.IsAssign() && type != CX_BinaryOperatorKind.CX_BO_ShlAssign && type != CX_BinaryOperatorKind.CX_BO_ShrAssign)
						{
							var typeInfo = info.ToTypeInfo();

							// Explicity cast right to left
							if (!typeInfo.IsPointer)
							{
								if (b.Info.CursorKind == CXCursorKind.CXCursor_ParenExpr && b.Info.CursorChildren.Count > 0)
								{
									var bb = ProcessChildByIndex(b.Info, 0);
									if (bb.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator &&
										clangsharp.Cursor_getBinaryOpcode(bb.Info.Handle).IsLogicalBooleanOperator())
									{
										b = bb;
									}
								}

								if (b.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator &&
									clangsharp.Cursor_getBinaryOpcode(b.Info.Handle).IsLogicalBooleanOperator())
								{
									b.Expression = "(" + b.Expression + "?1:0)";
								}
								else
								{
									b.Expression = b.Expression.Parentize();
								}

								b.Expression = b.Expression.ApplyCast(ToRoslynString(typeInfo));
							}
						}

						if (a.IsPointer)
						{
							if (a.IsClass)
							{
								switch (type)
								{
									case CX_BinaryOperatorKind.CX_BO_Add:
										return a.Expression + "[" + b.Expression + "]";
								}
							}
						}

						if (a.IsPointer && (type == CX_BinaryOperatorKind.CX_BO_Assign || type.IsBooleanOperator()) &&
							(b.Expression.Deparentize() == "0"))
						{
							b.Expression = "null";
						}

						var str = info.GetOperatorString();
						var result = a.Expression + " " + str + " " + b.Expression;

						return result;
					}
				case CXCursorKind.CXCursor_UnaryOperator:
					{
						var a = ProcessChildByIndex(info, 0);

						var type = clangsharp.Cursor_getUnaryOpcode(info.Handle);
						var str = info.GetOperatorString();

						var typeInfo = info.ToTypeInfo();

						if (IsClass(typeInfo) && 
							(type == CX_UnaryOperatorKind.CX_UO_AddrOf || type == CX_UnaryOperatorKind.CX_UO_Deref))
						{
							str = string.Empty;
						}

						var left = type.IsUnaryOperatorPre();
						if (left)
						{
							return str + a.Expression;
						}

						return a.Expression + str;
					}

				case CXCursorKind.CXCursor_CallExpr:
					{
						var size = info.CursorChildren.Count;

						var functionExpr = ProcessChildByIndex(info, 0);
						var functionName = functionExpr.Expression.Deparentize();

						// Retrieve arguments
						var args = new List<string>();
						for (var i = 1; i < size; ++i)
						{
							var argExpr = ProcessChildByIndex(info, i);

							if (!argExpr.IsPointer)
							{
								argExpr.Expression = argExpr.Expression.ApplyCast(argExpr.CsType);
							}
							else if (argExpr.Expression.Deparentize() == "0")
							{
								argExpr.Expression = "null";
							}

							args.Add(argExpr.Expression);
						}

						var sb = new StringBuilder();

						sb.Append(functionName + "(");
						sb.Append(string.Join(", ", args));
						sb.Append(")");


						return sb.ToString();
					}
				case CXCursorKind.CXCursor_ReturnStmt:
					{
						var child = ProcessPossibleChildByIndex(info, 0);

						var ret = child.GetExpression();

						var tt = _functionDecl.ReturnType.ToTypeInfo();
						if (_functionDecl.ReturnType.Kind != CXTypeKind.CXType_Void)
						{
							if (!tt.IsPointer)
							{
								if (child != null && child.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator &&
									clangsharp.Cursor_getBinaryOpcode(child.Info.Handle).IsLogicalBooleanOperator())
								{
									ret = "(" + ret + "?1:0)";
								}

								return "return " + ret.ApplyCast(ToRoslynString(tt));
							}
						}

						if (tt.IsPointer && ret == "0")
						{
							ret = "null";
						}

						var exp = string.IsNullOrEmpty(ret) ? "return" : "return " + ret;

						return exp;
					}
				case CXCursorKind.CXCursor_IfStmt:
					{
						var conditionExpr = ProcessChildByIndex(info, 0);
						AppendGZ(conditionExpr);

						var executionExpr = ProcessChildByIndex(info, 1);
						var elseExpr = ProcessPossibleChildByIndex(info, 2);

						if (executionExpr != null && !string.IsNullOrEmpty(executionExpr.Expression))
						{
							executionExpr.Expression = executionExpr.Expression.EnsureStatementFinished();
						}

						var expr = "if (" + conditionExpr.Expression + ") " + executionExpr.Expression;

						if (elseExpr != null)
						{
							expr += " else " + elseExpr.Expression;
						}

						return expr;
					}
				case CXCursorKind.CXCursor_ForStmt:
					{
						var size = info.CursorChildren.Count;

						CursorProcessResult execution = null, start = null, condition = null, it = null;
						switch (size)
						{
							case 1:
								execution = ProcessChildByIndex(info, 0);
								break;
							case 2:
								start = ProcessChildByIndex(info, 0);
								condition = ProcessChildByIndex(info, 1);
								break;
							case 3:
								var expr = ProcessChildByIndex(info, 0);
								if (expr.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator &&
									clangsharp.Cursor_getBinaryOpcode(expr.Info.Handle).IsBooleanOperator())
								{
									condition = expr;
								}
								else
								{
									start = expr;
								}

								expr = ProcessChildByIndex(info, 1);
								if (expr.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator &&
									clangsharp.Cursor_getBinaryOpcode(expr.Info.Handle).IsBooleanOperator())
								{
									condition = expr;
								}
								else
								{
									it = expr;
								}

								execution = ProcessChildByIndex(info, 2);
								break;
							case 4:
								start = ProcessChildByIndex(info, 0);
								condition = ProcessChildByIndex(info, 1);
								it = ProcessChildByIndex(info, 2);
								execution = ProcessChildByIndex(info, 3);
								break;
						}

						var executionExpr = ReplaceCommas(execution);
						executionExpr = executionExpr.EnsureStatementFinished();

						return "for (" + start.GetExpression().EnsureStatementEndWithSemicolon() + condition.GetExpression().EnsureStatementEndWithSemicolon() + it.GetExpression() + ") " +
								executionExpr.Curlize();
					}

				case CXCursorKind.CXCursor_CaseStmt:
					{
						var expr = ProcessChildByIndex(info, 0);
						var execution = ProcessChildByIndex(info, 1);
						return "case " + expr.Expression + ":" + execution.Expression;
					}

				case CXCursorKind.CXCursor_DefaultStmt:
					{
						var execution = ProcessChildByIndex(info, 0);
						return "default: " + execution.Expression;
					}

				case CXCursorKind.CXCursor_SwitchStmt:
					{
						var expr = ProcessChildByIndex(info, 0);
						var execution = ProcessChildByIndex(info, 1);
						return "switch (" + expr.Expression + ")" + execution.Expression;
					}

				case CXCursorKind.CXCursor_DoStmt:
					{
						var execution = ProcessChildByIndex(info, 0);
						var expr = ProcessChildByIndex(info, 1);
						AppendGZ(expr);

						return "do " + execution.Expression.EnsureStatementFinished() + " while (" + expr.Expression + ")";
					}

				case CXCursorKind.CXCursor_WhileStmt:
					{
						var expr = ProcessChildByIndex(info, 0);
						AppendGZ(expr);
						var execution = ProcessChildByIndex(info, 1);

						return "while (" + expr.Expression + ") " + execution.Expression.EnsureStatementFinished().Curlize();
					}

				case CXCursorKind.CXCursor_LabelRef:
					return info.Spelling;
				case CXCursorKind.CXCursor_GotoStmt:
					{
						var label = ProcessChildByIndex(info, 0);

						return "goto " + label.Expression;
					}

				case CXCursorKind.CXCursor_LabelStmt:
					{
						var sb = new StringBuilder();

						sb.Append(info.Spelling);
						sb.Append(":;\n");

						var size = info.CursorChildren.Count;
						for (var i = 0; i < size; ++i)
						{
							var child = ProcessChildByIndex(info, i);
							sb.Append(child.Expression);
						}

						return sb.ToString();
					}

				case CXCursorKind.CXCursor_ConditionalOperator:
					{
						var condition = ProcessChildByIndex(info, 0);

						var a = ProcessChildByIndex(info, 1);
						var b = ProcessChildByIndex(info, 2);

						if (condition.TypeInfo.IsPrimitiveNumericType())
						{
							var gz = true;

							if (condition.Info.CursorKind == CXCursorKind.CXCursor_ParenExpr)
							{
								gz = false;
							}
							else if (condition.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator)
							{
								var op = clangsharp.Cursor_getBinaryOpcode(condition.Info.Handle);

								if (op == CX_BinaryOperatorKind.CX_BO_Or || op == CX_BinaryOperatorKind.CX_BO_And)
								{
								}
								else
								{
									gz = false;
								}
							}

							if (gz)
							{
								condition.Expression = condition.Expression.Parentize() + " != 0";
							}
						}

						return condition.Expression + "?" + a.Expression + ":" + b.Expression;
					}

				case CXCursorKind.CXCursor_MemberRefExpr:
					{
						var a = ProcessChildByIndex(info, 0);

						var op = ".";
						if (a.Expression != "this" && !a.IsClass && a.IsPointer)
						{
							op = "->";
						}

						var result = a.Expression + op + info.Spelling.FixSpecialWords();

						return result;
					}

				case CXCursorKind.CXCursor_IntegerLiteral:
				case CXCursorKind.CXCursor_FloatingLiteral:
					{
						return info.GetLiteralString();
					}
				case CXCursorKind.CXCursor_CharacterLiteral:
					{

						var r = info.GetLiteralString();
						if (string.IsNullOrEmpty(r))
						{
							r = @"\0";
						}
						return "'" + r + "'";
					}

				case CXCursorKind.CXCursor_StringLiteral:
					return info.Spelling.StartsWith("L") ? info.Spelling.Substring(1) : info.Spelling;
				case CXCursorKind.CXCursor_VarDecl:
					{
						string left, right;
						ProcessDeclaration((VarDecl)info, out left, out right);
						var expr = left;
						if (!string.IsNullOrEmpty(right))
						{
							expr += " = " + right;
						}
						return expr;
					}

				case CXCursorKind.CXCursor_DeclStmt:
					{
						var sb = new StringBuilder();
						var size = info.CursorChildren.Count;
						for (var i = 0; i < size; ++i)
						{
							var exp = ProcessChildByIndex(info, i);
							exp.Expression = exp.Expression.EnsureStatementFinished();
							sb.Append(exp.Expression);
						}

						return sb.ToString();
					}

				case CXCursorKind.CXCursor_CompoundStmt:
					{
						var sb = new StringBuilder();
						sb.Append("{\n");

						var size = info.CursorChildren.Count;
						for (var i = 0; i < size; ++i)
						{
							var exp = ProcessChildByIndex(info, i);
							exp.Expression = exp.Expression.EnsureStatementFinished();
							sb.Append(exp.Expression);
						}

						sb.Append("}\n");

						var fullExp = sb.ToString();

						return fullExp;
					}

				case CXCursorKind.CXCursor_ArraySubscriptExpr:
					{
						var var = ProcessChildByIndex(info, 0);
						var expr = ProcessChildByIndex(info, 1);

						return var.Expression + "[" + expr.Expression + "]";
					}

				case CXCursorKind.CXCursor_InitListExpr:
					{
						var sb = new StringBuilder();

						var tt = info.ToTypeInfo();
						var initStruct = _currentStructInfo != null && !tt.IsArray;
						if (initStruct)
						{
							sb.Append("new " + ToRoslynTypeName(tt) + " ");
						}

						sb.Append("{ ");
						var size = info.CursorChildren.Count;
						for (var i = 0; i < size; ++i)
						{
							var exp = ProcessChildByIndex(info, i);

							if (initStruct)
							{
								if (i < _currentStructInfo.Count)
								{
									sb.Append(_currentStructInfo[i].Name + " = ");
								}
							}

							var val = exp.Expression;
							if (initStruct && i < _currentStructInfo.Count && _currentStructInfo[i].Type.Kind == CXTypeKind.CXType_Bool)
							{
								if (val == "0")
								{
									val = "false";
								}
								else if (val == "1")
								{
									val = "true";
								}
							}

							sb.Append(val);

							if (i < size - 1)
							{
								sb.Append(", ");
							}
						}

						sb.Append(" }");
						return sb.ToString();
					}

				case CXCursorKind.CXCursor_ParenExpr:
					{
						var expr = ProcessPossibleChildByIndex(info, 0);
						var e = expr.GetExpression();

						var tt = info.ToTypeInfo();
						var csType = ToRoslynString(tt);

						if (csType != expr.CsType)
						{
							e = e.ApplyCast(csType);
						}
						else
						{
							e = e.Parentize();
						}

						return e;
					}

				case CXCursorKind.CXCursor_BreakStmt:
					return "break";
				case CXCursorKind.CXCursor_ContinueStmt:
					return "continue";

				case CXCursorKind.CXCursor_CStyleCastExpr:
					{
						var size = info.CursorChildren.Count;
						var child = ProcessChildByIndex(info, size - 1);

						var expr = child.Expression;
						var tt = info.ToTypeInfo();
						var csType = ToRoslynString(tt);

						if (csType != child.CsType)
						{
							expr = expr.ApplyCast(csType);
						}

						if (expr.Deparentize() == "0")
						{
							expr = "null";
						}

						return expr;
					}

				case CXCursorKind.CXCursor_UnexposedExpr:
					{
						// Return last child
						var size = info.CursorChildren.Count;

						if (size == 0)
						{
							return string.Empty;
						}

						var expr = ProcessPossibleChildByIndex(info, size - 1);

						var tt = info.ToTypeInfo();
						if (tt.IsPointer && expr.Expression.Deparentize() == "0")
						{
							expr.Expression = "null";
						}

						return expr.Expression;
					}

				default:
					{
						// Return last child
						var size = info.CursorChildren.Count;

						if (size == 0)
						{
							return string.Empty;
						}

						var expr = ProcessPossibleChildByIndex(info, size - 1);

						return expr.GetExpression();
					}
			}
		}

		private CursorProcessResult Process(Cursor cursor)
		{
			var expr = InternalProcess(cursor);

			return new CursorProcessResult(this, cursor)
			{
				Expression = expr
			};
		}

		private string ReplaceCommas(CursorProcessResult info)
		{
			var executionExpr = info.GetExpression();
			if (info != null && info.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator)
			{
				var type = clangsharp.Cursor_getBinaryOpcode(info.Info.Handle);
				if (type == CX_BinaryOperatorKind.CX_BO_Comma)
				{
					var a = ReplaceCommas(ProcessChildByIndex(info.Info, 0));
					var b = ReplaceCommas(ProcessChildByIndex(info.Info, 1));

					executionExpr = a + ";" + b;
				}
			}

			return executionExpr;
		}
	}
}