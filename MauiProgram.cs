using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using StormPDF.Controls;
using StormPDF.Services;

namespace StormPDF;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureMauiHandlers(handlers =>
			{
				#if MACCATALYST
				handlers.AddHandler(typeof(NativePdfView), typeof(Platforms.MacCatalyst.Handlers.NativePdfViewHandler));
				#endif
			})
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<IPdfEngine, PlatformPdfEngine>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
