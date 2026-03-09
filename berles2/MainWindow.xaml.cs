using Microsoft.EntityFrameworkCore;
using Serilog;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ToolRental.Core.Models;
using ToolRental.Data;
using SystemIO = System.IO;
using WpfBorder = System.Windows.Controls.Border;
using System.Linq;

namespace berles2
{
    public partial class MainWindow : Window
    {
        private ToolRentalDbContext _context;
        private List<Device> _selectedDevices = new List<Device>();
        private List<Device> _allDevices = new List<Device>();
        private List<Device> _filteredDevices = new List<Device>();
        private Customer? _selectedExistingCustomer = null;
        private string? _lastContractTimestamp = null;
        private System.Windows.Threading.DispatcherTimer _searchTimer;
        private System.Windows.Threading.DispatcherTimer _sqlStatusTimer;

        public MainWindow()
        {
            InitializeComponent();
            InitializeDatabase();
            InitializeForm();
            LoadDevices();
            this.Activated += MainWindow_Activated;

            _searchTimer = new System.Windows.Threading.DispatcherTimer();
            _searchTimer.Interval = TimeSpan.FromMilliseconds(300);
            _searchTimer.Tick += SearchTimer_Tick;

            _sqlStatusTimer = new System.Windows.Threading.DispatcherTimer();
            _sqlStatusTimer.Interval = TimeSpan.FromSeconds(15);
            _sqlStatusTimer.Tick += SqlStatusTimer_Tick;
            _sqlStatusTimer.Start();
            _ = CheckSqlStatusAsync(); // azonnali első ellenőrzés indításkor
        }

        // ===========================================
        // INICIALIZÁLÁS
        // ===========================================

        private void InitializeDatabase()
        {
            _context = new ToolRentalDbContext(DatabaseConfig.GetOptions());
            UpdateModeIndicator();
        }

        private void InitializeForm()
        {
            RentStartTextBox.Text = DateTime.Now.ToString("yyyy.MM.dd HH:mm");
            GenerateNextTicketNumber();
            LoadCompanySettings();
            TotalAmountTextBox.Text = "0 Ft";
            UpdateTotalAmount();
            DiscountTextBox.TextChanged += DiscountTextBox_TextChanged;
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            LoadDevices();
        }

        private void GenerateNextTicketNumber()
        {
            try
            {
                var allTicketNumbers = _context.Rentals
                    .Where(r => r.TicketNr.StartsWith("RNT"))
                    .Select(r => r.TicketNr)
                    .ToList();

                int maxNumber = 0;
                foreach (var ticketNr in allTicketNumbers)
                {
                    if (int.TryParse(ticketNr.Replace("RNT", ""), out int number))
                        maxNumber = Math.Max(maxNumber, number);
                }

                TicketNumberTextBox.Text = $"RNT{maxNumber + 1:D4}";
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Warning(ex, "Hiba a ticket szám generálásakor");
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
                    if (!string.IsNullOrEmpty(settings.CompanyLogo) && SystemIO.File.Exists(settings.CompanyLogo))
                        CompanyLogo.Source = new BitmapImage(new Uri(settings.CompanyLogo));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error(ex, "Hiba a cég beállítások betöltésekor");
            }
        }

        // ===========================================
        // ESZKÖZ KEZELÉS
        // ===========================================

        public void LoadDevices()
        {
            try
            {
                _context.ChangeTracker.Clear();
                _allDevices = _context.Devices
                    .Include(d => d.DeviceTypeNavigation)
                    .Where(d => d.Available)
                    .ToList();

                DisplayDevices();
                UpdateTotalAmount();
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error(ex, "Hiba az eszközök betöltésekor");
                MessageBox.Show($"Hiba az eszközök betöltésekor: {ex.Message}");
            }
        }

        private void DeviceSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void SearchTimer_Tick(object sender, EventArgs e)
        {
            _searchTimer.Stop();
            FilterDevices();
        }

        private void FilterDevices()
        {
            if (_allDevices == null) return;

            string searchText = DeviceSearchTextBox.Text?.ToLower() ?? "";

            _filteredDevices = string.IsNullOrWhiteSpace(searchText)
                ? _allDevices.ToList()
                : _allDevices.Where(d =>
                    d.DeviceName.ToLower().Contains(searchText) ||
                    d.Serial.ToLower().Contains(searchText) ||
                    d.DeviceTypeNavigation?.TypeName.ToLower().Contains(searchText) == true
                  ).ToList();

            DisplayFilteredDevices();
        }

