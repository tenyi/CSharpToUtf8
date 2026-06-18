using System;
using Ude;

namespace CSharpToUtf8.Conversion;

/// <summary>
/// Ude 統計學偵測結果。<see cref="Charset"/> 為 null 時代表退化為 UTF-8 相容（純 ASCII / 偵測不到）。
/// </summary>
public readonly record struct UdeDetectionResult(string? Charset, float Confidence);

/// <summary>
/// 封裝 Ude.CharsetDetector（Mozilla Universal Charset Detector 的 C# 移植）。
/// 純粹「統計學」猜測編碼 + 信心度，不負責轉碼。
/// 處理 null / "ASCII" 退化為 UTF-8 相容路徑（Charset=null, Confidence=1.0）。
/// </summary>
public static class UdeCharsetDetector
{
    /// <summary>
    /// 對輸入位元組做統計學編碼偵測。
    /// 空檔案視為 UTF-8 無 BOM（信心度 1.0）。
    /// Ude 對 null 或 "ASCII" 的結果會映射成 Charset=null, Confidence=1.0（代表 UTF-8 相容）。
    /// </summary>
    public static UdeDetectionResult Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            // 空檔案視為 UTF-8 無 BOM，信心度 1.0
            return new UdeDetectionResult(null, 1.0f);
        }

        var detector = new CharsetDetector();
        // Ude 1.2.0 提供 Feed(byte[], int, int) 多載；複製到陣列是因為 Span 不能直接傳遞
        detector.Feed(bytes.ToArray(), 0, bytes.Length);
        detector.DataEnd();

        var charset = detector.Charset;
        var confidence = detector.Confidence;

        // null / "ASCII" / 空字串：退化為 UTF-8 相容（無資訊，視為 UTF-8 處理路徑）
        if (string.IsNullOrEmpty(charset) ||
            charset.Equals("ASCII", StringComparison.OrdinalIgnoreCase))
        {
            return new UdeDetectionResult(null, 1.0f);
        }

        return new UdeDetectionResult(charset, confidence);
    }
}
