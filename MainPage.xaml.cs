using CommunityToolkit.Maui.Storage;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using StormPDF.Controls;
using StormPDF.Services;
using StormPDF.Utilities;
#if MACCATALYST
using CoreGraphics;
using Foundation;
using PdfKit;
using UIKit;
#endif

namespace StormPDF;

public partial class MainPage : ContentPage
{
	private readonly IPdfEngine _pdfEngine;
	private readonly ObservableCollection<PdfTabItem> _tabs = new();
	private readonly ObservableCollection<PdfPageItem> _pages = new();
	private bool _dependencyChecked;
	private PdfTabItem? _activeTab;
	private CancellationTokenSource? _thumbnailLoadCts;
	private NativePdfView _pdfPreview = null!;

	public MainPage()
	{
		InitializeComponent();
		_pdfEngine = IPlatformApplication.Current?.Services.GetService<IPdfEngine>()
			?? throw new InvalidOperationException("IPdfEngine service is not registered.");

		_pdfPreview = CreatePreviewControl();
		PreviewHost.Content = _pdfPreview;

		DocumentTabsCollectionView.ItemsSource = _tabs;
		PagesCollectionView.ItemsSource = _pages;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (_dependencyChecked)
		{
			return;
		}

		_dependencyChecked = true;
		var dependency = _pdfEngine.GetDependencyStatus();
		StatusLabel.Text = dependency.IsAvailable
			? "Ready."
			: dependency.Message;
	}

	private async void OnOpenPdfClicked(object? sender, EventArgs e)
	{
		var pickedFile = await PickSinglePdfAsync();
		if (pickedFile?.FullPath is not { Length: > 0 } fullPath)
		{
			return;
		}

		await OpenDocumentAsync(fullPath);
	}

	private async void OnMergePdfClicked(object? sender, EventArgs e)
	{
		var pickedFiles = await FilePicker.Default.PickMultipleAsync(new PickOptions
		{
			PickerTitle = _activeTab is null
				? "Choose PDFs to merge"
				: "Choose PDF(s) to add to current document",
			FileTypes = BuildPdfFileType()
		});

		if (pickedFiles is null)
		{
			return;
		}

		var pickedPaths = pickedFiles
			.Where(file => file?.FullPath is { Length: > 0 })
			.Select(file => file!.FullPath)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var mergeSources = new List<string>();
		if (_activeTab is not null)
		{
			mergeSources.Add(_activeTab.WorkingFilePath);
			mergeSources.AddRange(pickedPaths.Where(path => !string.Equals(path, _activeTab.WorkingFilePath, StringComparison.OrdinalIgnoreCase)));
		}
		else
		{
			mergeSources.AddRange(pickedPaths);
		}

		if (mergeSources.Count < 2)
		{
			StatusLabel.Text = _activeTab is null
				? "Select at least two PDFs to merge."
				: "Select at least one additional PDF to merge with the current document.";
			return;
		}

		var outputPath = BuildWorkingOutputPath("merged");
		await RunOperationAsync("Merging PDFs...", async () =>
		{
			await _pdfEngine.MergeAsync(mergeSources, outputPath);
			await OpenDocumentAsync(outputPath, markAsDirty: true);

			StatusLabel.Text = $"Merged PDF opened: {Path.GetFileName(outputPath)}";
		});
	}

	private async void OnOpenInSystemViewerClicked(object? sender, EventArgs e)
	{
		if (_activeTab is null)
		{
			StatusLabel.Text = "Open a PDF first.";
			return;
		}

		await RunOperationAsync("Opening in system viewer...", async () =>
		{
			await _pdfEngine.ViewAsync(_activeTab.WorkingFilePath);
			StatusLabel.Text = "Opened PDF in system viewer.";
		});
	}

