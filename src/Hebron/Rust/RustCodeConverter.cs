using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Text;
using Type = ClangSharp.Type;

namespace Hebron.Rust
{
	public partial class RustCodeConverter
	{
		private enum State
		{
			Structs,
			GlobalVariables,
			Enums,
			Functions
		}

		private State _state = State.Functions;

		public TranslationUnit TranslationUnit { get; }
		public RustConversionParameters Parameters { get; }
		private RustConversionResult Result { get; }

		private RustCodeConverter(RustConversionParameters parameters)
		{
			Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
			TranslationUnit = Utility.Compile(parameters.InputPath, parameters.Defines);
			Result = new RustConversionResult();
		}

		public RustConversionResult Convert()
		{
			ConvertEnums();
			ConvertStructs();
			ConvertGlobalVariables();
//			ConvertFunctions();

			return Result;
		}

		public static RustConversionResult Convert(RustConversionParameters parameters)
		{
			var converter = new RustCodeConverter(parameters);
			return converter.Convert();
		}

		public string ToRustTypeName(BaseTypeDescriptor type)
		{
			var asPrimitiveType = type as PrimitiveTypeInfo;
			if (asPrimitiveType != null)
			{
				switch (asPrimitiveType.PrimitiveType)
				{
					case PrimitiveType.Boolean:
						return "bool";
					case PrimitiveType.Byte:
						return "u8";
					case PrimitiveType.Sbyte:
						return "i8";
					case PrimitiveType.UShort:
						return "u16";
					case PrimitiveType.Short:
						return "i16";
					case PrimitiveType.Float:
						return "f32";
					case PrimitiveType.Double:
						return "f64";
					case PrimitiveType.Int:
						return "i32";
					case PrimitiveType.Uint:
						return "u32";
					case PrimitiveType.Long:
						return "i64";
					case PrimitiveType.ULong:
						return "u64";
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

			var sb = new StringBuilder();
			sb.Append("fn(");
			if (asFunctionPointerType.Arguments != null)
			{
				for(var i = 0; i < asFunctionPointerType.Arguments.Length; ++i)
				{
					var arg = asFunctionPointerType.Arguments[i];
					var argName = "arg" + i;

					sb.Append(argName + ": " + ToRustString(arg));
					if (i < asFunctionPointerType.Arguments.Length - 1)
					{
						sb.Append(", ");
					}
				}
			}
			
			sb.Append(")");

			if (!asFunctionPointerType.ReturnType.IsVoid())
			{
				sb.Append(" -> ");
				sb.Append(ToRustString(asFunctionPointerType.ReturnType));
			}

			return sb.ToString();
		}

		public string ToRustTypeName(TypeInfo type) => ToRustTypeName(type.TypeDescriptor);
		public string ToRustTypeName(CXType type) => ToRustTypeName(type.ToTypeInfo());
		public string ToRustTypeName(Type type) => ToRustTypeName(type.Handle);

		public string ToRustString(TypeInfo type)
		{
			var typeName = ToRustTypeName(type);

			var sb = new StringBuilder();

			if (type.ConstantArraySizes.Length > 0)
			{
				sb.Append(new string('[', type.ConstantArraySizes.Length));
				sb.Append(typeName);
				for (var i = 0; i < type.ConstantArraySizes.Length; ++i)
				{
					sb.Append(";");
					sb.Append(type.ConstantArraySizes[i]);
					sb.Append("]");
				}

				return sb.ToString();
			}

			if (!type.IsPointer)
			{
				sb.Append(typeName);
			}
			else
			{
				sb.Append(new string('*', type.PointerCount));
				sb.Append("mut ");

				if (typeName == "void")
				{
					// Rust doesnt support void pointers
					typeName = "u8";
				}

				sb.Append(typeName);
			}

			return sb.ToString();
		}

		public string ToRustString(CXType type) => ToRustString(type.ToTypeInfo());
		public string ToRustString(Type type) => ToRustString(type.Handle);
	}
}
