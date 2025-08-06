using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using ToolRental.Data;
using ToolRental.Core.Models;

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

        public DataManagerWindow()
        {
            InitializeComponent();
            InitializeDatabase();
            LoadCustomers();
            LoadDevices();
            LoadRentals();
        }

        private void InitializeDatabase()
        {
            var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();
            optionsBuilder.UseSqlite("Data Source=ToolRental.db");
            _context = new ToolRentalDbContext(optionsBuilder.Options);
        }

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

                    LoadCustomers(); // Frissítés
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
                            LoadCustomers(); // Frissítés

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
                    "Törlés megerősítése", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Ellenőrizzük, hogy vannak-e kapcsolódó bérlések
                        var hasRentals = _context.Rentals.Any(r => r.CustomerId == selectedCustomer.Id);
                        if (hasRentals)
                        {
                            MessageBox.Show(
                                "Ez az ügyfél nem törölhető, mert vannak hozzá kapcsolódó bérlések!",
                                "Törlés nem lehetséges", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        var customerToDelete = _context.Customers.Find(selectedCustomer.Id);
                        if (customerToDelete != null)
                        {
                            _context.Customers.Remove(customerToDelete);
                            _context.SaveChanges();
                            LoadCustomers(); // Frissítés

                            MessageBox.Show("Ügyfél sikeresen törölve!", "Siker",
                                          MessageBoxButton.OK, MessageBoxImage.Information);
                        }
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
            string searchText = SearchCustomerTextBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Ha üres a keresés, mutasd az összes ügyfelet
                _customers.Clear();
                foreach (var customer in _allCustomers)
                {
                    _customers.Add(customer);
                }
            }
            else
            {
                // Szűrés név, város vagy e-mail alapján
                var filteredCustomers = _allCustomers.Where(c =>
                    c.Name.ToLower().Contains(searchText) ||
                    c.City.ToLower().Contains(searchText) ||
                    c.Email.ToLower().Contains(searchText)
                ).ToList();

                _customers.Clear();
                foreach (var customer in filteredCustomers)
                {
                    _customers.Add(customer);
                }
            }
        }

        // ESZKÖZÖK KEZELÉSE
        private void LoadDevices()
        {
            try
            {
                _allDevices = _context.Devices
                    .Include(d => d.DeviceTypeNavigation)
                    .OrderBy(d => d.DeviceName)
                    .ToList();
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

                    LoadDevices(); // Frissítés
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
                            existingDevice.Picture = device.Picture;
                            existingDevice.Notes = device.Notes;

                            _context.SaveChanges();
                            LoadDevices(); // Frissítés

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
                    "Törlés megerősítése", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Ellenőrizzük, hogy vannak-e kapcsolódó bérlések
                        var hasRentals = _context.RentalDevices.Any(rd => rd.DeviceId == selectedDevice.Id);
                        if (hasRentals)
                        {
                            MessageBox.Show(
                                "Ez az eszköz nem törölhető, mert vannak hozzá kapcsolódó bérlések!",
                                "Törlés nem lehetséges", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        var deviceToDelete = _context.Devices.Find(selectedDevice.Id);
                        if (deviceToDelete != null)
                        {
                            _context.Devices.Remove(deviceToDelete);
                            _context.SaveChanges();
                            LoadDevices(); // Frissítés

                            MessageBox.Show("Eszköz sikeresen törölve!", "Siker",
                                          MessageBoxButton.OK, MessageBoxImage.Information);
                        }
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
            string searchText = SearchDeviceTextBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Ha üres a keresés, mutasd az összes eszközt
                _devices.Clear();
                foreach (var device in _allDevices)
                {
                    _devices.Add(device);
                }
            }
            else
            {
                // Szűrés név vagy sorozatszám alapján
                var filteredDevices = _allDevices.Where(d =>
                    d.DeviceName.ToLower().Contains(searchText) ||
                    d.Serial.ToLower().Contains(searchText) ||
                    (d.DeviceTypeNavigation?.TypeName.ToLower().Contains(searchText) ?? false)
                ).ToList();

                _devices.Clear();
                foreach (var device in filteredDevices)
                {
                    _devices.Add(device);
                }
            }
        }

        // BÉRLÉSEK KEZELÉSE
        private void LoadRentals()
        {
            try
            {
                var rentalsFromDb = _context.Rentals
                    .Include(r => r.Customer)
                    .Include(r => r.RentalDevices)
                        .ThenInclude(rd => rd.Device)
                    .OrderByDescending(r => r.RentStart)
                    .ToList();

                // Konvertálás display modellre
                _allRentals = rentalsFromDb.Select(r => new RentalDisplayModel
                {
                    Id = r.Id,
                    TicketNr = r.TicketNr,
                    CustomerName = r.Customer.Name,
                    RentStart = r.RentStart,
                    RentalDays = r.RentalDays,
                    TotalAmount = r.TotalAmount,
                    PaymentMode = r.PaymentMode,
                    Comment = r.Comment ?? "",
                    DevicesText = string.Join(", ", r.RentalDevices.Select(rd => rd.Device.DeviceName))
                }).ToList();

                _rentals = new ObservableCollection<RentalDisplayModel>(_allRentals);
                RentalsDataGrid.ItemsSource = _rentals;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a bérlések betöltésekor: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshRentalsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadRentals();
            SearchRentalTextBox.Clear();
        }

        private void SearchRentalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchRentalTextBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Ha üres a keresés, mutasd az összes bérlést
                _rentals.Clear();
                foreach (var rental in _allRentals)
                {
                    _rentals.Add(rental);
                }
            }
            else
            {
                // Szűrés jegy szám vagy ügyfél név alapján
                var filteredRentals = _allRentals.Where(r =>
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
        }

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
    }
}