using System;

namespace CSharpToUtf8.Conversion;

/// <summary>
/// BOM 種類。None 表示檔案沒有 BOM。
/// </summary>
public enum BomKind
{
    /// <summary>無 BOM</summary>
    None,
    /// <summary>UTF-8 BOM（EF BB BF）</summary>
    Utf8,
    /// <summary>UTF-16 LE BOM（FF FE）</summary>
    Utf16Le,
    /// <summary>UTF-16 BE BOM（FE FF）</summary>
    Utf16Be,
    /// <summary>UTF-32 LE BOM（FF FE 00 00）</summary>
    Utf32Le,
    /// <summary>UTF-32 BE BOM（00 00 FE FF）</summary>
    Utf32Be,
}

/// <summary>
/// BOM 判定結果。<see cref="Length"/> 為 BOM 佔用的位元組數（用於跳過 BOM 取內容）。
/// </summary>
public readonly record struct BomInspectionResult(BomKind Kind, int Length);

/// <summary>
/// 確定性 BOM 判定：檢查檔案前幾個位元組是否為已知 UTF BOM。
/// 與 Ude 統計學偵測互補：此層為「確定性」短路，BOM 是檔案自我聲明編碼的位元組，
/// 必須優先於統計學結果。
/// 注意：UTF-32 LE 與 UTF-16 LE 的前 2 bytes 都是 FF FE，必須先檢查長的（4 bytes）再檢查短的（2 bytes）。
/// </summary>
public static class BomInspector
{
    // UTF-8 BOM: EF BB BF
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    // UTF-16 LE BOM: FF FE
    private static readonly byte[] Utf16LeBom = [0xFF, 0xFE];
    // UTF-16 BE BOM: FE FF
    private static readonly byte[] Utf16BeBom = [0xFE, 0xFF];
    // UTF-32 LE BOM: FF FE 00 00
    private static readonly byte[] Utf32LeBom = [0xFF, 0xFE, 0x00, 0x00];
    // UTF-32 BE BOM: 00 00 FE FF
    private static readonly byte[] Utf32BeBom = [0x00, 0x00, 0xFE, 0xFF];

    /// <summary>
    /// 檢查輸入位元組的 BOM 種類。
    /// </summary>
    public static BomInspectionResult Inspect(ReadOnlySpan<byte> bytes)
    {
        // 必須先檢查 UTF-32 LE（4 bytes），再檢查 UTF-16 LE（2 bytes），因為前 2 bytes 重疊
        if (bytes.Length >= Utf32LeBom.Length && bytes[..Utf32LeBom.Length].SequenceEqual(Utf32LeBom))
        {
            return new BomInspectionResult(BomKind.Utf32Le, Utf32LeBom.Length);
        }
        if (bytes.Length >= Utf32BeBom.Length && bytes[..Utf32BeBom.Length].SequenceEqual(Utf32BeBom))
        {
            return new BomInspectionResult(BomKind.Utf32Be, Utf32BeBom.Length);
        }
        if (bytes.Length >= Utf8Bom.Length && bytes[..Utf8Bom.Length].SequenceEqual(Utf8Bom))
        {
            return new BomInspectionResult(BomKind.Utf8, Utf8Bom.Length);
        }
        if (bytes.Length >= Utf16LeBom.Length && bytes[..Utf16LeBom.Length].SequenceEqual(Utf16LeBom))
        {
            return new BomInspectionResult(BomKind.Utf16Le, Utf16LeBom.Length);
        }
        if (bytes.Length >= Utf16BeBom.Length && bytes[..Utf16BeBom.Length].SequenceEqual(Utf16BeBom))
        {
            return new BomInspectionResult(BomKind.Utf16Be, Utf16BeBom.Length);
        }
        return new BomInspectionResult(BomKind.None, 0);
    }
}
