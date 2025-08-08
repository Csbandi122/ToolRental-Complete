using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ToolRental.Core.Models;
using ToolRental.Data;

namespace berles2
{
    public partial class DeviceDialog : Window
    {
        public Device Device { get; private set; }
        private bool _isEditMode;
       

        // Konstruktor új eszköz hozzáadásához
        public DeviceDialog()
        {
            InitializeComponent();
            
            _isEditMode = false;
            Device = new Device();
            TitleTextBlock.Text = "Új eszköz hozzáadása";
            LoadDeviceTypes();
        }

        // Konstruktor meglévő eszköz szerkesztéséhez
        public DeviceDialog(Device device)
        {
            InitializeComponent();
            
            _isEditMode = true;
            Device = new Device
            {
                Id = device.Id,
                DeviceName = device.DeviceName,
                DeviceType = device.DeviceType,
                Serial = device.Serial,
                Price = device.Price,
                RentPrice = device.RentPrice,
                Available = device.Available,
                Picture = device.Picture,
                Notes = device.Notes,
                RentCount = device.RentCount
            };

            TitleTextBlock.Text = "Eszköz szerkesztése";
            LoadDeviceTypes();
            LoadDeviceData();
        }



        private void LoadDeviceTypes()
        {
            try
            {
                using var context = GetDbContext();
                var deviceTypes = context.DeviceTypes.OrderBy(dt => dt.TypeName).ToList();
                DeviceTypeComboBox.ItemsSource = deviceTypes;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba az eszköztípusok betöltésekor: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDeviceData()
        {
            DeviceNameTextBox.Text = Device.DeviceName;
            DeviceTypeComboBox.SelectedValue = Device.DeviceType;
            SerialTextBox.Text = Device.Serial;
            PriceTextBox.Text = Device.Price > 0 ? Device.Price.ToString("F0") : "";
            RentPriceTextBox.Text = Device.RentPrice.ToString("F0");
            AvailableCheckBox.IsChecked = Device.Available;
            PicturePathTextBox.Text = Device.Picture ?? "";
            NotesTextBox.Text = Device.Notes;

            // Kép előnézet betöltése
            LoadImagePreview(Device.Picture);
        }

        private void BrowsePictureButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Kép fájlok (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Minden fájl (*.*)|*.*",
                Title = "Válasszon képfájlt"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFile = openFileDialog.FileName;
                PicturePathTextBox.Text = selectedFile;
                LoadImagePreview(selectedFile);
            }
        }

        private void LoadImagePreview(string imagePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath);
                    bitmap.DecodePixelWidth = 200; // Optimalizáció
                    bitmap.EndInit();
                    ImagePreview.Source = bitmap;
                }
                else
                {
                    ImagePreview.Source = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a kép betöltésekor: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                ImagePreview.Source = null;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateForm())
            {
                Device.DeviceName = DeviceNameTextBox.Text.Trim();
                Device.DeviceType = (int)DeviceTypeComboBox.SelectedValue;
                Device.Serial = SerialTextBox.Text.Trim();

                // Árak konvertálása
                if (decimal.TryParse(PriceTextBox.Text, out decimal price))
                    Device.Price = price;
                else
                    Device.Price = 0;

                if (decimal.TryParse(RentPriceTextBox.Text, out decimal rentPrice))
                    Device.RentPrice = rentPrice;

                Device.Available = AvailableCheckBox.IsChecked ?? true;
                Device.Picture = string.IsNullOrWhiteSpace(PicturePathTextBox.Text) ? null : PicturePathTextBox.Text.Trim();
                Device.Notes = NotesTextBox.Text.Trim();

                DialogResult = true;
                Close();
            }
        }

        private bool ValidateForm()
        {
            // Eszköz neve ellenőrzése
            if (string.IsNullOrWhiteSpace(DeviceNameTextBox.Text))
            {
                MessageBox.Show("Az eszköz neve megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                DeviceNameTextBox.Focus();
                return false;
            }

            // Eszköz típus ellenőrzése
            if (DeviceTypeComboBox.SelectedValue == null)
            {
                MessageBox.Show("Az eszköz típusának kiválasztása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                DeviceTypeComboBox.Focus();
                return false;
            }

            // Bérlési ár ellenőrzése
            if (!decimal.TryParse(RentPriceTextBox.Text, out decimal rentPrice) || rentPrice <= 0)
            {
                MessageBox.Show("Kérem adjon meg egy érvényes bérlési árat!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                RentPriceTextBox.Focus();
                return false;
            }

            // Vételár ellenőrzése (opcionális, de ha van, akkor pozitívnak kell lennie)
            if (!string.IsNullOrWhiteSpace(PriceTextBox.Text))
            {
                if (!decimal.TryParse(PriceTextBox.Text, out decimal price) || price < 0)
                {
                    MessageBox.Show("Kérem adjon meg egy érvényes vételárat!", "Hiba",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    PriceTextBox.Focus();
                    return false;
                }
            }

            // Kép ellenőrzése (ha van megadva, létezik-e)
            if (!string.IsNullOrWhiteSpace(PicturePathTextBox.Text) && !File.Exists(PicturePathTextBox.Text))
            {
                MessageBox.Show("A megadott kép fájl nem található!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            
            base.OnClosing(e);
        }
        
        private ToolRentalDbContext GetDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();
            optionsBuilder.UseSqlite("Data Source=ToolRental.db");
            return new ToolRentalDbContext(optionsBuilder.Options);
        }
    }
}