// MainWindow.xaml.cs
#nullable enable
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace localAIscaler
{
    public partial class MainWindow : Window
    {
        private string? _inputPath;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            // MessageBox.Show($"Process Architecture: {RuntimeInformation.ProcessArchitecture}\nOS Architecture: {RuntimeInformation.OSArchitecture}\nIs 64-bit process: {Environment.Is64BitProcess}");
        }

        private void WindowBackground_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();

        private void DropArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Image|*.png;*.jpg;*.jpeg;*.webp",
                Title = "Select an image…"
            };
            if (dlg.ShowDialog() == true) LoadPreview(dlg.FileName);
        }

        private void DropArea_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                var ext = Path.GetExtension(files[0]).ToLower();
                e.Effects = (ext is ".png" or ".jpg" or ".jpeg" or ".webp")
                            ? DragDropEffects.Copy
                            : DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DropArea_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                if (files.Length > 0) LoadPreview(files[0]);
            }
        }

        private void LoadPreview(string path)
        {
            _inputPath = path;
            InstructionLabel.Visibility = Visibility.Collapsed;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                PreviewImage.Source = bmp;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Image could not be loaded: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Resmin varlığını kontrol et
            if (PreviewImage.Source == null)
            {
                MessageBox.Show("Please select or drag an image to proceed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Model dizini ve exe dosyasını bul
            var modelsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
            var exePath = Path.Combine(modelsRoot, "waifu2x-ncnn-vulkan.exe");

            // Eğer exe dosyası bulunmuyor ise hata mesajı verir
            if (!File.Exists(exePath))
            {
                MessageBox.Show($"\"waifu2x-ncnn-vulkan.exe\" not found in '{modelsRoot}'.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // cmbBox seçili modeli al
            var selectedModel = ModelComboBox.SelectedItem as ComboBoxItem;
            // Model ismini al
            var modelName = selectedModel.Content.ToString()?.ToLowerInvariant();
            string modelDir;

            // Seçilen model dizini ayarlanıyor
            switch (modelName)
            {
                case "artwork":
                    modelDir = Path.Combine(modelsRoot, "models-upconv_7_anime_style_art_rgb");
                    break;
                case "artwork/scans":
                    modelDir = Path.Combine(modelsRoot, "models-cunet");
                    break;
                case "photo":
                    modelDir = Path.Combine(modelsRoot, "models-upconv_7_photo");
                    break;
                default:
                    MessageBox.Show("Invalid model selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
            }

            // Tamamlanan resim geçici olarak saklanacağı alan
            var outputFileName = Path.Combine(modelsRoot, "temp_img.png");

            // Slider'dan gürültü ve çarpan değeri alma
            var noise = (int)NoiseSlider.Value;
            var scale = (int)ScaleSlider.Value;

            // waifu2x parametreleri
            var args = $"-i \"{_inputPath}\" -o \"{outputFileName}\" -n {noise} -s {scale} -m \"{modelDir}\"";

            //waifu2x-ncnn-vulkan.exe'yi başlat
            var startInfo = new ProcessStartInfo(exePath, args)
            {
                WorkingDirectory = modelDir,
                RedirectStandardOutput = true, // çıktıyı almak için
                RedirectStandardError = true, // hata çıktısını almak için (geçici)
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            ProcessProgressBar.Value = 0;
            StartButton.IsEnabled = false;

            try
            {
                using var process = Process.Start(startInfo) ?? throw new Exception("Process could not be started.");
                // İşlem bitene kadar bekle
                await process.WaitForExitAsync();
                // İşlem tamamlandıktan sonra
                Dispatcher.Invoke(() =>
                {
                    ProcessProgressBar.Value = 100;
                    if (File.Exists(outputFileName))
                    {
                        // dosya yönetimini aç
                        var saveFileDialog = new SaveFileDialog
                        {
                            FileName = Path.GetFileNameWithoutExtension(_inputPath) + "_upscaled.png",
                            Filter = "PNG Image|*.png|JPEG Image|*jpg;*.jpeg|WebP Image|*.webp",
                            DefaultExt = ".png",
                        };

                        if (saveFileDialog.ShowDialog() == true)
                        {
                            // Kullanıcının seçtiği yeri al
                            var finalOutputPath = saveFileDialog.FileName;
                            // Çıktı dosyasını kaydet
                            File.Copy(outputFileName, finalOutputPath, true);
                            // Yükseltilmiş resmin önizlemesini PreviewImage'e ekle
                            var finalImage = new BitmapImage();
                            finalImage.BeginInit();
                            finalImage.CacheOption = BitmapCacheOption.OnLoad;
                            finalImage.UriSource = new Uri(finalOutputPath, UriKind.Absolute);
                            finalImage.EndInit();
                            PreviewImage.Source = finalImage;
                        }
                    }

                    StartButton.IsEnabled = true;
                });

            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StartButton.IsEnabled = true;
            }
        }
    }
}
