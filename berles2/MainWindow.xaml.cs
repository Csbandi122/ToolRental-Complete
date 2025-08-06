using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Microsoft.EntityFrameworkCore;
using ToolRental.Data;
using ToolRental.Core.Models;

namespace berles2
{
    public partial class MainWindow : Window
    {
        private ToolRentalDbContext _context;
        private List<Device> _selectedDevices = new List<Device>();
        private List<Device> _allDevices = new List<Device>();

        public MainWindow()
        {
            InitializeComponent();
            InitializeDatabase();
            InitializeForm();
            LoadDevices();
        }

        private void InitializeDatabase()
        {
            // Adatbázis kapcsolat inicializálása
            var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();
            optionsBuilder.UseSqlite("Data Source=ToolRental.db");
            _context = new ToolRentalDbContext(optionsBuilder.Options);
        }

        private void InitializeForm()
        {
            // Mai dátum beállítása
            RentStartTextBox.Text = DateTime.Now.ToString("yyyy.MM.dd HH:mm");

            // Következő ticket szám generálása
            GenerateNextTicketNumber();

            // Company név és logo betöltése
            LoadCompanySettings();

            // Végösszeg nullázása - direkt érték beállítás
            TotalAmountTextBox.Text = "0 Ft";

            // Végösszeg frissítése biztonságosan
            UpdateTotalAmount();
        }

        private void GenerateNextTicketNumber()
        {
            try
            {
                // Legutolsó rental ticket szám keresése
                var lastRental = _context.Rentals
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefault();

                int nextNumber = 1;
                if (lastRental != null && !string.IsNullOrEmpty(lastRental.TicketNr))
                {
                    // RNT0001 formátumból a számot kivesszük
                    var numberPart = lastRental.TicketNr.Replace("RNT", "");
                    if (int.TryParse(numberPart, out int lastNumber))
                    {
                        nextNumber = lastNumber + 1;
                    }
                }

                TicketNumberTextBox.Text = $"RNT{nextNumber:D4}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a ticket szám generálásakor: {ex.Message}");
                TicketNumberTextBox.Text = "RNT0001";
            }
        }

