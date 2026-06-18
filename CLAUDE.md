# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 專案目標（核心需求）

這是一個將 `.cs` 檔案轉換為 UTF-8 的桌面工具。核心行為：

1. **偵測編碼**：判斷每個 `.cs` 檔案目前的編碼（語系），例如 Big5、UTF-8（含/不含 BOM）、其他 ANSI 編碼等。
2. **轉換為 UTF-8 並列出**：將非 UTF-8 檔案轉成 UTF-8，並在 UI 列出每個檔案的處理結果（原始編碼 → UTF-8）。
3. **BOM 行為由兩個獨立參數控制**（語意很重要，實作時務必遵守）：
   - `addBom`（是否加上 BOM）：控制**轉換後**的 UTF-8 是否帶 BOM。
   - `overrideExistingUtf8Bom`（是否覆寫已是 UTF-8 檔案的 BOM 狀態）：控制**已經是 UTF-8** 的檔案是否要被重新寫入以調整 BOM。
   - **預設值：`addBom = false` 且 `overrideExistingUtf8Bom = false`** —— 不加 BOM，且不動已經是 UTF-8 的檔案（保持其原 BOM 狀態）。

兩個參數與檔案目前狀態的對照（實作時的判定邏輯）：

| 檔案目前狀態 | `overrideExistingUtf8Bom=false`（預設） | `overrideExistingUtf8Bom=true` |
| --- | --- | --- |
| 非 UTF-8（如 Big5） | 依 `addBom` 決定轉出 UTF-8 是否帶 BOM | 依 `addBom` 決定轉出 UTF-8 是否帶 BOM |
| 已是 UTF-8（帶或不帶 BOM） | **保持原樣不重寫** | 依 `addBom` 強制調整 BOM 狀態（可能重寫） |

> 換句話說：`overrideExistingUtf8Bom=false` 時，已是 UTF-8 的檔案**完全跳過寫入**；只有非 UTF-8 的檔案會被轉換。

## 技術堆疊與建置

- **框架**：Avalonia UI 12（`*.axaml`，非 WPF 的 `*.xaml`），`FluentTheme`，ClassicDesktopLifetime。
- **目標框架**：.NET 10.0，`WinExe`（僅 Windows 桌面）。
- **Nullable** 已啟用。

常用指令：

```powershell
dotnet build                              # 建置（Debug）
dotnet build -c Release                   # Release 建置
dotnet run                                # 執行桌面應用程式
dotnet run --project CSharpToUtf8.csproj  # 指定專案執行
```

Solution 檔使用較新的 `.slnx` 格式（XML，非傳統 `.sln`），IDE 與 `dotnet` CLI 皆可直接開啟。

## 架構

標準 Avalonia 單視窗桌面應用程式，進入點與視窗結構：

- `Program.cs` → `Program.Main`：`[STAThread]`，組態 `AppBuilder`（`UsePlatformDetect` + `WithInterFont`），以 `StartWithClassicDesktopLifetime` 啟動。注意 Avalonia 註解警告：**在 `AppMain` 被呼叫前不可使用任何 Avalonia / 第三方 API**。
- `App.axaml(.cs)` → `App`：`Application` 子類別，`OnFrameworkInitializationCompleted` 中建立 `MainWindow` 並掛到 `IClassicDesktopStyleApplicationLifetime`。
- `MainWindow.axaml(.cs)` → `MainWindow`：主視窗，目前為範本預設內容，**編碼偵測 / 轉換 / 列表 的 UI 與邏輯需於此實作**。

UI 模式目前為 code-behind（未引入 MVVM 框架）；新增複雜狀態前先確認是否需要 MVU/MVVM，避免與既有風格衝突（見全域 CLAUDE.md「Rule 11 — 遵循既有慣例」）。

## 其他

- `.editorconfig` 指定 `charset = utf-8`：專案檔案本身以 UTF-8 儲存。
- `app.manifest` 僅 Windows 使用，宣告 Windows 10 `supportedOS`，勿移除（會影響視窗透明與嵌入控制項）。
- `AvaloniaUI.DiagnosticsSupport` 套件在 Debug 設定才引入，Release 會自動排除。
