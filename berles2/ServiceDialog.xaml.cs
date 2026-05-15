using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
        private ObservableCollection<AddedPartModel> _addedParts;
        private bool _isEditMode;

        public ServiceDialog()
        {
            InitializeComponent();
            _context = new ToolRentalDbContext(DatabaseConfig.GetOptions());
            _addedParts = new ObservableCollection<AddedPartModel>();
            AddedPartsListBox.ItemsSource = _addedParts;
            LoadDevices();
            LoadParts();
            InitializeNewService();
        }

        public ServiceDialog(Service service) : this()
        {
            _isEditMode = true;
            Service = service;
            TitleTextBlock.Text = "Szervíz jegy szerkesztése";
            LoadServiceData();
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
                TicketNrTextBox.Text = _context.GetNextServiceTicketNr();
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
            var svc = _context.Services
                .Include(s => s.ServiceDevices)
                .Include(s => s.ServiceParts)
                    .ThenInclude(sp => sp.Part)
                .FirstOrDefault(s => s.Id == Service.Id);

            if (svc == null) return;
            Service = svc;

            TicketNrTextBox.Text = svc.TicketNr;

            for (int i = 0; i < ServiceTypeComboBox.Items.Count; i++)
            {
                var item = (System.Windows.Controls.ComboBoxItem)ServiceTypeComboBox.Items[i];
                if (item.Content.ToString() == svc.ServiceType)
                {
                    ServiceTypeComboBox.SelectedIndex = i;
                    break;
                }
            }

            ServiceDatePicker.SelectedDate = svc.ServiceDate;
            CostAmountTextBox.Text = svc.CostAmount.ToString("0");
            WorkHoursTextBox.Text = svc.WorkHours.ToString();
            WorkMinutesTextBox.Text = svc.WorkMinutes.ToString();
            DescriptionTextBox.Text = svc.Description;

            foreach (var sp in svc.ServiceParts)
            {
                _addedParts.Add(new AddedPartModel
                {
                    PartId = sp.PartId,
                    PartName = sp.Part.Name,
                    Quantity = sp.Quantity
                });
            }

            var deviceIds = svc.ServiceDevices.Select(sd => sd.DeviceId).ToHashSet();
            foreach (var d in _allDevices)
            {
                d.IsSelected = deviceIds.Contains(d.Id);
            }
            UpdateSelectedDevicesDisplay();
        }

        private void LoadDevices()
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

        private void LoadParts()
        {
            var parts = _context.Parts.OrderBy(p => p.Name).ToList();
            PartComboBox.ItemsSource = parts;
        }

        // === ALKATRÉSZEK ===

        private void AddPartButton_Click(object sender, RoutedEventArgs e)
        {
            Part? selectedPart = PartComboBox.SelectedItem as Part;
            if (selectedPart == null)
            {
                MessageBox.Show("Válassz alkatrészt a listából!", "Figyelmeztetés",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int qty = 1;
            if (!string.IsNullOrWhiteSpace(PartQuantityTextBox.Text))
                int.TryParse(PartQuantityTextBox.Text, out qty);
            if (qty < 1) qty = 1;

            var existing = _addedParts.FirstOrDefault(p => p.PartId == selectedPart.Id);
            if (existing != null)
            {
                existing.Quantity += qty;
                AddedPartsListBox.ItemsSource = null;
                AddedPartsListBox.ItemsSource = _addedParts;
            }
            else
            {
                _addedParts.Add(new AddedPartModel
                {
                    PartId = selectedPart.Id,
                    PartName = selectedPart.Name,
                    Quantity = qty
                });
            }

            PartQuantityTextBox.Text = "1";
            PartComboBox.SelectedItem = null;
            PartComboBox.Text = "";
        }

        private void NewPartButton_Click(object sender, RoutedEventArgs e)
        {
            string partName = PartComboBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(partName))
            {
                MessageBox.Show("Írd be az új alkatrész nevét a mezőbe!", "Figyelmeztetés",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existingPart = _context.Parts.FirstOrDefault(p => p.Name == partName);
            if (existingPart != null)
            {
                MessageBox.Show($"'{partName}' már létezik az alkatrészek között.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                PartComboBox.SelectedItem = existingPart;
                return;
            }

            var newPart = new Part { Name = partName };
            _context.Parts.Add(newPart);
            _context.SaveChanges();

            LoadParts();
            PartComboBox.SelectedItem = _context.Parts.First(p => p.Name == partName);

            int qty = 1;
            if (!string.IsNullOrWhiteSpace(PartQuantityTextBox.Text))
                int.TryParse(PartQuantityTextBox.Text, out qty);
            if (qty < 1) qty = 1;

            _addedParts.Add(new AddedPartModel
            {
                PartId = newPart.Id,
                PartName = newPart.Name,
                Quantity = qty
            });

            PartQuantityTextBox.Text = "1";
            PartComboBox.Text = "";
        }

        private void RemovePartButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is AddedPartModel part)
            {
                _addedParts.Remove(part);
            }
        }

        // === ESZKÖZÖK ===

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
            foreach (var device in filtered.OrderByDescending(d => d.Available).ThenBy(d => d.DeviceName))
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

        // === KÖLTSÉG ELŐNÉZET ===

        private void CostAmountTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
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

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        // === MENTÉS ===

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm()) return;

            try
            {
                Service.TicketNr = TicketNrTextBox.Text;
                Service.ServiceType = ((System.Windows.Controls.ComboBoxItem)ServiceTypeComboBox.SelectedItem).Content.ToString()!;
                Service.ServiceDate = ServiceDatePicker.SelectedDate ?? DateTime.Now;
                Service.CostAmount = decimal.Parse(CostAmountTextBox.Text);
                Service.Description = DescriptionTextBox.Text.Trim();
                Service.WorkHours = int.TryParse(WorkHoursTextBox.Text, out int h) ? h : 0;
                Service.WorkMinutes = int.TryParse(WorkMinutesTextBox.Text, out int m) ? m : 0;

                if (_isEditMode)
                {
                    SaveEdit();
                }
                else
                {
                    SaveNew();
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a mentés során: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveNew()
        {
            _context.Services.Add(Service);
            _context.SaveChanges();

            SaveServiceDevices();
            SaveServiceParts();
            SaveFinancialRecord();

            _context.SaveChanges();
        }

        private void SaveEdit()
        {
            // Régi ServiceDevice kapcsolatok törlése
            var oldDevices = _context.ServiceDevices.Where(sd => sd.ServiceId == Service.Id).ToList();
            _context.ServiceDevices.RemoveRange(oldDevices);

            // Régi ServicePart kapcsolatok törlése
            var oldParts = _context.ServiceParts.Where(sp => sp.ServiceId == Service.Id).ToList();
            _context.ServiceParts.RemoveRange(oldParts);

            _context.SaveChanges();

            SaveServiceDevices();
            SaveServiceParts();

            // Financial rekord frissítése
            var financial = _context.Financials
                .FirstOrDefault(f => f.SourceType == ToolRental.Core.SourceTypes.Szerviz && f.SourceId == Service.Id);
            if (financial != null)
            {
                financial.TicketNr = Service.TicketNr;
                financial.Date = Service.ServiceDate;
                financial.Amount = Service.CostAmount;
                financial.Comment = $"Szervíz: {Service.ServiceType} - {Service.Description}";

                // FinancialDevice kapcsolatok frissítése
                var oldFinDevices = _context.FinancialDevices.Where(fd => fd.FinancialId == financial.Id).ToList();
                _context.FinancialDevices.RemoveRange(oldFinDevices);
                _context.SaveChanges();

                var selectedDevices = _allDevices.Where(d => d.IsSelected).ToList();
                foreach (var sd in selectedDevices)
                {
                    _context.FinancialDevices.Add(new FinancialDevice
                    {
                        FinancialId = financial.Id,
                        DeviceId = sd.Id
                    });
                }
            }

            _context.SaveChanges();
        }

        private void SaveServiceDevices()
        {
            var selectedDevices = _allDevices.Where(d => d.IsSelected).ToList();
            foreach (var sd in selectedDevices)
            {
                _context.ServiceDevices.Add(new ServiceDevice
                {
                    ServiceId = Service.Id,
                    DeviceId = sd.Id
                });
            }
        }

        private void SaveServiceParts()
        {
            foreach (var ap in _addedParts)
            {
                _context.ServiceParts.Add(new ServicePart
                {
                    ServiceId = Service.Id,
                    PartId = ap.PartId,
                    Quantity = ap.Quantity
                });
            }
        }

        private void SaveFinancialRecord()
        {
            var financial = new Financial
            {
                TicketNr = Service.TicketNr,
                EntryType = ToolRental.Core.EntryTypes.Koltseg,
                SourceType = ToolRental.Core.SourceTypes.Szerviz,
                SourceId = Service.Id,
                Date = Service.ServiceDate,
                Comment = $"Szervíz: {Service.ServiceType} - {Service.Description}",
                Amount = Service.CostAmount
            };
            _context.Financials.Add(financial);
            _context.SaveChanges();

            var selectedDevices = _allDevices.Where(d => d.IsSelected).ToList();
            foreach (var sd in selectedDevices)
            {
                _context.FinancialDevices.Add(new FinancialDevice
                {
                    FinancialId = financial.Id,
                    DeviceId = sd.Id
                });
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

        // === SEGÉD OSZTÁLYOK ===

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

        public class AddedPartModel
        {
            public int PartId { get; set; }
            public string PartName { get; set; } = string.Empty;
            public int Quantity { get; set; } = 1;
        }
    }
}
