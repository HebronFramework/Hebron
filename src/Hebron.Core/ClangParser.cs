using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hebron
{
	public class ClangParser
	{
		public unsafe ConversionResult Process(ConversionParameters parameters)
		{
			if (parameters == null)
			{
				throw new ArgumentNullException("parameters");
			}

			var arr = new List<string>();

			foreach (var d in parameters.Defines)
			{
				arr.Add("-D" + d);
			}

			//			arr.Add("-I" + @"D:\Develop\Microsoft Visual Studio 12.0\VC\include");

			var createIndex = CXIndex.Create();
			CXUnsavedFile unsavedFile;

			CXTranslationUnit tu;
			var res = CXTranslationUnit.TryParse(createIndex,
				parameters.InputPath,
				arr.ToArray(),
				new CXUnsavedFile[0],
				CXTranslationUnit_Flags.CXTranslationUnit_None,
				out tu);

			var numDiagnostics = clang.getNumDiagnostics(tu);
			for (uint i = 0; i < numDiagnostics; ++i)
			{
				var diag = clang.getDiagnostic(tu, i);
				var str =
					clang.formatDiagnostic(diag,
						(uint)
							(CXDiagnosticDisplayOptions.CXDiagnostic_DisplaySourceLocation |
							 CXDiagnosticDisplayOptions.CXDiagnostic_DisplaySourceRanges)).ToString();
				Logger.LogLine(str);
				clang.disposeDiagnostic(diag);
			}

			if (res != CXErrorCode.CXError_Success)
			{
				var sb = new StringBuilder();

				sb.AppendLine(res.ToString());

				numDiagnostics = clang.getNumDiagnostics(tu);
				for (uint i = 0; i < numDiagnostics; ++i)
				{
					var diag = clang.getDiagnostic(tu, i);
					sb.AppendLine(clang.getDiagnosticSpelling(diag).ToString());
					clang.disposeDiagnostic(diag);
				}

				throw new Exception(sb.ToString());
			}

			// Process
			var cw = new ConversionProcessor(parameters, tu);
			var result = cw.Run();
/*			using (var tw = new StreamWriter("dump.txt"))
			{
				var dump = new DumpProcessor(tu, tw);
				dump.Run();
			}*/

			clang.disposeTranslationUnit(tu);
			clang.disposeIndex(createIndex);

			return result;
		}
	}
}