        private void LoadCompanySettings()
        {
            try
            {
                var settings = _context.Settings.FirstOrDefault();
                if (settings != null)
                {
                    CompanyNameText.Text = settings.CompanyName;

                    // Logo betöltése ha van
                    if (!string.IsNullOrEmpty(settings.CompanyLogo) && File.Exists(settings.CompanyLogo))
                    {
                        CompanyLogo.Source = new BitmapImage(new Uri(settings.CompanyLogo));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a cég beállítások betöltésekor: {ex.Message}");
            }
        }

        private void LoadDevices()
        {
            try
            {
                // Csak elérhető eszközök betöltése
                _allDevices = _context.Devices
                    .Include(d => d.DeviceTypeNavigation)
                    .Where(d => d.Available)
                    .ToList();

                DisplayDevices();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba az eszközök betöltésekor: {ex.Message}");
            }
        }

        private void DisplayDevices()
        {
            DevicesWrapPanel.Children.Clear();

            foreach (var device in _allDevices)
            {
                var deviceWidget = CreateDeviceWidget(device);
                DevicesWrapPanel.Children.Add(deviceWidget);
            }
        }

        private Border CreateDeviceWidget(Device device)
        {
            var border = new Border
            {
                Width = 150,
                Height = 200,
                Margin = new Thickness(5),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Cursor = Cursors.Hand
            };

            var stackPanel = new StackPanel();

            // Eszköz képe - EGYSZERŰ VERZIÓ
            var imageContainer = new Border
            {
                Width = 120,
                Height = 120,
                Margin = new Thickness(10, 10, 10, 5),
                Background = Brushes.LightGray,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };

            // Ha van kép, próbáljuk betölteni
            try
            {
                if (!string.IsNullOrEmpty(device.Picture) && File.Exists(device.Picture))
                {
                    var image = new Image
                    {
                        Source = new BitmapImage(new Uri(device.Picture)),
                        Stretch = Stretch.Uniform
                    };
                    imageContainer.Child = image;
                }
                else
                {
                    // Ha nincs kép, egy emoji ikon
                    var iconText = new TextBlock
                    {
                        Text = "🚲",  // Kerékpár emoji
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

            // Kattintás esemény
            border.MouseLeftButtonDown += (sender, e) => ToggleDeviceSelection(device, border);
            border.Tag = device;

            return border;
        }

        private void ToggleDeviceSelection(Device device, Border border)
        {
            if (_selectedDevices.Contains(device))
            {
                // Kijelölés megszüntetése
                _selectedDevices.Remove(device);
                border.Background = Brushes.White;
                border.BorderBrush = Brushes.Gray;
                border.BorderThickness = new Thickness(1);
            }
            else
            {
                // Kijelölés
                _selectedDevices.Add(device);
                border.Background = Brushes.LightBlue;
                border.BorderBrush = Brushes.Blue;
                border.BorderThickness = new Thickness(3);
            }

            UpdateTotalAmount();
        }

        private void UpdateTotalAmount()
        {
            // Null ellenőrzés - ha még nem lettek inicializálva a vezérlők
            if (RentalDaysTextBox == null || TotalAmountTextBox == null)
                return;

            int rentalDays = 1;
            if (int.TryParse(RentalDaysTextBox.Text, out int days))
            {
                rentalDays = Math.Max(1, days);
            }

            decimal total = _selectedDevices.Sum(d => d.RentPrice) * rentalDays;
            TotalAmountTextBox.Text = $"{total:N0} Ft";
        }

        private void RentalDaysTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTotalAmount();
        }

        private void ContractButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateForm())
            {
                try
                {
                    SaveRental();
                    MessageBox.Show("Bérlés sikeresen létrehozva!", "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearForm();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hiba történt a bérlés létrehozásakor: {ex.Message}", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(CustomerNameTextBox.Text))
            {
                MessageBox.Show("A név megadása kötelező!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(CustomerEmailTextBox.Text))
            {
                MessageBox.Show("Az e-mail cím megadása kötelező!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (_selectedDevices.Count == 0)
            {
                MessageBox.Show("Legalább egy eszközt ki kell választani!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void SaveRental()
        {
            using var transaction = _context.Database.BeginTransaction();

            try
            {
                // 1. Customer létrehozása
                var customer = new Customer
                {
                    Name = CustomerNameTextBox.Text.Trim(),
                    Zipcode = CustomerZipTextBox.Text.Trim(),
                    City = CustomerCityTextBox.Text.Trim(),
                    Address = CustomerAddressTextBox.Text.Trim(),
                    Email = CustomerEmailTextBox.Text.Trim(),
                    IdNumber = CustomerIdNumberTextBox.Text.Trim(),
                    Comment = CustomerCommentTextBox.Text.Trim()
                };

                _context.Customers.Add(customer);
                _context.SaveChanges();

                // 2. Rental létrehozása
                int rentalDays = int.TryParse(RentalDaysTextBox.Text, out int days) ? Math.Max(1, days) : 1;
                decimal totalAmount = _selectedDevices.Sum(d => d.RentPrice) * rentalDays;
                string ticketNr = TicketNumberTextBox.Text;

                var rental = new Rental
                {
                    TicketNr = ticketNr,
                    CustomerId = customer.Id,
                    RentStart = DateTime.Now,
                    RentalDays = rentalDays,
                    PaymentMode = ((ComboBoxItem)PaymentModeComboBox.SelectedItem).Content.ToString(),
                    Comment = RentalCommentTextBox.Text.Trim(),
                    TotalAmount = totalAmount
                };

                _context.Rentals.Add(rental);
                _context.SaveChanges();

                // 3. RentalDevice rekordok létrehozása
                foreach (var device in _selectedDevices)
                {
                    var rentalDevice = new RentalDevice
                    {
                        RentalId = rental.Id,
                        DeviceId = device.Id
                    };
                    _context.RentalDevices.Add(rentalDevice);

                    // Eszköz rent count növelése
                    device.RentCount++;
                }

                // 4. Financial rekord létrehozása
                var financial = new Financial
                {
                    TicketNr = ticketNr,
                    EntryType = "bevétel",
                    SourceType = "bérlés",
                    SourceId = rental.Id,
                    Date = DateTime.Now,
                    Comment = $"Bérlési díj - {ticketNr}",
                    Amount = totalAmount
                };

                _context.Financials.Add(financial);
                _context.SaveChanges();

                // 5. FinancialDevice rekordok létrehozása
                foreach (var device in _selectedDevices)
                {
                    var financialDevice = new FinancialDevice
                    {
                        FinancialId = financial.Id,
                        DeviceId = device.Id
                    };
                    _context.FinancialDevices.Add(financialDevice);
                }

                _context.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private void ClearForm()
        {
            // Ügyfél adatok törlése
            CustomerNameTextBox.Clear();
            CustomerZipTextBox.Clear();
            CustomerCityTextBox.Clear();
            CustomerAddressTextBox.Clear();
            CustomerEmailTextBox.Clear();
            CustomerIdNumberTextBox.Clear();
            CustomerCommentTextBox.Clear();

            // Bérlés adatok visszaállítása
            RentalDaysTextBox.Text = "1";
            PaymentModeComboBox.SelectedIndex = 0;
            RentalCommentTextBox.Clear();

            // Eszköz kijelölések törlése
            _selectedDevices.Clear();
            foreach (Border border in DevicesWrapPanel.Children)
            {
                border.Background = Brushes.White;
                border.BorderBrush = Brushes.Gray;
                border.BorderThickness = new Thickness(1);
            }

            // Új ticket szám generálása
            GenerateNextTicketNumber();
            UpdateTotalAmount();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Biztosan bezárja az ablakot? A bevitt adatok elvesznek!",
                                       "Megerősítés", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                this.Close();
            }
        }

        private void DataManagerButton_Click(object sender, RoutedEventArgs e)
        {
            var dataManagerWindow = new DataManagerWindow();
            dataManagerWindow.ShowDialog();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _context?.Dispose();
            base.OnClosing(e);
        }
    }
}