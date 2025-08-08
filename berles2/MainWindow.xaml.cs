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
using Microsoft.EntityFrameworkCore;
using ToolRental.Data;
using ToolRental.Core.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Diagnostics;
using SystemIO = System.IO;
using WpfBorder = System.Windows.Controls.Border;
using MailKit.Net.Smtp;
using MimeKit;
using WordInterop = Microsoft.Office.Interop.Word;


// ALIASOK - mint a régi kódban
using OpenXmlTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using OpenXmlTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using OpenXmlTableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using OpenXmlParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using OpenXmlRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using OpenXmlText = DocumentFormat.OpenXml.Wordprocessing.Text;
using BorderValues = DocumentFormat.OpenXml.Wordprocessing.BorderValues;
using OpenXmlBold = DocumentFormat.OpenXml.Wordprocessing.Bold;

namespace berles2
{
    public partial class MainWindow : Window
    {
        private ToolRentalDbContext _context;
        private List<Device> _selectedDevices = new List<Device>();
        private List<Device> _allDevices = new List<Device>();
        private Customer? _selectedExistingCustomer = null;

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
                    if (!string.IsNullOrEmpty(settings.CompanyLogo) && SystemIO.File.Exists(settings.CompanyLogo))
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

            // Eszköz képe - EGYSZERŰ VERZIÓ
            var imageContainer = new WpfBorder
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
                if (!string.IsNullOrEmpty(device.Picture) && SystemIO.File.Exists(device.Picture))
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

        private void ToggleDeviceSelection(Device device, WpfBorder border)
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
            // Validáció és bérlés mentése
            if (ValidateForm())
            {
                try
                {
                    SaveRental();

                    // WORD SZERZŐDÉS GENERÁLÁS - ÚJ FUNKCIÓ!
                    GenerateWordContract();

                    // Sikeres mentés után gombok állapotának frissítése
                    ContractButton.IsEnabled = false;
                    ContractButton.Background = System.Windows.Media.Brushes.Gray;

                    EmailButton.IsEnabled = true;
                    EmailButton.Background = System.Windows.Media.Brushes.Blue;

                    
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hiba történt a bérlés létrehozásakor: {ex.Message}",
                                  "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
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
                // 1. Customer kezelése - meglévő vagy új
                Customer customer;
                if (_selectedExistingCustomer != null)
                {
                    // Meglévő ügyfél használata
                    customer = _selectedExistingCustomer;
                }
                else
                {
                    // Új ügyfél létrehozása
                    customer = new Customer
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
                }


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
            foreach (WpfBorder border in DevicesWrapPanel.Children)
            {
                border.Background = Brushes.White;
                border.BorderBrush = Brushes.Gray;
                border.BorderThickness = new Thickness(1);
            }

            // Új ticket szám generálása
            GenerateNextTicketNumber();
            UpdateTotalAmount();

            // Kiválasztott ügyfél törlése
            _selectedExistingCustomer = null;
            SelectedCustomerBorder.Visibility = Visibility.Collapsed;
            SetCustomerFieldsEnabled(true);
        }





        private void DataManagerButton_Click(object sender, RoutedEventArgs e)
        {
            var dataManagerWindow = new DataManagerWindow();
            dataManagerWindow.ShowDialog();
        }
        private void SelectExistingCustomerButton_Click(object sender, RoutedEventArgs e)
        {
            var customerSelectDialog = new CustomerSelectDialog();
            if (customerSelectDialog.ShowDialog() == true && customerSelectDialog.SelectedCustomer != null)
            {
                _selectedExistingCustomer = customerSelectDialog.SelectedCustomer;
                LoadSelectedCustomerData();
                ShowSelectedCustomerIndicator();
            }
        }

        private void LoadSelectedCustomerData()
        {
            if (_selectedExistingCustomer != null)
            {
                CustomerNameTextBox.Text = _selectedExistingCustomer.Name;
                CustomerZipTextBox.Text = _selectedExistingCustomer.Zipcode;
                CustomerCityTextBox.Text = _selectedExistingCustomer.City;
                CustomerAddressTextBox.Text = _selectedExistingCustomer.Address;
                CustomerEmailTextBox.Text = _selectedExistingCustomer.Email;
                CustomerIdNumberTextBox.Text = _selectedExistingCustomer.IdNumber;
                CustomerCommentTextBox.Text = _selectedExistingCustomer.Comment ?? "";
            }
        }

        private void ShowSelectedCustomerIndicator()
        {
            if (_selectedExistingCustomer != null)
            {
                SelectedCustomerBorder.Visibility = Visibility.Visible;
                SelectedCustomerText.Text = $"✅ Kiválasztott ügyfél: {_selectedExistingCustomer.Name}";

                // Mezők letiltása hogy ne lehessen módosítani
                SetCustomerFieldsEnabled(false);
            }
        }

        private void ClearSelectedCustomerButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedExistingCustomer = null;
            SelectedCustomerBorder.Visibility = Visibility.Collapsed;

            // Mezők engedélyezése és törlése
            SetCustomerFieldsEnabled(true);
            ClearCustomerFields();
        }

