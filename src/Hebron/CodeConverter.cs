using ClangSharp;
using System;

namespace Hebron
{
	public partial class CodeConverter
	{
		public TranslationUnit TranslationUnit { get; }
		public ConversionParameters Parameters { get; }
		public IOutput Output { get; }

		private CodeConverter(ConversionParameters parameters, IOutput output)
		{
			Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
			Output = output ?? throw new ArgumentNullException(nameof(output));
			TranslationUnit = Utility.Compile(parameters.InputPath, parameters.Defines);
		}

		public void Convert()
		{
			ConvertEnums();
			ConvertFunctions();
		}

		public static void Convert(ConversionParameters parameters, IOutput output)
		{
			var converter = new CodeConverter(parameters, output);
			converter.Convert();
		}
	}
}
