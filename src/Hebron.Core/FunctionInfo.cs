using System.Collections.Generic;

namespace Hebron
{
	public class FunctionInfo
	{
		private readonly List<FunctionArgumentType> _argumentTypes = new List<FunctionArgumentType>();

		public string Name { get; set; }
		public string Signature { get; set; }
		public List<FunctionArgumentType> ArgumentTypes
		{
			get
			{
				return _argumentTypes;
			}
		}
	}
}
