namespace StormPDF.Controls;

public sealed class NativePdfView : View
{
	public static readonly BindableProperty SourcePathProperty = BindableProperty.Create(
		nameof(SourcePath),
		typeof(string),
		typeof(NativePdfView),
		default(string));

	public static readonly BindableProperty CurrentPageNumberProperty = BindableProperty.Create(
		nameof(CurrentPageNumber),
		typeof(int),
		typeof(NativePdfView),
		1);

	public string? SourcePath
	{
		get => (string?)GetValue(SourcePathProperty);
		set => SetValue(SourcePathProperty, value);
	}

	public int CurrentPageNumber
	{
		get => (int)GetValue(CurrentPageNumberProperty);
		set => SetValue(CurrentPageNumberProperty, value);
	}
}
