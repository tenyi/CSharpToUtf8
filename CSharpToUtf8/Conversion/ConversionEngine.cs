using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CSharpToUtf8.Conversion;

/// <summary>
/// 編碼轉換核心引擎（orchestrator）。
/// 集中實作 <c>CLAUDE.md</c> 中 BOM 雙參數對照表的決策邏輯：
/// <list type="bullet">
/// <item><c>addBom</c>：轉出 UTF-8 是否帶 BOM</item>
/// <item><c>overrideExistingUtf8Bom</c>：已是 UTF-8 的檔案是否要重寫以調整 BOM</item>
/// </list>
/// 預設 <c>addBom=false</c> 且 <c>overrideExistingUtf8Bom=false</c>：不加 BOM，也不動已是 UTF-8 的檔案。
/// 信心度門檻 <see cref="ConfidenceThreshold"/>（70%）：低於此值的非 UTF-8 檔案只警告不寫入（安全優先）。
/// </summary>
public static class ConversionEngine
{
    /// <summary>
    /// 信心度門檻。低於此值的非 UTF-8 檔案只警告不寫入（避免誤判造成亂碼）。
    /// </summary>
    public const float ConfidenceThreshold = 0.70f;

    /// <summary>
    /// 處理單一檔案，回傳 <see cref="ConversionResult"/>。檔案不存在或讀寫失敗會回傳 <see cref="ConversionAction.Error"/>。
    /// </summary>
    public static ConversionResult Process(
        string filePath,
        bool addBom,
        bool overrideExistingUtf8Bom)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(filePath);
        }
        catch (Exception ex)
        {
            return ErrorResult(filePath, $"讀檔失敗: {ex.Message}");
        }

        // 1) BOM 確定性判定（優先於 Ude 統計學）
        var bom = BomInspector.Inspect(bytes);

        // 2) Ude 統計學偵測
        var detected = UdeCharsetDetector.Detect(bytes);

        // 3) 判定是否為「已是 UTF-8」
        //    三條路徑任一成立：a) 有任何 UTF BOM b) Ude 明確說是 UTF-8 c) Ude 退化為 null（純 ASCII / 無資訊）
        bool hasUtfBom = bom.Kind is BomKind.Utf8
            or BomKind.Utf16Le
            or BomKind.Utf16Be
            or BomKind.Utf32Le
            or BomKind.Utf32Be;
        bool udeClaimsUtf8 = detected.Charset is not null
            && detected.Charset.StartsWith("UTF-8", StringComparison.OrdinalIgnoreCase);
        bool udeClaimsAscii = detected.Charset is null; // null = 退化為 UTF-8 相容

        bool wasAlreadyUtf8 = hasUtfBom || udeClaimsUtf8 || udeClaimsAscii;

        // 4) 分派到對應處理路徑
        if (wasAlreadyUtf8)
        {
            return HandleAlreadyUtf8(filePath, bytes, bom, detected, addBom, overrideExistingUtf8Bom);
        }
        return HandleNonUtf8(filePath, bytes, bom, detected, addBom);
    }

    /// <summary>
    /// 已是 UTF-8 檔案的處理：依 <c>overrideExistingUtf8Bom</c> 決定跳過或覆寫 BOM。
    /// UTF-16/32 BOM 視為「非 UTF-8 編碼」，降級到非 UTF-8 處理路徑（重新解碼為字串再以 UTF-8 重寫）。
    /// </summary>
    private static ConversionResult HandleAlreadyUtf8(
        string filePath,
        byte[] bytes,
        BomInspectionResult bom,
        UdeDetectionResult detected,
        bool addBom,
        bool overrideExistingUtf8Bom)
    {
        // UTF-16/32 BOM 不是 UTF-8 編碼，走非 UTF-8 處理路徑
        if (bom.Kind is BomKind.Utf16Le or BomKind.Utf16Be or BomKind.Utf32Le or BomKind.Utf32Be)
        {
            return HandleNonUtf8(filePath, bytes, bom, detected, addBom);
        }

        // 預設：override=false → 保持原樣不重寫
        if (!overrideExistingUtf8Bom)
        {
            return new ConversionResult(
                FilePath: filePath,
                DetectedCharset: detected.Charset ?? "UTF-8",
                MappedEncodingName: "utf-8",
                Confidence: detected.Confidence,
                HadBom: bom.Kind != BomKind.None,
                BomKind: bom.Kind,
                WasAlreadyUtf8: true,
                Action: ConversionAction.SkippedAlreadyUtf8,
                Warning: null,
                Error: null);
        }

        // override=true：依 addBom 決定目標 BOM 狀態
        bool currentlyHasBom = bom.Kind == BomKind.Utf8;
        bool targetHasBom = addBom;
        if (currentlyHasBom == targetHasBom)
        {
            // 現狀 == 目標，無實質變更（記錄此狀態以利 UI 反映）
            return new ConversionResult(
                FilePath: filePath,
                DetectedCharset: detected.Charset ?? "UTF-8",
                MappedEncodingName: "utf-8",
                Confidence: detected.Confidence,
                HadBom: bom.Kind != BomKind.None,
                BomKind: bom.Kind,
                WasAlreadyUtf8: true,
                Action: ConversionAction.OverriddenNoChange,
                Warning: null,
                Error: null);
        }

        // 需要重寫 BOM：以 UTF-8 解碼內容（剝掉現有 BOM），再以目標 BOM 重新編碼
        try
        {
            var contentBytes = currentlyHasBom ? bytes[bom.Length..] : bytes;
            // 已是 UTF-8 路徑，內容已是合法 UTF-8，用無 BOM decoder 解析
            var decodeEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
            string text = decodeEncoding.GetString(contentBytes);
            var targetEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: addBom);
            File.WriteAllText(filePath, text, targetEncoding);

            return new ConversionResult(
                FilePath: filePath,
                DetectedCharset: detected.Charset ?? "UTF-8",
                MappedEncodingName: "utf-8",
                Confidence: detected.Confidence,
                HadBom: addBom,
                BomKind: addBom ? BomKind.Utf8 : BomKind.None,
                WasAlreadyUtf8: true,
                Action: ConversionAction.OverriddenRewritten,
                Warning: null,
                Error: null);
        }
        catch (Exception ex)
        {
            return new ConversionResult(
                FilePath: filePath,
                DetectedCharset: detected.Charset ?? "UTF-8",
                MappedEncodingName: "utf-8",
                Confidence: detected.Confidence,
                HadBom: bom.Kind != BomKind.None,
                BomKind: bom.Kind,
                WasAlreadyUtf8: true,
                Action: ConversionAction.Error,
                Warning: null,
                Error: $"覆寫 BOM 失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// 非 UTF-8 檔案的處理：依信心度決定是否實際轉換。
    /// 信心度低於門檻 → 只警告不寫入；信心度足夠 → 用對映的 .NET Encoding 解碼後以 UTF-8 重寫。
    /// </summary>
    private static ConversionResult HandleNonUtf8(
        string filePath,
        byte[] bytes,
        BomInspectionResult bom,
        UdeDetectionResult detected,
        bool addBom)
    {
        // 跳過 BOM 取內容
        var content = bom.Length > 0 ? bytes[bom.Length..] : bytes;

        // 信心度不足 → 只警告不寫入
        if (detected.Confidence < ConfidenceThreshold)
        {
            return new ConversionResult(
                FilePath: filePath,
                DetectedCharset: detected.Charset,
                MappedEncodingName: null,
                Confidence: detected.Confidence,
                HadBom: bom.Kind != BomKind.None,
                BomKind: bom.Kind,
                WasAlreadyUtf8: false,
                Action: ConversionAction.NotWrittenLowConfidence,
                Warning: $"此檔案疑似為 {detected.Charset ?? "未知編碼"}，但信心度僅 {detected.Confidence:P0}，請人工確認。",
                Error: null);
        }

        // 信心度足夠 → 對映到 .NET Encoding
        var sourceEncoding = UdeToDotNetEncodingMap.Resolve(detected.Charset);
        if (sourceEncoding is null)
        {
            return new ConversionResult(
                FilePath: filePath,
                DetectedCharset: detected.Charset,
                MappedEncodingName: null,
                Confidence: detected.Confidence,
                HadBom: bom.Kind != BomKind.None,
                BomKind: bom.Kind,
                WasAlreadyUtf8: false,
                Action: ConversionAction.NotWrittenLowConfidence,
                Warning: $"Ude 偵測為 {detected.Charset}，但找不到對應的 .NET Encoding，無法轉換。",
                Error: null);
        }

        try
        {
            string text = sourceEncoding.GetString(content);
            var targetEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: addBom);
            File.WriteAllText(filePath, text, targetEncoding);

            return new ConversionResult(
                FilePath: filePath,
                DetectedCharset: detected.Charset,
                MappedEncodingName: sourceEncoding.WebName,
                Confidence: detected.Confidence,
                HadBom: bom.Kind != BomKind.None,
                BomKind: bom.Kind,
                WasAlreadyUtf8: false,
                Action: ConversionAction.Converted,
                Warning: null,
                Error: null);
        }
        catch (Exception ex)
        {
            return new ConversionResult(
                FilePath: filePath,
                DetectedCharset: detected.Charset,
                MappedEncodingName: sourceEncoding.WebName,
                Confidence: detected.Confidence,
                HadBom: bom.Kind != BomKind.None,
                BomKind: bom.Kind,
                WasAlreadyUtf8: false,
                Action: ConversionAction.Error,
                Warning: null,
                Error: $"轉換失敗: {ex.Message}");
        }
    }

    private static ConversionResult ErrorResult(string filePath, string error) =>
        new(
            FilePath: filePath,
            DetectedCharset: null,
            MappedEncodingName: null,
            Confidence: 0f,
            HadBom: false,
            BomKind: BomKind.None,
            WasAlreadyUtf8: false,
            Action: ConversionAction.Error,
            Warning: null,
            Error: error);

    /// <summary>
    /// 遞迴掃描指定資料夾，處理所有 <c>.cs</c> 檔案。
    /// 若資料夾不存在，回傳空清單。
    /// </summary>
    public static IReadOnlyList<ConversionResult> RunOnDirectory(
        string folderPath,
        bool addBom,
        bool overrideExistingUtf8Bom)
    {
        var results = new List<ConversionResult>();
        if (!Directory.Exists(folderPath))
        {
            return results;
        }
        foreach (var file in Directory.EnumerateFiles(folderPath, "*.cs", SearchOption.AllDirectories))
        {
            results.Add(Process(file, addBom, overrideExistingUtf8Bom));
        }
        return results;
    }
}
