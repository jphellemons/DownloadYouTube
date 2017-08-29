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
using System.Windows.Forms;
using System.Net;
using System.Drawing;
using TagLib;
using Gecko;

namespace DownloadYouTube
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        VideoInfo viAudio;
        VideoInfo viVideo;

        public MainWindow()
        {
            InitializeComponent();
            tbDir.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            Gecko.Xpcom.Initialize("Firefox");
            GeckoWebBrowser browser = new GeckoWebBrowser();
            browser.Navigated += Browser_Navigated;
            host.Child = browser;
            browser.Navigate("http://www.youtube.com");
        }

        private void Browser_Navigated(object sender, GeckoNavigatedEventArgs e)
        {
            string newUrl = ((GeckoWebBrowser)sender).Url.AbsoluteUri;
            if (!newUrl.Equals("http://www.youtube.com/") && !newUrl.Equals("https://www.youtube.com/") && !newUrl.Contains("results?search_query")) // first run + search result
            {
                try
                {
                    IEnumerable<VideoInfo> videoInfos = DownloadUrlResolver.GetDownloadUrls(newUrl);
                    btnDownload.Visibility = Visibility.Visible;

                    viAudio = videoInfos.Where(i => i.VideoType == VideoType.Mp4 && i.Resolution == 0 && i.AudioBitrate > 0).OrderByDescending(q => q.AudioBitrate).First();
                    viVideo = videoInfos.Where(p => p.AudioBitrate > 0).OrderByDescending(i => i.Resolution).ThenByDescending(a => a.AudioBitrate).First();

                }
                catch (VideoNotAvailableException vnae)
                {
                    lblMsg.Content = vnae.Message;
                    Console.WriteLine(vnae.Message);
                    Console.WriteLine(vnae.StackTrace);
                    btnDownload.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    btnDownload.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            lblMsg.Content = "Download started";
            if (rbvideo.IsChecked == true) // because nullable
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (viVideo.RequiresDecryption)
                        DownloadUrlResolver.DecryptDownloadUrl(viVideo);

                    var videoDownloader = new VideoDownloader(viVideo, System.IO.Path.Combine(tbDir.Text, SafeFileName(viVideo.Title) + viVideo.VideoExtension));

                    videoDownloader.DownloadProgressChanged += (s, args) => lblMsg.Content = args.ProgressPercentage;
                    try
                    {
                        videoDownloader.Execute();
                        lblMsg.Content = "Completed!";
                        Process.Start(tbDir.Text);
                    }
                    catch (UnauthorizedAccessException uae)
                    {
                        Console.WriteLine(uae.Message);
                        Console.WriteLine(uae.StackTrace);
                        lblMsg.Content = "You are not allowed to write in this folder, please select an other destination folder to store the video";
                    }
                }));
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (viAudio.RequiresDecryption)
                        DownloadUrlResolver.DecryptDownloadUrl(viAudio);

                    var videoDownloader = new VideoDownloader(viAudio, System.IO.Path.Combine(tbDir.Text, SafeFileName(viAudio.Title) + ".M4A"));

                    videoDownloader.DownloadProgressChanged += (s, args) => lblMsg.Content = args.ProgressPercentage;

                    try
                    {
                        videoDownloader.Execute();
                        ProcessAudio(System.IO.Path.Combine(tbDir.Text, SafeFileName(viAudio.Title) + ".M4A"));

                        lblMsg.Content = "Completed!";
                        Process.Start(tbDir.Text);
                    }
                    catch (UnauthorizedAccessException uae)
                    {
                        Console.WriteLine(uae.Message);
                        Console.WriteLine(uae.StackTrace);
                        lblMsg.Content = "You are not allowed to write in this folder, please select an other destination folder to store the video";
                    }
                    catch (IOException ioe)
                    {
                        Console.WriteLine(ioe.Message);
                        Console.WriteLine(ioe.StackTrace);
                    }
                    catch (WebException wex)
                    {
                        Console.WriteLine(wex.Message);
                        Console.WriteLine(wex.StackTrace);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                    }
                }));
            }
        }

        private void ProcessAudio(string fileIn)
        {
            TagLib.File f = TagLib.File.Create(fileIn);
            string artists = viAudio.Title;
            string title = viAudio.Title;

            if (viAudio.Title.Contains("-"))
            {
                artists = viAudio.Title.Substring(0, viAudio.Title.IndexOf("-")).Trim();
                title = viAudio.Title.Substring(viAudio.Title.IndexOf("-") + 1).Trim();
            }

            f.Tag.Album = viAudio.Title;
            f.Tag.Title = title;
            f.Tag.Performers = new string[] { artists };

            WebClient wc = new WebClient();
            using (MemoryStream ms = new MemoryStream(wc.DownloadData(viAudio.ThumbnailUrl)))
            {
                byte[] myBytes = ms.ToArray();
                ByteVector byteVector = new ByteVector(myBytes, myBytes.Length);
                TagLib.Picture picture = new Picture(byteVector);

                f.Tag.Pictures = new TagLib.IPicture[]
                {
                    picture
                };
            }
            
            f.Save();
        }

        private string SafeFileName(string title)
        {
            return title.Replace("\\", " ").Replace("/", " ").Replace("|", " ").Replace(".", " ").Replace("[", "").Replace("]", "").Replace("\"", "");
        }

        private void btnDir_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            DialogResult result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
                tbDir.Text = dialog.SelectedPath;
        }
    }
}