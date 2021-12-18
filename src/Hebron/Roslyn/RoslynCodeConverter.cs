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

		public TranslationUnit TranslationUnit { get; }
		public RoslynConversionParameters Parameters { get; }
		private RoslynConversionResult Result { get; }

		private RoslynCodeConverter(RoslynConversionParameters parameters)
		{
			Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
			TranslationUnit = Utility.Compile(parameters.InputPath, parameters.Defines);
			Result = new RoslynConversionResult();
		}

		private bool IsClass(string name) => Classes.Contains(name);

		public RoslynConversionResult Convert()
		{
			ConvertEnums();
			ConvertStructs();
			ConvertFunctions();

			return Result;
		}

		public static RoslynConversionResult Convert(RoslynConversionParameters parameters)
		{
			var converter = new RoslynCodeConverter(parameters);
			return converter.Convert();
		}

		public string ToRoslynTypeName(BaseTypeInfo type)
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

		public string ToRoslynTypeName(CXType type) => ToRoslynTypeName(type.ToTypeInfo());
		public string ToRoslynTypeName(Type type) => ToRoslynTypeName(type.Handle);

		public string ToRoslynString(BaseTypeInfo type)
		{
			var typeName = ToRoslynTypeName(type);

			var asStruct = type as StructTypeInfo;
			if (asStruct != null && Classes.Contains(typeName))
			{
				return typeName;
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

		public string ToRoslynString(CXType type) => ToRoslynString(type.ToTypeInfo());
		public string ToRoslynString(Type type) => ToRoslynString(type.Handle);

		public VariableDeclarationSyntax VariableDeclaration2(Type type, string name, EqualsValueClauseSyntax initializer = null)
		{
			var vd = VariableDeclarator(name);
			if (initializer != null)
			{
				vd = vd.WithInitializer(initializer);
			}

			return VariableDeclaration(ParseTypeName(ToRoslynString(type))).AddVariables(vd);
		}
	}
}
