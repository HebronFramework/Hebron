namespace Hebron
{
	public class ConversionParameters
	{
		public string InputPath { get; set; }
		public string[] Defines { get; set; }

		public string OutputPath
		{
			get; set;
		}

		public bool IsPartial { get; set; }

		public bool AddGeneratedByHebron { get; set; }

		public string[] SkipStructs { get; set; }
		public string[] SkipGlobalVariables { get; set; }
		public string[] SkipFunctions { get; set; }
		public string[] Classes { get; set; }
		public string[] GlobalArrays { get; set; }

		public ConversionParameters()
		{
			IsPartial = true;
			Defines = new string[0];
			SkipStructs = new string[0];
			SkipGlobalVariables = new string[0];
			SkipFunctions = new string[0];
			Classes = new string[0];
			GlobalArrays = new string[0];
		}
	}
}