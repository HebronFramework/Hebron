using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;
using Type = ClangSharp.Type;

namespace Hebron.Roslyn
{
	public partial class RoslynCodeConverter
	{
		private class DelegateInfo
		{
			public string Name;
			public FunctionPointerTypeInfo FunctionInfo;
		}

		private readonly Dictionary<string, DelegateInfo> DelegateMap = new Dictionary<string, DelegateInfo>();
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
		internal bool IsClass(TypeInfo typeInfo) => IsClass(typeInfo.TypeName);

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

		public string ToRoslynTypeName(TypeInfo type, bool declareMissingTypes = false)
		{
			var asPrimitiveType = type.TypeDescriptor as PrimitiveTypeInfo;
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

			var asStructType = type.TypeDescriptor as StructTypeInfo;
			if (asStructType != null)
			{
				return asStructType.StructName;
			}

			var asFunctionPointerType = (FunctionPointerTypeInfo)type.TypeDescriptor;
			if (!declareMissingTypes)
			{
				return asFunctionPointerType.TypeName;
			}

			var key = type.TypeString;
			DelegateInfo decl;
			if (!DelegateMap.TryGetValue(key, out decl))
			{
				var name = "delegate" + DelegateMap.Count;
				decl = new DelegateInfo
				{
					Name = name,
					FunctionInfo = asFunctionPointerType
				};
				DelegateMap[key] = decl;
			}

			return decl.Name;
		}

		public string ToRoslynTypeName(CXType type, bool declareMissingTypes = false) => ToRoslynTypeName(type.ToTypeInfo(), declareMissingTypes);
		public string ToRoslynTypeName(Type type, bool declareMissingTypes = false) => ToRoslynTypeName(type.Handle, declareMissingTypes);

		public string ToRoslynString(TypeInfo type, bool declareMissingTypes = false)
		{
			var typeName = ToRoslynTypeName(type, declareMissingTypes);

			var asStruct = type.TypeDescriptor as StructTypeInfo;
			if (asStruct != null && IsClass(typeName))
			{
				return typeName;
			}

			if (_state == State.Functions &&
				type.ConstantArraySizes.Length == 1 &&
				(type is PrimitiveTypeInfo || (asStruct != null && !IsClass(typeName))))
			{
				// stackalloc
			}

			var asFunctionPointerType = type.TypeDescriptor as FunctionPointerTypeInfo;
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

		public string BuildUnsafeArrayTypeName(TypeInfo typeInfo)
		{
			var typeName = ToRoslynTypeName(typeInfo);
			return "UnsafeArray" + typeInfo.ConstantArraySizes.Length + "D<" + typeName + ">";
		}
	}
}
