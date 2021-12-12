using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace Hebron.Roslyn
{
	public static partial class RoslynCodeConverter
	{
		public static RoslynConversionResult Convert(RoslynConversionParameters parameters)
		{
			if (parameters == null)
			{
				throw new ArgumentNullException(nameof(parameters));
			}

			var translationUnit = Utility.Compile(parameters.InputPath, parameters.Defines);

			Dictionary<string, EnumDeclarationSyntax> namedEnums;
			Dictionary<string, AssignmentExpressionSyntax> unnamedEnumValues;

			translationUnit.ConvertEnums(parameters.SkipEnums, out namedEnums, out unnamedEnumValues);

			return null;
		}
	}
}
