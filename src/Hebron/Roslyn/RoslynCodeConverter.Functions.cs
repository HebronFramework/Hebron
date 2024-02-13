using ClangSharp;
using ClangSharp.Interop;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Type = ClangSharp.Type;

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

		private State _state = State.Functions;
		private FunctionDecl _functionDecl;
		private List<FieldInfo> _currentStructInfo;
		private readonly Dictionary<string, string> _localVariablesMap = new Dictionary<string, string>();

		public void ConvertFunctions()
		{
			if (!Parameters.ConversionEntities.HasFlag(ConversionEntities.Functions))
			{
				return;
			}

			Logger.Info("Processing functions...");

			_state = State.Functions;
			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				_localVariablesMap.Clear();

				var funcDecl = cursor as FunctionDecl;
				if (funcDecl == null || !funcDecl.HasBody)
				{
					continue;
				}

				_functionDecl = funcDecl;

				var functionName = cursor.Spelling.FixSpecialWords();
				Logger.Info("Processing function {0}", functionName);

				if (Parameters.SkipFunctions.Contains(functionName))
				{
					Logger.Info("Skipped.");
					continue;
				}

				var md = MethodDeclaration(ParseTypeName(ToRoslynString(funcDecl.ReturnType)), cursor.Spelling)
					.MakePublic()
					.MakeStatic();

				foreach (var p in funcDecl.Parameters)
				{
					var name = p.Name.FixSpecialWords();
					var csType = ToRoslynString(p.Type, true);
					PushVariableInfo(name, csType);
					md = md.AddParameterListParameters(Parameter(Identifier(name)).WithType(ParseTypeName(csType)));
				}

				foreach (var child in funcDecl.Body.Children)
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

				foreach (var p in funcDecl.Parameters)
				{
					var name = p.Name.FixSpecialWords();
					PopVariableInfo(name);
				}
			}
		}

		private string ProcessDeclaration(VarDecl info, out string name)
		{
			var isGlobalVariable = _state == State.GlobalVariables || info.StorageClass == CX_StorageClass.CX_SC_Static;

			string left, right;
			var size = info.CursorChildren.Count;
			name = info.Spelling.FixSpecialWords();

			if (_state == State.Functions && info.StorageClass == CX_StorageClass.CX_SC_Static)
			{
				name = _functionDecl.Spelling + "_" + name;
			}

			var typeInfo = info.Type.ToTypeInfo();

			var type = ToRoslynString(typeInfo, true);
			var typeName = ToRoslynTypeName(typeInfo);

			left = type + " " + name;
			right = string.Empty;

			if (size > 0)
			{
				var rvalue = ProcessChildByIndex(info, size - 1);

				var expr = rvalue.Expression;
				if (AppendBoolToInt(rvalue.Info, ref expr))
				{
					right = expr;
				}
				else if (rvalue.Info.CursorKind == CXCursorKind.CXCursor_InitListExpr)
				{
					var initListExpr = rvalue.Expression;
					if (rvalue.Info.CursorChildren.Count == 1 &&
						typeInfo.ConstantArraySizes.Length > 0 &&
						typeInfo.ConstantArraySizes[0] > 1)
					{
						var sb = new StringBuilder();
						sb.Append("{");

						var element = rvalue.Expression.Decurlize();
						for (var i = 0; i < typeInfo.ConstantArraySizes[0]; ++i)
						{
							sb.Append(element);
							if (i < typeInfo.ConstantArraySizes[0] - 1)
							{
								sb.Append(", ");
							}
						}
						sb.Append("}");

						initListExpr = sb.ToString();
					}

					if (isGlobalVariable)
					{
						if (Parameters.GlobalVariablesUnsafeArrayUsage == UnsafeArrayUsage.UseUnsafeArray)
						{
							// Declare array field
							var arrayVariableName = name + "Array";
							var arrayTypeName = BuildUnsafeArrayTypeName(typeInfo);
							var arrayExpr = "var " + arrayVariableName +
								" = new " + arrayTypeName +
								"(new " + typeName + "[] " + initListExpr + ");";

							left = arrayExpr + type + " " + name;
							right = "(" + type + ")" + arrayVariableName;
						} else
						{
							left = type.PointerToArray() + " " + name;
							right = "new " + typeName + "[]" + initListExpr + ";";
						}
					}
					else
					{
						right = "stackalloc " + type.Depoint() + "[]" + initListExpr + ";";
					}
				}
				else if (!isGlobalVariable && typeInfo.ConstantArraySizes.Length == 1 && !IsClass(typeName))
				{
					right = "stackalloc " + typeName + "[" + typeInfo.ConstantArraySizes[0] + "];";
				}
				else if(typeInfo.ConstantArraySizes.Length > 0)
				{
					var sb = new StringBuilder();
					for (var i = 0; i < typeInfo.ConstantArraySizes.Length; ++i)
					{
						sb.Append(typeInfo.ConstantArraySizes[i]);
						if (i < typeInfo.ConstantArraySizes.Length - 1)
						{
							sb.Append(", ");
						}
					}

					if (!IsClass(typeName) && 
						(!isGlobalVariable || Parameters.GlobalVariablesUnsafeArrayUsage == UnsafeArrayUsage.UseUnsafeArray))
					{
						var arrayVariableName = name + "Array";
						var arrayTypeName = BuildUnsafeArrayTypeName(typeInfo);

						var arrayExpr = arrayTypeName + " " + arrayVariableName +
							" = new " + arrayTypeName + "(" + sb.ToString() + ");";

						if (!isGlobalVariable)
						{
							left = arrayExpr + "var " + name;
						}
						right = "(" + type + ")" + arrayVariableName;
					} else
					{
						if (!isGlobalVariable)
						{
							left = "var " + name;
						} else
						{
							left = type.PointerToArray() + " " + name;
						}

						right = "new " + typeName + "[" + sb.ToString() + "];";

						if (IsClass(typeName))
						{
							for (var i = 0; i < typeInfo.ConstantArraySizes[0]; ++i)
							{
								right += name + "[" + i + "] = new " + typeName + "();";
							}
						}
					}
				}
				else
				{
					right = rvalue.Expression;
				}

				if (!string.IsNullOrEmpty(right) && !typeInfo.IsPointer)
				{
					right = right.ApplyCast(type);
				}
			}

			if (typeInfo.IsPointer && right.Deparentize() == "0")
			{
				right = "null";
			}

			if (string.IsNullOrEmpty(right) && typeInfo.TypeDescriptor is StructTypeInfo && !typeInfo.IsPointer)
			{
				right = "new " + type + "()";
			}

			if (string.IsNullOrEmpty(right) && !typeInfo.IsPointer)
			{
				right = "0";
			}

			_currentStructInfo = null;

			var result = left;
			if (!string.IsNullOrEmpty(right))
			{
				result += " = " + right;
			}

			return result;
		}

		internal void AppendNonZeroCheck(CursorProcessResult crp)
		{
			var info = crp.Info;

			if (info.CursorKind == CXCursorKind.CXCursor_BinaryOperator)
			{
				var type = clang.getCursorBinaryOperatorKind(info.Handle);
				if (!type.IsBinaryOperator())
				{
					return;
				}
			}

			if (info.CursorKind == CXCursorKind.CXCursor_ParenExpr)
			{
				var child2 = ProcessChildByIndex(info, 0);
				if (child2.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator)
				{
					var type = clang.getCursorBinaryOperatorKind(child2.Info.Handle);
					if (!type.IsBinaryOperator())
					{
						return;
					}
				}
			}

			if (info.CursorKind == CXCursorKind.CXCursor_UnaryOperator)
			{
				var child = ProcessChildByIndex(info, 0);
				var type = clang.getCursorUnaryOperatorKind(info.Handle);
				if (child.IsPointer)
				{
					if (type == CXUnaryOperatorKind.CXUnaryOperator_LNot)
					{
						crp.Expression = child.Expression + "== null";
					}

					return;
				}

				if (child.Info.CursorKind == CXCursorKind.CXCursor_ParenExpr)
				{
					var child2 = ProcessChildByIndex(child.Info, 0);
					if (child2.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator &&
						clang.getCursorBinaryOperatorKind(child2.Info.Handle).IsBinaryOperator())
					{
					}
					else
					{
						return;
					}
				}

				if (type == CXUnaryOperatorKind.CXUnaryOperator_LNot)
				{
					var sub = ProcessChildByIndex(crp.Info, 0);
					crp.Expression = sub.Expression + "== 0";

					return;
				}
			}

			if (crp.TypeInfo.TypeDescriptor is PrimitiveTypeInfo && !crp.IsPointer)
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

		private bool AppendBoolToInt(Cursor info, ref string expression)
		{
			if (info.CursorKind == CXCursorKind.CXCursor_BinaryOperator &&
				clang.getCursorBinaryOperatorKind(info.Handle).IsLogicalBooleanOperator())
			{
				expression = "(" + expression + "?1:0)";
				return true;
			}
			else if (info.CursorKind == CXCursorKind.CXCursor_ParenExpr)
			{
				var child2 = ProcessPossibleChildByIndex(info, 0);
				if (child2 != null &&
					child2.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator &&
					clang.getCursorBinaryOperatorKind(child2.Info.Handle).IsLogicalBooleanOperator())
				{
					expression = "(" + expression + "?1:0)";
					return true;
				}
			}

			return false;
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
						var opCode = clang.getCursorUnaryOperatorKind(info.Handle);
						var expr = ProcessPossibleChildByIndex(info, 0);

						string[] tokens = null;
						if (opCode == CXUnaryOperatorKind.CXUnaryOperator_Invalid && expr != null)
						{
							tokens = info.Tokenize();
							var op = "sizeof";
							if (tokens.Length > 0 && tokens[0] == "__alignof")
							{
								// 4 is default alignment
								return "4";
							}

							if (op == "sizeof" && !string.IsNullOrEmpty(expr.Expression))
							{
								if (expr.TypeInfo.ConstantArraySizes.Length > 1)
								{
									throw new Exception(string.Format("sizeof for arrays with {0} dimensions isn't supported.", 
										expr.TypeInfo.ConstantArraySizes.Length));
								}

								if (expr.TypeInfo.ConstantArraySizes.Length == 1)
								{
									return expr.TypeInfo.ConstantArraySizes[0] + " * sizeof(" + ToRoslynTypeName(expr.TypeInfo) + ")";
								}

								return "sizeof(" + expr.CsType + ")";
							}

							if (expr.Info.CursorKind == CXCursorKind.CXCursor_TypeRef)
							{
								return op + "(" + expr.CsType + ")";
							}
						}

						if (tokens == null)
						{
							tokens = info.Tokenize();
						}

						return string.Join(string.Empty, tokens);
					}
				case CXCursorKind.CXCursor_DeclRefExpr:
					{
						var name = info.Spelling.FixSpecialWords();
						if (_localVariablesMap.ContainsKey(name))
						{
							name = _localVariablesMap[name];
						}

						return name;
					}
				case CXCursorKind.CXCursor_CompoundAssignOperator:
				case CXCursorKind.CXCursor_BinaryOperator:
					{
						var a = ProcessChildByIndex(info, 0);
						var b = ProcessChildByIndex(info, 1);
						var type = clang.getCursorBinaryOperatorKind(info.Handle);

						if (type.IsLogicalBinaryOperator())
						{
							AppendNonZeroCheck(a);
							AppendNonZeroCheck(b);
						}

						if (type.IsLogicalBooleanOperator())
						{
							a.Expression = a.Expression.Parentize();
							b.Expression = b.Expression.Parentize();
						}

						if (type.IsAssign() && type != CXBinaryOperatorKind.CXBinaryOperator_ShlAssign && type != CXBinaryOperatorKind.CXBinaryOperator_ShrAssign)
						{
							var typeInfo = info.ToTypeInfo();

							// Explicity cast right to left
							if (!typeInfo.IsPointer)
							{
								if (b.Info.CursorKind == CXCursorKind.CXCursor_ParenExpr && b.Info.CursorChildren.Count > 0)
								{
									var bb = ProcessChildByIndex(b.Info, 0);
									if (bb.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator &&
										clang.getCursorBinaryOperatorKind(bb.Info.Handle).IsLogicalBooleanOperator())
									{
										b = bb;
									}
								}

								var expr = b.Expression;
								if (AppendBoolToInt(b.Info, ref expr))
								{
									b.Expression = expr;
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
									case CXBinaryOperatorKind.CXBinaryOperator_Add:
										return a.Expression + "[" + b.Expression + "]";
								}
							}
						}

						if (a.IsPointer && (type == CXBinaryOperatorKind.CXBinaryOperator_Assign || type.IsBooleanOperator()) &&
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

						var type = clang.getCursorUnaryOperatorKind(info.Handle);
						var str = info.GetOperatorString();

						var typeInfo = info.ToTypeInfo();

						if (IsClass(typeInfo) && 
							(type == CXUnaryOperatorKind.CXUnaryOperator_AddrOf || type == CXUnaryOperatorKind.CXUnaryOperator_Deref))
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
						var functionName = functionExpr.Expression.Deparentize().UpdateNativeCall();

						// Retrieve arguments
						var args = new List<string>();
						for (var i = 1; i < size; ++i)
						{
							var argExpr = ProcessChildByIndex(info, i);

							var expr = argExpr.Expression;
							if (AppendBoolToInt(argExpr.Info, ref expr))
							{
								argExpr.Expression = expr;
							}
							else if (!argExpr.IsPointer)
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
								if (AppendBoolToInt(child.Info, ref ret))
								{
									return "return " + ret;
								}

								return "return " + ret.ApplyCast(ToRoslynString(tt));
							}
						}

						if (tt.IsPointer && ret.Deparentize() == "0")
						{
							ret = "null";
						}

						var exp = string.IsNullOrEmpty(ret) ? "return" : "return " + ret;

						return exp;
					}
				case CXCursorKind.CXCursor_IfStmt:
					{
						var conditionExpr = ProcessChildByIndex(info, 0);
						AppendNonZeroCheck(conditionExpr);

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
								it = ProcessChildByIndex(info, 0);
								execution = ProcessChildByIndex(info, 1);
								break;
							case 3:
								var expr = ProcessChildByIndex(info, 0);
								if (expr.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator &&
									clang.getCursorBinaryOperatorKind(expr.Info.Handle).IsBooleanOperator())
								{
									condition = expr;
								}
								else
								{
									start = expr;
								}

								expr = ProcessChildByIndex(info, 1);
								if (expr.Info.CursorKind == CXCursorKind.CXCursor_BinaryOperator &&
									clang.getCursorBinaryOperatorKind(expr.Info.Handle).IsBooleanOperator())
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

						var sci = start.GetExpression().EnsureStatementEndWithSemicolon() + condition.GetExpression().EnsureStatementEndWithSemicolon() + it.GetExpression();
						return "for (" + sci + ") " +
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
						AppendNonZeroCheck(expr);

						return "do " + execution.Expression.EnsureStatementFinished() + " while (" + expr.Expression + ")";
					}

				case CXCursorKind.CXCursor_WhileStmt:
					{
						var expr = ProcessChildByIndex(info, 0);
						AppendNonZeroCheck(expr);
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
								var op = clang.getCursorBinaryOperatorKind(condition.Info.Handle);

								if (op == CXBinaryOperatorKind.CXBinaryOperator_Or || op == CXBinaryOperatorKind.CXBinaryOperator_And)
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
							return @"0";
						}

						return r.ToString();
					}

				case CXCursorKind.CXCursor_StringLiteral:
					return info.Spelling.StartsWith("L") ? info.Spelling.Substring(1) : info.Spelling;
				case CXCursorKind.CXCursor_VarDecl:
					{
						var varDecl = (VarDecl)info;
						string name;
						var expr = ProcessDeclaration(varDecl, out name);

						if (_state == State.Functions && 
							varDecl.StorageClass == CX_StorageClass.CX_SC_Static)
						{
							_localVariablesMap[varDecl.Spelling.FixSpecialWords()] = name;

							expr = "public static " + expr;
							Result.GlobalVariables[name] = (FieldDeclarationSyntax)ParseMemberDeclaration(expr);
							return string.Empty;
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
						} else
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

						if (expr == "0" && tt.IsPointer)
						{
							// null
						} else if (csType != child.CsType)
						{
							// cast
							expr = expr.ApplyCast(csType);
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

						var expr = ProcessChildByIndex(info, size - 1);

						var typeInfo = info.ToTypeInfo();
						if (typeInfo.IsPointer && expr.Expression.Deparentize() == "0")
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
				var type = clang.getCursorBinaryOperatorKind(info.Info.Handle);
				if (type == CXBinaryOperatorKind.CXBinaryOperator_Comma)
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