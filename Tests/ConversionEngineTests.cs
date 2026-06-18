using System.Runtime.CompilerServices;
using System.Text;
using CSharpToUtf8.Conversion;

namespace CSharpToUtf8.Tests;

/// <summary>
/// ConversionEngine 決策矩陣的單元測試。
/// 涵蓋 <c>CLAUDE.md</c> 中 BOM 雙參數對照表的所有關鍵路徑，
/// 以及信心度門檻、低信心度只警告不寫入、錯誤處理、目錄遞迴。
/// </summary>
public class ConversionEngineTests : IDisposable
{
    private readonly string _tempDir;

    public ConversionEngineTests()
    {
        // 每個測試實例一個獨立 temp 資料夾，避免互相干擾
        _tempDir = Path.Combine(Path.GetTempPath(), "CSharpToUtf8_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            // 清理 temp；忽略失敗（Windows 上可能有短暫檔案鎖）
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* 忽略 */ }
        }
    }

    /// <summary>
    /// 在 assembly 載入時一次性註冊 CodePages provider，
    /// 確保 Big5/GBK/Shift_JIS 等東亞 codepage 在所有測試中可用。
    /// </summary>
    [ModuleInitializer]
    internal static void Init()
    {
        EncodingBootstrap.Register();
    }

    // ---- 工具方法 ----

    private string WriteBytes(string fileName, byte[] bytes)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>
    /// 寫 UTF-8 **無 BOM** 的檔案。
    /// 注意：<c>Encoding.GetBytes</c> 不包含 BOM；只有 <c>GetPreamble</c> 才回 BOM bytes。
    /// </summary>
    private string WriteUtf8NoBom(string fileName, string content)
    {
        var enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        return WriteBytes(fileName, enc.GetBytes(content));
    }

    /// <summary>
    /// 寫 UTF-8 **有 BOM** 的檔案（手動把 preamble 拼接到 bytes 前面）。
    /// </summary>
    private string WriteUtf8WithBom(string fileName, string content)
    {
        var enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var bytes = enc.GetPreamble().Concat(enc.GetBytes(content)).ToArray();
        return WriteBytes(fileName, bytes);
    }

    /// <summary>
    /// 寫 Big5 編碼的檔案（Big5 標準無 BOM）。
    /// </summary>
    private string WriteBig5(string fileName, string content)
    {
        var big5 = Encoding.GetEncoding("big5");
        return WriteBytes(fileName, big5.GetBytes(content));
    }

    // ---- 測試案例 ----

    [Fact]
    public void Big5_充足中文樣本_應Converted_且無Warning()
    {
        // 200 個中文字的大樣本，Ude 信心度應高於 70%
        var content = "// 中文註解：測試用\nclass C {\n    /* " + new string('中', 200) + " */\n    void M() {}\n}\n";
        var path = WriteBig5("big5_sample.cs", content);

        var result = ConversionEngine.Process(path, addBom: false, overrideExistingUtf8Bom: false);

        Assert.Equal(ConversionAction.Converted, result.Action);
        Assert.Null(result.Warning);
        Assert.Null(result.Error);
        Assert.False(result.WasAlreadyUtf8);
        Assert.NotNull(result.DetectedCharset);
    }

    [Fact]
    public void Big5_短樣本_應NotWrittenLowConfidence_且含Warning()
    {
        // 極短樣本（僅 1 個中文字），Ude 信心度通常 < 70%
        var content = "// 中";
        var path = WriteBig5("big5_short.cs", content);

        var result = ConversionEngine.Process(path, addBom: false, overrideExistingUtf8Bom: false);

        Assert.Equal(ConversionAction.NotWrittenLowConfidence, result.Action);
        Assert.NotNull(result.Warning);
        Assert.Null(result.Error);
        Assert.False(result.WasAlreadyUtf8);
    }

    [Fact]
    public void Utf8無BOM_預設參數_應SkippedAlreadyUtf8()
    {
        var path = WriteUtf8NoBom("utf8_nobom.cs", "// 中文註解\nclass C {}\n");

        var result = ConversionEngine.Process(path, addBom: false, overrideExistingUtf8Bom: false);

        Assert.Equal(ConversionAction.SkippedAlreadyUtf8, result.Action);
        Assert.True(result.WasAlreadyUtf8);
        Assert.Null(result.Warning);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Utf8無BOM_overrideTrue_addBomTrue_應OverriddenRewritten_且檔案加BOM()
    {
        // 先寫一個 UTF-8 無 BOM 的檔案
        var path = WriteUtf8NoBom("utf8_nobom.cs", "// 中文註解\nclass C {}\n");

        var result = ConversionEngine.Process(path, addBom: true, overrideExistingUtf8Bom: true);

        Assert.Equal(ConversionAction.OverriddenRewritten, result.Action);
        Assert.True(result.WasAlreadyUtf8);
        // 驗證磁碟上的檔案確實被加上 UTF-8 BOM（EF BB BF）
        var newBytes = File.ReadAllBytes(path);
        Assert.True(newBytes.Length >= 3, "檔案至少應有 BOM 三 bytes");
        Assert.Equal(0xEF, newBytes[0]);
        Assert.Equal(0xBB, newBytes[1]);
        Assert.Equal(0xBF, newBytes[2]);
    }

    [Fact]
    public void Utf8無BOM_overrideTrue_addBomFalse_應OverriddenNoChange()
    {
        // 現狀=無 BOM、目標=無 BOM，應記為 NoChange（不實質重寫）
        var path = WriteUtf8NoBom("utf8_nobom.cs", "// 中文\nclass C {}\n");

        var result = ConversionEngine.Process(path, addBom: false, overrideExistingUtf8Bom: true);

        Assert.Equal(ConversionAction.OverriddenNoChange, result.Action);
        Assert.True(result.WasAlreadyUtf8);
    }

    [Fact]
    public void Utf8有BOM_預設參數_應SkippedAlreadyUtf8_且HadBom為True()
    {
        var path = WriteUtf8WithBom("utf8_bom.cs", "// 中文註解\nclass C {}\n");

        var result = ConversionEngine.Process(path, addBom: false, overrideExistingUtf8Bom: false);

        Assert.Equal(ConversionAction.SkippedAlreadyUtf8, result.Action);
        Assert.True(result.WasAlreadyUtf8);
        Assert.True(result.HadBom);
        Assert.Equal(BomKind.Utf8, result.BomKind);
        Assert.Null(result.Warning);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Utf8有BOM_overrideTrue_addBomFalse_應OverriddenRewritten_且BOM被移除()
    {
        // 先寫 UTF-8 有 BOM 的檔案
        var path = WriteUtf8WithBom("utf8_bom.cs", "// 中文\nclass C {}\n");

        var result = ConversionEngine.Process(path, addBom: false, overrideExistingUtf8Bom: true);

        Assert.Equal(ConversionAction.OverriddenRewritten, result.Action);
        Assert.True(result.WasAlreadyUtf8);
        // 驗證磁碟上的檔案 BOM 已被移除
        var newBytes = File.ReadAllBytes(path);
        if (newBytes.Length >= 3)
        {
            Assert.False(
                newBytes[0] == 0xEF && newBytes[1] == 0xBB && newBytes[2] == 0xBF,
                "BOM 應已被移除");
        }
        // 重新跑一次，現狀=無 BOM、目標=無 BOM，應為 NoChange
        var result2 = ConversionEngine.Process(path, addBom: false, overrideExistingUtf8Bom: true);
        Assert.Equal(ConversionAction.OverriddenNoChange, result2.Action);
    }

    [Fact]
    public void 純ASCII_應SkippedAlreadyUtf8()
    {
        var path = WriteBytes("ascii.cs", Encoding.ASCII.GetBytes("// hello world\nclass C {}\n"));

        var result = ConversionEngine.Process(path, addBom: false, overrideExistingUtf8Bom: false);

        Assert.Equal(ConversionAction.SkippedAlreadyUtf8, result.Action);
        Assert.True(result.WasAlreadyUtf8);
        Assert.Null(result.Warning);
    }

    [Fact]
    public void 不存在的檔案_應Error_且Error訊息不為空()
    {
        var result = ConversionEngine.Process(
            Path.Combine(_tempDir, "this_file_does_not_exist.cs"),
            addBom: false,
            overrideExistingUtf8Bom: false);

        Assert.Equal(ConversionAction.Error, result.Action);
        Assert.NotNull(result.Error);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public void RunOnDirectory_遞迴處理所有Cs檔_結果數量符合()
    {
        // 準備資料夾：3 個 .cs + 1 個非 .cs 應被忽略
        WriteUtf8NoBom("a.cs", "class A {}");
        WriteUtf8NoBom("b.cs", "class B {}");
        WriteUtf8NoBom("c.cs", "class C {}");
        WriteBytes("readme.txt", Encoding.ASCII.GetBytes("not a .cs"));

        var results = ConversionEngine.RunOnDirectory(_tempDir, addBom: false, overrideExistingUtf8Bom: false);

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.EndsWith(".cs", r.FilePath));
    }
}
