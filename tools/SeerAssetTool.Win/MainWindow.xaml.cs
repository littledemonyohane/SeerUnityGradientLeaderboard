using System.IO;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace SeerAssetTool.Win;

public partial class MainWindow : Window
{
    readonly SeerToolService _service;
    bool _isBusy;

    public MainWindow(SeerToolService service)
    {
        _service = service;
        InitializeComponent();
        InitializeDefaults();
    }

    void InitializeDefaults()
    {
        SourceRootTextBox.Text = _service.FindBestInstallRoot();
        MirrorRootTextBox.Text = _service.DefaultMirrorRoot;
        OutputRootTextBox.Text = _service.DefaultExportRoot;
        AppendLog($"Tool base directory: {_service.BaseDirectory}");
        AppendLog("Ready.");
    }

    async void DoctorButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync(async () =>
        {
            AppendLog("Checking Python and UnityPy...");
            await _service.EnsurePythonReadyAsync(AppendLog, bootstrap: BootstrapPythonCheckBox.IsChecked == true);
            AppendLog("Dependencies are ready.");
        });
    }

    async void SyncButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync(async () =>
        {
            var options = BuildSyncOptions();
            await _service.RunSyncAsync(options, AppendLog);
            AppendLog("Mirror sync completed.");

            if (OpenFolderAfterSuccessCheckBox.IsChecked == true)
            {
                _service.OpenFolder(options.MirrorRoot);
            }
        });
    }

    async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync(async () =>
        {
            var options = BuildExportOptions();
            var summary = await _service.RunExportAsync(options, AppendLog);
            AppendLog($"Export completed. monsters={summary.MonsterCount}, heads={summary.ExportedHeadCount}, countermarks={summary.ExportedCountermarkCount}");

            if (OpenFolderAfterSuccessCheckBox.IsChecked == true)
            {
                _service.OpenFolder(options.OutputRoot);
            }
        });
    }

    async void SyncExportButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync(async () =>
        {
            var options = BuildSyncExportOptions();
            var summary = await _service.RunSyncAndExportAsync(options, AppendLog);
            SourceRootTextBox.Text = options.MirrorRoot;
            AppendLog($"Sync + export completed. monsters={summary.MonsterCount}, heads={summary.ExportedHeadCount}, countermarks={summary.ExportedCountermarkCount}");

            if (OpenFolderAfterSuccessCheckBox.IsChecked == true)
            {
                _service.OpenFolder(options.MirrorRoot);
                _service.OpenFolder(options.OutputRoot);
            }
        });
    }

    void BrowseSourceRoot_Click(object sender, RoutedEventArgs e) =>
        BrowseFolderInto(SourceRootTextBox);

    void BrowseMirrorRoot_Click(object sender, RoutedEventArgs e) =>
        BrowseFolderInto(MirrorRootTextBox);

    void BrowseOutputRoot_Click(object sender, RoutedEventArgs e) =>
        BrowseFolderInto(OutputRootTextBox);

    void AutoDetectSourceRoot_Click(object sender, RoutedEventArgs e)
    {
        SourceRootTextBox.Text = _service.FindBestInstallRoot();
        AppendLog($"Auto-detected source root: {SourceRootTextBox.Text}");
    }

    void OpenMirrorRoot_Click(object sender, RoutedEventArgs e) =>
        _service.OpenFolder(MirrorRootTextBox.Text);

    void OpenOutputRoot_Click(object sender, RoutedEventArgs e) =>
        _service.OpenFolder(OutputRootTextBox.Text);

    void OpenReadmeButton_Click(object sender, RoutedEventArgs e)
    {
        var readmePath = Path.Combine(_service.BaseDirectory, "README.md");
        if (File.Exists(readmePath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = readmePath,
                UseShellExecute = true,
            });
        }
    }

    void ClearLogButton_Click(object sender, RoutedEventArgs e) =>
        LogTextBox.Clear();

    void BrowseFolderInto(System.Windows.Controls.TextBox targetTextBox)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            UseDescriptionForTitle = true,
            Description = "Choose a folder",
            ShowNewFolderButton = true,
            InitialDirectory = string.IsNullOrWhiteSpace(targetTextBox.Text)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : targetTextBox.Text,
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            targetTextBox.Text = dialog.SelectedPath;
        }
    }

    SyncOptions BuildSyncOptions() => new()
    {
        InstallRoot = SourceRootTextBox.Text.Trim(),
        MirrorRoot = MirrorRootTextBox.Text.Trim(),
        AllHeads = AllHeadsCheckBox.IsChecked == true,
        AllCountermarks = AllCountermarksCheckBox.IsChecked == true,
    };

    ExportOptions BuildExportOptions() => new()
    {
        SourceRoot = SourceRootTextBox.Text.Trim(),
        OutputRoot = OutputRootTextBox.Text.Trim(),
        Layout = GetSelectedLayout(),
        Limit = ParseLimit(),
        SkipMonsters = SkipMonstersCheckBox.IsChecked == true,
        SkipHeads = SkipHeadsCheckBox.IsChecked == true,
        SkipCountermarks = SkipCountermarksCheckBox.IsChecked == true,
        BootstrapPython = BootstrapPythonCheckBox.IsChecked == true,
    };

    SyncExportOptions BuildSyncExportOptions() => new()
    {
        InstallRoot = SourceRootTextBox.Text.Trim(),
        MirrorRoot = MirrorRootTextBox.Text.Trim(),
        OutputRoot = OutputRootTextBox.Text.Trim(),
        Layout = GetSelectedLayout(),
        Limit = ParseLimit(),
        SkipMonsters = SkipMonstersCheckBox.IsChecked == true,
        SkipHeads = SkipHeadsCheckBox.IsChecked == true,
        SkipCountermarks = SkipCountermarksCheckBox.IsChecked == true,
        AllHeads = AllHeadsCheckBox.IsChecked == true,
        AllCountermarks = AllCountermarksCheckBox.IsChecked == true,
        BootstrapPython = BootstrapPythonCheckBox.IsChecked == true,
    };

    ExportLayout GetSelectedLayout()
    {
        if (LayoutComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return string.Equals(tag, "mirror", StringComparison.OrdinalIgnoreCase)
                ? ExportLayout.Mirror
                : ExportLayout.Cache;
        }

        return ExportLayout.Cache;
    }

    int ParseLimit() =>
        int.TryParse(LimitTextBox.Text.Trim(), out var limit) && limit > 0 ? limit : 0;

    async Task RunOperationAsync(Func<Task> action)
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            _isBusy = true;
            SetControlsEnabled(false);
            AppendLog(new string('-', 72));
            await action();
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: {ex.Message}");
            System.Windows.MessageBox.Show(this, ex.Message, "Seer Asset Tool", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetControlsEnabled(true);
            _isBusy = false;
        }
    }

    void SetControlsEnabled(bool enabled)
    {
        DoctorButton.IsEnabled = enabled;
        SyncButton.IsEnabled = enabled;
        ExportButton.IsEnabled = enabled;
        SyncExportButton.IsEnabled = enabled;
        OpenReadmeButton.IsEnabled = enabled;
        ClearLogButton.IsEnabled = enabled;
    }

    void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogTextBox.ScrollToEnd();
        });
    }
}
