using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Hebron.Rust
{
	internal static class RustUtility
	{
		private static readonly HashSet<string> _specialWords = new HashSet<string>(new[]
		{
			"type"
		});

		public static string FixSpecialWords(this string name)
		{
			if (_specialWords.Contains(name))
			{
				name = "_" + name + "_";
			}

			return name;
		}

		public static string ApplyCast(this string expr, string type)
		{
			if (string.IsNullOrEmpty(expr))
			{
				return expr;
			}

			var lastCast = string.Empty;
			var dexpr = expr.Deparentize();

			var m = Regex.Match(dexpr, @"^\((\w+)\)(\(.+\))$");
			if (m.Success)
			{
				lastCast = m.Groups[1].Value;
				var val = m.Groups[2].Value;

				if (!val.CorrectlyParentized())
				{
					lastCast = string.Empty;
				}
			}

			if (!string.IsNullOrEmpty(lastCast) && string.CompareOrdinal(lastCast, type) == 0)
			{
				return expr;
			}

			return type.Parentize() + expr.Parentize();
		}

		internal static string GetExpression(this CursorProcessResult cursorProcessResult)
		{
			return cursorProcessResult != null ? cursorProcessResult.Expression : string.Empty;
		}

		public static string UpdateNativeCall(this string functionName)
		{
			if (functionName.IsNativeFunctionName())
			{
				return "CRuntime." + functionName;
			}

			return functionName;
		}

		public static string GetDefaltValue(this BaseTypeDescriptor type)
		{
			var asPrimitive = type as PrimitiveTypeInfo;
			if (asPrimitive != null)
			{
				switch (asPrimitive.PrimitiveType)
				{
					case PrimitiveType.Boolean:
						return "false";
					case PrimitiveType.Byte:
						return "0";
					case PrimitiveType.Sbyte:
						return "0";
					case PrimitiveType.UShort:
						return "0";
					case PrimitiveType.Short:
						return "0";
					case PrimitiveType.Float:
						return "0";
					case PrimitiveType.Double:
						return "0";
					case PrimitiveType.Int:
						return "0";
					case PrimitiveType.Uint:
						return "0";
					case PrimitiveType.Long:
						return "0";
					case PrimitiveType.ULong:
						return "0";
				}
			}

			var asStruct = type as StructTypeInfo;
			if (asStruct != null)
			{
				return asStruct.StructName + "::default()";
			}

			throw new Exception(string.Format("Unable to create default value for type {0}", type.ToString()));
		}

		public static string GetDefaltValue(this TypeInfo type)
		{
			if (type.IsArray)
			{
				var sb = new StringBuilder();
				if (type.ConstantArraySizes.Length > 0)
				{
					sb.Append(new string('[', type.ConstantArraySizes.Length));
					sb.Append(GetDefaltValue(type.TypeDescriptor));
					for (var i = 0; i < type.ConstantArraySizes.Length; ++i)
					{
						sb.Append(";");
						sb.Append(type.ConstantArraySizes[i]);
						sb.Append("]");
					}

					return sb.ToString();
				}
			}

			if (type.IsPointer)
			{
				return "std::ptr::null_mut()";
			}

			return GetDefaltValue(type.TypeDescriptor);
		}
	}
}