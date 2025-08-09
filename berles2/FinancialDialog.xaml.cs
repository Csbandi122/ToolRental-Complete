using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using ToolRental.Core.Models;
using ToolRental.Data;


namespace berles2
{
    public partial class FinancialDialog : Window
    {
        public Financial Financial { get; private set; }
        public List<Device> SelectedDevices { get; private set; } = new List<Device>();

        
        private ToolRentalDbContext _context;
        private ObservableCollection<DeviceSelectionModel> _allDevices;
        private ObservableCollection<DeviceSelectionModel> _filteredDevices;

        // Új tétel konstruktor
        public FinancialDialog()
        {
            InitializeComponent();
            InitializeDatabase();
            LoadDevices();
            InitializeNewFinancial();
        }

        // Szerkesztés konstruktor
        

        private void InitializeDatabase()
        {
            var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();
            optionsBuilder.UseSqlite("Data Source=ToolRental.db");
            _context = new ToolRentalDbContext(optionsBuilder.Options);
        }

        private void InitializeNewFinancial()
        {
            DatePicker.SelectedDate = DateTime.Now;
            EntryTypeComboBox.SelectedIndex = 0; // bevétel
            SourceTypeComboBox.SelectedIndex = 0; // kézi
            AmountTextBox.Text = "0";
            UpdateAmountPreview();
        }

       
        private void EntryTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateAmountPreview();
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Csak számokat engedünk
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void AmountTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateAmountPreview();
        }

        private void UpdateAmountPreview()
        {
            if (AmountTextBox == null || AmountPreviewText == null || AmountPreviewBorder == null) return;

            decimal amount = 0;
            if (decimal.TryParse(AmountTextBox.Text, out amount))
            {
                AmountPreviewText.Text = $"{amount:N0} Ft";

                // Színek a típus alapján
                if (EntryTypeComboBox.SelectedIndex == 0) // bevétel
                {
                    AmountPreviewText.Foreground = System.Windows.Media.Brushes.Green;
                    AmountPreviewBorder.BorderBrush = System.Windows.Media.Brushes.Green;
                    AmountPreviewBorder.Background = System.Windows.Media.Brushes.LightGreen;
                }
                else // költség
                {
                    AmountPreviewText.Foreground = System.Windows.Media.Brushes.Red;
                    AmountPreviewBorder.BorderBrush = System.Windows.Media.Brushes.Red;
                    AmountPreviewBorder.Background = System.Windows.Media.Brushes.LightPink;
                }
            }
            else
            {
                AmountPreviewText.Text = "0 Ft";
                AmountPreviewText.Foreground = System.Windows.Media.Brushes.Gray;
                AmountPreviewBorder.BorderBrush = System.Windows.Media.Brushes.Gray;
                AmountPreviewBorder.Background = System.Windows.Media.Brushes.LightGray;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateForm())
            {
                try
                {
                    var entryType = ((System.Windows.Controls.ComboBoxItem)EntryTypeComboBox.SelectedItem).Content.ToString();
                    var sourceType = ((System.Windows.Controls.ComboBoxItem)SourceTypeComboBox.SelectedItem).Content.ToString();

                    // Kiválasztott eszközök összegyűjtése
                    SelectedDevices = _allDevices.Where(d => d.IsSelected)
                                                .Select(d => new Device { Id = d.Id, DeviceName = d.DeviceName })
                                                .ToList();

                    // Új tétel létrehozása
                    Financial = new Financial
                    {
                        Date = DatePicker.SelectedDate ?? DateTime.Now,
                        EntryType = entryType,
                        SourceType = sourceType,
                        TicketNr = TicketNumberTextBox.Text.Trim(),
                        Amount = decimal.Parse(AmountTextBox.Text),
                        Comment = CommentTextBox.Text.Trim(),
                        SourceId = null
                    };

                    this.DialogResult = true;
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hiba történt a mentés során: {ex.Message}",
                                  "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool ValidateForm()
        {
            if (!DatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("A dátum megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                DatePicker.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(AmountTextBox.Text) || !decimal.TryParse(AmountTextBox.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Érvényes összeg megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                AmountTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(CommentTextBox.Text))
            {
                MessageBox.Show("A megjegyzés megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                CommentTextBox.Focus();
                return false;
            }

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // TextChanged esemény hozzáadása az inicializálás után
            AmountTextBox.TextChanged += AmountTextBox_TextChanged;
        }
        private void LoadDevices()
        {
            try
            {
                var devices = _context.Devices
                    .Include(d => d.DeviceTypeNavigation)
                    .OrderBy(d => d.DeviceName)
                    .Select(d => new DeviceSelectionModel
                    {
                        Id = d.Id,
                        DeviceName = d.DeviceName,
                        Serial = d.Serial,
                        DeviceTypeNavigation = d.DeviceTypeNavigation,
                        IsSelected = false,
                        Available = d.Available
                    })
                    .ToList();

                _allDevices = new ObservableCollection<DeviceSelectionModel>(devices);
                _filteredDevices = new ObservableCollection<DeviceSelectionModel>(devices);
                DevicesListBox.ItemsSource = _filteredDevices;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba az eszközök betöltésekor: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeviceSearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            FilterDevices();
        }

        private void FilterDevices()
        {
            if (_allDevices == null) return;

            string searchText = DeviceSearchTextBox.Text?.ToLower() ?? "";

            var filtered = _allDevices.Where(d =>
                string.IsNullOrWhiteSpace(searchText) ||
                d.DeviceName.ToLower().Contains(searchText) ||
                d.Serial.ToLower().Contains(searchText) ||
                d.DeviceTypeNavigation?.TypeName.ToLower().Contains(searchText) == true
            ).ToList();

            _filteredDevices.Clear();
            // Rendezés: elérhető eszközök felül, nem elérhető alul
            var sortedDevices = filtered.OrderByDescending(d => d.Available).ThenBy(d => d.DeviceName);
            foreach (var device in sortedDevices)
            {
                _filteredDevices.Add(device);
            }
        }

        private void DevicesListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateSelectedDevicesDisplay();
        }

        private void UpdateSelectedDevicesDisplay()
        {
            var selectedItems = _allDevices.Where(d => d.IsSelected).ToList();

            if (selectedItems.Any())
            {
                SelectedDevicesBorder.Visibility = Visibility.Visible;
                SelectedDevicesText.Text = string.Join(", ", selectedItems.Select(d => d.DeviceName));
            }
            else
            {
                SelectedDevicesBorder.Visibility = Visibility.Collapsed;
                SelectedDevicesText.Text = "";
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _context?.Dispose();
            base.OnClosing(e);
        }
        // SEGÉD OSZTÁLY ESZKÖZ KIVÁLASZTÁSHOZ
        public class DeviceSelectionModel : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string DeviceName { get; set; } = string.Empty;
            public string Serial { get; set; } = string.Empty;
            public DeviceType? DeviceTypeNavigation { get; set; }

            public bool Available { get; set; } = true;

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}