	private async void OnOptimizePdfClicked(object? sender, EventArgs e)
	{
		if (_activeTab is null)
		{
			StatusLabel.Text = "Open a PDF first.";
			return;
		}

		var inputPath = _activeTab.WorkingFilePath;
		var outputPath = BuildWorkingOutputPath("optimized");
		var inputBytes = new FileInfo(inputPath).Length;

		await RunOperationAsync("Optimizing PDF...", async () =>
		{
			await _pdfEngine.OptimizeAsync(inputPath, outputPath);
			var outputBytes = new FileInfo(outputPath).Length;

			_activeTab.WorkingFilePath = outputPath;
			_activeTab.IsDirty = true;
			await RefreshPagesAsync(_activeTab);
			await UpdatePreviewAsync(_activeTab.WorkingFilePath);

			var deltaBytes = inputBytes - outputBytes;
			var deltaPercent = inputBytes > 0 ? Math.Max(0, (deltaBytes * 100d) / inputBytes) : 0d;
			StatusLabel.Text = deltaBytes > 0
				? $"Optimized {Path.GetFileName(_activeTab.Title)} ({deltaPercent:0.#}% smaller)."
				: "Optimization complete. File size unchanged (already optimized or non-compressible).";
			UpdateDebugState();
		});
	}

	private async void OnOptimizeLossyPdfClicked(object? sender, EventArgs e)
	{
		if (_activeTab is null)
		{
			StatusLabel.Text = "Open a PDF first.";
			return;
		}

		var inputPath = _activeTab.WorkingFilePath;
		var outputPath = BuildWorkingOutputPath("optimized_lossy");
		var inputBytes = new FileInfo(inputPath).Length;

		await RunOperationAsync("Optimizing PDF (lossy)...", async () =>
		{
			await _pdfEngine.OptimizeLossyAsync(inputPath, outputPath);
			var outputBytes = new FileInfo(outputPath).Length;

			_activeTab.WorkingFilePath = outputPath;
			_activeTab.IsDirty = true;
			await RefreshPagesAsync(_activeTab);
			await UpdatePreviewAsync(_activeTab.WorkingFilePath);

			var deltaBytes = inputBytes - outputBytes;
			var deltaPercent = inputBytes > 0 ? Math.Max(0, (deltaBytes * 100d) / inputBytes) : 0d;
			StatusLabel.Text = deltaBytes > 0
				? $"Lossy optimize complete ({deltaPercent:0.#}% smaller)."
				: "Lossy optimization complete. File size unchanged.";
			UpdateDebugState();
		});
	}

