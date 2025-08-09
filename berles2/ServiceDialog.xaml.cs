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
    public partial class ServiceDialog : Window
    {
        public Service Service { get; private set; }
        public List<Device> SelectedDevices { get; private set; } = new List<Device>();

        private ToolRentalDbContext _context;
        private ObservableCollection<DeviceSelectionModel> _allDevices;
        private ObservableCollection<DeviceSelectionModel> _filteredDevices;

        // Új szervíz konstruktor
        public ServiceDialog()
        {
            InitializeComponent();
            InitializeDatabase();
            LoadDevices();
            InitializeNewService();
        }

        // Szerkesztés konstruktor (opcionális, később implementálható)
        public ServiceDialog(Service service) : this()
        {
            Service = service;
            TitleTextBlock.Text = "Szervíz jegy szerkesztése";
            LoadServiceData();
        }

        private void InitializeDatabase()
        {
            var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();
            optionsBuilder.UseSqlite("Data Source=ToolRental.db");
            _context = new ToolRentalDbContext(optionsBuilder.Options);
        }

        private void InitializeNewService()
        {
            Service = new Service();
            ServiceDatePicker.SelectedDate = DateTime.Now;
            GenerateTicketNumber();
        }

        private void GenerateTicketNumber()
        {
            try
            {
                // Legmagasabb SRV számot keresés
                var lastService = _context.Services
                    .Where(s => s.TicketNr.StartsWith("SRV"))
                    .OrderByDescending(s => s.Id)
                    .FirstOrDefault();

                int nextNumber = 1;
                if (lastService != null)
                {
                    // SRV0001 formátumból a számot kinyerjük
                    string numberPart = lastService.TicketNr.Substring(3);
                    if (int.TryParse(numberPart, out int lastNumber))
                    {
                        nextNumber = lastNumber + 1;
                    }
                }

                TicketNrTextBox.Text = $"SRV{nextNumber:D4}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a jegy szám generálásakor: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                TicketNrTextBox.Text = "SRV0001";
            }
        }

        private void LoadServiceData()
        {
            // Ez szerkesztéskor töltené be az adatokat
            // Most csak üres implementáció
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
                        IsSelected = false
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
            foreach (var device in filtered)
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

        private void CostAmountTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Színes összeg előnézet
            if (decimal.TryParse(CostAmountTextBox.Text, out decimal amount))
            {
                CostPreviewTextBlock.Text = $"-{amount:N0} Ft";
                CostPreviewTextBlock.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                CostPreviewTextBlock.Text = "";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateForm())
            {
                try
                {
                    // Service objektum feltöltése
                    Service.TicketNr = TicketNrTextBox.Text;
                    Service.ServiceType = ((System.Windows.Controls.ComboBoxItem)ServiceTypeComboBox.SelectedItem).Content.ToString();
                    Service.Technician = TechnicianTextBox.Text.Trim();
                    Service.ServiceDate = ServiceDatePicker.SelectedDate ?? DateTime.Now;
                    Service.CostAmount = decimal.Parse(CostAmountTextBox.Text);
                    Service.Description = DescriptionTextBox.Text.Trim();

                    // Service mentése
                    _context.Services.Add(Service);
                    _context.SaveChanges();

                    // ServiceDevice kapcsolatok létrehozása
                    var selectedDevices = _allDevices.Where(d => d.IsSelected).ToList();
                    foreach (var selectedDevice in selectedDevices)
                    {
                        var serviceDevice = new ServiceDevice
                        {
                            ServiceId = Service.Id,
                            DeviceId = selectedDevice.Id
                        };
                        _context.ServiceDevices.Add(serviceDevice);
                    }

                    // Financial rekord generálása (költség)
                    var financial = new Financial
                    {
                        TicketNr = Service.TicketNr,
                        EntryType = "költség",
                        SourceType = "szervíz",
                        SourceId = Service.Id,
                        Date = Service.ServiceDate,
                        Comment = $"Szervíz: {Service.ServiceType} - {Service.Description}",
                        Amount = Service.CostAmount
                    };
                    _context.Financials.Add(financial);
                    _context.SaveChanges();

                    // FinancialDevice kapcsolatok létrehozása
                    foreach (var selectedDevice in selectedDevices)
                    {
                        var financialDevice = new FinancialDevice
                        {
                            FinancialId = financial.Id,
                            DeviceId = selectedDevice.Id
                        };
                        _context.FinancialDevices.Add(financialDevice);
                    }

                    _context.SaveChanges();

                    //MessageBox.Show("Szervíz jegy sikeresen mentve!",
                      //            "Siker", MessageBoxButton.OK, MessageBoxImage.Information);

                    this.DialogResult = true;
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hiba a mentés során: {ex.Message}",
                                  "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool ValidateForm()
        {
            if (ServiceTypeComboBox.SelectedItem == null)
            {
                MessageBox.Show("Szervíz típus kiválasztása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                ServiceTypeComboBox.Focus();
                return false;
            }

            if (ServiceDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Szervíz dátum megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                ServiceDatePicker.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(CostAmountTextBox.Text) ||
                !decimal.TryParse(CostAmountTextBox.Text, out decimal cost) || cost < 0)
            {
                MessageBox.Show("Érvényes költség megadása kötelező (0 vagy nagyobb)!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                CostAmountTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                MessageBox.Show("Leírás megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                DescriptionTextBox.Focus();
                return false;
            }

            var selectedDevices = _allDevices?.Where(d => d.IsSelected).ToList();
            if (selectedDevices == null || !selectedDevices.Any())
            {
                MessageBox.Show("Legalább egy eszköz kiválasztása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
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