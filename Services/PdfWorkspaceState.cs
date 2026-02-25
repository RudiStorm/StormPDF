namespace StormPDF.Services;

public interface IWorkspacePdfEngine
{
	Task MergeAsync(IReadOnlyList<string> inputPdfPaths, string outputPdfPath, CancellationToken cancellationToken = default);
	Task<int> GetPageCountAsync(string inputPdfPath, CancellationToken cancellationToken = default);
}

public sealed class PdfWorkspaceState
{
	private readonly IWorkspacePdfEngine _engine;
	private readonly List<WorkspaceTabState> _tabs = new();

	public PdfWorkspaceState(IWorkspacePdfEngine engine)
	{
		_engine = engine;
	}

	public IReadOnlyList<WorkspaceTabState> Tabs => _tabs;
	public WorkspaceTabState? ActiveTab { get; private set; }
	public int? ActivePageNumber { get; private set; }

	public async Task<WorkspaceTabState> OpenAsync(
		string sourcePath,
		Func<string, Task<string>> createWorkingCopyAsync,
		CancellationToken cancellationToken = default)
	{
		var existing = _tabs.FirstOrDefault(tab => string.Equals(tab.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase));
		if (existing is not null)
		{
			ActiveTab = existing;
			ActivePageNumber = 1;
			return existing;
		}

		var workingPath = await createWorkingCopyAsync(sourcePath);
		var pageCount = await _engine.GetPageCountAsync(workingPath, cancellationToken);
		var tab = new WorkspaceTabState(sourcePath, workingPath, Path.GetFileName(sourcePath), pageCount, false);
		_tabs.Add(tab);
		ActiveTab = tab;
		ActivePageNumber = 1;
		return tab;
	}

	public async Task<WorkspaceTabState> MergeAsync(
		IReadOnlyList<string> mergeSources,
		Func<string, string> buildWorkingOutputPath,
		Func<string, Task<string>> createWorkingCopyAsync,
		CancellationToken cancellationToken = default)
	{
		if (mergeSources.Count < 2)
		{
			throw new InvalidOperationException("Select at least two PDFs to merge.");
		}

		var outputPath = buildWorkingOutputPath("merged");
		await _engine.MergeAsync(mergeSources, outputPath, cancellationToken);
		return await OpenAsync(outputPath, createWorkingCopyAsync, cancellationToken);
	}

	public bool SelectPage(int pageNumber)
	{
		if (ActiveTab is null)
		{
			return false;
		}

		if (pageNumber < 1 || pageNumber > ActiveTab.PageCount)
		{
			return false;
		}

		ActivePageNumber = pageNumber;
		return true;
	}
}

public sealed record WorkspaceTabState(
	string SourcePath,
	string WorkingPath,
	string Title,
	int PageCount,
	bool IsDirty);
