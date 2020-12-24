using System;
using System.IO;

using Microsoft.Data.Sqlite;

namespace TestMicrosoftDataSQLite
{
	public static class Test
	{
		public static void Run()
		{
			using var tempFile = new TempFile("db.sqlite");
			using var dbConnection = new SqliteConnection($"Data Source={tempFile.FilePath}");

			dbConnection.Open();
		}

		public class TempFile : IDisposable
		{
			public TempFile(string fileName)
			{
				FilePath = Path.Combine(Path.GetTempPath(), fileName);
			}

			public string FilePath { get; }

			public void Dispose()
			{
				if (File.Exists(FilePath))
					File.Delete(FilePath);
			}
		}
	}
}
