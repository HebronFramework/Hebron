using ClangSharp.Interop;
using System.IO;

namespace Hebron
{
	public abstract class BaseProcessor
	{
		protected readonly CXTranslationUnit _translationUnit;
		protected int _indentLevel = 2;

		protected abstract TextWriter Writer { get; }

		protected BaseProcessor(CXTranslationUnit translationUnit)
		{
			_translationUnit = translationUnit;
		}

		public void WriteIndent()
		{
			if (Writer == null)
			{
				return;
			}

			for (var i = 0; i < _indentLevel; ++i)
			{
				Writer.Write("\t");
			}
		}

		public void IndentedWriteLine(string line)
		{
			if (Writer == null)
			{
				return;
			}

			WriteIndent();
			Writer.WriteLine(line);
		}

		public void IndentedWriteLine(string line, params object[] p)
		{
			if (Writer == null)
			{
				return;
			}

			WriteIndent();
			Writer.WriteLine(string.Format(line, p));
		}


		public void IndentedWrite(string data)
		{
			if (Writer == null)
			{
				return;
			}

			WriteIndent();
			Writer.Write(data);
		}

		public void WriteLine()
		{
			if (Writer == null)
			{
				return;
			}

			Writer.WriteLine();
		}

		public void WriteLine(string s)
		{
			if (Writer == null)
			{
				return;
			}

			Writer.WriteLine(s);
		}

		public void WriteLine(string s, params object[] p)
		{
			if (Writer == null)
			{
				return;
			}

			Writer.WriteLine(string.Format(s, p));
		}

		public void Write(string s)
		{
			if (Writer == null)
			{
				return;
			}

			Writer.Write(s);
		}
	}
}