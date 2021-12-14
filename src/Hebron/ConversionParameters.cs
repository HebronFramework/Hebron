namespace Hebron
{
	public class ConversionParameters
	{
		public string InputPath { get; set; }
		public string[] Defines { get; set; } = new string[0];

		/// <summary>
		/// Names of enums to skip
		/// </summary>
		public string[] SkipEnums { get; set; } = new string[0];

		/// <summary>
		/// Names of structs to skip
		/// </summary>
		public string[] SkipStructs { get; set; } = new string[0];

		/// <summary>
		/// Names of global variables to skip
		/// </summary>
		public string[] SkipGlobalVariables { get; set; } = new string[0];

		/// <summary>
		/// Names of functions to skip
		/// </summary>
		public string[] SkipFunctions { get; set; } = new string[0];

		/// <summary>
		/// Names of structs that should be converted as classes
		/// </summary>
		public string[] Classes { get; } = new string[0];
	}
}