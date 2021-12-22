using ClangSharp;
using ClangSharp.Interop;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Type = ClangSharp.Type;

namespace Hebron.Roslyn
{
	public partial class RoslynCodeConverter
	{
		private readonly Dictionary<string, DelegateDeclarationSyntax> DelegateMap = new Dictionary<string, DelegateDeclarationSyntax>();
		private readonly HashSet<string> Classes = new HashSet<string>();
		private Dictionary<string, Stack<string>> _variables = new Dictionary<string, Stack<string>>();

		public TranslationUnit TranslationUnit { get; }
		public RoslynConversionParameters Parameters { get; }
		private RoslynConversionResult Result { get; }

		private RoslynCodeConverter(RoslynConversionParameters parameters)
		{
			Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
			TranslationUnit = Utility.Compile(parameters.InputPath, parameters.Defines);
			Result = new RoslynConversionResult();
		}

		internal bool IsClass(string name) => Classes.Contains(name);
		internal bool IsClass(BaseTypeInfo typeInfo) => IsClass(typeInfo.TypeName);

		public RoslynConversionResult Convert()
		{
			ConvertEnums();
			ConvertStructs();
			ConvertGlobalVariables();
			ConvertFunctions();

			return Result;
		}

		public static RoslynConversionResult Convert(RoslynConversionParameters parameters)
		{
			var converter = new RoslynCodeConverter(parameters);
			return converter.Convert();
		}

		public string ToRoslynTypeName(BaseTypeInfo type, bool declareMissingTypes = false)
		{
			var asPrimitiveType = type as PrimitiveTypeInfo;
			if (asPrimitiveType != null)
			{
				switch (asPrimitiveType.PrimitiveType)
				{
					case PrimitiveType.Boolean:
						return "bool";
					case PrimitiveType.Byte:
						return "byte";
					case PrimitiveType.Sbyte:
						return "sbyte";
					case PrimitiveType.UShort:
						return "ushort";
					case PrimitiveType.Short:
						return "short";
					case PrimitiveType.Float:
						return "float";
					case PrimitiveType.Double:
						return "double";
					case PrimitiveType.Int:
						return "int";
					case PrimitiveType.Uint:
						return "uint";
					case PrimitiveType.Long:
						return "long";
					case PrimitiveType.ULong:
						return "ulong";
					case PrimitiveType.Void:
						return "void";
				}
			}

			var asStructType = type as StructTypeInfo;
			if (asStructType != null)
			{
				return asStructType.StructName;
			}

			var asFunctionPointerType = (FunctionPointerTypeInfo)type;
			if (!declareMissingTypes)
			{
				return asFunctionPointerType.TypeName;
			}

			var key = asFunctionPointerType.TypeString;
			DelegateDeclarationSyntax decl;
			if (!DelegateMap.TryGetValue(key, out decl))
			{
				var name = "delegate" + DelegateMap.Count;
				decl = DelegateDeclaration(ParseTypeName(ToRoslynString(asFunctionPointerType.ReturnType)), name)
					.MakePublic();

				if (asFunctionPointerType.Arguments != null)
				{
					for(var i = 0; i < asFunctionPointerType.Arguments.Length; ++i)
					{
						var arg = asFunctionPointerType.Arguments[i];
						var argName = "arg" + i;
						decl = decl.AddParameterListParameters(Parameter(Identifier(argName)).WithType(ParseTypeName(ToRoslynString(arg))));
					}
				}

				DelegateMap[key] = decl;
				Result.Delegates[name] = decl;
			}

			return decl.Identifier.Text;
		}

		public string ToRoslynTypeName(CXType type, bool declareMissingTypes = false) => ToRoslynTypeName(type.ToTypeInfo(), declareMissingTypes);
		public string ToRoslynTypeName(Type type, bool declareMissingTypes = false) => ToRoslynTypeName(type.Handle, declareMissingTypes);

		public string ToRoslynString(BaseTypeInfo type, bool declareMissingTypes = false)
		{
			var typeName = ToRoslynTypeName(type, declareMissingTypes);

			var asStruct = type as StructTypeInfo;
			if (asStruct != null && IsClass(typeName))
			{
				return typeName;
			}

			if (_state == State.Functions &&
				type.ConstantArraySizes.Length == 1 &&
				(type is PrimitiveTypeInfo || (asStruct != null && !IsClass(typeName))))
			{
				// stackalloc
			} else
			if ((type is PrimitiveTypeInfo || asStruct != null) &&
				type.IsArray)
			{
				return BuildUnsafeArrayTypeName(type, declareMissingTypes);
			}

			var asFunctionPointerType = type as FunctionPointerTypeInfo;
			if (asFunctionPointerType != null)
			{
				return typeName;
			}

			var sb = new StringBuilder();
			sb.Append(typeName);

			for (var i = 0; i < type.PointerCount; ++i)
			{
				sb.Append("*");
			}

			return sb.ToString();
		}

		public string ToRoslynString(CXType type, bool declareMissingTypes = false) => ToRoslynString(type.ToTypeInfo(), declareMissingTypes);
		public string ToRoslynString(Type type, bool declareMissingTypes = false) => ToRoslynString(type.Handle, declareMissingTypes);

		public string BuildUnsafeArrayTypeName(BaseTypeInfo typeInfo, 
			Func<string, bool> isDeclaredChecker,
			Action<string, TypeDeclarationSyntax> declarationAdder)
		{
			var typeName = ToRoslynTypeName(typeInfo);
			var arrayTypeName = "UnsafeArray" + typeInfo.ConstantArraySizes.Length + "D" + typeName.UppercaseFirstLetter();

			var isDeclared = isDeclaredChecker(arrayTypeName);
			if (!isDeclared)
			{
				string template;
				switch (typeInfo.ConstantArraySizes.Length)
				{
					case 1:
						template = Resources.UnsafeArray1DTemplate;
						break;
					case 2:
						template = Resources.UnsafeArray2DTemplate;
						break;
					default:
						throw new Exception(string.Format("Arrays with {0} dimensions arent supported.", typeInfo.ConstantArraySizes.Length));
				}

				var declExpr = template.
					Replace("$arrayTypeName$", arrayTypeName).
					Replace("$typeName$", typeName);

				var decl = (TypeDeclarationSyntax)ParseMemberDeclaration(declExpr);
				declarationAdder(arrayTypeName, decl);
			}

			return arrayTypeName;
		}

		public string BuildUnsafeArrayTypeName(BaseTypeInfo typeInfo, bool declareMissingTypes = false) =>
			BuildUnsafeArrayTypeName(typeInfo,
			n => declareMissingTypes ? Result.Structs.ContainsKey(n) : true,
			(n, decl) => Result.Structs[n] = decl);

		public string ToUnsafeArrayDeclaration(BaseTypeInfo typeInfo, string name, 
			Func<string, bool> isDeclaredChecker, 
			Action<string, TypeDeclarationSyntax> declarationAdder)
		{
			var arrayTypeName = BuildUnsafeArrayTypeName(typeInfo, isDeclaredChecker, declarationAdder);

			var sb = new StringBuilder();
			for (var i = 0; i < typeInfo.ConstantArraySizes.Length; ++i)
			{
				sb.Append(typeInfo.ConstantArraySizes[i]);
				if (i < typeInfo.ConstantArraySizes.Length - 1)
				{
					sb.Append(",");
				}
			}

			var initializer = "new " + arrayTypeName + "(" + sb.ToString() + ")";

			return arrayTypeName + " " + name + " = " + initializer + ";";
		}
	}
}
