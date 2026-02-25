namespace StormPDF.Services;

public sealed class WorkspacePdfEngineAdapter : IWorkspacePdfEngine
{
	private readonly IPdfEngine _inner;

	public WorkspacePdfEngineAdapter(IPdfEngine inner)
	{
		_inner = inner;
	}

	public Task MergeAsync(IReadOnlyList<string> inputPdfPaths, string outputPdfPath, CancellationToken cancellationToken = default)
	{
		return _inner.MergeAsync(inputPdfPaths, outputPdfPath, cancellationToken);
	}

	public Task<int> GetPageCountAsync(string inputPdfPath, CancellationToken cancellationToken = default)
	{
		return _inner.GetPageCountAsync(inputPdfPath, cancellationToken);
	}
}
