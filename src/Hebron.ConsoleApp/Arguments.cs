using Microsoft.CodeAnalysis;
using Ookii.CommandLine;
using System.ComponentModel;

namespace Hebron.ConsoleApp;

[GeneratedParser]
[ParseOptions(IsPosix = true)]
partial class Arguments
{
	[CommandLineArgument(IsPositional = true)]
	[Description("The path to the header file.")]
	public required string InputPath { get; init; }

	[CommandLineArgument(IsPositional = true)]
	[Description("The destination file path for output.")]
	public required string OutputPath { get; init; }

	[CommandLineArgument]
	[Description("The namespace for the generated output.")]
	public string? Namespace { get; set; }

	[CommandLineArgument]
	[Description("The class name for the generated output.")]
	public string? ClassName { get; set; }

	[CommandLineArgument]
	[Description("The set of defines.")]
	public string[]? Defines { get; set; }

	[CommandLineArgument]
	[Description("The set of global variables to skip.")]
	public string[]? SkipGlobalVariables { get; set; }

	[CommandLineArgument]
	[Description("The set of functions to skip.")]
	public string[]? SkipFunctions { get; set; }

	[CommandLineArgument]
	[Description("The set of string pairs to use in replacement after generating code.")]
	public string[]? Replacements { get; set; }

	public bool ReplacementsInvalid => Replacements != null && Replacements.Length % 2 != 0;

	public string GetNamespace()
	{
		return string.IsNullOrEmpty(Namespace) ? "Namespace" : Namespace;
	}

	public string GetClassName()
	{
		return string.IsNullOrEmpty(ClassName) ? "Class" : ClassName;
	}
}