        private void DisplayDevices()
        {
            _filteredDevices = _allDevices.ToList();
            DisplayFilteredDevices();
        }

        private void DisplayFilteredDevices()
        {
            DevicesWrapPanel.Children.Clear();
            foreach (var device in _filteredDevices)
                DevicesWrapPanel.Children.Add(CreateDeviceWidget(device));
        }

        private WpfBorder CreateDeviceWidget(Device device)
        {
            var border = new WpfBorder
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

            var imageContainer = new WpfBorder
            {
                Width = 120,
                Height = 120,
                Margin = new Thickness(10, 10, 10, 5),
                Background = Brushes.LightGray,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };

            try
            {
                if (!string.IsNullOrEmpty(device.Picture) && SystemIO.File.Exists(device.Picture))
                {
                    imageContainer.Child = new Image
                    {
                        Source = new BitmapImage(new Uri(device.Picture)),
                        Stretch = Stretch.Uniform
                    };
                }
                else
                {
                    imageContainer.Child = MakeEmojiIcon();
                }
            }
            catch
            {
                imageContainer.Child = MakeEmojiIcon();
            }

            stackPanel.Children.Add(imageContainer);
            stackPanel.Children.Add(new TextBlock
            {
                Text = device.DeviceName,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(5, 0, 5, 5)
            });
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"{device.RentPrice:N0} Ft/nap",
                Foreground = Brushes.Green,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 10)
            });

            border.Child = stackPanel;
            border.MouseLeftButtonDown += (sender, e) => ToggleDeviceSelection(device, border);
            border.Tag = device;

            if (_selectedDevices.Any(d => d.Id == device.Id))
            {
                border.Background = Brushes.LightBlue;
                border.BorderBrush = Brushes.Blue;
                border.BorderThickness = new Thickness(3);
            }

            return border;
        }

        private static TextBlock MakeEmojiIcon() => new TextBlock
        {
            Text = "🚲",
            FontSize = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.DarkGray
        };

        private void ToggleDeviceSelection(Device device, WpfBorder border)
        {
            var existing = _selectedDevices.FirstOrDefault(d => d.Id == device.Id);
            if (existing != null)
            {
                _selectedDevices.Remove(existing);
                border.Background = Brushes.White;
                border.BorderBrush = Brushes.Gray;
                border.BorderThickness = new Thickness(1);
            }
            else
            {
                _selectedDevices.Add(device);
                border.Background = Brushes.LightBlue;
                border.BorderBrush = Brushes.Blue;
                border.BorderThickness = new Thickness(3);
            }

            UpdateTotalAmount();
        }

        // ===========================================
        // VÉGÖSSZEG SZÁMÍTÁS
        // ===========================================

        private int ParseRentalDays()
        {
            return int.TryParse(RentalDaysTextBox?.Text, out int d) ? Math.Max(1, d) : 1;
        }

        private int ParseDiscount()
        {
            return int.TryParse(DiscountTextBox?.Text, out int disc) ? Math.Max(0, Math.Min(100, disc)) : 0;
        }

        private decimal CalculateTotal()
        {
            return _selectedDevices.Sum(x => x.RentPrice) * (100 - ParseDiscount()) / 100 * ParseRentalDays();
        }

        private void UpdateTotalAmount()
        {
            if (TotalAmountTextBox == null) return;
            TotalAmountTextBox.Text = $"{CalculateTotal():N0} Ft";
        }

        private void RentalDaysTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTotalAmount();
        }

        private void CustomerZipTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string zip = CustomerZipTextBox.Text.Trim();
            if (zip.Length == 4)
            {
                string? city = Services.ZipCodeService.GetCity(zip);
                CustomerCityTextBox.Text = city ?? "";
            }
            else if (zip.Length < 4)
            {
                CustomerCityTextBox.Text = "";
            }
        }

        private void DiscountTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            string text = string.IsNullOrEmpty(textBox.Text) ? "0" : textBox.Text;

            if (int.TryParse(text, out int value))
            {
                if (value < 0) { textBox.Text = "0"; textBox.SelectionStart = textBox.Text.Length; }
                else if (value > 100) { textBox.Text = "100"; textBox.SelectionStart = textBox.Text.Length; }
            }
            else
            {
                textBox.Text = "0";
                textBox.SelectionStart = textBox.Text.Length;
            }

            UpdateTotalAmount();
        }

        // ===========================================
        // ÜGYFÉL KEZELÉS
        // ===========================================

        private void SelectExistingCustomerButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CustomerSelectDialog();
            if (dialog.ShowDialog() == true && dialog.SelectedCustomer != null)
            {
                _selectedExistingCustomer = dialog.SelectedCustomer;
                LoadSelectedCustomerData();
                ShowSelectedCustomerIndicator();
            }
        }

        private void LoadSelectedCustomerData()
        {
            if (_selectedExistingCustomer == null) return;
            CustomerNameTextBox.Text    = _selectedExistingCustomer.Name;
            CustomerZipTextBox.Text     = _selectedExistingCustomer.Zipcode;
            CustomerCityTextBox.Text    = _selectedExistingCustomer.City;
            CustomerAddressTextBox.Text = _selectedExistingCustomer.Address;
            CustomerEmailTextBox.Text   = _selectedExistingCustomer.Email;
            CustomerIdNumberTextBox.Text = _selectedExistingCustomer.IdNumber;
            CustomerCommentTextBox.Text  = _selectedExistingCustomer.Comment ?? "";
        }

        private void ShowSelectedCustomerIndicator()
        {
            if (_selectedExistingCustomer == null) return;
            SelectedCustomerBorder.Visibility = Visibility.Visible;
            SelectedCustomerText.Text = $"✅ Kiválasztott ügyfél: {_selectedExistingCustomer.Name}";
            SetCustomerFieldsEnabled(false);
        }

        private void ClearSelectedCustomerButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedExistingCustomer = null;
            SelectedCustomerBorder.Visibility = Visibility.Collapsed;
            SetCustomerFieldsEnabled(true);
            ClearCustomerFields();
        }

        private void SetCustomerFieldsEnabled(bool enabled)
        {
            CustomerNameTextBox.IsEnabled     = enabled;
            CustomerZipTextBox.IsEnabled      = enabled;
            CustomerCityTextBox.IsEnabled     = enabled;
            CustomerAddressTextBox.IsEnabled  = enabled;
            CustomerEmailTextBox.IsEnabled    = enabled;
            CustomerIdNumberTextBox.IsEnabled = enabled;
            CustomerCommentTextBox.IsEnabled  = enabled;
        }

        private void ClearCustomerFields()
        {
            CustomerNameTextBox.Clear();
            CustomerZipTextBox.Clear();
            CustomerCityTextBox.Clear();
            CustomerAddressTextBox.Clear();
            CustomerEmailTextBox.Clear();
            CustomerIdNumberTextBox.Clear();
            CustomerCommentTextBox.Clear();
        }

        // ===========================================
        // BÉRLÉSI FOLYAMAT
        // ===========================================

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(CustomerNameTextBox.Text))
            {
                MessageBox.Show("A név megadása kötelező!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string email = CustomerEmailTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show("Az e-mail cím megadása kötelező!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
            }
            catch
            {
                MessageBox.Show("Kérem adjon meg egy érvényes e-mail címet! (például: pelda@email.hu)",
                    "Hibás e-mail formátum", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (_selectedDevices.Count == 0)
            {
                MessageBox.Show("Legalább egy eszközt ki kell választani!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void ContractButton_Click(object sender, RoutedEventArgs e)
        {
            ContractButton.IsEnabled = false;

            if (!ValidateForm())
            {
                ContractButton.IsEnabled = true;
                return;
            }

            try
            {
                string address  = $"{CustomerZipTextBox.Text} {CustomerCityTextBox.Text}, {CustomerAddressTextBox.Text}";

                var confirmDialog = new ConfirmationDialog(
                    _selectedExistingCustomer?.Name ?? CustomerNameTextBox.Text,
                    address,
                    _selectedExistingCustomer?.Email ?? CustomerEmailTextBox.Text,
                    _selectedDevices,
                    CalculateTotal()
                );

                if (confirmDialog.ShowDialog() == true && confirmDialog.Confirmed)
                {
                    SaveRental();
                    GenerateWordContract();

                    ContractButton.Background = System.Windows.Media.Brushes.Gray;
                    EmailButton.IsEnabled = true;
                    EmailButton.Background = System.Windows.Media.Brushes.Blue;
                    LockAllInputFields();
                }
                else
                {
                    ContractButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                ContractButton.IsEnabled = true;
                ContractButton.Background = System.Windows.Media.Brushes.Green;
                MessageBox.Show($"Hiba történt a bérlés létrehozásakor: {ex.Message}",
                    "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveRental()
        {
            var data = new Services.RentalData
            {
                TicketNr            = TicketNumberTextBox.Text,
                RentalDays          = ParseRentalDays(),
                PaymentMode         = ((ComboBoxItem)PaymentModeComboBox.SelectedItem).Content.ToString() ?? "Készpénz",
                Comment             = RentalCommentTextBox.Text.Trim(),
                TotalAmount         = CalculateTotal(),
                Devices             = _selectedDevices.ToList(),
                ExistingCustomer    = _selectedExistingCustomer,
                NewCustomerName     = CustomerNameTextBox.Text.Trim(),
                NewCustomerZip      = CustomerZipTextBox.Text.Trim(),
                NewCustomerCity     = CustomerCityTextBox.Text.Trim(),
                NewCustomerAddress  = CustomerAddressTextBox.Text.Trim(),
                NewCustomerEmail    = CustomerEmailTextBox.Text.Trim(),
                NewCustomerIdNumber = CustomerIdNumberTextBox.Text.Trim(),
                NewCustomerComment  = CustomerCommentTextBox.Text.Trim()
            };

            new Services.RentalService(_context).SaveRental(data);
        }

        private void GenerateWordContract()
        {
            try
            {
                var setting = _context.Settings.FirstOrDefault();
                if (setting == null)
                {
                    MessageBox.Show("Nincsenek beállítások! Kérjük állítsa be a Beállítások menüben.",
                        "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var data = new Services.ContractData
                {
                    CustomerName     = _selectedExistingCustomer?.Name      ?? CustomerNameTextBox.Text,
                    CustomerZip      = _selectedExistingCustomer?.Zipcode   ?? CustomerZipTextBox.Text,
                    CustomerCity     = _selectedExistingCustomer?.City      ?? CustomerCityTextBox.Text,
                    CustomerAddress  = _selectedExistingCustomer?.Address   ?? CustomerAddressTextBox.Text,
                    CustomerEmail    = _selectedExistingCustomer?.Email     ?? CustomerEmailTextBox.Text,
                    CustomerIdNumber = _selectedExistingCustomer?.IdNumber  ?? CustomerIdNumberTextBox.Text,
                    RentalDays       = ParseRentalDays(),
                    DiscountPercent  = ParseDiscount(),
                    TotalAmount      = CalculateTotal(),
                    Devices          = _selectedDevices.ToList()
                };

                _lastContractTimestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");

                string pdfPath = new Services.DocumentService(setting).GenerateContract(data);
                if (!string.IsNullOrEmpty(pdfPath))
                    Process.Start(new ProcessStartInfo { FileName = pdfPath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error(ex, "Hiba a szerződés generálásakor");
                MessageBox.Show($"Hiba a szerződés generálásakor: {ex.Message}",
                    "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===========================================
        // EMAIL ÉS SZÁMLA
        // ===========================================

        private void EmailButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string pdfPath = ConvertWordToPdf();
                if (string.IsNullOrEmpty(pdfPath)) return;

                SendContractEmail(pdfPath);

                EmailButton.IsEnabled = false;
                EmailButton.Background = System.Windows.Media.Brushes.Gray;
                InvoiceButton.IsEnabled = true;
                InvoiceButton.Background = System.Windows.Media.Brushes.Orange;

                MessageBox.Show("Email sikeresen elküldve!", "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error(ex, "Email küldési hiba");
                MessageBox.Show($"Hiba az email küldésekor: {ex.Message}",
                    "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string ConvertWordToPdf()
        {
            try
            {
                var setting = _context.Settings.FirstOrDefault();
                if (setting == null) return "";

                string customerName = Services.DocumentService.GetCleanFileName(
                    _selectedExistingCustomer?.Name ?? CustomerNameTextBox.Text);
                string timestamp = _lastContractTimestamp ?? DateTime.Now.ToString("yyyy-MM-dd_HH-mm");

                return new Services.DocumentService(setting).GetOrCreateContractPdf(customerName, timestamp);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a PDF generálásakor: {ex.Message}",
                    "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                return "";
            }
        }

        private void SendContractEmail(string pdfPath)
        {
            var setting = _context.Settings.FirstOrDefault();
            if (setting == null)
            {
                MessageBox.Show("Email beállítások nincsenek megadva!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveContractPath(pdfPath);
            new Services.EmailService(setting).SendContractEmail(
                _selectedExistingCustomer?.Email ?? CustomerEmailTextBox.Text,
                _selectedExistingCustomer?.Name  ?? CustomerNameTextBox.Text,
                pdfPath);
        }

        private async void InvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            InvoiceButton.IsEnabled = false;
            try
            {
                await GenerateInvoiceXml();
                InvoiceButton.Background = System.Windows.Media.Brushes.Gray;
            }
            catch (Exception ex)
            {
                InvoiceButton.IsEnabled = true;
                InvoiceButton.Background = System.Windows.Media.Brushes.Blue;
                MessageBox.Show($"Hiba a számla generálásakor: {ex.Message}",
                    "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task GenerateInvoiceXml()
        {
            var setting = _context.Settings.FirstOrDefault()
                ?? throw new InvalidOperationException("Nincsenek beállítások! Kérlek állítsd be a beállításokban.");

            var data = new Services.InvoiceData
            {
                CustomerName    = _selectedExistingCustomer?.Name      ?? CustomerNameTextBox.Text,
                CustomerZip     = _selectedExistingCustomer?.Zipcode   ?? CustomerZipTextBox.Text,
                CustomerCity    = _selectedExistingCustomer?.City      ?? CustomerCityTextBox.Text,
                CustomerAddress = _selectedExistingCustomer?.Address   ?? CustomerAddressTextBox.Text,
                CustomerEmail   = _selectedExistingCustomer?.Email     ?? CustomerEmailTextBox.Text,
                PaymentMode     = (PaymentModeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Készpénz",
                NetPrice        = CalculateTotal(),
                DevicesList     = string.Join(", ", _selectedDevices.Select(d => d.DeviceName))
            };

            string pdfPath = await new Services.InvoiceService(setting).GenerateAndSendAsync(data);

            if (!string.IsNullOrEmpty(pdfPath))
            {
                MessageBox.Show($"Számla sikeresen elküldve és mentve!\nHelye: {pdfPath}",
                    "Számla siker", MessageBoxButton.OK, MessageBoxImage.Information);
                SaveInvoicePath(pdfPath);
            }
        }

        // ===========================================
        // ÉRTÉKELŐ EMAIL
        // ===========================================

        private void ReviewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SendReviewEmails();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba az értékelő emailek küldésekor: {ex.Message}",
                    "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SendReviewEmails()
        {
            var setting = _context.Settings.FirstOrDefault();
            if (setting == null || string.IsNullOrWhiteSpace(setting.ReviewEmailTemplate) ||
                string.IsNullOrWhiteSpace(setting.ReviewEmailSubject))
            {
                MessageBox.Show("Értékelő email beállítások hiányoznak! Kérjük állítsa be a Beállításokban.",
                    "Hiányzó beállítások", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var candidates = _context.Rentals
                .Include(r => r.Customer)
                .Where(r => !r.ReviewEmailSent &&
                            r.RentStart.AddDays(r.RentalDays).AddDays(setting.ReviewEmailDelayDays) <= DateTime.Now)
                .ToList();

            if (candidates.Count == 0)
            {
                MessageBox.Show("Nincs olyan bérlés, amelyhez értékelő emailt kellene küldeni.",
                    "Nincs feladat", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirmationWindow = new ReviewConfirmationWindow(candidates);
            if (confirmationWindow.ShowDialog() == true)
                SendReviewEmailsToCustomers(candidates, setting);
        }

        private void SendReviewEmailsToCustomers(List<Rental> rentals, Setting setting)
        {
            int successCount = 0;
            int errorCount   = 0;
            string errors    = "";
            var emailService = new Services.EmailService(setting);

            foreach (var rental in rentals)
            {
                try
                {
                    emailService.SendReviewEmail(rental);
                    rental.ReviewEmailSent = true;
                    _context.Update(rental);
                    successCount++;
                }
                catch (Exception ex)
                {
                    AppLogger.Logger.Error(ex, "Értékelő email küldési hiba - bérlés: {TicketNr}, ügyfél: {Customer}",
                        rental.TicketNr, rental.Customer.Name);
                    errorCount++;
                    errors += $"- {rental.Customer.Name} ({rental.TicketNr}): {ex.Message}\n";
                }
            }

            try { _context.SaveChanges(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba az adatbázis frissítésekor: {ex.Message}",
                    "Adatbázis hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string result = $"Email küldés befejezve!\n\nSikeresen elküldve: {successCount} db\nHibák: {errorCount} db";
            if (errorCount > 0) result += $"\n\nHiba részletek:\n{errors}";

            MessageBox.Show(result, "Email küldés eredménye", MessageBoxButton.OK,
                errorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            foreach (Window window in Application.Current.Windows)
            {
                if (window is DataManagerWindow dmw) { dmw.LoadRentals(); break; }
            }
        }

        // ===========================================
        // GOMBOK ÉS NAVIGÁCIÓ
        // ===========================================

        private void FinishButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Biztosan befejezed és törölni szeretnéd az összes adatot?",
                "Bérlés befejezése", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
                ClearAllForms();
        }

        private void ClearAllForms()
        {
            ClearCustomerFields();
            RentalDaysTextBox.Text = "1";
            PaymentModeComboBox.SelectedIndex = 0;
            RentalCommentTextBox.Text = "";
            TotalAmountTextBox.Text = "0 Ft";
            _selectedDevices.Clear();
            DisplayDevices();
            GenerateNextTicketNumber();
            UnlockAllInputFields();
            _selectedExistingCustomer = null;
            if (SelectedCustomerBorder != null)
                SelectedCustomerBorder.Visibility = Visibility.Collapsed;
            SetCustomerFieldsEnabled(true);
            UpdateTotalAmount();
            ContractButton.IsEnabled = true;
            ContractButton.Background = System.Windows.Media.Brushes.Green;
            EmailButton.IsEnabled = false;
            EmailButton.Background = System.Windows.Media.Brushes.Gray;
            InvoiceButton.IsEnabled = false;
            InvoiceButton.Background = System.Windows.Media.Brushes.Gray;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsDialog = new SettingsDialog();
            if (settingsDialog.ShowDialog() == true)
            {
                LoadCompanySettings();
                MessageBox.Show("Beállítások alkalmazva!", "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DataManagerButton_Click(object sender, RoutedEventArgs e)
        {
            new DataManagerWindow().ShowDialog();
        }

        private void ReportingButton_Click(object sender, RoutedEventArgs e)
        {
            new ReportingWindow().ShowDialog();
        }

        // ===========================================
        // MEZŐ ZÁROLÁS / FELOLDÁS
        // ===========================================

        private void LockAllInputFields()
        {
            CustomerNameTextBox.IsEnabled = false;
            CustomerZipTextBox.IsEnabled = false;
            CustomerCityTextBox.IsEnabled = false;
            CustomerAddressTextBox.IsEnabled = false;
            CustomerEmailTextBox.IsEnabled = false;
            CustomerIdNumberTextBox.IsEnabled = false;
            CustomerCommentTextBox.IsEnabled = false;
            SelectExistingCustomerButton.IsEnabled = false;
            ClearSelectedCustomerButton.IsEnabled = false;
            RentalDaysTextBox.IsEnabled = false;
            DiscountTextBox.IsEnabled = false;
            PaymentModeComboBox.IsEnabled = false;
            RentalCommentTextBox.IsEnabled = false;
            DeviceSearchTextBox.IsEnabled = false;
            DevicesWrapPanel.IsEnabled = false;
        }

        private void UnlockAllInputFields()
        {
            CustomerNameTextBox.IsEnabled = true;
            CustomerZipTextBox.IsEnabled = true;
            CustomerCityTextBox.IsEnabled = true;
            CustomerAddressTextBox.IsEnabled = true;
            CustomerEmailTextBox.IsEnabled = true;
            CustomerIdNumberTextBox.IsEnabled = true;
            CustomerCommentTextBox.IsEnabled = true;
            SelectExistingCustomerButton.IsEnabled = true;
            ClearSelectedCustomerButton.IsEnabled = true;
            RentalDaysTextBox.IsEnabled = true;
            DiscountTextBox.IsEnabled = true;
            PaymentModeComboBox.IsEnabled = true;
            RentalCommentTextBox.IsEnabled = true;
            DeviceSearchTextBox.IsEnabled = true;
            DevicesWrapPanel.IsEnabled = true;
        }

        // ===========================================
        // ADATBÁZIS SEGÉD METÓDUSOK
        // ===========================================

        private void SaveContractPath(string pdfPath)
        {
            try
            {
                var rental = _context.Rentals.FirstOrDefault(r => r.TicketNr == TicketNumberTextBox.Text);
                if (rental != null)
                {
                    rental.Contract = pdfPath;
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Warning(ex, "Hiba a szerződés útvonal mentésekor");
            }
        }

        private void SaveInvoicePath(string invoicePath)
        {
            try
            {
                var rental = _context.Rentals.FirstOrDefault(r => r.TicketNr == TicketNumberTextBox.Text);
                if (rental != null)
                {
                    rental.Invoice = invoicePath;
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Warning(ex, "Hiba a számla útvonal mentésekor");
            }
        }

        // ===========================================
        // TEST / PROD MÓD VÁLTÁS
        // ===========================================

        private void TestModeButton_Click(object sender, RoutedEventArgs e)
        {
            string targetDb = DatabaseConfig.IsTestMode ? "ToolRentalDB" : "ToolRentalDB_TEST";
            string targetLabel = DatabaseConfig.IsTestMode ? "PROD" : "TEST";

            var result = MessageBox.Show(
                $"Biztosan átváltasz {targetLabel} módra?\n\nAdatbázis: {targetDb}",
                "Mód váltás",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                _context?.Dispose();
                DatabaseConfig.SwitchDatabase(targetDb);
                _context = new ToolRentalDbContext(DatabaseConfig.GetOptions());
                _context.Database.EnsureCreated();

                _selectedDevices.Clear();
                LoadDevices();
                LoadCompanySettings();
                GenerateNextTicketNumber();
                UpdateModeIndicator();

                AppLogger.Logger.Information("Adatbázis átváltva: {Database}", targetDb);
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error(ex, "Hiba a mód váltáskor: {Database}", targetDb);
                MessageBox.Show($"Hiba az adatbázis váltáskor:\n{ex.Message}", "Hiba",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateModeIndicator()
        {
            if (DatabaseConfig.IsTestMode)
            {
                HeaderBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100"));
                TestModeButton.Content = "PROD módra váltás";
                TestModeButton.Background = new SolidColorBrush(Colors.White);
                TestModeButton.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100"));
                Title = "Kerékpár Bérlő Rendszer [TEST]";
            }
            else
            {
                HeaderBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                TestModeButton.Content = "TEST módra váltás";
                TestModeButton.Background = Brushes.Transparent;
                TestModeButton.Foreground = Brushes.White;
                Title = "Kerékpár Bérlő Rendszer";
            }
        }

        // ===========================================
        // SQL STÁTUSZ ELLENŐRZÉS
        // ===========================================

        private async void SqlStatusTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                await CheckSqlStatusAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Warning(ex, "SQL státusz ellenőrzés sikertelen");
            }
        }

        private async Task CheckSqlStatusAsync()
        {
            try
            {
                bool connected = await _context.Database.CanConnectAsync();
                SqlStatusEllipse.Fill = connected
                    ? System.Windows.Media.Brushes.LimeGreen
                    : System.Windows.Media.Brushes.Red;
            }
            catch
            {
                SqlStatusEllipse.Fill = System.Windows.Media.Brushes.Red;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _searchTimer?.Stop();
            _sqlStatusTimer?.Stop();
            this.Activated -= MainWindow_Activated;
            _context?.Dispose();
            base.OnClosing(e);
        }
    }
}
