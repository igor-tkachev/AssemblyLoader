using System;
using System.Drawing.Printing;

namespace TestSystemDrawing
{
	public static class Test
	{
		public static void Run()
		{
			var pd = new PrintDocument();
			_ = typeof(PrintDocument).Assembly;
		}
	}
}
