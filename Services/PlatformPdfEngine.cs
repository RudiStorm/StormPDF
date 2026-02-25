namespace StormPDF.Services;

public sealed class PlatformPdfEngine : IPdfEngine
{
	private readonly IPdfEngine _innerEngine;
	private readonly QpdfPdfEngine? _qpdfEngine;

	public PlatformPdfEngine()
	{
		#if MACCATALYST
		_innerEngine = new PdfKitPdfEngine();
		_qpdfEngine = new QpdfPdfEngine();
		#else
		_innerEngine = new QpdfPdfEngine();
		#endif
	}

	public PdfEngineDependencyStatus GetDependencyStatus()
	{
		return _innerEngine.GetDependencyStatus();
	}

	public Task ViewAsync(string pdfPath, CancellationToken cancellationToken = default)
	{
		return _innerEngine.ViewAsync(pdfPath, cancellationToken);
	}

	public Task MergeAsync(IReadOnlyList<string> inputPdfPaths, string outputPdfPath, CancellationToken cancellationToken = default)
	{
		return _innerEngine.MergeAsync(inputPdfPaths, outputPdfPath, cancellationToken);
	}

	public Task DeletePagesAsync(string inputPdfPath, IReadOnlyCollection<int> pagesToDelete, string outputPdfPath, CancellationToken cancellationToken = default)
	{
		return _innerEngine.DeletePagesAsync(inputPdfPath, pagesToDelete, outputPdfPath, cancellationToken);
	}

	public Task<int> GetPageCountAsync(string inputPdfPath, CancellationToken cancellationToken = default)
	{
		return _innerEngine.GetPageCountAsync(inputPdfPath, cancellationToken);
	}

	public Task OptimizeAsync(string inputPdfPath, string outputPdfPath, CancellationToken cancellationToken = default)
	{
		#if MACCATALYST
		if (_qpdfEngine is not null)
		{
			return TryOptimizeWithQpdfThenFallbackAsync(inputPdfPath, outputPdfPath, cancellationToken);
		}
		#endif

		return _innerEngine.OptimizeAsync(inputPdfPath, outputPdfPath, cancellationToken);
	}

	public Task OptimizeLossyAsync(string inputPdfPath, string outputPdfPath, CancellationToken cancellationToken = default)
	{
		return _innerEngine.OptimizeLossyAsync(inputPdfPath, outputPdfPath, cancellationToken);
	}

	#if MACCATALYST
	private async Task TryOptimizeWithQpdfThenFallbackAsync(string inputPdfPath, string outputPdfPath, CancellationToken cancellationToken)
	{
		try
		{
			await _qpdfEngine!.OptimizeAsync(inputPdfPath, outputPdfPath, cancellationToken);
		}
		catch (Exception)
		{
			await _innerEngine.OptimizeAsync(inputPdfPath, outputPdfPath, cancellationToken);
		}
	}
	#endif
}
