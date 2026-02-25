using StormPDF.Services;
using StormPDF.Utilities;
using Xunit;

namespace StormPDF.Tests;

public sealed class PdfWorkspaceStateTests
{
	[Fact]
	public async Task OpenAsync_AddsTabAndSetsActiveState()
	{
		var engine = new FakeWorkspacePdfEngine(pageCount: 4);
		var workspace = new PdfWorkspaceState(engine);

		var tab = await workspace.OpenAsync("/tmp/input.pdf", source => Task.FromResult(source));

		Assert.Single(workspace.Tabs);
		Assert.Equal(tab, workspace.ActiveTab);
		Assert.Equal(1, workspace.ActivePageNumber);
		Assert.Equal("input.pdf", tab.Title);
		Assert.Equal(4, tab.PageCount);
	}

	[Fact]
	public async Task MergeAsync_CreatesMergedTabAndCallsEngine()
	{
		var engine = new FakeWorkspacePdfEngine(pageCount: 9);
		var workspace = new PdfWorkspaceState(engine);

		var tab = await workspace.MergeAsync(
			new[] { "/tmp/a.pdf", "/tmp/b.pdf" },
			suffix => $"/tmp/{suffix}.pdf",
			source => Task.FromResult(source));

		Assert.True(engine.MergeCalled);
		Assert.Equal("/tmp/merged.pdf", engine.LastMergeOutputPath);
		Assert.Equal(tab, workspace.ActiveTab);
		Assert.Equal(9, tab.PageCount);
	}

	[Fact]
	public async Task SelectPage_ReturnsFalseForOutOfRangeAndTrueForValid()
	{
		var engine = new FakeWorkspacePdfEngine(pageCount: 3);
		var workspace = new PdfWorkspaceState(engine);
		await workspace.OpenAsync("/tmp/input.pdf", source => Task.FromResult(source));

		var invalid = workspace.SelectPage(8);
		var valid = workspace.SelectPage(2);

		Assert.False(invalid);
		Assert.True(valid);
		Assert.Equal(2, workspace.ActivePageNumber);
	}

	[Fact]
	public async Task WaitForFileReadyAsync_ReturnsWhenFileExistsWithContent()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), $"stormpdf_{Guid.NewGuid():N}.pdf");
		try
		{
			await File.WriteAllTextAsync(tempPath, "pdf-bytes");
			await FileReadyUtilities.WaitForFileReadyAsync(tempPath, attempts: 2, delayMs: 5);
			Assert.True(File.Exists(tempPath));
		}
		finally
		{
			if (File.Exists(tempPath))
			{
				File.Delete(tempPath);
			}
		}
	}

	private sealed class FakeWorkspacePdfEngine : IWorkspacePdfEngine
	{
		private readonly int _pageCount;

		public FakeWorkspacePdfEngine(int pageCount)
		{
			_pageCount = pageCount;
		}

		public bool MergeCalled { get; private set; }
		public string LastMergeOutputPath { get; private set; } = string.Empty;

		public Task MergeAsync(IReadOnlyList<string> inputPdfPaths, string outputPdfPath, CancellationToken cancellationToken = default)
		{
			MergeCalled = true;
			LastMergeOutputPath = outputPdfPath;
			return Task.CompletedTask;
		}

		public Task<int> GetPageCountAsync(string inputPdfPath, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_pageCount);
		}
	}
}
