using System.Collections.Generic;

namespace Hebron
{
	public enum PrimitiveType
	{
		Boolean,
		Byte,
		Sbyte,
		UShort,
		Short,
		Float,
		Double,
		Int,
		Uint,
		Long,
		ULong,
		Void
	}

	public class TypeInfo
	{
		public PrimitiveType? PrimitiveType;
		public string StructName;
		public int PointerCount;
	}

	public interface IOutput
	{
		void IntegerConstant(string name, int value);
		void Enum(string name, Dictionary<string, int?> values);
		void Function(string name, TypeInfo returnType, Dictionary<string, TypeInfo> parameters);
	}
}
