using System.IO;
using Avalonia.Media;
using CSharpToUtf8.Conversion;

namespace CSharpToUtf8;

/// <summary>
/// ListBox 列顯示用：將 <see cref="ConversionResult"/> 轉成可讀標籤與色彩。
/// </summary>
public sealed class ResultRowView
{
    private static readonly IBrush ConvertedBrush = SolidColorBrush.Parse("#2E7D32");
    private static readonly IBrush SkippedBrush = SolidColorBrush.Parse("#616161");
    private static readonly IBrush OverriddenBrush = SolidColorBrush.Parse("#1565C0");
    private static readonly IBrush NoChangeBrush = SolidColorBrush.Parse("#757575");
    private static readonly IBrush WarningBrush = SolidColorBrush.Parse("#E65100");
    private static readonly IBrush ErrorBrush = SolidColorBrush.Parse("#C62828");
    private static readonly IBrush MutedBrush = SolidColorBrush.Parse("#9E9E9E");

    public ResultRowView(ConversionResult source) => Source = source;

    public ConversionResult Source { get; }

    public string FilePath => Source.FilePath;

    public string FileName => Path.GetFileName(Source.FilePath);

    public string DetectedCharset => Source.DetectedCharset ?? "UTF-8";

    public string ConfidenceText => $"{Source.Confidence:P0}";

    public string BomKindText => Source.BomKind switch
    {
        BomKind.None => "無",
        BomKind.Utf8 => "UTF-8",
        BomKind.Utf16Le => "UTF-16 LE",
        BomKind.Utf16Be => "UTF-16 BE",
        BomKind.Utf32Le => "UTF-32 LE",
        BomKind.Utf32Be => "UTF-32 BE",
        _ => Source.BomKind.ToString(),
    };

    public string ActionText => Source.Action switch
    {
        ConversionAction.Converted => "已轉換",
        ConversionAction.SkippedAlreadyUtf8 => "已是 UTF-8",
        ConversionAction.OverriddenRewritten => "已覆寫 BOM",
        ConversionAction.OverriddenNoChange => "BOM 無需變更",
        ConversionAction.NotWrittenLowConfidence => "未寫入",
        ConversionAction.Error => "錯誤",
        _ => Source.Action.ToString(),
    };

    public string Note => Source.Error ?? Source.Warning ?? "—";

    public IBrush ActionBrush => Source.Action switch
    {
        ConversionAction.Converted => ConvertedBrush,
        ConversionAction.SkippedAlreadyUtf8 => SkippedBrush,
        ConversionAction.OverriddenRewritten => OverriddenBrush,
        ConversionAction.OverriddenNoChange => NoChangeBrush,
        ConversionAction.NotWrittenLowConfidence => WarningBrush,
        ConversionAction.Error => ErrorBrush,
        _ => Brushes.Gray,
    };

    public IBrush NoteBrush => Source.Error is not null
        ? ErrorBrush
        : Source.Warning is not null
            ? WarningBrush
            : MutedBrush;
}
