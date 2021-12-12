namespace Hebron
{
	public class BaseConversionParameters
	{
		public string InputPath { get; set; }
		public string[] Defines { get; set; }

		/// <summary>
		/// Names of enums to skip
		/// </summary>
		public string[] SkipEnums { get; set; }

		/// <summary>
		/// Names of structs to skip
		/// </summary>
		public string[] SkipStructs { get; set; }

		/// <summary>
		/// Names of global variables to skip
		/// </summary>
		public string[] SkipGlobalVariables { get; set; }

		/// <summary>
		/// Names of functions to skip
		/// </summary>
		public string[] SkipFunctions { get; set; }
		
		public BaseConversionParameters()
		{
			Defines = new string[0];
			SkipEnums = new string[0];
			SkipStructs = new string[0];
			SkipGlobalVariables = new string[0];
			SkipFunctions = new string[0];
		}
	}
}