namespace Hebron.Roslyn
{
	public enum UnsafeArrayUsage
	{
		UseOrdinaryArray,
		UseUnsafeArray
	}

	public class RoslynConversionParameters: BaseConversionParameters
	{
		public UnsafeArrayUsage GlobalVariablesUnsafeArrayUsage;

		public string[] Classes { get; set; }

		public RoslynConversionParameters()
		{
			Classes = new string[0];
		}
	}
}
