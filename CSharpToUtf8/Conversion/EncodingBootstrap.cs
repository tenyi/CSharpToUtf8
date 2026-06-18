using System.Text;
using System.Threading;

namespace CSharpToUtf8.Conversion;

/// <summary>
/// 啟動時註冊 System.Text.Encoding.CodePages provider，
/// 以支援 Big5 (cp950)、GBK (cp936)、Shift_JIS (cp932)、GB18030 等東亞 codepage。
/// .NET 5+ 預設不包含這些編碼，必須在第一次呼叫 <see cref="Encoding.GetEncoding(string)"/> 之前執行。
/// 多次呼叫為冪等（idempotent）。
/// </summary>
public static class EncodingBootstrap
{
    // 用 Interlocked 旗標確保執行緒安全且只註冊一次
    private static int _registered;

    /// <summary>
    /// 註冊 <see cref="CodePagesEncodingProvider"/>。
    /// 應在 <c>Program.Main</c> 開頭（任何 Encoding.GetEncoding 之前）呼叫一次。
    /// </summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
    }
}
