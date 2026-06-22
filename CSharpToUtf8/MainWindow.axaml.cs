using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CSharpToUtf8.Conversion;


namespace CSharpToUtf8;

public partial class MainWindow : Window
{
    private readonly List<ResultRowView> _allResultRows = [];
    private readonly ObservableCollection<ResultRowView> _resultRows = [];
    private bool _isRunning;
    private bool _hasConversionSummary;
    private ResultFilter _activeFilter = ResultFilter.All;
    private string? _lastOpenedFilePath;
    private DateTime _lastOpenedAtUtc;

    public MainWindow()
    {
        InitializeComponent();
        ResultsList.ItemsSource = _resultRows;
        UpdateEmptyHintVisibility();
    }

    private async void OnPickFolderClick(object? sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "選擇包含 .cs 檔案的資料夾",
            AllowMultiple = false,
        });
        if (folders.Count > 0)
        {
            var path = folders[0].Path.LocalPath;
            FolderPathText.Text = path;
            SetStatus($"已選擇資料夾：{path}", StatusKind.Ready);
        }
    }

    private async void OnRunClick(object? sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        var path = FolderPathText.Text;
        if (string.IsNullOrWhiteSpace(path) || path == "（尚未選擇）")
        {
            SetStatus("請先選擇資料夾", StatusKind.Warning);
            return;
        }

        if (!Directory.Exists(path))
        {
            SetStatus($"資料夾不存在：{path}", StatusKind.Error);
            return;
        }

        RunButton.IsEnabled = false;
        _isRunning = true;
        SetRunningControls(false);
        ProgressOverlay.IsVisible = true;
        ProgressOverlay.BeginScanning();
        SetStatus("正在掃描資料夾…", StatusKind.Running);

        ClearResults();
        ResetResultFilter();

        await Task.Yield();

        var addBom = AddBomCheck.IsChecked ?? false;
        var overrideExistingUtf8Bom = OverrideCheck.IsChecked ?? false;

        List<string> files;
        try
        {
            files = await Task.Run(() =>
                Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories).ToList());
        }
        catch (Exception ex)
        {
            HideProgressOverlay();
            SetStatus($"掃描失敗：{ex.Message}", StatusKind.Error);
            return;
        }

        ProgressOverlay.SetScanComplete(files.Count);
        SetStatus($"掃描完成，找到 {files.Count} 個 .cs 檔案", StatusKind.Running);

        if (files.Count == 0)
        {
            await Task.Delay(600);
            ClearResults();
            HideProgressOverlay();
            SetStatus("此資料夾內沒有 .cs 檔案", StatusKind.Warning);
            return;
        }

        await Task.Delay(450);

        ClearResults();

        var results = new List<ConversionResult>(files.Count);

        try
        {
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var fileName = Path.GetFileName(file);
                var index = i + 1;

                ProgressOverlay.SetProcessing(index, files.Count, fileName);
                SetStatus($"處理中 ({index}/{files.Count})：{file}", StatusKind.Running);

                var result = await Task.Run(() =>
                    ConversionEngine.Process(file, addBom, overrideExistingUtf8Bom));

                results.Add(result);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _allResultRows.Add(new ResultRowView(result));
                    if (MatchesFilter(_allResultRows[^1]))
                    {
                        _resultRows.Add(_allResultRows[^1]);
                    }

                    UpdateEmptyHintVisibility();
                });
            }

            ProgressOverlay.SetFinishing();
            await Task.Delay(350);

            var total = results.Count;
            var converted = results.Count(r => r.Action == ConversionAction.Converted);
            var overridden = results.Count(r =>
                r.Action is ConversionAction.OverriddenRewritten or ConversionAction.OverriddenNoChange);
            var skipped = results.Count(r => r.Action == ConversionAction.SkippedAlreadyUtf8);
            var warned = results.Count(r => r.Warning != null);
            var errors = results.Count(r => r.Action == ConversionAction.Error);

            SetConversionSummary(
                total,
                converted,
                overridden,
                skipped,
                warned,
                errors,
                errors > 0 ? StatusKind.Error : warned > 0 ? StatusKind.Warning : StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"執行失敗：{ex.Message}", StatusKind.Error);
        }
        finally
        {
            HideProgressOverlay();
        }
    }

    private void HideProgressOverlay()
    {
        ProgressOverlay.IsVisible = false;
        _isRunning = false;
        RunButton.IsEnabled = true;
        SetRunningControls(true);
    }

    private void SetRunningControls(bool enabled)
    {
        PickFolderButton.IsEnabled = enabled;
        AddBomCheck.IsEnabled = enabled;
        OverrideCheck.IsEnabled = enabled;
    }

    private void OnResultTapped(object? sender, TappedEventArgs e)
    {
        var row = GetResultRowFromEvent(e);
        if (row is not null)
        {
            OpenFileInEditor(row);
        }
    }

    private void OnResultsListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ResultsList.SelectedItem is ResultRowView row)
        {
            OpenFileInEditor(row);
            e.Handled = true;
        }
    }

    private static ResultRowView? GetResultRowFromEvent(RoutedEventArgs e)
    {
        if (e.Source is not Visual visual)
        {
            return null;
        }

        var listBoxItem = visual.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
        return listBoxItem?.DataContext as ResultRowView;
    }

    private void OpenFileInEditor(ResultRowView row)
    {
        if (_isRunning)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (row.FilePath == _lastOpenedFilePath
            && (now - _lastOpenedAtUtc).TotalMilliseconds < 400)
        {
            return;
        }

        _lastOpenedFilePath = row.FilePath;
        _lastOpenedAtUtc = now;

        var filePath = row.FilePath;
        if (!File.Exists(filePath))
        {
            SetStatus($"檔案不存在，無法開啟：{filePath}", StatusKind.Error);
            return;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true,
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", filePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", filePath);
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true,
                });
            }
            SetStatus($"已在編輯器中開啟：{row.FileName}", StatusKind.Ready);
        }
        catch (Exception ex)
        {
            SetStatus($"無法開啟檔案：{ex.Message}", StatusKind.Error);
        }
    }

    private void OnFilterConvertedClick(object? sender, RoutedEventArgs e) =>
        ToggleFilter(ResultFilter.Converted);

    private void OnFilterOverriddenClick(object? sender, RoutedEventArgs e) =>
        ToggleFilter(ResultFilter.Overridden);

    private void OnFilterSkippedClick(object? sender, RoutedEventArgs e) =>
        ToggleFilter(ResultFilter.Skipped);

    private void OnFilterWarningClick(object? sender, RoutedEventArgs e) =>
        ToggleFilter(ResultFilter.Warning);

    private void OnFilterErrorClick(object? sender, RoutedEventArgs e) =>
        ToggleFilter(ResultFilter.Error);

    private void ToggleFilter(ResultFilter filter)
    {
        if (_isRunning || !_hasConversionSummary)
        {
            return;
        }

        _activeFilter = _activeFilter == filter ? ResultFilter.All : filter;
        ApplyResultFilter();
        UpdateFilterButtonStates();
    }

    private void ResetResultFilter()
    {
        _activeFilter = ResultFilter.All;
        _hasConversionSummary = false;
        HideFilterButtons();
    }

    private void ClearResults()
    {
        _allResultRows.Clear();
        _resultRows.Clear();
        UpdateEmptyHintVisibility();
    }

    private void ApplyResultFilter()
    {
        _resultRows.Clear();
        foreach (var row in _allResultRows.Where(MatchesFilter))
        {
            _resultRows.Add(row);
        }

        UpdateEmptyHintVisibility();
    }

    private bool MatchesFilter(ResultRowView row) => MatchesFilter(row.Source);

    private bool MatchesFilter(ConversionResult result) => _activeFilter switch
    {
        ResultFilter.All => true,
        ResultFilter.Converted => result.Action == ConversionAction.Converted,
        ResultFilter.Overridden => result.Action
            is ConversionAction.OverriddenRewritten
            or ConversionAction.OverriddenNoChange,
        ResultFilter.Skipped => result.Action == ConversionAction.SkippedAlreadyUtf8,
        ResultFilter.Warning => result.Warning is not null,
        ResultFilter.Error => result.Action == ConversionAction.Error,
        _ => true,
    };

    private void UpdateEmptyHintVisibility()
    {
        if (_allResultRows.Count == 0)
        {
            EmptyResultsHint.Text = "選擇資料夾並執行轉換後，結果會顯示於此（點擊檔案可在編輯器中開啟）";
            EmptyResultsHint.IsVisible = true;
            ResultsList.IsVisible = false;
            return;
        }

        if (_resultRows.Count == 0)
        {
            EmptyResultsHint.Text = _activeFilter == ResultFilter.All
                ? "選擇資料夾並執行轉換後，結果會顯示於此（點擊檔案可在編輯器中開啟）"
                : $"「{GetFilterLabel(_activeFilter)}」篩選下沒有符合的檔案（再點一次可顯示全部）";
            EmptyResultsHint.IsVisible = true;
            ResultsList.IsVisible = false;
            return;
        }

        EmptyResultsHint.IsVisible = false;
        ResultsList.IsVisible = true;
    }

    private static string GetFilterLabel(ResultFilter filter) => filter switch
    {
        ResultFilter.Converted => "轉換",
        ResultFilter.Overridden => "覆寫 BOM",
        ResultFilter.Skipped => "跳過",
        ResultFilter.Warning => "警告",
        ResultFilter.Error => "錯誤",
        _ => "全部",
    };

    private void SetConversionSummary(
        int total,
        int converted,
        int overridden,
        int skipped,
        int warned,
        int errors,
        StatusKind kind)
    {
        _hasConversionSummary = true;
        StatusPrefixText.Text = $"完成 — 共 {total} 個 .cs";
        StatusFilterSeparator.IsVisible = true;

        FilterConvertedButton.Content = $"轉換 {converted}";
        FilterConvertedButton.IsVisible = true;
        FilterConvertedButton.IsEnabled = converted > 0;
        SepAfterConverted.IsVisible = true;

        FilterOverriddenButton.Content = $"覆寫 BOM {overridden}";
        FilterOverriddenButton.IsVisible = true;
        FilterOverriddenButton.IsEnabled = overridden > 0;
        SepAfterOverridden.IsVisible = true;

        FilterSkippedButton.Content = $"跳過 {skipped}";
        FilterSkippedButton.IsVisible = true;
        FilterSkippedButton.IsEnabled = skipped > 0;
        SepAfterSkipped.IsVisible = true;

        FilterWarningButton.Content = $"警告 {warned}";
        FilterWarningButton.IsVisible = true;
        FilterWarningButton.IsEnabled = warned > 0;
        SepAfterWarning.IsVisible = true;

        FilterErrorButton.Content = $"錯誤 {errors}";
        FilterErrorButton.IsVisible = true;
        FilterErrorButton.IsEnabled = errors > 0;

        UpdateFilterButtonStates();
        SetStatusIndicator(kind);
    }

    private void HideFilterButtons()
    {
        StatusFilterSeparator.IsVisible = false;
        FilterConvertedButton.IsVisible = false;
        SepAfterConverted.IsVisible = false;
        FilterOverriddenButton.IsVisible = false;
        SepAfterOverridden.IsVisible = false;
        FilterSkippedButton.IsVisible = false;
        SepAfterSkipped.IsVisible = false;
        FilterWarningButton.IsVisible = false;
        SepAfterWarning.IsVisible = false;
        FilterErrorButton.IsVisible = false;
    }

    private void UpdateFilterButtonStates()
    {
        SetFilterButtonActive(FilterConvertedButton, _activeFilter == ResultFilter.Converted);
        SetFilterButtonActive(FilterOverriddenButton, _activeFilter == ResultFilter.Overridden);
        SetFilterButtonActive(FilterSkippedButton, _activeFilter == ResultFilter.Skipped);
        SetFilterButtonActive(FilterWarningButton, _activeFilter == ResultFilter.Warning);
        SetFilterButtonActive(FilterErrorButton, _activeFilter == ResultFilter.Error);
    }

    private static void SetFilterButtonActive(Button button, bool isActive)
    {
        if (isActive)
        {
            if (!button.Classes.Contains("active"))
            {
                button.Classes.Add("active");
            }
        }
        else
        {
            button.Classes.Remove("active");
        }
    }

    private enum StatusKind
    {
        Ready,
        Running,
        Success,
        Warning,
        Error,
    }

    private void SetStatus(string message, StatusKind kind)
    {
        StatusPrefixText.Text = message;
        HideFilterButtons();
        SetStatusIndicator(kind);
    }

    private void SetStatusIndicator(StatusKind kind)
    {
        StatusIndicator.Fill = kind switch
        {
            StatusKind.Running => new SolidColorBrush(Color.Parse("#F9A825")),
            StatusKind.Success => new SolidColorBrush(Color.Parse("#2E7D32")),
            StatusKind.Warning => new SolidColorBrush(Color.Parse("#E65100")),
            StatusKind.Error => new SolidColorBrush(Color.Parse("#C62828")),
            _ => Application.Current?.FindResource("ThemeAccentBrush") as IBrush
                 ?? new SolidColorBrush(Color.Parse("#1565C0")),
        };
    }
}
