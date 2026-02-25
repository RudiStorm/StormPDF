using System.Diagnostics;
using StormPDF.Utilities;

namespace StormPDF.Services;

public sealed class QpdfPdfEngine : IPdfEngine
{
	public PdfEngineDependencyStatus GetDependencyStatus()
	{
		if (TryResolveQpdfBinaryPath(out var resolvedPath))
		{
			return new PdfEngineDependencyStatus(true, "qpdf is available.", resolvedPath);
		}

		var message = OperatingSystem.IsWindows()
			? "qpdf is missing. Add tools/qpdf/win/qpdf.exe or install qpdf on PATH."
			: "qpdf is missing. Add tools/qpdf/mac/qpdf (chmod +x) or install qpdf on PATH.";

		return new PdfEngineDependencyStatus(false, message, null);
	}

	public async Task ViewAsync(string pdfPath, CancellationToken cancellationToken = default)
	{
		if (!File.Exists(pdfPath))
		{
			throw new FileNotFoundException("PDF not found.", pdfPath);
		}

		var request = new OpenFileRequest("Open PDF", new ReadOnlyFile(pdfPath));
		if (!await Launcher.Default.OpenAsync(request))
		{
			throw new InvalidOperationException("Could not open PDF in system viewer.");
		}
	}

	public async Task MergeAsync(IReadOnlyList<string> inputPdfPaths, string outputPdfPath, CancellationToken cancellationToken = default)
	{
		if (inputPdfPaths.Count < 2)
		{
			throw new InvalidOperationException("At least two PDFs are required for merge.");
		}

		var arguments = new List<string> { "--empty", "--pages" };
		arguments.AddRange(inputPdfPaths);
		arguments.Add("--");
		arguments.Add(outputPdfPath);

		await RunQpdfAsync(arguments, cancellationToken);
	}

	public async Task DeletePagesAsync(string inputPdfPath, IReadOnlyCollection<int> pagesToDelete, string outputPdfPath, CancellationToken cancellationToken = default)
	{
		if (pagesToDelete.Count == 0)
		{
			throw new InvalidOperationException("No pages were provided for deletion.");
		}

		var totalPages = await GetPageCountAsync(inputPdfPath, cancellationToken);
		var pagesToKeep = Enumerable.Range(1, totalPages).Except(pagesToDelete).ToArray();
		if (pagesToKeep.Length == 0)
		{
			throw new InvalidOperationException("You cannot delete all pages from the PDF.");
		}

		var pageRange = PdfInputUtilities.BuildPageRange(pagesToKeep);
		var arguments = new List<string>
		{
			inputPdfPath,
			"--pages",
			inputPdfPath,
			pageRange,
			"--",
			outputPdfPath
		};

		await RunQpdfAsync(arguments, cancellationToken);
	}

	public async Task<int> GetPageCountAsync(string inputPdfPath, CancellationToken cancellationToken = default)
	{
		var output = await RunQpdfAsync(new[] { "--show-npages", inputPdfPath }, cancellationToken, captureOutput: true);
		if (!int.TryParse(output.Trim(), out var totalPages) || totalPages < 1)
		{
			throw new InvalidOperationException("Could not determine PDF page count.");
		}

		return totalPages;
	}

	public async Task OptimizeAsync(string inputPdfPath, string outputPdfPath, CancellationToken cancellationToken = default)
	{
		if (!File.Exists(inputPdfPath))
		{
			throw new FileNotFoundException("PDF not found.", inputPdfPath);
		}

		EnsureOutputDirectory(outputPdfPath);
		var arguments = new[]
		{
			"--linearize",
			"--recompress-flate",
			"--compression-level=9",
			"--object-streams=generate",
			"--stream-data=compress",
			inputPdfPath,
			outputPdfPath
		};

		await RunQpdfAsync(arguments, cancellationToken);
	}

	public Task OptimizeLossyAsync(string inputPdfPath, string outputPdfPath, CancellationToken cancellationToken = default)
	{
		return GhostscriptPdfOptimizer.OptimizeLossyAsync(inputPdfPath, outputPdfPath, cancellationToken);
	}

	private async Task<string> RunQpdfAsync(IEnumerable<string> arguments, CancellationToken cancellationToken, bool captureOutput = false)
	{
		if (!TryResolveQpdfBinaryPath(out var executablePath))
		{
			throw new InvalidOperationException(GetDependencyStatus().Message);
		}

		var startInfo = new ProcessStartInfo
		{
			FileName = executablePath,
			RedirectStandardError = true,
			RedirectStandardOutput = captureOutput,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		foreach (var argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		using var process = new Process { StartInfo = startInfo };
		if (!process.Start())
		{
			throw new InvalidOperationException("Could not start qpdf process.");
		}

		var standardOutputTask = captureOutput ? process.StandardOutput.ReadToEndAsync(cancellationToken) : Task.FromResult(string.Empty);
		var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

		await process.WaitForExitAsync(cancellationToken);
		var standardError = await standardErrorTask;
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"qpdf failed: {standardError.Trim()}");
		}

		return await standardOutputTask;
	}

	private static bool TryResolveQpdfBinaryPath(out string resolvedPath)
	{
		var appBaseDirectory = AppContext.BaseDirectory;
		if (OperatingSystem.IsWindows())
		{
			var bundledPath = Path.Combine(appBaseDirectory, "tools", "qpdf", "win", "qpdf.exe");
			if (File.Exists(bundledPath))
			{
				resolvedPath = bundledPath;
				return true;
			}

			if (TryFindExecutableOnPath("qpdf.exe", out resolvedPath))
			{
				return true;
			}

			resolvedPath = string.Empty;
			return false;
		}

		if (OperatingSystem.IsMacCatalyst())
		{
			var bundledPath = Path.Combine(appBaseDirectory, "tools", "qpdf", "mac", "qpdf");
			if (File.Exists(bundledPath))
			{
				resolvedPath = bundledPath;
				return true;
			}

			if (TryFindExecutableOnPath("qpdf", out resolvedPath))
			{
				return true;
			}

			resolvedPath = string.Empty;
			return false;
		}

		throw new PlatformNotSupportedException("Only Windows and macOS are supported.");
	}

	private static void EnsureOutputDirectory(string outputPdfPath)
	{
		var directory = Path.GetDirectoryName(outputPdfPath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}
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
}
