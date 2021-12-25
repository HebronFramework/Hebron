using System.Collections.Generic;

namespace Hebron.Rust
{
	public class RustConversionResult
	{
		public readonly Dictionary<string, string> UnnamedEnumValues = new Dictionary<string, string>();
		public readonly Dictionary<string, string> GlobalVariables = new Dictionary<string, string>();
		public readonly Dictionary<string, string> Structs = new Dictionary<string, string>();
		public readonly Dictionary<string, string> StructDefaults = new Dictionary<string, string>();
		public readonly Dictionary<string, string> Functions = new Dictionary<string, string>();
	}
}
