namespace StormPDF.Services;

public sealed record PdfEngineDependencyStatus(bool IsAvailable, string Message, string? ResolvedPath);
