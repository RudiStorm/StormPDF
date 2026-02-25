#if MACCATALYST
using Foundation;
using PdfKit;

namespace StormPDF.Services;

public sealed class PdfKitPdfEngine : IPdfEngine
{
	public PdfEngineDependencyStatus GetDependencyStatus()
	{
		return new PdfEngineDependencyStatus(true, "PDFKit is available.", "Apple PDFKit");
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

		await Task.Run(() =>
		{
			var mergedDocument = new PdfDocument();
			PdfDocument? firstSourceDocument = null;

			foreach (var inputPdfPath in inputPdfPaths)
			{
				cancellationToken.ThrowIfCancellationRequested();
				EnsureInputPdfExists(inputPdfPath);

				using var sourceUrl = NSUrl.FromFilename(inputPdfPath);
				using var sourceDocument = new PdfDocument(sourceUrl);
				if (sourceDocument is null)
				{
					throw new InvalidOperationException($"Could not read PDF: {inputPdfPath}");
				}

				firstSourceDocument ??= sourceDocument;

				for (var pageIndex = 0; pageIndex < sourceDocument.PageCount; pageIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					var sourcePage = sourceDocument.GetPage((nint)pageIndex)
						?? throw new InvalidOperationException($"Could not access page {pageIndex + 1} in {inputPdfPath}");

					var copiedPage = (PdfPage)sourcePage.Copy();
					mergedDocument.InsertPage(copiedPage, mergedDocument.PageCount);
				}
			}

			if (firstSourceDocument?.DocumentAttributes is not null)
			{
				mergedDocument.DocumentAttributes = firstSourceDocument.DocumentAttributes;
			}

			EnsureOutputDirectory(outputPdfPath);
			using var outputUrl = NSUrl.FromFilename(outputPdfPath);
			if (!mergedDocument.Write(outputUrl))
			{
				throw new InvalidOperationException("PDFKit failed to write merged PDF.");
			}
		}, cancellationToken);
	}

	public async Task DeletePagesAsync(string inputPdfPath, IReadOnlyCollection<int> pagesToDelete, string outputPdfPath, CancellationToken cancellationToken = default)
	{
		if (pagesToDelete.Count == 0)
		{
			throw new InvalidOperationException("No pages were provided for deletion.");
		}

		await Task.Run(() =>
		{
			cancellationToken.ThrowIfCancellationRequested();
			EnsureInputPdfExists(inputPdfPath);

			using var sourceUrl = NSUrl.FromFilename(inputPdfPath);
			using var document = new PdfDocument(sourceUrl);
			if (document is null)
			{
				throw new InvalidOperationException("Could not read source PDF.");
			}

			var pageCount = (int)document.PageCount;
			if (pageCount < 1)
			{
				throw new InvalidOperationException("Could not determine PDF page count.");
			}

			var orderedDistinctPages = pagesToDelete
				.Where(page => page >= 1 && page <= pageCount)
				.Distinct()
				.OrderByDescending(page => page)
				.ToArray();

			if (orderedDistinctPages.Length == 0)
			{
				throw new InvalidOperationException("No valid page numbers were provided for this PDF.");
			}

			if (orderedDistinctPages.Length >= pageCount)
			{
				throw new InvalidOperationException("You cannot delete all pages from the PDF.");
			}

			foreach (var pageNumber in orderedDistinctPages)
			{
				cancellationToken.ThrowIfCancellationRequested();
				document.RemovePage((nint)(pageNumber - 1));
			}

			EnsureOutputDirectory(outputPdfPath);
			using var outputUrl = NSUrl.FromFilename(outputPdfPath);
			if (!document.Write(outputUrl))
			{
				throw new InvalidOperationException("PDFKit failed to write updated PDF.");
			}
		}, cancellationToken);
	}

	public async Task<int> GetPageCountAsync(string inputPdfPath, CancellationToken cancellationToken = default)
	{
		return await Task.Run(() =>
		{
			cancellationToken.ThrowIfCancellationRequested();
			EnsureInputPdfExists(inputPdfPath);

			using var sourceUrl = NSUrl.FromFilename(inputPdfPath);
			using var document = new PdfDocument(sourceUrl);
			if (document is null)
			{
				throw new InvalidOperationException("Could not read source PDF.");
			}

			var pageCount = (int)document.PageCount;
			if (pageCount < 1)
			{
				throw new InvalidOperationException("Could not determine PDF page count.");
			}

			return pageCount;
		}, cancellationToken);
	}

	public async Task OptimizeAsync(string inputPdfPath, string outputPdfPath, CancellationToken cancellationToken = default)
	{
		await Task.Run(() =>
		{
			cancellationToken.ThrowIfCancellationRequested();
			EnsureInputPdfExists(inputPdfPath);

			using var sourceUrl = NSUrl.FromFilename(inputPdfPath);
			using var document = new PdfDocument(sourceUrl);
			if (document is null)
			{
				throw new InvalidOperationException("Could not read source PDF.");
			}

			EnsureOutputDirectory(outputPdfPath);
			using var outputUrl = NSUrl.FromFilename(outputPdfPath);
			if (!document.Write(outputUrl))
			{
				throw new InvalidOperationException("PDFKit failed to write optimized PDF.");
			}
		}, cancellationToken);
	}

	public Task OptimizeLossyAsync(string inputPdfPath, string outputPdfPath, CancellationToken cancellationToken = default)
	{
		return GhostscriptPdfOptimizer.OptimizeLossyAsync(inputPdfPath, outputPdfPath, cancellationToken);
	}

	private static void EnsureInputPdfExists(string path)
	{
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("PDF not found.", path);
		}
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
#endif
