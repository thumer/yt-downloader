// =============================
// File: MainWindow.xaml.cs
// =============================
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;
using YoutubeDownloader.Properties;

namespace YoutubeDownloader;

public partial class MainWindow : Window
{
    private readonly YoutubeDL _ytdl;
    private readonly string _toolsDir;
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();

        _toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
        Directory.CreateDirectory(_toolsDir);

        string ydPath = Path.Combine(_toolsDir, "yt-dlp.exe");
        string ffPath = Path.Combine(_toolsDir, "ffmpeg.exe");

        _ytdl = new YoutubeDL
        {
            YoutubeDLPath = ydPath,
            FFmpegPath = ffPath,
            OutputFolder = _toolsDir // fallback
        };

        EnsureToolsAsync().ConfigureAwait(false);

        PathTextBox.Text = Settings.Default.SavePath;

        // Drag & Drop
        LinkTextBox.PreviewDragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.Text) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
            e.Handled = true;
        };
        LinkTextBox.Drop += (_, e) =>
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.Text))
                LinkTextBox.Text = (string)e.Data.GetData(System.Windows.DataFormats.Text)!;
        };
    }

    private async Task EnsureToolsAsync()
    {
        string ydPath = _ytdl.YoutubeDLPath;
        string ffPath = _ytdl.FFmpegPath;

        if (!File.Exists(ydPath))
        {
            StatusText.Text = "Lade yt‑dlp …";
            try
            {
                await DownloadFileAsync("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe", ydPath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Fehler beim Herunterladen von yt-dlp.exe. Bitte manuell herunterladen und in den Unterordner 'tools' geben!\r\n\r\nException: {ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        if (!File.Exists(ffPath))
        {
            StatusText.Text = "Lade ffmpeg …";
            try
            {
                await DownloadFileAsync("https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip", new FileInfo(ffPath).DirectoryName, unzip: true);
                File.Move("tools\\ffmpeg-master-latest-win64-gpl\\bin\\ffmpeg.exe", "tools\\ffmpeg.exe");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Fehler beim Herunterladen von ffmpeg.exe. Bitte manuell herunterladen und in den Unterordner 'tools' geben!\r\n\r\nException: {ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        StatusText.Text = string.Empty;
    }

    private static async Task DownloadFileAsync(string url, string destination, bool unzip = false)
    {
        using var http = new HttpClient();
        var data = await http.GetByteArrayAsync(url);

        if (unzip)
        {
            string zipPath = destination + ".zip";
            await File.WriteAllBytesAsync(zipPath, data);
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, Path.GetDirectoryName(destination)!);
            File.Delete(zipPath);
        }
        else
        {
            await File.WriteAllBytesAsync(destination, data);
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = PathTextBox.Text };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            PathTextBox.Text = dlg.SelectedPath;
            SavePath(dlg.SelectedPath);
        }
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        string link = LinkTextBox.Text.Trim();
        string targetDir = PathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(targetDir))
        {
            System.Windows.MessageBox.Show("Bitte Link UND Zielordner angeben.");
            return;
        }
        if (!Directory.Exists(targetDir))
        {
            System.Windows.MessageBox.Show("Zielordner existiert nicht.");
            return;
        }

        DownloadButton.IsEnabled = false;
        StatusText.Text = "Starte Download …";
        Progress.Value = 0;

        ProgressHandler progress = new ProgressHandler(Dispatcher, Progress, StatusText);
        _cts = new CancellationTokenSource();

        try
        {
            _ytdl.OutputFolder = targetDir;
            var res = await _ytdl.RunAudioDownload(link, AudioConversionFormat.Mp3, _cts.Token, progress);

            if (res.Success)
                System.Windows.MessageBox.Show("Download abgeschlossen! ✓");
            else
                System.Windows.MessageBox.Show("Fehler:\n" + string.Join("\n", res.ErrorOutput));
        }
        catch (OperationCanceledException)
        {
            System.Windows.MessageBox.Show("Vorgang abgebrochen.");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Unerwarteter Fehler:\n" + ex.Message);
        }
        finally
        {
            DownloadButton.IsEnabled = true;
            StatusText.Text = string.Empty;
            Progress.Value = 0;
        }
    }

    private class ProgressHandler : IProgress<DownloadProgress>
    {
        private readonly System.Windows.Controls.ProgressBar _progressBar;
        private readonly Dispatcher _dispatcher;
        private readonly TextBlock _statusText;

        public ProgressHandler(System.Windows.Threading.Dispatcher dispatcher, System.Windows.Controls.ProgressBar progressBar, TextBlock statusText)
        {
            _progressBar = progressBar;
            _dispatcher = dispatcher;
            _statusText = statusText;
        }

        public void Report(DownloadProgress value)
        {
            _dispatcher.Invoke(() =>
            {
                _progressBar.Value = value.Progress;
                _statusText.Text = $"{value.Progress:0}% – {value.DownloadSpeed} | ETA {value.ETA}";
            });
        }
    }

    private static void SavePath(string path)
    {
        Settings.Default.SavePath = path;
        Settings.Default.Save();
    }
}