        private void SetCustomerFieldsEnabled(bool enabled)
        {
            CustomerNameTextBox.IsEnabled = enabled;
            CustomerZipTextBox.IsEnabled = enabled;
            CustomerCityTextBox.IsEnabled = enabled;
            CustomerAddressTextBox.IsEnabled = enabled;
            CustomerEmailTextBox.IsEnabled = enabled;
            CustomerIdNumberTextBox.IsEnabled = enabled;
            CustomerCommentTextBox.IsEnabled = enabled;
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
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _context?.Dispose();
            base.OnClosing(e);
        }
        // ===========================================
        // BÉRLÉSI FOLYAMAT GOMBOK
        // ===========================================

        private void EmailButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. PDF generálás a Word dokumentumból
                string pdfPath = ConvertWordToPdf();
                if (string.IsNullOrEmpty(pdfPath))
                    return;

                // 2. Email küldés
                SendContractEmail(pdfPath);

                // 3. Gombok állapotának frissítése
                EmailButton.IsEnabled = false;
                EmailButton.Background = System.Windows.Media.Brushes.Gray;

                InvoiceButton.IsEnabled = true;
                InvoiceButton.Background = System.Windows.Media.Brushes.Orange;

                MessageBox.Show("Email sikeresen elküldve!",
                              "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba az email küldésekor: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FinishButton_Click(object sender, RoutedEventArgs e)
        {
            // Ablak inicializálása
            var result = MessageBox.Show("Biztosan befejezed és törölni szeretnéd az összes adatot?",
                                        "Bérlés befejezése", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ClearAllForms();
                MessageBox.Show("Az ablak inicializálva! Új bérlést kezdhetsz.",
                              "Kész", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearAllForms()
        {
            // Bérlő adatok törlése
            CustomerNameTextBox.Text = "";
            CustomerZipTextBox.Text = "";  // ← JAVÍTVA!
            CustomerCityTextBox.Text = "";
            CustomerAddressTextBox.Text = "";
            CustomerEmailTextBox.Text = "";
            CustomerIdNumberTextBox.Text = "";
            CustomerCommentTextBox.Text = "";

            // Bérlés részletek törlése
            RentalDaysTextBox.Text = "1";  // ← Alapértelmezett érték
            PaymentModeComboBox.SelectedIndex = 0;
            RentalCommentTextBox.Text = "";
            TotalAmountTextBox.Text = "0 Ft";

            // Kiválasztott eszközök törlése
            _selectedDevices.Clear();

            // Eszközök megjelenítésének újratöltése
            DisplayDevices();

            // Ticket szám újragenerálása
            GenerateNextTicketNumber();  // ← JAVÍTVA!

            // Kiválasztott ügyfél törlése
            _selectedExistingCustomer = null;
            if (SelectedCustomerBorder != null)
                SelectedCustomerBorder.Visibility = Visibility.Collapsed;
            SetCustomerFieldsEnabled(true);

            // Végösszeg frissítése
            UpdateTotalAmount();

            // Gombok visszaállítása
            ContractButton.IsEnabled = true;
            ContractButton.Background = System.Windows.Media.Brushes.Green;

            EmailButton.IsEnabled = false;
            EmailButton.Background = System.Windows.Media.Brushes.Gray;

            InvoiceButton.IsEnabled = false;
            InvoiceButton.Background = System.Windows.Media.Brushes.Gray;
        }
        // ===========================================
        // BÉRLÉSI FOLYAMAT GOMBOK
        // ===========================================

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsDialog = new SettingsDialog();
            if (settingsDialog.ShowDialog() == true)
            {
                // Beállítások frissítése után reload company settings
                LoadCompanySettings();
                MessageBox.Show("Beállítások alkalmazva!",
                              "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        // ===========================================
        // WORD TÁBLÁZAT GENERÁLÁS - RÉGI KÓD ALAPJÁN
        // ===========================================

        private void GenerateWordContract()
        {
            try
            {
                // 1. Beállítások betöltése
                var setting = _context.Settings.FirstOrDefault();
                if (setting?.TemplateContract == null || !SystemIO.File.Exists(setting.TemplateContract))
                {
                    MessageBox.Show("A szerződés sablon nincs beállítva vagy nem található!\nKérjük állítsa be a Beállítások menüben.",
                                  "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. Mappák létrehozása
                string exeDirectory = SystemIO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                string contractsFolder = SystemIO.Path.Combine(exeDirectory, "files", "contracts-word");
                SystemIO.Directory.CreateDirectory(contractsFolder);

                // 3. Fájlnév generálása
                string customerName = GetCleanFileName(_selectedExistingCustomer?.Name ?? CustomerNameTextBox.Text);
                string rentalDate = DateTime.Now.ToString("yyyy-MM-dd");
                string fileName = $"szerződés_{customerName}_{rentalDate}.docx";
                string outputPath = SystemIO.Path.Combine(contractsFolder, fileName);

                // 4. Template másolása
                SystemIO.File.Copy(setting.TemplateContract, outputPath, true);

                // 5. Változók helyettesítése
                ReplaceVariablesInWordDocument(outputPath);

                // 6. Automatikus megnyitás
                Process.Start(new ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true
                });

               
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a szerződés generálásakor: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReplaceVariablesInWordDocument(string filePath)
        {
            using (WordprocessingDocument document = WordprocessingDocument.Open(filePath, true))
            {
                var body = document.MainDocumentPart?.Document.Body;
                if (body != null)
                {
                    // Szöveg elemek változói
                    foreach (var text in body.Descendants<OpenXmlText>())
                    {
                        if (text.Text.Contains("{{"))
                        {
                            string originalText = text.Text;

                            // Ügyfél adatok
                            originalText = originalText.Replace("{{CUSTOMER_NAME}}", _selectedExistingCustomer?.Name ?? CustomerNameTextBox.Text);
                            originalText = originalText.Replace("{{CUSTOMER_ZIP}}", _selectedExistingCustomer?.Zipcode ?? CustomerZipTextBox.Text);
                            originalText = originalText.Replace("{{CUSTOMER_CITY}}", _selectedExistingCustomer?.City ?? CustomerCityTextBox.Text);
                            originalText = originalText.Replace("{{CUSTOMER_ADDRESS}}", _selectedExistingCustomer?.Address ?? CustomerAddressTextBox.Text);
                            originalText = originalText.Replace("{{CUSTOMER_EMAIL}}", _selectedExistingCustomer?.Email ?? CustomerEmailTextBox.Text);
                            originalText = originalText.Replace("{{CUSTOMER_ID_NUMBER}}", _selectedExistingCustomer?.IdNumber ?? CustomerIdNumberTextBox.Text);

                            // Bérlés adatok
                            originalText = originalText.Replace("{{RENTAL_DATE}}", DateTime.Now.ToString("yyyy. MM. dd."));
                            originalText = originalText.Replace("{{RENTAL_DAYS}}", RentalDaysTextBox.Text);
                            originalText = originalText.Replace("{{DEVICE_COUNT}}", _selectedDevices.Count.ToString());

                            // Végösszeg
                            int rentalDays = int.TryParse(RentalDaysTextBox.Text, out int days) ? days : 1;
                            decimal totalAmount = _selectedDevices.Sum(d => d.RentPrice) * rentalDays;
                            originalText = originalText.Replace("{{TOTAL_AMOUNT}}", $"{totalAmount:N0}");

                            text.Text = originalText;
                        }
                    }

                    // TÁBLÁZAT GENERÁLÁS
                    ReplaceDeviceTable(body);
                }

                document.Save();
            }
        }

        private void ReplaceDeviceTable(Body body)
        {
            foreach (var text in body.Descendants<OpenXmlText>())
            {
                if (text.Text.Contains("{{DEVICE_TABLE}}"))
                {
                    // Táblázat létrehozása - PONT MINT A RÉGI KÓDBAN
                    OpenXmlTable table = new OpenXmlTable();

                    // Táblázat tulajdonságok - RÉGI KÓD ALAPJÁN
                    TableProperties tblProp = new TableProperties(
                        new TableBorders(
                            new TopBorder() { Val = BorderValues.Single, Size = 12 },
                            new BottomBorder() { Val = BorderValues.Single, Size = 12 },
                            new LeftBorder() { Val = BorderValues.Single, Size = 12 },
                            new RightBorder() { Val = BorderValues.Single, Size = 12 },
                            new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 12 },
                            new InsideVerticalBorder() { Val = BorderValues.Single, Size = 12 }
                        )
                    );
                    table.AppendChild(tblProp);

                    // Fejléc sor - RÉGI KÓD ALAPJÁN
                    OpenXmlTableRow headerRow = new OpenXmlTableRow();
                    headerRow.AppendChild(CreateTableCell("Eszköztípus", true));
                    headerRow.AppendChild(CreateTableCell("Eszköz neve", true));
                    headerRow.AppendChild(CreateTableCell("Sorozatszám", true));
                    headerRow.AppendChild(CreateTableCell("Eszköz értéke", true));
                    headerRow.AppendChild(CreateTableCell("Bérleti díj", true));
                    table.AppendChild(headerRow);

                    // Eszköz sorok - RÉGI KÓD ALAPJÁN
                    foreach (var device in _selectedDevices)
                    {
                        OpenXmlTableRow dataRow = new OpenXmlTableRow();
                        dataRow.AppendChild(CreateTableCell(device.DeviceTypeNavigation?.TypeName ?? "N/A", false));
                        dataRow.AppendChild(CreateTableCell(device.DeviceName, false));
                        dataRow.AppendChild(CreateTableCell(device.Serial, false));
                        dataRow.AppendChild(CreateTableCell(device.Price.ToString("F0") + " Ft", false));
                        dataRow.AppendChild(CreateTableCell(device.RentPrice.ToString("F0") + " Ft/nap", false));
                        table.AppendChild(dataRow);
                    }

                    // Szöveg cseréje táblázatra - RÉGI KÓD ALAPJÁN
                    var paragraph = text.Ancestors<OpenXmlParagraph>().First();
                    text.Text = text.Text.Replace("{{DEVICE_TABLE}}", "");
                    paragraph.Parent.InsertAfter(table, paragraph);
                    break;
                }
            }
        }

        private OpenXmlTableCell CreateTableCell(string text, bool isBold)
        {
            OpenXmlTableCell cell = new OpenXmlTableCell();

            OpenXmlParagraph paragraph = new OpenXmlParagraph();
            OpenXmlRun run = new OpenXmlRun();

            if (isBold)
            {
                RunProperties runProps = new RunProperties();
                DocumentFormat.OpenXml.Wordprocessing.Bold bold = new DocumentFormat.OpenXml.Wordprocessing.Bold();
                runProps.AppendChild(bold);
                run.AppendChild(runProps);
            }

            OpenXmlText cellText = new OpenXmlText(text);
            run.AppendChild(cellText);
            paragraph.AppendChild(run);
            cell.AppendChild(paragraph);

            return cell;
        }


        private string GetCleanFileName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "ismeretlen";

            char[] invalidChars = SystemIO.Path.GetInvalidFileNameChars();
            string clean = input;
            foreach (char c in invalidChars)
            {
                clean = clean.Replace(c.ToString(), "");
            }

            clean = clean.Replace(" ", "_");
            return clean;
        }
        // ===========================================
        // PDF GENERÁLÁS ÉS EMAIL KÜLDÉS
        // ===========================================

        private string ConvertWordToPdf()
        {
            try
            {
                // 1. Word fájl elérési útjának megkeresése
                string exeDirectory = SystemIO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                string contractsWordFolder = SystemIO.Path.Combine(exeDirectory, "files", "contracts-word");

                string customerName = GetCleanFileName(_selectedExistingCustomer?.Name ?? CustomerNameTextBox.Text);
                string rentalDate = DateTime.Now.ToString("yyyy-MM-dd");
                string wordFileName = $"szerződés_{customerName}_{rentalDate}.docx";
                string wordPath = SystemIO.Path.Combine(contractsWordFolder, wordFileName);

                if (!SystemIO.File.Exists(wordPath))
                {
                    MessageBox.Show("A Word szerződés fájl nem található! Először generálja le a szerződést.",
                                  "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return "";
                }

                // 2. PDF mappa létrehozása
                string contractsPdfFolder = SystemIO.Path.Combine(exeDirectory, "files", "contracts-pdf");
                SystemIO.Directory.CreateDirectory(contractsPdfFolder);

                // 3. PDF fájl neve
                string pdfFileName = $"szerződés_{customerName}_{rentalDate}.pdf";
                string pdfPath = SystemIO.Path.Combine(contractsPdfFolder, pdfFileName);

                // 4. Word -> PDF konverzió
                WordInterop.Application wordApp = new WordInterop.Application();
                WordInterop.Document doc = null;

                try
                {
                    wordApp.Visible = false;
                    doc = wordApp.Documents.Open(wordPath);
                    doc.SaveAs2(pdfPath, WordInterop.WdSaveFormat.wdFormatPDF);
                }
                finally
                {
                    doc?.Close();
                    wordApp?.Quit();
                }

                return pdfPath;
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
            // 1. Beállítások betöltése
            var setting = _context.Settings.FirstOrDefault();
            if (setting == null)
            {
                MessageBox.Show("Email beállítások nincsenek megadva!",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Email címzett meghatározása
            string recipientEmail = _selectedExistingCustomer?.Email ?? CustomerEmailTextBox.Text;
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                MessageBox.Show("Az ügyfél email címe nincs megadva!",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. Email üzenet készítése
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(setting.SenderName, setting.SenderEmail));
            message.To.Add(new MailboxAddress("", recipientEmail));

            // CC hozzáadása ha van
            if (!string.IsNullOrWhiteSpace(setting.CcAddress))
            {
                message.Cc.Add(new MailboxAddress("", setting.CcAddress));
            }

            message.Subject = setting.EmailSubject;

            // 4. Email tartalom betöltése
            string emailBody = GetEmailBody(setting);

            // 5. Mellékletek hozzáadása
            var bodyBuilder = new BodyBuilder { HtmlBody = emailBody };

            // PDF szerződés csatolása
            if (SystemIO.File.Exists(pdfPath))
            {
                bodyBuilder.Attachments.Add(pdfPath);
            }

            // ÁSZF csatolása ha van
            if (!string.IsNullOrWhiteSpace(setting.AszfFile) && SystemIO.File.Exists(setting.AszfFile))
            {
                bodyBuilder.Attachments.Add(setting.AszfFile);
            }

            message.Body = bodyBuilder.ToMessageBody();

            // 6. Email küldés
            using (var client = new SmtpClient())
            {
                client.Connect(setting.EmailSmtp, setting.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                client.Authenticate(setting.SenderEmail, setting.EmailPassword);
                client.Send(message);
                client.Disconnect(true);
            }
        }

        private string GetEmailBody(Setting setting)
        {
            try
            {
                // Email template betöltése ha van
                if (!string.IsNullOrWhiteSpace(setting.ContractEmailTemplate) && SystemIO.File.Exists(setting.ContractEmailTemplate))
                {
                    string template = SystemIO.File.ReadAllText(setting.ContractEmailTemplate);

                    // Változók helyettesítése a template-ben
                    template = template.Replace("{{CUSTOMER_NAME}}", _selectedExistingCustomer?.Name ?? CustomerNameTextBox.Text);
                    template = template.Replace("{{COMPANY_NAME}}", setting.CompanyName);
                    template = template.Replace("{{RENTAL_DATE}}", DateTime.Now.ToString("yyyy. MM. dd."));

                    return template;
                }
                else
                {
                    // Alapértelmezett email tartalom ha nincs template
                    string customerName = _selectedExistingCustomer?.Name ?? CustomerNameTextBox.Text;
                    return $@"
            <html>
            <body>
                <h2>Kedves {customerName}!</h2>
                <p>Köszönjük, hogy választotta a {setting.CompanyName} szolgáltatásait!</p>
                <p>Mellékletben megtalálja:</p>
                <ul>
                    <li>A bérlési szerződést PDF formátumban</li>
                    <li>Az Általános Szerződési Feltételeket</li>
                </ul>
                <p>Kérjük, olvassa át figyelmesen a dokumentumokat.</p>
                <p>Köszönjük a bizalmát!</p>
                <br>
                <p>Üdvözlettel,<br>{setting.CompanyName}</p>
            </body>
            </html>";
                }
            }
            catch (Exception)
            {
                // Ha bármi probléma van, egyszerű szöveges üzenet
                string customerName = _selectedExistingCustomer?.Name ?? CustomerNameTextBox.Text;
                return $"Kedves {customerName}!\n\nMellékletben megtalálja a bérlési szerződést és az ÁSZF-et.\n\nKöszönjük!\n{setting.CompanyName}";
            }
        }
            private void InvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            // Később implementáljuk
            MessageBox.Show("Számla generálás funkció - hamarosan implementálva!",
                          "Fejlesztés alatt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    }
