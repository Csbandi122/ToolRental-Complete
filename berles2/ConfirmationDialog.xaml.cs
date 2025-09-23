using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ToolRental.Core.Models;

namespace berles2
{
    public partial class ConfirmationDialog : Window
    {
        public bool Confirmed { get; private set; } = false;

        public ConfirmationDialog(
            string customerName,
            string customerAddress,
            string customerEmail,
            List<Device> selectedDevices,
            decimal totalAmount)
        {
            InitializeComponent();

            // Adatok beállítása
            CustomerNameText.Text = customerName;
            CustomerAddressText.Text = customerAddress;
            CustomerEmailText.Text = customerEmail;
            DeviceCountText.Text = $"{selectedDevices.Count} darab";

            // Kerékpárok vizuális megjelenítése
            DisplayDevices(selectedDevices);

            // Teljes összeg
            TotalAmountText.Text = $"{totalAmount:N0} Ft";
        }

        private void DisplayDevices(List<Device> devices)
        {
            DevicesWrapPanel.Children.Clear();

            foreach (var device in devices)
            {
                var border = CreateDeviceBorder(device);
                DevicesWrapPanel.Children.Add(border);
            }
        }

        private Border CreateDeviceBorder(Device device)
        {
            // Külső Border
            var border = new Border
            {
                Width = 120,
                Height = 150,
                Margin = new Thickness(5),
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5)
            };

            var stackPanel = new StackPanel();

            // KÉP CONTAINER
            var imageContainer = new Border
            {
                Width = 110,
                Height = 80,
                Margin = new Thickness(5, 5, 5, 0),
                Background = Brushes.LightGray,
                CornerRadius = new CornerRadius(3)
            };

            // Kép betöltése vagy emoji
            try
            {
                if (!string.IsNullOrEmpty(device.Picture) && File.Exists(device.Picture))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(device.Picture);
                    bitmap.DecodePixelWidth = 110;
                    bitmap.EndInit();

                    var image = new Image
                    {
                        Source = bitmap,
                        Stretch = Stretch.UniformToFill
                    };
                    imageContainer.Child = image;
                }
                else
                {
                    // Ha nincs kép, emoji
                    var iconText = new TextBlock
                    {
                        Text = "🚲",
                        FontSize = 48,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Brushes.DarkGray
                    };
                    imageContainer.Child = iconText;
                }
            }
            catch
            {
                // Ha hiba van, emoji ikon
                var iconText = new TextBlock
                {
                    Text = "🚲",
                    FontSize = 48,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.DarkGray
                };
                imageContainer.Child = iconText;
            }

            // Eszköz neve
            var nameText = new TextBlock
            {
                Text = device.DeviceName,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(5, 0, 5, 5)
            };

            // Bérlési ár
            var priceText = new TextBlock
            {
                Text = $"{device.RentPrice:N0} Ft/nap",
                Foreground = Brushes.Green,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 10)
            };

            stackPanel.Children.Add(imageContainer);
            stackPanel.Children.Add(nameText);
            stackPanel.Children.Add(priceText);
            border.Child = stackPanel;

            return border;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            this.DialogResult = false;
            this.Close();
        }
    }
}