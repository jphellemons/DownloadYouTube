using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using YoutubeExtractor;
using System.Reflection;
using System.Windows.Threading;
using System.Diagnostics;

namespace DownloadYouTube
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            tbDir.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        private void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            lblMsg.Content = "Download started";
            if (rbvideo.IsChecked == true) // because nullable
            {
                var video = (VideoInfo)options.SelectedItem;

                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {

                    if (video.RequiresDecryption)
                        DownloadUrlResolver.DecryptDownloadUrl(video);

                    var videoDownloader = new VideoDownloader(video, System.IO.Path.Combine(tbDir.Text, SafeFileName(video.Title) + video.VideoExtension));

                    videoDownloader.DownloadProgressChanged += (s, args) => lblMsg.Content = args.ProgressPercentage;
                    try
                    {
                        videoDownloader.Execute();
                        lblMsg.Content = "Completed!";
                        Process.Start(tbDir.Text);
                    }
                    catch (UnauthorizedAccessException uae)
                    {
                        lblMsg.Content = "You are not allowed to write in this folder, please select an other destination folder to store the video";
                    }
                }));
            }
            else
            {
                IEnumerable<VideoInfo> videoInfos = DownloadUrlResolver.GetDownloadUrls(webBrowser.Source.AbsoluteUri);
                if (videoInfos.Where(info => info.CanExtractAudio).Count() > 0)
                {
                    VideoInfo video = videoInfos.Where(info => info.CanExtractAudio).OrderByDescending(info => info.AudioBitrate).First();

                    if (video.RequiresDecryption)
                    {
                        DownloadUrlResolver.DecryptDownloadUrl(video);
                    }

                    var audioDownloader = new AudioDownloader(video, System.IO.Path.Combine(tbDir.Text, SafeFileName(video.Title) + video.AudioExtension));

                    audioDownloader.DownloadProgressChanged += (s, args) => lblMsg.Content = args.ProgressPercentage * 0.85;
                    audioDownloader.AudioExtractionProgressChanged += (s, args) => lblMsg.Content = 85 + args.ProgressPercentage * 0.15;
                    try
                    {
                        audioDownloader.Execute();
                        lblMsg.Content = "Completed!";
                        Process.Start(tbDir.Text);
                    }
                    catch (UnauthorizedAccessException uae)
                    {
                        lblMsg.Content = "You are not allowed to write in this folder, please select an other destination folder to store the video";
                        System.Diagnostics.Debug.WriteLine(uae.Message);
                    }
                }
                else
                    lblMsg.Content = "Sorry, cannot extract audio for this video";
            }
        }

        private string SafeFileName(string title)
        {
            return title.Replace("\\", " ").Replace("/", " ").Replace("|", " ").Replace(".", " ");
        }

        private void WebBrowser_Navigated(object sender, NavigationEventArgs e)
        {
            HideScriptErrors(webBrowser, true);
            string newUrl = webBrowser.Source.AbsoluteUri;
            if (!newUrl.Equals("http://www.youtube.com/") && !newUrl.Equals("https://www.youtube.com/")) // first run
            {
                try
                {
                    IEnumerable<VideoInfo> videoInfos = DownloadUrlResolver.GetDownloadUrls(newUrl);
                    btnDownload.Visibility = System.Windows.Visibility.Visible;
                    options.Visibility = System.Windows.Visibility.Visible;
                    options.ItemsSource = videoInfos.OrderByDescending(i => i.Resolution).Where(p => p.AudioBitrate > 0);
                    if (videoInfos.Count() > 0)
                        options.SelectedIndex = 0;
                }
                catch {
                    btnDownload.Visibility = System.Windows.Visibility.Collapsed;
                    options.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
        }

        private void btnDir_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                tbDir.Text = dialog.SelectedPath;
            }
        }

        private void HideScriptErrors(WebBrowser wb, bool Hide)
        {
            FieldInfo fiComWebBrowser = typeof(WebBrowser).GetField("_axIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiComWebBrowser == null) return;
            object objComWebBrowser = fiComWebBrowser.GetValue(wb);
            if (objComWebBrowser == null) return;
            objComWebBrowser.GetType().InvokeMember("Silent", BindingFlags.SetProperty, null, objComWebBrowser, new object[] { Hide });
        }
    }
}