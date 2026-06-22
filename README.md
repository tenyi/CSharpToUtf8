# ⚡ CSharpToUtf8

[![OS](https://img.shields.io/badge/OS-Windows%20%7C%20macOS%20%7C%20Linux-blue?style=flat-square)](https://dotnet.microsoft.com/)
[![Framework](https://img.shields.io/badge/Framework-AvaloniaUI_12-orange?style=flat-square&logo=dotnet)](https://avaloniaui.net/)
[![Target](https://img.shields.io/badge/.NET-10.0-purple?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)

**CSharpToUtf8** 是一款跨平台的 `.cs` 檔案編碼偵測與批次轉換工具（支援 Windows、macOS 與 Linux）。

旨在優雅、高效且安全地解決遺留專案（Legacy Projects）中殘留的非 UTF-8 原始碼編碼問題（例如 Big5、GBK、Shift_JIS 等 ANSI 編碼），協助團隊輕鬆完成程式碼編碼的現代化轉型。

---

## ✨ 核心特色

- 🔍 **智慧編碼探針**：結合「靜態 BOM 結構分析」與「Ude 統計學字元集偵測」雙重機制，精確判定原始碼編碼與信心度（Confidence）。
- 🛡️ **安全寫入防護網**：當編碼偵測信心度低於 **70%** 時，系統將自動將其標記為「警告」並拒絕寫入，以防止因誤判而產生程式碼亂碼的悲劇。
- ⚙️ **精準 BOM 安全閥**：採用 `addBom` 與 `overrideExistingUtf8Bom` 雙獨立參數設計，極具彈性地掌控新舊檔案的 BOM 狀態（詳見下方說明）。
- 📊 **直覺式結果清單**：清晰列出所有檔案的原始編碼、偵測信心度、目標 BOM 狀態以及最終執行的處理動作，並支援點擊於系統預設編輯器中直接開啟檔案進行檢視。
- 🎯 **多功能狀態篩選**：轉換完成後，可透過狀態列一鍵過濾出「轉換成功」、「覆寫 BOM」、「跳過」、「警告」與「錯誤」的檔案。
- 📂 **深度遞迴掃描**：一鍵掃描指定目錄及其所有子資料夾，快速批次處理海量檔案。

---

## ⚙️ BOM 行為控制矩陣

檔案的 BOM（Byte Order Mark）處理邏輯由兩個**完全獨立**的參數控制。預設情況下，兩者皆為 `false`（即不強制加上 BOM，且不變動已是 UTF-8 編碼的檔案）：

1. **`addBom`**：控制**轉換後**的 UTF-8 檔案是否帶有 BOM。
2. **`overrideExistingUtf8Bom`**：控制**原本已是 UTF-8** 的檔案，是否要強制根據 `addBom` 的設定重寫其 BOM 狀態。

### 📌 決策矩陣

| 檔案當前狀態 | `overrideExistingUtf8Bom = false` (預設) | `overrideExistingUtf8Bom = true` |
| :--- | :--- | :--- |
| **非 UTF-8 檔案** (例如 Big5) | 依 `addBom` 決定轉出的 UTF-8 是否帶 BOM | 依 `addBom` 決定轉出的 UTF-8 是否帶 BOM |
| **已是 UTF-8 檔案** (帶或不帶 BOM) | 🟢 **保持原樣，跳過寫入** | 🔄 **強制調整**（依 `addBom` 重新寫入檔案以符合狀態） |

> [!IMPORTANT]  
> 當 `overrideExistingUtf8Bom` 設定為 `false` 時，原本已是 UTF-8 的檔案**完全不會被重複寫入**，這能極大程度地保持 Git 歷史記錄的乾淨度，僅針對真正需要轉換的非 UTF-8 檔案進行變更。

### 🔄 處理動作說明

轉換完成後，狀態清單中會顯示以下動作之一：

| 狀態動作 | 具體語意與執行結果 |
| :--- | :--- |
| `Converted` | 非 UTF-8 檔案已成功轉換並寫入為 UTF-8。 |
| `SkippedAlreadyUtf8` | 檔案已是 UTF-8 且 `overrideExistingUtf8Bom` 為 `false`，安全跳過。 |
| `OverriddenRewritten` | 檔案原為 UTF-8 但開啟了強行覆寫，BOM 狀態已被重新寫入調整。 |
| `OverriddenNoChange` | 開啟強行覆寫，但該 UTF-8 檔案當前的 BOM 狀態已符合 `addBom` 的目標，因此無須重複寫入。 |
| `NotWrittenLowConfidence` | 偵測信心度低於 70%，為防止亂碼，系統拒絕寫入，僅標記為警告。 |
| `Error` | 讀取或寫入檔案時發生 I/O 異常。 |

---

## 📊 支援的編碼

本工具在啟動時會自動註冊 `CodePagesEncodingProvider` 以完整支援東亞地區 Codepage。底層 Ude 偵測結果會被對應至 .NET 的 `Encoding` 類型：

- 🇹🇼 🇭🇰 **繁體中文**：Big5 (cp950)、EUC-TW、ISO-2022-CN
- 🇨🇳 **簡體中文**：GB18030、GB2312、GBK (cp936)、HZ-GB-2312
- 🇯🇵 🇰🇷 **日韓語系**：Shift_JIS (cp932)、EUC-JP、EUC-KR、ISO-2022-JP / KR
- 🌍 **西歐語系**：ISO-8859-1 / 2 / 15、windows-1250 / 1251 / 1252
- 🌐 **萬國碼系列**：UTF-8、UTF-16 (LE/BE)、UTF-32

> [!NOTE]  
> 當中文原始碼的樣本長度不足或特徵不明使統計偵測（Ude）有可能將其誤判為西歐編碼。此時**偵測信心度通常會顯著偏低**，進而觸發「低信心度防護機制」將其標記為警告，而不會貿然覆寫檔案。

---

## 🚀 快速開始

### 💻 環境要求
- **作業系統**：Windows、macOS 或 Linux
- **開發環境**：.NET 10.0 SDK (或對應平台的 .NET 10.0 Runtime)

### 🛠️ 建置與執行

在專案根目錄下，您可以使用 .NET CLI 進行建置與執行：

```powershell
# 1. 還原並建置專案 (Debug 模式)
dotnet build

# 2. 建置 Release 版本
dotnet build -c Release

# 3. 直接啟動桌面應用程式
dotnet run --project CSharpToUtf8/CSharpToUtf8.csproj
```

### 🧪 執行單元測試

專案附帶完善的單元測試，用以驗證編碼轉換引擎與 BOM 判斷的邏輯：

```powershell
dotnet test
```

### 🖱️ 操作指南

1. **指定目錄**：啟動程式後，點擊 **「選擇資料夾」** 按鈕，選擇欲掃描的專案根目錄。
2. **組態參數**：根據團隊規範，勾選是否要強制帶上 BOM (`addBom`)，或是否要變更現有 UTF-8 檔案的 BOM (`overrideExistingUtf8Bom`)。
3. **執行與確認**：點擊 **「執行轉換」**。轉換結束後，可在列表中逐一檢視。
4. **快速檢視**：雙擊或點擊列表中的檔案列，即可透過系統預設的文字編輯器開啟該檔案以確認轉換效果。

---

## 📂 專案架構

```text
CSharpToUtf8/
├─ CSharpToUtf8.slnx                # 新版 XML 格式方案檔 (SLNX)
├─ CSharpToUtf8/                    # 主程式 (Avalonia UI 桌面應用程式)
│  ├─ Program.cs                    # 程式進入點 (初始化與編碼 Provider 註冊)
│  ├─ App.axaml(.cs)                # 應用程式生命週期管理與 MainWindow 載入
│  ├─ MainWindow.axaml(.cs)         # 主視窗 UI 佈局與事件處理邏輯 (Code-Behind)
│  ├─ ConversionProgressOverlay.*   # 執行轉換時的防呆遮罩與進度條 UI
│  ├─ ResultRowView.cs              # 轉換結果資料模型 (UI 綁定用)
│  ├─ ResultFilter.cs               # 結果狀態過濾列舉
│  └─ Conversion/                   # 編碼處理核心模組
│     ├─ ConversionEngine.cs        # 轉換決策引擎 (BOM 控制核心邏輯)
│     ├─ ConversionResult.cs        # 轉換結果資料契約與狀態列舉
│     ├─ BomInspector.cs            # 靜態 BOM 確定性判定器
│     ├─ UdeCharsetDetector.cs      # Ude 統計學偵測器封裝
│     ├─ UdeToDotNetEncodingMap.cs  # Ude 偵測標籤與 .NET 實體編碼的映射表
│     └─ EncodingBootstrap.cs       # 註冊東亞編碼擴充支援
└─ Tests/                           # 單元測試專案
   └─ ConversionEngineTests.cs      # 針對 ConversionEngine 的完整測試案例
```

---

## 🛠️ 技術棧

- **視窗框架**：[Avalonia UI 12](https://avaloniaui.net/) (使用 `FluentTheme` 現代化主題)
- **目標框架**：.NET 10.0 (Windows 上輸出為 WinExe，支援跨平台建置)
- **字元集偵測**：[Ude.NetStandard](https://www.nuget.org/packages/Ude.NetStandard) (Mozilla Charset Detector 移植版)
- **東亞字元集擴充**：`System.Text.Encoding.CodePages`
- **UI 架構設計**：Code-Behind 輕量化事件驅動
