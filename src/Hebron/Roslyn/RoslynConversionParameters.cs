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
	}
}
