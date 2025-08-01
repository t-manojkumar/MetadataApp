using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Text;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ImageMagick;
using Windows.Storage.Pickers;
using System.Collections.Generic; // Required for List

namespace MetadataApp
{
    public class ImageInfo
    {
        public BitmapImage? Thumbnail { get; set; }
        public string? Metadata { get; set; }
        public string? FilePath { get; set; } // Store path for thumbnail creation
    }

    public sealed partial class MainWindow : Window
    {
        private ObservableCollection<ImageInfo> loadedImages = new ObservableCollection<ImageInfo>();

        public MainWindow()
        {
            this.InitializeComponent();
            ImageGridView.ItemsSource = loadedImages;
        }

        private async void myButton_Click(object sender, RoutedEventArgs e)
        {
            var fileOpenPicker = new FileOpenPicker();
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(fileOpenPicker, windowHandle);
            fileOpenPicker.FileTypeFilter.Add("*");

            var files = await fileOpenPicker.PickMultipleFilesAsync();

            if (files != null && files.Count > 0)
            {
                // Show progress and clear old items
                ProgressGrid.Visibility = Visibility.Visible;
                loadedImages.Clear();
                resultsTextBlock.Text = "Loading...";

                // --- THE FIX: Process data in the background, then update UI ---
                var newImageInfos = await Task.Run(() =>
                {
                    var tempList = new List<ImageInfo>();
                    foreach (var file in files)
                    {
                        var metadata = GetMetadataAsString(file.Path);
                        // We only store the path for now, not the thumbnail
                        tempList.Add(new ImageInfo { FilePath = file.Path, Metadata = metadata });
                    }
                    return tempList;
                });

                // Now, update the UI with the processed data
                foreach (var info in newImageInfos)
                {
                    // Create the thumbnail on the UI thread
                    info.Thumbnail = new BitmapImage(new Uri(info.FilePath));
                    loadedImages.Add(info);
                }

                // Select first item and hide progress
                if (loadedImages.Count > 0)
                {
                    ImageGridView.SelectedIndex = 0;
                    resultsTextBlock.Text = loadedImages[0].Metadata;
                }
                ProgressGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void ImageGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedImage = (ImageInfo)e.ClickedItem;
            if (clickedImage != null)
            {
                resultsTextBlock.Text = clickedImage.Metadata;
            }
        }

        // Note: This method no longer needs to be async
        private string GetMetadataAsString(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                using (var image = new MagickImage(filePath))
                {
                    var profile = image.GetExifProfile();
                    int gcd = GetGcd((int)image.Width, (int)image.Height);

                    var sb = new StringBuilder();
                    sb.AppendLine($"--- File System ---");
                    sb.AppendLine($"File Name: {fileInfo.Name}");
                    sb.AppendLine($"File Size: {fileInfo.Length / 1024.0:F2} KB");
                    sb.AppendLine($"Date Created: {fileInfo.CreationTime}");
                    sb.AppendLine($"Date Modified: {fileInfo.LastWriteTime}");
                    sb.AppendLine($"File Extension: {fileInfo.Extension}");
                    sb.AppendLine();
                    sb.AppendLine($"--- Image Properties ---");
                    sb.AppendLine($"Dimensions: {image.Width}x{image.Height}");
                    sb.AppendLine($"Aspect Ratio: {image.Width / gcd}:{image.Height / gcd}");
                    sb.AppendLine($"Megapixels: {((image.Width * image.Height) / 1000000.0):F2} MP");
                    sb.AppendLine($"File Format: {image.Format}");
                    sb.AppendLine($"Bit Depth: {image.Depth}");
                    sb.AppendLine($"Quality (JPEG): {image.Quality}%");
                    sb.AppendLine();

                    if (profile != null)
                    {
                        sb.AppendLine($"--- EXIF Data ---");
                        sb.AppendLine($"Make: {profile.GetValue(ExifTag.Make)?.ToString() ?? "N/A"}");
                        sb.AppendLine($"Camera Model: {profile.GetValue(ExifTag.Model)?.ToString() ?? "N/A"}");
                        sb.AppendLine($"Software: {profile.GetValue(ExifTag.Software)?.ToString() ?? "N/A"}");
                        sb.AppendLine($"Copyright: {profile.GetValue(ExifTag.Copyright)?.ToString() ?? "N/A"}");
                        sb.AppendLine($"Lens Model: {profile.GetValue(ExifTag.LensModel)?.ToString() ?? "N/A"}");
                        sb.AppendLine($"Shutter Speed: {profile.GetValue(ExifTag.ExposureTime)?.ToString() ?? "N/A"}s");
                        sb.AppendLine($"F-Number: F/{profile.GetValue(ExifTag.FNumber)?.ToString() ?? "N/A"}");
                        sb.AppendLine($"ISO: {profile.GetValue(ExifTag.ISOSpeedRatings)?.ToString() ?? "N/A"}");
                        sb.AppendLine($"Focal Length: {profile.GetValue(ExifTag.FocalLength)?.ToString() ?? "N/A"}mm");
                        sb.AppendLine($"Flash: {profile.GetValue(ExifTag.Flash)?.ToString() ?? "N/A"}");
                        sb.AppendLine($"Metering Mode: {profile.GetValue(ExifTag.MeteringMode)?.ToString() ?? "N/A"}");
                        sb.AppendLine($"Exposure Program: {profile.GetValue(ExifTag.ExposureProgram)?.ToString() ?? "N/A"}");
                    }
                    return sb.ToString();
                }
            }
            catch (Exception)
            {
                // We will handle errors differently since we can't show a dialog from a background thread easily
                return $"Error reading: {Path.GetFileName(filePath)}";
            }
        }

        private int GetGcd(int a, int b)
        {
            while (b != 0) { int temp = b; b = a % b; a = temp; }
            return a;
        }
    }
}