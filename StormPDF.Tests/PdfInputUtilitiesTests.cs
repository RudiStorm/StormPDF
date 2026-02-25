using StormPDF.Utilities;
using Xunit;

namespace StormPDF.Tests;

public class PdfInputUtilitiesTests
{
	[Fact]
	public void TryParsePageSelection_ReturnsPages_ForValidInput()
	{
		var ok = PdfInputUtilities.TryParsePageSelection("2,4-6,9", out var pages, out var message);

		Assert.True(ok);
		Assert.Equal(string.Empty, message);
		Assert.Equal(new[] { 2, 4, 5, 6, 9 }, pages);
	}

	[Fact]
	public void TryParsePageSelection_ReturnsError_ForInvalidInput()
	{
		var ok = PdfInputUtilities.TryParsePageSelection("2,a-6", out var pages, out var message);

		Assert.False(ok);
		Assert.Empty(pages);
		Assert.Equal("Invalid page range: 'a-6'.", message);
	}

	[Fact]
	public void BuildOutputPath_AppendsSuffixAndTimestamp()
	{
		var timestamp = new DateTimeOffset(2026, 2, 25, 13, 14, 15, TimeSpan.Zero);
		var output = PdfInputUtilities.BuildOutputPath("/tmp/invoice.pdf", "merged", timestamp);

		Assert.Equal("/tmp/invoice_merged_20260225131415.pdf", output);
	}

	[Fact]
	public void BuildPageRange_CompressesConsecutivePages()
	{
		var range = PdfInputUtilities.BuildPageRange(new[] { 1, 2, 3, 5, 9, 10 });

		Assert.Equal("1-3,5,9-10", range);
	}
}
