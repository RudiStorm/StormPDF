using System.Diagnostics;

namespace StormPDF.Services;

internal static class GhostscriptPdfOptimizer
{
	public static async Task OptimizeLossyAsync(string inputPdfPath, string outputPdfPath, CancellationToken cancellationToken = default)
	{
		if (!File.Exists(inputPdfPath))
		{
			throw new FileNotFoundException("PDF not found.", inputPdfPath);
		}

		if (!TryResolveGhostscriptBinaryPath(out var executablePath))
		{
			throw new InvalidOperationException("Lossy optimization requires Ghostscript. On Windows, bundle tools/ghostscript/win/gswin64c.exe (or gswin32c.exe), or install gs on PATH.");
		}

		EnsureOutputDirectory(outputPdfPath);

		var startInfo = new ProcessStartInfo
		{
			FileName = executablePath,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		startInfo.ArgumentList.Add("-sDEVICE=pdfwrite");
		startInfo.ArgumentList.Add("-dCompatibilityLevel=1.4");
		startInfo.ArgumentList.Add("-dPDFSETTINGS=/ebook");
		startInfo.ArgumentList.Add("-dNOPAUSE");
		startInfo.ArgumentList.Add("-dQUIET");
		startInfo.ArgumentList.Add("-dBATCH");
		startInfo.ArgumentList.Add("-dDetectDuplicateImages=true");
		startInfo.ArgumentList.Add("-dCompressFonts=true");
		startInfo.ArgumentList.Add($"-sOutputFile={outputPdfPath}");
		startInfo.ArgumentList.Add(inputPdfPath);

		using var process = new Process { StartInfo = startInfo };
		if (!process.Start())
		{
			throw new InvalidOperationException("Could not start Ghostscript process.");
		}

		var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);
		if (process.ExitCode != 0)
		{
			var standardError = await standardErrorTask;
			throw new InvalidOperationException($"Ghostscript failed: {standardError.Trim()}");
		}
	}

	private static bool TryResolveGhostscriptBinaryPath(out string resolvedPath)
	{
		if (OperatingSystem.IsWindows())
		{
			var appBaseDirectory = AppContext.BaseDirectory;
			var bundledCandidates = new[]
			{
				Path.Combine(appBaseDirectory, "tools", "ghostscript", "win", "gswin64c.exe"),
				Path.Combine(appBaseDirectory, "tools", "ghostscript", "win", "gswin32c.exe")
			};

			foreach (var bundledCandidate in bundledCandidates)
			{
				if (File.Exists(bundledCandidate))
				{
					resolvedPath = bundledCandidate;
					return true;
				}
			}
		}

		var candidates = OperatingSystem.IsWindows()
			? new[] { "gswin64c.exe", "gswin32c.exe", "gs.exe" }
			: new[] { "gs" };

		foreach (var candidate in candidates)
		{
			if (TryFindExecutableOnPath(candidate, out resolvedPath))
			{
				return true;
			}
		}

		resolvedPath = string.Empty;
		return false;
	}

	private static bool TryFindExecutableOnPath(string fileName, out string fullPath)
	{
		var pathValue = Environment.GetEnvironmentVariable("PATH");
		if (string.IsNullOrWhiteSpace(pathValue))
		{
			fullPath = string.Empty;
			return false;
		}

		foreach (var pathPart in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var candidate = Path.Combine(pathPart, fileName);
			if (File.Exists(candidate))
			{
				fullPath = candidate;
				return true;
			}
		}

		fullPath = string.Empty;
		return false;
	}

	private static void EnsureOutputDirectory(string outputPdfPath)
	{
		var directory = Path.GetDirectoryName(outputPdfPath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}
	}
}
