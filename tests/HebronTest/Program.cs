using Hebron.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Hebron
{
    class Program
    {
        static void Main(string[] args)
        {
            var parameters = new RoslynConversionParameters
            {
                Defines = new[]
                {
                    "STBI_NO_SIMD",
                    "STBI_NO_PIC",
                    "STBI_NO_PNM",
                    "STBI_NO_STDIO",
                    "STB_IMAGE_IMPLEMENTATION",
                },
                InputPath = @"D:\Projects\StbSharp\stb\stb_image.h"
            };


//            var result = TextCodeConverter.Convert(parameters.InputPath, parameters.Defines);


            var result = RoslynCodeConverter.Convert(parameters);

            var cls = ClassDeclaration("StbImage")
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.UnsafeKeyword));

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

            foreach (var pair in result.Functions)
            {
                cls = cls.AddMembers(pair.Value);
            }

            var ns = NamespaceDeclaration(ParseName("StbImageSharp")).AddMembers(cls);

            string s;
            using (var sw = new StringWriter())
            {
                ns.NormalizeWhitespace().WriteTo(sw);

                s = sw.ToString();
            }

            File.WriteAllText(@"D:\Projects\Chaos\RoslynTest\RoslynTest\StbImage.Generated.cs", s);
        }
    }
}