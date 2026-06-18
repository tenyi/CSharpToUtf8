using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace CSharpToUtf8;

public partial class ConversionProgressOverlay : UserControl
{
    private DispatcherTimer? _dotsTimer;
    private int _dotFrame;
    private string _phaseBase = "正在掃描資料夾";

    public ConversionProgressOverlay()
    {
        InitializeComponent();
    }

    public void BeginScanning()
    {
        StopDotsAnimation();
        MainProgress.IsIndeterminate = true;
        MainProgress.Value = 0;
        PhaseText.Text = "正在掃描資料夾…";
        CountText.Text = "—";
        CountLabel.Text = "掃描中";
        DetailText.Text = "正在遞迴搜尋 .cs 檔案";
        StartDotsAnimation("正在掃描資料夾");
    }

    public void SetScanComplete(int fileCount)
    {
        StopDotsAnimation();
        MainProgress.IsIndeterminate = false;
        MainProgress.Maximum = fileCount > 0 ? fileCount : 1;
        MainProgress.Value = 0;
        PhaseText.Text = "掃描完成";
        CountText.Text = fileCount.ToString();
        CountLabel.Text = fileCount == 1 ? "個 .cs 檔案" : "個 .cs 檔案";
        DetailText.Text = fileCount > 0 ? "即將開始轉換…" : "找不到可轉換的檔案";
    }

    public void SetProcessing(int current, int total, string fileName)
    {
        StopDotsAnimation();
        MainProgress.IsIndeterminate = false;
        MainProgress.Maximum = total;
        MainProgress.Value = current;
        PhaseText.Text = "轉換中";
        CountText.Text = $"{current} / {total}";
        CountLabel.Text = "已處理";
        DetailText.Text = fileName;
    }

    public void SetFinishing()
    {
        StopDotsAnimation();
        MainProgress.IsIndeterminate = true;
        PhaseText.Text = "即將完成";
        CountLabel.Text = "整理結果中";
        DetailText.Text = "";
        StartDotsAnimation("整理結果");
    }

    private void StartDotsAnimation(string phaseBase)
    {
        _phaseBase = phaseBase;
        _dotFrame = 0;
        _dotsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _dotsTimer.Tick += OnDotsTick;
        _dotsTimer.Start();
    }

    private void OnDotsTick(object? sender, EventArgs e)
    {
        _dotFrame = (_dotFrame + 1) % 4;
        var dots = new string('.', _dotFrame);
        PhaseText.Text = _phaseBase + dots;
    }

    private void StopDotsAnimation()
    {
        if (_dotsTimer is null)
        {
            return;
        }

        _dotsTimer.Tick -= OnDotsTick;
        _dotsTimer.Stop();
        _dotsTimer = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopDotsAnimation();
        base.OnDetachedFromVisualTree(e);
    }
}
