using Hebron.Roslyn;
using System;

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


            // var result = TextCodeConverter.Convert(parameters.InputPath, parameters.Defines);

            var result = RoslynCodeConverter.Convert(parameters);

            Console.WriteLine(result);
        }
    }
}