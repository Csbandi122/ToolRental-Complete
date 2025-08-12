using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xaml;
using ToolRental.Core.Models;
using ToolRental.Data;
using System.Windows.Input;

namespace berles2
{
    public partial class DataManagerWindow : Window
    {
        private ToolRentalDbContext _context;
        private ObservableCollection<Customer> _customers;
        private List<Customer> _allCustomers;
        private ObservableCollection<Device> _devices;
        private List<Device> _allDevices;
        private ObservableCollection<RentalDisplayModel> _rentals;
        private List<RentalDisplayModel> _allRentals;
        private ObservableCollection<FinancialDisplayModel> _financials;
        private List<FinancialDisplayModel> _allFinancials;
        private ObservableCollection<ServiceDisplayModel> _services;
        private List<ServiceDisplayModel> _allServices;
        
        public bool ReviewEmailSent { get; set; } = false;

        public DataManagerWindow()
        {
            InitializeComponent();
            InitializeDatabase();
            LoadCustomers();
            LoadDevices();
            LoadRentals();
            LoadFinancials();
            LoadServices();
        }

        private void InitializeDatabase()
        {
            var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();
            optionsBuilder.UseSqlite("Data Source=ToolRental.db");
            _context = new ToolRentalDbContext(optionsBuilder.Options);
        }

