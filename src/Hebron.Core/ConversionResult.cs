using System.Collections.Generic;

namespace Hebron
{
	public class ConversionResult
	{
		public readonly Dictionary<string, string> Enums = new Dictionary<string, string>();
		public readonly Dictionary<string, string> Constants = new Dictionary<string, string>();
		public readonly Dictionary<string, string> GlobalVariables = new Dictionary<string, string>();
		public readonly Dictionary<string, string> Structs = new Dictionary<string, string>();
		public readonly Dictionary<string, string> Methods = new Dictionary<string, string>();
	}
}
