namespace CSharpToUtf8.Conversion;

/// <summary>
/// 對單一檔案的處理動作。
/// </summary>
public enum ConversionAction
{
    /// <summary>非 UTF-8 檔案已成功轉成 UTF-8（依 <c>addBom</c> 決定 BOM）</summary>
    Converted,
    /// <summary>已是 UTF-8（帶 BOM 或無 BOM），預設情況下不重寫</summary>
    SkippedAlreadyUtf8,
    /// <summary>已是 UTF-8 且 <c>overrideExistingUtf8Bom=true</c>，已重寫 BOM 狀態</summary>
    OverriddenRewritten,
    /// <summary>已是 UTF-8 且 <c>overrideExistingUtf8Bom=true</c>，但 addBom 與現狀相同，無實質變更</summary>
    OverriddenNoChange,
    /// <summary>信心度低於門檻（70%），不寫入，僅警告</summary>
    NotWrittenLowConfidence,
    /// <summary>處理過程發生錯誤（讀檔/寫檔失敗）</summary>
    Error,
}

/// <summary>
/// 單一檔案編碼處理結果的資料契約（純資料，從核心邏輯傳給 UI 顯示）。
/// 使用 sealed record 確保不可變，方便測試斷言。
/// </summary>
public sealed record ConversionResult(
    /// <summary>絕對或相對檔案路徑</summary>
    string FilePath,
    /// <summary>Ude 偵測出的編碼名稱（如 "Big5" / "GB18030"）；null = UTF-8 相容退化</summary>
    string? DetectedCharset,
    /// <summary>對應到 .NET Encoding 的名稱（如 "big5" / "GB18030"）</summary>
    string? MappedEncodingName,
    /// <summary>Ude 信心度，0.0 ~ 1.0</summary>
    float Confidence,
    /// <summary>檔案是否有任何 BOM</summary>
    bool HadBom,
    /// <summary>BOM 種類</summary>
    BomKind BomKind,
    /// <summary>是否已是 UTF-8 編碼（含帶 BOM、無 BOM 退化、Ude 判定 UTF-8）</summary>
    bool WasAlreadyUtf8,
    /// <summary>實際採取的處理動作</summary>
    ConversionAction Action,
    /// <summary>低信心度警告訊息（UI 顯示 ⚠）；null 表示無警告</summary>
    string? Warning,
    /// <summary>錯誤訊息（讀檔/寫檔失敗時）；null 表示無錯誤</summary>
    string? Error
);
