using ClangSharp;
using System;

namespace Hebron.Roslyn
{
	public partial class RoslynCodeConverter
	{
		public TranslationUnit TranslationUnit { get; }
		public RoslynConversionParameters Parameters { get; }
		private RoslynConversionResult Result { get; }

		private RoslynCodeConverter(RoslynConversionParameters parameters)
		{
			Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
			TranslationUnit = Utility.Compile(parameters.InputPath, parameters.Defines);
			Result = new RoslynConversionResult();
		}

		public RoslynConversionResult Convert()
		{
			ConvertEnums();
			ConvertFunctions();

			return Result;
		}

		public static RoslynConversionResult Convert(RoslynConversionParameters parameters)
		{
			var converter = new RoslynCodeConverter(parameters);
			return converter.Convert();
		}
	}
}
