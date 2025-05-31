using Hebron.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Hebron.ConsoleApp;

internal static class Program
{
	static void Main(string[] args)
	{
		var arguments = Arguments.Parse(args);
		if (arguments == null)
		{
			return;
		}

		if (arguments.ReplacementsInvalid)
		{
			Console.Error.WriteLine("Replacements must be in pairs of strings.");
			return;
		}

		var parameters = new RoslynConversionParameters
		{
			Defines = arguments.Defines ?? [],
			InputPath = arguments.InputPath,
			SkipGlobalVariables = arguments.SkipGlobalVariables ?? [],
			SkipFunctions = arguments.SkipFunctions ?? [],
		};

		var result = RoslynCodeConverter.Convert(parameters);

		var cls = SyntaxFactory.ClassDeclaration(arguments.GetClassName())
			.AddModifiers(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword));

		foreach (var pair in result.NamedEnums)
		{
			cls = cls.AddMembers(pair.Value);
		}

		foreach (var pair in result.UnnamedEnumValues)
		{
			cls = cls.AddMembers(pair.Value);
		}

		foreach (var pair in result.Delegates)
		{
			cls = cls.AddMembers(pair.Value);
		}

		foreach (var pair in result.Structs)
		{
			cls = cls.AddMembers(pair.Value);
		}

		foreach (var pair in result.GlobalVariables)
		{
			cls = cls.AddMembers(pair.Value);
		}

		foreach (var pair in result.Functions)
		{
			cls = cls.AddMembers(pair.Value);
		}

		var ns = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(arguments.GetNamespace())).AddMembers(cls);

		string s;
		using (var sw = new StringWriter())
		{
			ns.NormalizeWhitespace().WriteTo(sw);

			s = sw.ToString();
		}

		if (arguments.Replacements != null)
		{
			for (var i = 0; i < arguments.Replacements.Length; i += 2)
			{
				s = s.Replace(arguments.Replacements[i], arguments.Replacements[i + 1]);
			}
		}

		File.WriteAllText(arguments.OutputPath, s);
	}
}
