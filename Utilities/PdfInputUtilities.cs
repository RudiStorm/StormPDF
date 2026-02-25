namespace StormPDF.Utilities;

public static class PdfInputUtilities
{
	public static string BuildOutputPath(string inputPath, string suffix, DateTimeOffset? timestamp = null)
	{
		var directory = Path.GetDirectoryName(inputPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var baseName = Path.GetFileNameWithoutExtension(inputPath);
		var stamp = (timestamp ?? DateTimeOffset.Now).ToString("yyyyMMddHHmmss");
		return Path.Combine(directory, $"{baseName}_{suffix}_{stamp}.pdf");
	}

	public static bool TryParsePageSelection(string? input, out IReadOnlyCollection<int> pages, out string validationMessage)
	{
		var result = new SortedSet<int>();
		if (string.IsNullOrWhiteSpace(input))
		{
			pages = Array.Empty<int>();
			validationMessage = "Enter page numbers like 2,4-6.";
			return false;
		}

		var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		foreach (var part in parts)
		{
			if (part.Contains('-'))
			{
				var bounds = part.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				if (bounds.Length != 2 || !int.TryParse(bounds[0], out var start) || !int.TryParse(bounds[1], out var end) || start < 1 || end < start)
				{
					pages = Array.Empty<int>();
					validationMessage = $"Invalid page range: '{part}'.";
					return false;
				}

				for (var page = start; page <= end; page++)
				{
					result.Add(page);
				}
			}
			else if (int.TryParse(part, out var page) && page > 0)
			{
				result.Add(page);
			}
			else
			{
				pages = Array.Empty<int>();
				validationMessage = $"Invalid page number: '{part}'.";
				return false;
			}
		}

		pages = result.ToArray();
		validationMessage = string.Empty;
		return pages.Count > 0;
	}

	public static string BuildPageRange(IReadOnlyList<int> pages)
	{
		if (pages.Count == 0)
		{
			throw new ArgumentException("At least one page is required.", nameof(pages));
		}

		var ranges = new List<string>();
		var start = pages[0];
		var previous = pages[0];

		for (var index = 1; index < pages.Count; index++)
		{
			var current = pages[index];
			if (current == previous + 1)
			{
				previous = current;
				continue;
			}

			ranges.Add(start == previous ? start.ToString() : $"{start}-{previous}");
			start = current;
			previous = current;
		}

		ranges.Add(start == previous ? start.ToString() : $"{start}-{previous}");
		return string.Join(',', ranges);
	}
}
