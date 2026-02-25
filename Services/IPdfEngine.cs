namespace StormPDF.Services;

public interface IPdfEngine
{
	PdfEngineDependencyStatus GetDependencyStatus();
	Task ViewAsync(string pdfPath, CancellationToken cancellationToken = default);
	Task MergeAsync(IReadOnlyList<string> inputPdfPaths, string outputPdfPath, CancellationToken cancellationToken = default);
	Task DeletePagesAsync(string inputPdfPath, IReadOnlyCollection<int> pagesToDelete, string outputPdfPath, CancellationToken cancellationToken = default);
	Task<int> GetPageCountAsync(string inputPdfPath, CancellationToken cancellationToken = default);
	Task OptimizeAsync(string inputPdfPath, string outputPdfPath, CancellationToken cancellationToken = default);
	Task OptimizeLossyAsync(string inputPdfPath, string outputPdfPath, CancellationToken cancellationToken = default);
}
