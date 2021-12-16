using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Hebron.Roslyn
{
	public class RoslynConversionResult
	{
		public readonly Dictionary<string, EnumDeclarationSyntax> NamedEnums = new Dictionary<string, EnumDeclarationSyntax>();
		public readonly Dictionary<string, FieldDeclarationSyntax> UnnamedEnumValues = new Dictionary<string, FieldDeclarationSyntax>();
		public readonly Dictionary<string, TypeDeclarationSyntax> Structs = new Dictionary<string, TypeDeclarationSyntax>();
		public readonly Dictionary<string, MethodDeclarationSyntax> Functions = new Dictionary<string, MethodDeclarationSyntax>();
	}
}