	private async void OnDocumentTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is not PdfTabItem selectedTab)
		{
			return;
		}

		await SetActiveTabAsync(selectedTab);
	}

	private async void OnCloseTabClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { CommandParameter: PdfTabItem tab })
		{
			return;
		}

		await CloseTabAsync(tab);
	}

	private void OnPagesSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.Count == 0)
		{
			return;
		}

		if (e.CurrentSelection[^1] is not PdfPageItem lastSelected)
		{
			return;
		}

		_pdfPreview.CurrentPageNumber = lastSelected.Number;
		StatusLabel.Text = e.CurrentSelection.Count == 1
			? $"Navigated to page {lastSelected.Number}."
			: $"Selected {e.CurrentSelection.Count} pages.";
		UpdateDebugState();
	}

	private async void OnDeletePageMenuClicked(object? sender, EventArgs e)
	{
		if (sender is not MenuFlyoutItem menuItem)
		{
			StatusLabel.Text = "Could not resolve selected page.";
			return;
		}

		var commandValue = menuItem.CommandParameter;
		if (!TryGetPageNumber(commandValue, out var pageNumber))
		{
			StatusLabel.Text = "Could not resolve selected page.";
			return;
		}

		await DeletePagesFromActiveDocumentAsync(new[] { pageNumber });
	}

	private async void OnDeleteSelectedPagesMenuClicked(object? sender, EventArgs e)
	{
		if (!TryGetSelectedPageNumbers(out var selectedPages))
		{
			StatusLabel.Text = "Select page(s) first. Tip: use Shift-click for range.";
			return;
		}

		await DeletePagesFromActiveDocumentAsync(selectedPages);
	}

	private static bool TryGetPageNumber(object? value, out int pageNumber)
	{
		switch (value)
		{
			case int asInt:
				pageNumber = asInt;
				return true;
			case long asLong when asLong >= 1 && asLong <= int.MaxValue:
				pageNumber = (int)asLong;
				return true;
			case string asString when int.TryParse(asString, out var parsed):
				pageNumber = parsed;
				return true;
			default:
				pageNumber = 0;
				return false;
		}
	}

	private async Task OpenDocumentAsync(string sourcePath, bool markAsDirty = false)
	{
		if (!IsValidPdfPath(sourcePath))
		{
			StatusLabel.Text = "Please select a valid PDF file.";
			return;
		}

		var existingTab = _tabs.FirstOrDefault(tab => string.Equals(tab.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase));
		if (existingTab is not null)
		{
			existingTab.IsDirty = existingTab.IsDirty || markAsDirty;
			DocumentTabsCollectionView.SelectedItem = existingTab;
			DocumentTabsCollectionView.ScrollTo(existingTab, position: ScrollToPosition.MakeVisible, animate: false);
			await SetActiveTabAsync(existingTab);
			return;
		}

		await RunOperationAsync("Loading PDF...", async () =>
		{
			var workingPath = await CreateWorkingCopyAsync(sourcePath);
			await FileReadyUtilities.WaitForFileReadyAsync(workingPath);
			var pageCount = await _pdfEngine.GetPageCountAsync(workingPath);
			var tab = new PdfTabItem(sourcePath, workingPath, pageCount);
			tab.IsDirty = markAsDirty;
			_tabs.Add(tab);
			DocumentTabsCollectionView.SelectedItem = tab;
			DocumentTabsCollectionView.ScrollTo(tab, position: ScrollToPosition.End, animate: false);
			await SetActiveTabAsync(tab);
			StatusLabel.Text = $"Opened {tab.Title} ({pageCount} pages).";
			UpdateDebugState();
		});
	}

	private async Task SetActiveTabAsync(PdfTabItem tab)
	{
		_activeTab = tab;
		await UpdatePreviewAsync(tab.WorkingFilePath, forceRecreate: true);
		await RefreshPagesAsync(tab);
		UpdateDebugState();
	}

	private async Task RefreshPagesAsync(PdfTabItem tab)
	{
		_thumbnailLoadCts?.Cancel();
		_thumbnailLoadCts?.Dispose();
		_thumbnailLoadCts = new CancellationTokenSource();
		var thumbnailToken = _thumbnailLoadCts.Token;

		var pageCount = await _pdfEngine.GetPageCountAsync(tab.WorkingFilePath);
		tab.PageCount = pageCount;

		_pages.Clear();
		for (var page = 1; page <= tab.PageCount; page++)
		{
			_pages.Add(new PdfPageItem(page));
		}

		PagesCollectionView.ItemsSource = null;
		PagesCollectionView.ItemsSource = _pages;

		_ = LoadThumbnailsAsync(tab, _pages.ToList(), thumbnailToken);
	}

	private async Task LoadThumbnailsAsync(PdfTabItem tab, IReadOnlyList<PdfPageItem> pageItems, CancellationToken cancellationToken)
	{
		for (var index = 0; index < pageItems.Count; index++)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}

			var page = pageItems[index];
			var thumbnail = await CreateThumbnailAsync(tab.WorkingFilePath, page.Number);

			if (cancellationToken.IsCancellationRequested || _activeTab is null || !ReferenceEquals(tab, _activeTab))
			{
				return;
			}

			await MainThread.InvokeOnMainThreadAsync(() => page.ThumbnailSource = thumbnail);
		}
	}

	private Task UpdatePreviewAsync(string filePath)
	{
		return UpdatePreviewAsync(filePath, forceRecreate: false);
	}

	private async Task UpdatePreviewAsync(string filePath, bool forceRecreate)
	{
		if (forceRecreate)
		{
			_pdfPreview = CreatePreviewControl();
			PreviewHost.Content = _pdfPreview;
			await Task.Yield();
		}

		_pdfPreview.SourcePath = null;
		await Task.Delay(15);
		_pdfPreview.SourcePath = filePath;
		_pdfPreview.CurrentPageNumber = 1;
		_pdfPreview.IsVisible = true;
		PreviewPlaceholderLabel.IsVisible = false;
	}

	private void ClearPreview()
	{
		_pdfPreview.SourcePath = null;
		_pdfPreview.IsVisible = false;
		PreviewPlaceholderLabel.IsVisible = true;
	}

	private NativePdfView CreatePreviewControl()
	{
		return new NativePdfView
		{
			HorizontalOptions = LayoutOptions.Fill,
			VerticalOptions = LayoutOptions.Fill,
			IsVisible = false
		};
	}

	private bool TryGetSelectedPageNumbers(out IReadOnlyCollection<int> pages)
	{
		var selected = PagesCollectionView.SelectedItems
			.OfType<PdfPageItem>()
			.Select(item => item.Number)
			.Distinct()
			.Order()
			.ToArray();

		pages = selected;
		return selected.Length > 0;
	}

	private async Task DeletePagesFromActiveDocumentAsync(IReadOnlyCollection<int> pagesToDelete)
	{
		if (_activeTab is null)
		{
			StatusLabel.Text = "Open a PDF first.";
			return;
		}

		if (pagesToDelete.Count == 0)
		{
			StatusLabel.Text = "Select page(s) first.";
			return;
		}

		var outOfRange = pagesToDelete.Where(page => page < 1 || page > _activeTab.PageCount).ToArray();
		if (outOfRange.Length > 0)
		{
			StatusLabel.Text = $"Page {outOfRange[0]} is out of range.";
			return;
		}

		if (pagesToDelete.Count >= _activeTab.PageCount)
		{
			StatusLabel.Text = "You cannot delete all pages.";
			return;
		}

		var outputPath = BuildWorkingOutputPath("trimmed");
		var orderedPages = pagesToDelete.Order().ToArray();
		await RunOperationAsync($"Deleting {orderedPages.Length} page(s)...", async () =>
		{
			await _pdfEngine.DeletePagesAsync(_activeTab.WorkingFilePath, orderedPages, outputPath);
			_activeTab.WorkingFilePath = outputPath;
			_activeTab.IsDirty = true;
			await RefreshPagesAsync(_activeTab);
			await UpdatePreviewAsync(_activeTab.WorkingFilePath);
			PagesCollectionView.SelectedItems?.Clear();
			StatusLabel.Text = $"Deleted {orderedPages.Length} page(s).";
			UpdateDebugState();
		});
	}

	private async Task CloseTabAsync(PdfTabItem tab)
	{
		if (tab.IsDirty)
		{
			var action = await DisplayActionSheetAsync(
				$"Save changes to {tab.Title}?",
				"Cancel",
				null,
				"Save",
				"Discard");

			if (action == "Cancel")
			{
				return;
			}

			if (action == "Save")
			{
				var saved = await SaveDirtyTabAsync(tab);
				if (!saved)
				{
					StatusLabel.Text = "Save canceled.";
					return;
				}
			}
		}

		var wasActive = ReferenceEquals(tab, _activeTab);
		_tabs.Remove(tab);

		if (_tabs.Count == 0)
		{
			_thumbnailLoadCts?.Cancel();
			_activeTab = null;
			_pages.Clear();
			ClearPreview();
			StatusLabel.Text = "No PDF open.";
			UpdateDebugState();
			return;
		}

		if (!wasActive)
		{
			return;
		}

		var nextTab = _tabs[^1];
		DocumentTabsCollectionView.SelectedItem = nextTab;
		await SetActiveTabAsync(nextTab);
		UpdateDebugState();
	}

	private async Task<bool> SaveDirtyTabAsync(PdfTabItem tab)
	{
		var previousSourcePath = tab.SourcePath;
		await using var sourceStream = File.OpenRead(tab.WorkingFilePath);
		var suggestedName = Path.GetFileName(tab.SourcePath);
		var saveResult = await FileSaver.Default.SaveAsync(suggestedName, sourceStream);
		if (!saveResult.IsSuccessful || string.IsNullOrWhiteSpace(saveResult.FilePath))
		{
			return false;
		}

		TryPreserveFileMetadata(previousSourcePath, saveResult.FilePath);

		tab.SourcePath = saveResult.FilePath;
		tab.Title = Path.GetFileName(saveResult.FilePath);
		tab.IsDirty = false;
		StatusLabel.Text = $"Saved: {saveResult.FilePath}";
		return true;
	}

	private static void TryPreserveFileMetadata(string sourcePath, string destinationPath)
	{
		if (!File.Exists(sourcePath) || !File.Exists(destinationPath))
		{
			return;
		}

		try
		{
			var createdUtc = File.GetCreationTimeUtc(sourcePath);
			if (createdUtc > DateTime.UnixEpoch)
			{
				File.SetCreationTimeUtc(destinationPath, createdUtc);
			}

			File.SetLastWriteTimeUtc(destinationPath, File.GetLastWriteTimeUtc(sourcePath));
			File.SetLastAccessTimeUtc(destinationPath, File.GetLastAccessTimeUtc(sourcePath));
		}
		catch
		{
		}
	}

	private async Task<string> CreateWorkingCopyAsync(string sourcePath)
	{
		var tempDirectory = Path.Combine(FileSystem.CacheDirectory, "working");
		Directory.CreateDirectory(tempDirectory);
		var workingPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.pdf");
		await Task.Run(() => File.Copy(sourcePath, workingPath, overwrite: true));
		return workingPath;
	}

	private string BuildWorkingOutputPath(string suffix)
	{
		var tempDirectory = Path.Combine(FileSystem.CacheDirectory, "working");
		Directory.CreateDirectory(tempDirectory);
		return Path.Combine(tempDirectory, $"{suffix}_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}.pdf");
	}

	private async Task<ImageSource?> CreateThumbnailAsync(string pdfPath, int pageNumber)
	{
		#if MACCATALYST
		return await Task.Run(() =>
		{
			using var url = NSUrl.FromFilename(pdfPath);
			using var document = new PdfDocument(url);
			var page = document?.GetPage(pageNumber - 1);
			if (page is null)
			{
				return (ImageSource?)null;
			}

			using var image = page.GetThumbnail(new CGSize(120, 160), PdfDisplayBox.Media);
			using var pngData = image.AsPNG();
			if (pngData is null)
			{
				return (ImageSource?)null;
			}

			var thumbDirectory = Path.Combine(FileSystem.CacheDirectory, "thumbs");
			Directory.CreateDirectory(thumbDirectory);
			var thumbPath = Path.Combine(thumbDirectory, $"{Path.GetFileNameWithoutExtension(pdfPath)}_{pageNumber}.png");

			pngData.Save(thumbPath, false, out _);
			return ImageSource.FromFile(thumbPath);
		});
		#else
		await Task.CompletedTask;
		return null;
		#endif
	}

	private async Task<FileResult?> PickSinglePdfAsync()
	{
		return await FilePicker.Default.PickAsync(new PickOptions
		{
			PickerTitle = "Choose a PDF",
			FileTypes = BuildPdfFileType()
		});
	}

	private static FilePickerFileType BuildPdfFileType()
	{
		return new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
		{
			{ DevicePlatform.WinUI, new[] { ".pdf" } },
			{ DevicePlatform.MacCatalyst, new[] { "public.pdf", "com.adobe.pdf", ".pdf" } }
		});
	}

	private static bool IsValidPdfPath(string path)
	{
		return !string.IsNullOrWhiteSpace(path)
		       && File.Exists(path)
		       && string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
	}

	private async Task RunOperationAsync(string progressMessage, Func<Task> operation)
	{
		SetBusy(true, progressMessage);
		try
		{
			await operation();
		}
		catch (Exception ex)
		{
			StatusLabel.Text = ex.Message;
		}
		finally
		{
			SetBusy(false, StatusLabel.Text);
		}
	}

	private void SetBusy(bool busy, string message)
	{
		BusyIndicator.IsVisible = busy;
		BusyIndicator.IsRunning = busy;

		OpenPdfButton.IsEnabled = !busy;
		MergePdfButton.IsEnabled = !busy;
		OptimizePdfButton.IsEnabled = !busy;
		OptimizeLossyPdfButton.IsEnabled = !busy;
		OpenInSystemViewerButton.IsEnabled = !busy;
		DocumentTabsCollectionView.IsEnabled = !busy;
		PagesCollectionView.IsEnabled = !busy;

		StatusLabel.Text = message;
	}

	private void UpdateDebugState()
	{
		#if DEBUG
		DebugStateLabel.IsVisible = true;
		var activeTitle = _activeTab?.Title ?? "(none)";
		var pageCount = _activeTab?.PageCount ?? 0;
		var currentPage = _pdfPreview.CurrentPageNumber;
		DebugStateLabel.Text = $"tab={activeTitle} pages={pageCount} current={currentPage}";
		#endif
	}

	public sealed class PdfTabItem : INotifyPropertyChanged
	{
		private string _sourcePath;
		private string _workingFilePath;
		private string _title;
		private bool _isDirty;
		private int _pageCount;

		public PdfTabItem(string sourcePath, string workingFilePath, int pageCount)
		{
			_sourcePath = sourcePath;
			_workingFilePath = workingFilePath;
			_title = Path.GetFileName(sourcePath);
			_pageCount = pageCount;
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		public string SourcePath
		{
			get => _sourcePath;
			set
			{
				if (value == _sourcePath)
				{
					return;
				}

				_sourcePath = value;
				OnPropertyChanged();
			}
		}

		public string WorkingFilePath
		{
			get => _workingFilePath;
			set
			{
				if (value == _workingFilePath)
				{
					return;
				}

				_workingFilePath = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(DisplayTitle));
			}
		}

		public string Title
		{
			get => _title;
			set
			{
				if (value == _title)
				{
					return;
				}

				_title = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(DisplayTitle));
			}
		}

		public int PageCount
		{
			get => _pageCount;
			set
			{
				if (value == _pageCount)
				{
					return;
				}

				_pageCount = value;
				OnPropertyChanged();
			}
		}

		public bool IsDirty
		{
			get => _isDirty;
			set
			{
				if (value == _isDirty)
				{
					return;
				}

				_isDirty = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(DisplayTitle));
			}
		}

		public string DisplayTitle
		{
			get
			{
				var sizeText = FormatFileSize(WorkingFilePath);
				var titleWithSize = $"{Title} ({sizeText})";
				return IsDirty ? $"{titleWithSize}*" : titleWithSize;
			}
		}

		private static string FormatFileSize(string filePath)
		{
			try
			{
				var bytes = new FileInfo(filePath).Length;
				if (bytes < 1024)
				{
					return $"{bytes} B";
				}

				var kilobytes = bytes / 1024d;
				if (kilobytes < 1024)
				{
					return $"{kilobytes:0.#} KB";
				}

				var megabytes = kilobytes / 1024d;
				if (megabytes < 1024)
				{
					return $"{megabytes:0.#} MB";
				}

				var gigabytes = megabytes / 1024d;
				return $"{gigabytes:0.##} GB";
			}
			catch
			{
				return "size n/a";
			}
		}

		private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public sealed class PdfPageItem : INotifyPropertyChanged
	{
		private ImageSource? _thumbnailSource;

		public PdfPageItem(int number)
		{
			Number = number;
			DisplayName = $"Page {number}";
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		public int Number { get; }
		public string DisplayName { get; }

		public ImageSource? ThumbnailSource
		{
			get => _thumbnailSource;
			set
			{
				if (ReferenceEquals(value, _thumbnailSource))
				{
					return;
				}

				_thumbnailSource = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailSource)));
			}
		}
	}
}
