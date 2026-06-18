using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpToUtf8.Conversion;

/// <summary>
/// Ude 偵測出的編碼名稱 → .NET Encoding 名稱/codepage 對照表。
/// Ude 只「說」編碼是什麼，本類別負責「找對的 .NET Encoding」來實際解碼。
/// 涵蓋常見中港日韓與西歐編碼，Ude 在中文樣本不足時也可能回傳西歐編碼（信心度通常偏低）。
/// 呼叫 <see cref="Resolve"/> 前須先 <see cref="EncodingBootstrap.Register"/>，
/// 否則 Big5/GBK/Shift_JIS 等東亞編碼會丟 <see cref="ArgumentException"/>。
/// </summary>
public static class UdeToDotNetEncodingMap
{
    // Ude 可能回傳的 charset 名稱 → .NET Encoding 名稱
    // 大小寫不敏感比對（Ude 大多使用標準 IANA 名稱）
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // 中港
        ["Big5"] = "big5",                  // cp950, 繁體中文
        ["GB18030"] = "GB18030",            // 簡體中文（強制）
        ["GB2312"] = "GB2312",              // cp20936, 簡體中文
        ["GBK"] = "GBK",                    // cp936, 簡體中文（Windows）
        ["HZ-GB-2312"] = "GB2312",          // 罕見郵件編碼
        // 日韓
        ["Shift_JIS"] = "shift_jis",        // cp932, 日文
        ["EUC-JP"] = "euc-jp",
        ["EUC-KR"] = "euc-kr",
        ["ISO-2022-JP"] = "iso-2022-jp",
        // 西歐（Ude 在中文樣本不足時可能誤判為這些，信心度通常偏低）
        ["ISO-8859-1"] = "iso-8859-1",
        ["ISO-8859-2"] = "iso-8859-2",
        ["ISO-8859-15"] = "iso-8859-15",
        ["windows-1252"] = "windows-1252",
        ["windows-1250"] = "windows-1250",
        ["windows-1251"] = "windows-1251",  // 西里爾
        // UTF 系列（理論上 Ude 不會在有 BOM 的情況下回這些，無 BOM 短樣本時可能回 "UTF-8"）
        ["UTF-8"] = "utf-8",
        ["UTF-16"] = "utf-16",
        ["UTF-16BE"] = "utf-16BE",
        ["UTF-16LE"] = "utf-16LE",
        ["UTF-32"] = "utf-32",
    };

    /// <summary>
    /// 給定 Ude 偵測結果，回傳對應的 <see cref="Encoding"/>。
    /// 找不到或註冊失敗時回傳 null（呼叫端需 fallback 到 UTF-8 處理路徑）。
    /// </summary>
    public static Encoding? Resolve(string? udeCharset)
    {
        if (string.IsNullOrEmpty(udeCharset))
        {
            return null;
        }
        if (Map.TryGetValue(udeCharset, out var dotnetName))
        {
            return TryGetEncoding(dotnetName);
        }
        // 退路：嘗試直接用 Ude 名稱（少數情況下 .NET 與 Ude 名稱一致）
        return TryGetEncoding(udeCharset);
    }

    private static Encoding? TryGetEncoding(string name)
    {
        try
        {
            return Encoding.GetEncoding(name);
        }
        catch
        {
            // 名稱無效或 provider 未註冊
            return null;
        }
    }
}
