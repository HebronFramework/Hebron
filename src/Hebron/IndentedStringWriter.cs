using System.IO;

namespace Hebron
{
	public class IndentedStringWriter
	{
		private readonly StringWriter Writer = new StringWriter();

		public int IndentLevel { get; set; } = 0;
		public string Result => Writer.ToString();

		public void WriteIndent()
		{
			if (Writer == null)
			{
				return;
			}

			for (var i = 0; i < IndentLevel; ++i)
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

		public override string ToString() => Result;
	}
}