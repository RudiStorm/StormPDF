#if MACCATALYST
using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Handlers;
using PdfKit;
using StormPDF.Controls;
using UIKit;

namespace StormPDF.Platforms.MacCatalyst.Handlers;

public sealed class NativePdfViewHandler : ViewHandler<NativePdfView, PdfView>
{
	public static readonly IPropertyMapper<NativePdfView, NativePdfViewHandler> PropertyMapper =
		new PropertyMapper<NativePdfView, NativePdfViewHandler>(ViewHandler.ViewMapper)
		{
			[nameof(NativePdfView.SourcePath)] = MapSourcePath,
			[nameof(NativePdfView.CurrentPageNumber)] = MapCurrentPageNumber
		};

	public NativePdfViewHandler() : base(PropertyMapper)
	{
	}

	protected override PdfView CreatePlatformView()
	{
		return new PdfView
		{
			AutoScales = true,
			DisplayMode = PdfDisplayMode.SinglePageContinuous,
			DisplayDirection = PdfDisplayDirection.Vertical,
			BackgroundColor = UIColor.White
		};
	}

	protected override void ConnectHandler(PdfView platformView)
	{
		base.ConnectHandler(platformView);
		MapSourcePath(this, VirtualView);
		MapCurrentPageNumber(this, VirtualView);
	}

	private static void MapSourcePath(NativePdfViewHandler handler, NativePdfView view)
	{
		if (handler.PlatformView is null)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(view.SourcePath) || !File.Exists(view.SourcePath))
		{
			handler.PlatformView.Document = null;
			return;
		}

		handler.PlatformView.Document = null;

		using var url = NSUrl.FromFilename(view.SourcePath);
		var document = new PdfDocument(url);
		handler.PlatformView.Document = document;
		handler.PlatformView.LayoutIfNeeded();
		MapCurrentPageNumber(handler, view);
	}

	private static void MapCurrentPageNumber(NativePdfViewHandler handler, NativePdfView view)
	{
		if (handler.PlatformView?.Document is not PdfDocument document)
		{
			return;
		}

		if (document.PageCount < 1)
		{
			return;
		}

		var targetIndex = Math.Clamp(view.CurrentPageNumber - 1, 0, (int)document.PageCount - 1);
		var page = document.GetPage(targetIndex);
		if (page is null)
		{
			return;
		}

		handler.PlatformView.GoToPage(page);
	}
}
#endif
