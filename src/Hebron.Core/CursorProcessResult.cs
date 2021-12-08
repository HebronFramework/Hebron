using System;

namespace Hebron
{
	public class CursorProcessResult
	{
		private readonly CursorInfo _info;

		public CursorInfo Info
		{
			get { return _info; }
		}

		public string Expression { get; set; }

		public CursorProcessResult(CursorInfo info)
		{
			if (info == null)
			{
				throw new ArgumentNullException("info");
			}

			_info = info;
		}
	}
}