        // ===========================================
        // ÜGYFELEK KEZELÉS
        // ===========================================
        private void LoadCustomers()
        {
            try
            {
                _allCustomers = _context.Customers.OrderBy(c => c.Name).ToList();
                _customers = new ObservableCollection<Customer>(_allCustomers);
                CustomersDataGrid.ItemsSource = _customers;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba az ügyfelek betöltésekor: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CustomersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = CustomersDataGrid.SelectedItem != null;
            EditCustomerButton.IsEnabled = hasSelection;
            DeleteCustomerButton.IsEnabled = hasSelection;
        }

        private void AddCustomerButton_Click(object sender, RoutedEventArgs e)
        {
            var customerDialog = new CustomerDialog();
            if (customerDialog.ShowDialog() == true)
            {
                try
                {
                    var customer = customerDialog.Customer;
                    _context.Customers.Add(customer);
                    _context.SaveChanges();
                    LoadCustomers();
                    MessageBox.Show("Ügyfél sikeresen hozzáadva!", "Siker",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hiba az ügyfél mentésekor: {ex.Message}",
                                  "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void EditCustomerButton_Click(object sender, RoutedEventArgs e)
        {
            if (CustomersDataGrid.SelectedItem is Customer selectedCustomer)
            {
                var customerDialog = new CustomerDialog(selectedCustomer);
                if (customerDialog.ShowDialog() == true)
                {
                    try
                    {
                        var customer = customerDialog.Customer;
                        var existingCustomer = _context.Customers.Find(customer.Id);
                        if (existingCustomer != null)
                        {
                            existingCustomer.Name = customer.Name;
                            existingCustomer.Zipcode = customer.Zipcode;
                            existingCustomer.City = customer.City;
                            existingCustomer.Address = customer.Address;
                            existingCustomer.Email = customer.Email;
                            existingCustomer.IdNumber = customer.IdNumber;
                            existingCustomer.Comment = customer.Comment;

                            _context.SaveChanges();
                            LoadCustomers();
                            MessageBox.Show("Ügyfél sikeresen módosítva!", "Siker",
                                          MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Hiba az ügyfél módosításakor: {ex.Message}",
                                      "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void DeleteCustomerButton_Click(object sender, RoutedEventArgs e)
        {
            if (CustomersDataGrid.SelectedItem is Customer selectedCustomer)
            {
                var result = MessageBox.Show(
                    $"Biztosan törli '{selectedCustomer.Name}' ügyfelet?\n\nFIGYELEM: A törlés visszavonhatatlan!",
                    "Megerősítés", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _context.Customers.Remove(selectedCustomer);
                        _context.SaveChanges();
                        LoadCustomers();
                        MessageBox.Show("Ügyfél sikeresen törölve!", "Siker",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Hiba az ügyfél törlésekor: {ex.Message}",
                                      "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void SearchCustomerTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterCustomers();
        }

        private void FilterCustomers()
        {
            if (_allCustomers == null || _customers == null) return;

            string searchText = SearchCustomerTextBox.Text?.ToLower() ?? "";
            var filteredCustomers = _allCustomers.Where(c =>
                string.IsNullOrWhiteSpace(searchText) ||
                c.Name.ToLower().Contains(searchText) ||
                c.Email.ToLower().Contains(searchText) ||
                c.City.ToLower().Contains(searchText) ||
                c.Address.ToLower().Contains(searchText)
            ).ToList();

            _customers.Clear();
            foreach (var customer in filteredCustomers.OrderBy(c => c.Name))
            {
                _customers.Add(customer);
            }
        }

        // ===========================================
        // ESZKÖZÖK KEZELÉS
        // ===========================================
        private void LoadDevices()
        {
            try
            {
                _allDevices = _context.Devices
                    .Include(d => d.DeviceTypeNavigation)
                    .OrderBy(d => d.DeviceName).ToList();
                _devices = new ObservableCollection<Device>(_allDevices);
                DevicesDataGrid.ItemsSource = _devices;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba az eszközök betöltésekor: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DevicesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = DevicesDataGrid.SelectedItem != null;
            EditDeviceButton.IsEnabled = hasSelection;
            DeleteDeviceButton.IsEnabled = hasSelection;
        }

        private void AddDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            var deviceDialog = new DeviceDialog();
            if (deviceDialog.ShowDialog() == true)
            {
                try
                {
                    var device = deviceDialog.Device;
                    _context.Devices.Add(device);
                    _context.SaveChanges();
                    LoadDevices();
                    MessageBox.Show("Eszköz sikeresen hozzáadva!", "Siker",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hiba az eszköz mentésekor: {ex.Message}",
                                  "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void EditDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (DevicesDataGrid.SelectedItem is Device selectedDevice)
            {
                var deviceDialog = new DeviceDialog(selectedDevice);
                if (deviceDialog.ShowDialog() == true)
                {
                    try
                    {
                        var device = deviceDialog.Device;
                        var existingDevice = _context.Devices.Find(device.Id);
                        if (existingDevice != null)
                        {
                            existingDevice.DeviceName = device.DeviceName;
                            existingDevice.DeviceType = device.DeviceType;
                            existingDevice.Serial = device.Serial;
                            existingDevice.Price = device.Price;
                            existingDevice.RentPrice = device.RentPrice;
                            existingDevice.Available = device.Available;
                            existingDevice.Notes = device.Notes;
                            // existingDevice.PicturePath = device.PicturePath;

                            _context.SaveChanges();
                            LoadDevices();
                            MessageBox.Show("Eszköz sikeresen módosítva!", "Siker",
                                          MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Hiba az eszköz módosításakor: {ex.Message}",
                                      "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void DeleteDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (DevicesDataGrid.SelectedItem is Device selectedDevice)
            {
                var result = MessageBox.Show(
                    $"Biztosan törli '{selectedDevice.DeviceName}' eszközt?\n\nFIGYELEM: A törlés visszavonhatatlan!",
                    "Megerősítés", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _context.Devices.Remove(selectedDevice);
                        _context.SaveChanges();
                        LoadDevices();
                        MessageBox.Show("Eszköz sikeresen törölve!", "Siker",
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Hiba az eszköz törlésekor: {ex.Message}",
                                      "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void SearchDeviceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterDevices();
        }

        private void FilterDevices()
        {
            if (_allDevices == null || _devices == null) return;

            string searchText = SearchDeviceTextBox.Text?.ToLower() ?? "";
            var filteredDevices = _allDevices.Where(d =>
                string.IsNullOrWhiteSpace(searchText) ||
                d.DeviceName.ToLower().Contains(searchText) ||
                d.Serial.ToLower().Contains(searchText) ||
                (d.DeviceTypeNavigation?.TypeName.ToLower().Contains(searchText) ?? false) ||
                (d.Notes?.ToLower().Contains(searchText) ?? false)
            ).ToList();

            _devices.Clear();
            foreach (var device in filteredDevices.OrderBy(d => d.DeviceName))
            {
                _devices.Add(device);
            }
        }

        // ===========================================
        // BÉRLÉSEK KEZELÉS
        // ===========================================
        private void LoadRentals()
        {
            try
            {
                var rentals = _context.Rentals
                    .Include(r => r.Customer)
                    .Include(r => r.RentalDevices)
                    .ThenInclude(rd => rd.Device)
                    .OrderByDescending(r => r.Id)
                    .Select(r => new RentalDisplayModel
                    {
                        Id = r.Id,
                        TicketNr = r.TicketNr,
                        CustomerName = r.Customer.Name,
                        RentStart = r.RentStart,
                        RentalDays = r.RentalDays,
                        TotalAmount = r.TotalAmount,
                        PaymentMode = r.PaymentMode,
                        Comment = r.Comment ?? "",
                        DevicesText = string.Join(", ", r.RentalDevices.Select(rd => rd.Device.DeviceName)),
                        Contract = r.Contract ?? "",
                        Invoice = r.Invoice ?? "",
                        ReviewEmailSent = r.ReviewEmailSent
                    })
                    .ToList();

                _allRentals = rentals;
                _rentals = new ObservableCollection<RentalDisplayModel>(rentals);
                RentalsDataGrid.ItemsSource = _rentals;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a bérlések betöltésekor: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void RentalsDataGrid_Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TextBlock textBlock &&
                textBlock.DataContext is RentalDisplayModel rental &&
                !string.IsNullOrWhiteSpace(rental.Contract))
            {
                try
                {
                    if (System.IO.File.Exists(rental.Contract))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = rental.Contract,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        MessageBox.Show("A PDF fájl nem található a következő helyen:\n" + rental.Contract,
                                      "Fájl nem található", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hiba a PDF megnyitásakor: {ex.Message}",
                                  "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SearchRentalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterRentals();
        }

        private void RefreshRentalsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadRentals();
        }

        private void FilterRentals()
        {
            if (_allRentals == null || _rentals == null) return;

            string searchText = SearchRentalTextBox.Text?.ToLower() ?? "";
            var filteredRentals = _allRentals.Where(r =>
                string.IsNullOrWhiteSpace(searchText) ||
                r.TicketNr.ToLower().Contains(searchText) ||
                r.CustomerName.ToLower().Contains(searchText) ||
                r.DevicesText.ToLower().Contains(searchText)
            ).ToList();

            _rentals.Clear();
            foreach (var rental in filteredRentals)
            {
                _rentals.Add(rental);
            }
        }

        // ===========================================
        // PÉNZÜGYEK KEZELÉS
        // ===========================================
        private void LoadFinancials()
        {
            try
            {
                var financials = _context.Financials
                    .Include(f => f.FinancialDevices)
                        .ThenInclude(fd => fd.Device)
                    .OrderByDescending(f => f.Date)
                    .Select(f => new FinancialDisplayModel
                    {
                        Id = f.Id,
                        Date = f.Date,
                        EntryType = f.EntryType,
                        SourceType = f.SourceType,
                        SourceId = f.SourceId, // ← ÚJ SOR!
                        TicketNr = f.TicketNr,
                        Amount = f.Amount,
                        Comment = f.Comment,
                        DevicesText = string.Join(", ", f.FinancialDevices.Select(fd => fd.Device.DeviceName))
                    })
                    .ToList();

                _allFinancials = financials;
                _financials = new ObservableCollection<FinancialDisplayModel>(_allFinancials);
                FinancialsDataGrid.ItemsSource = _financials;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a pénzügyi tételek betöltésekor: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        

        private void DeleteFinancialButton_Click(object sender, RoutedEventArgs e)
        {
            if (FinancialsDataGrid.SelectedItem is FinancialDisplayModel selectedFinancial)
            {
                // Bérléshez kapcsolódó tételeket nem lehet törölni
                if (selectedFinancial.SourceType == "bérlés" && selectedFinancial.SourceId.HasValue)
                {
                    MessageBox.Show("Bérléshez kapcsolódó pénzügyi tételeket nem lehet törölni!\n" +
                                  "A tétel automatikusan létrejött a bérléskor.",
                                  "Figyelmeztetés", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Biztosan törli ezt a pénzügyi tételt?\n\n" +
                    $"Dátum: {selectedFinancial.Date:yyyy.MM.dd}\n" +
                    $"Típus: {selectedFinancial.EntryType}\n" +
                    $"Összeg: {selectedFinancial.Amount:N0} Ft\n" +
                    $"Megjegyzés: {selectedFinancial.Comment}",
                    "Megerősítés", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Eredeti Financial entitás lekérése az Id alapján
                        var originalFinancial = _context.Financials.Find(selectedFinancial.Id);
                        if (originalFinancial != null)
                        {
                            _context.Financials.Remove(originalFinancial);
                            _context.SaveChanges();
                            LoadFinancials();
                            MessageBox.Show("Pénzügyi tétel sikeresen törölve!", "Siker",
                                          MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Hiba a törléskor: {ex.Message}",
                                      "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            }
        

        private void FinancialTypeFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterFinancials();
        }

        private void SearchFinancialTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterFinancials();
        }
        // ===========================================
        // PÉNZÜGYEK ESEMÉNYKEZELŐK
        // ===========================================

        private void AddFinancialButton_Click(object sender, RoutedEventArgs e)
        {
            var financialDialog = new FinancialDialog();
            if (financialDialog.ShowDialog() == true)
            {
                try
                {
                    var financial = financialDialog.Financial;
                    _context.Financials.Add(financial);
                    _context.SaveChanges();

                    // Eszköz kapcsolatok mentése
                    if (financialDialog.SelectedDevices.Any())
                    {
                        foreach (var device in financialDialog.SelectedDevices)
                        {
                            var financialDevice = new FinancialDevice
                            {
                                FinancialId = financial.Id,
                                DeviceId = device.Id
                            };
                            _context.FinancialDevices.Add(financialDevice);
                        }
                        _context.SaveChanges();
                    }

                    LoadFinancials();
                    MessageBox.Show("Pénzügyi tétel sikeresen hozzáadva!", "Siker",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hiba a pénzügyi tétel mentésekor: {ex.Message}",
                                  "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void FinancialsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = FinancialsDataGrid.SelectedItem != null;
            DeleteFinancialButton.IsEnabled = hasSelection;
        }

        private void FilterFinancials()
        {
            if (_allFinancials == null || _financials == null) return;

            string searchText = SearchFinancialTextBox.Text?.ToLower() ?? "";
            string selectedType = ((ComboBoxItem)FinancialTypeFilterComboBox.SelectedItem)?.Content?.ToString();

            var filteredFinancials = _allFinancials.Where(f =>
            {
                bool matchesSearch = string.IsNullOrWhiteSpace(searchText) ||
                                   f.Comment.ToLower().Contains(searchText) ||
                                   f.TicketNr.ToLower().Contains(searchText) ||
                                   f.SourceType.ToLower().Contains(searchText);

                bool matchesType = selectedType == "Összes típus" ||
                                 (selectedType == "Bevétel" && f.EntryType == "bevétel") ||
                                 (selectedType == "Költség" && f.EntryType == "költség");

                return matchesSearch && matchesType;
            }).ToList();

            _financials.Clear();
            foreach (var financial in filteredFinancials.OrderByDescending(f => f.Date))
            {
                _financials.Add(financial);
            }
        }

        // ===========================================
        // SZERVÍZ KEZELÉS
        // ===========================================
        private void LoadServices()
        {
            try
            {
                var services = _context.Services
                    .Include(s => s.ServiceDevices)
                    .ThenInclude(sd => sd.Device)
                    .OrderByDescending(s => s.ServiceDate)
                    .Select(s => new ServiceDisplayModel
                    {
                        Id = s.Id,
                        TicketNr = s.TicketNr,
                        ServiceDate = s.ServiceDate,
                        ServiceType = s.ServiceType,
                        Technician = s.Technician,
                        CostAmount = s.CostAmount,
                        Description = s.Description,
                        DeviceNames = string.Join(", ", s.ServiceDevices.Select(sd => sd.Device.DeviceName))
                    })
                    .ToList();

                _allServices = services;
                _services = new ObservableCollection<ServiceDisplayModel>(services);
                ServicesDataGrid.ItemsSource = _services;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a szervíz jegyek betöltésekor: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ServicesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = ServicesDataGrid.SelectedItem != null;
            
            DeleteServiceButton.IsEnabled = hasSelection;
        }

        private void AddServiceButton_Click(object sender, RoutedEventArgs e)
        {
            var serviceDialog = new ServiceDialog();
            if (serviceDialog.ShowDialog() == true)
            {
                LoadServices();
                MessageBox.Show("Szervíz jegy sikeresen létrehozva!",
                              "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }



        private void DeleteServiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServicesDataGrid.SelectedItem is ServiceDisplayModel selectedService)
            {
                var result = MessageBox.Show($"Biztosan törölni szeretnéd a(z) {selectedService.TicketNr} szervíz jegyet?\n\nEz a műveletet NEM lehet visszavonni!\n\nFIGYELEM: A kapcsolódó pénzügyi tétel is törlődni fog!",
                                           "Szervíz jegy törlése", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    using var transaction = _context.Database.BeginTransaction();
                    try
                    {
                        var service = _context.Services.Find(selectedService.Id);
                        if (service != null)
                        {
                            // 1. Kapcsolódó pénzügyi tétel keresése és törlése
                            var relatedFinancial = _context.Financials
                                .FirstOrDefault(f => f.SourceType == "szervíz" && f.SourceId == service.Id);

                            if (relatedFinancial != null)
                            {
                                _context.Financials.Remove(relatedFinancial);
                            }

                            // 2. Szervíz jegy törlése
                            _context.Services.Remove(service);

                            // 3. Változások mentése
                            _context.SaveChanges();
                            transaction.Commit();

                            // 4. Listák frissítése
                            LoadServices();
                            LoadFinancials(); // Pénzügyek lista is frissüljön

                            MessageBox.Show("Szervíz jegy és a kapcsolódó pénzügyi tétel sikeresen törölve!",
                                          "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show($"Hiba a törlés során: {ex.Message}",
                                      "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ServiceSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = ServiceSearchTextBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                _services.Clear();
                foreach (var service in _allServices)
                {
                    _services.Add(service);
                }
            }
            else
            {
                var filteredServices = _allServices.Where(s =>
                    s.TicketNr.ToLower().Contains(searchText) ||
                    s.Description.ToLower().Contains(searchText) ||
                    s.ServiceType.ToLower().Contains(searchText) ||
                    s.Technician.ToLower().Contains(searchText) ||
                    s.DeviceNames.ToLower().Contains(searchText)
                ).ToList();

                _services.Clear();
                foreach (var service in filteredServices)
                {
                    _services.Add(service);
                }
            }
        }
        private void ContractLink_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock &&
                textBlock.DataContext is RentalDisplayModel rental &&
                !string.IsNullOrWhiteSpace(rental.Contract))
            {
                try
                {
                    if (System.IO.File.Exists(rental.Contract))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = rental.Contract,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        MessageBox.Show("A PDF fájl nem található a következő helyen:\n" + rental.Contract,
                                      "Fájl nem található", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hiba a PDF megnyitásakor: {ex.Message}",
                                  "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void InvoiceLink_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock &&
                textBlock.DataContext is RentalDisplayModel rental &&
                !string.IsNullOrWhiteSpace(rental.Invoice))
            {
                try
                {
                    if (System.IO.File.Exists(rental.Invoice))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = rental.Invoice,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        MessageBox.Show("A számla fájl nem található a következő helyen:\n" + rental.Invoice,
                                      "Fájl nem található", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hiba a számla megnyitásakor: {ex.Message}",
                                  "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ===========================================
        // ABLAK KEZELÉS
        // ===========================================
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _context?.Dispose();
            base.OnClosing(e);
        }
    }

    // ===========================================
    // SEGÉD OSZTÁLYOK
    // ===========================================

    // SEGÉD OSZTÁLY A BÉRLÉSEK MEGJELENÍTÉSÉHEZ
    public class RentalDisplayModel
    {
        public int Id { get; set; }
        public string TicketNr { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public DateTime RentStart { get; set; }
        public int RentalDays { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMode { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string DevicesText { get; set; } = string.Empty;
        public string? Contract { get; set; } = string.Empty;
        public string? Invoice { get; set; } = string.Empty;
        public bool ReviewEmailSent { get; set; } = false;
    }
    // FinancialDisplayModel osztály a pénzügyek megjelenítéséhez
    public class FinancialDisplayModel
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string EntryType { get; set; } = string.Empty; // bevétel/költség
        public string SourceType { get; set; } = string.Empty; // bérlés/szervíz/egyéb
        public int? SourceId { get; set; } // ← ÚJ MEZŐ!
        public string TicketNr { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Comment { get; set; }
        public string DevicesText { get; set; } = string.Empty; // ← ÚJ!
    }

    // SEGÉD OSZTÁLY SZERVÍZ MEGJELENÍTÉSHEZ
    public class ServiceDisplayModel
    {
        public int Id { get; set; }
        public string TicketNr { get; set; } = string.Empty;
        public DateTime ServiceDate { get; set; }
        public string ServiceType { get; set; } = string.Empty;
        public string Technician { get; set; } = string.Empty;
        public decimal CostAmount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string DeviceNames { get; set; } = string.Empty;
    }

}
