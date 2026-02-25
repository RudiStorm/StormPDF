namespace StormPDF.Utilities;

public static class FileReadyUtilities
{
	public static async Task WaitForFileReadyAsync(string filePath, int attempts = 12, int delayMs = 50)
	{
		for (var attempt = 0; attempt < attempts; attempt++)
		{
			if (File.Exists(filePath))
			{
				try
				{
					using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
					if (stream.Length > 0)
					{
						return;
					}
				}
				catch (IOException)
				{
				}
			}

			await Task.Delay(delayMs);
		}
	}
}
