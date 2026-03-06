using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using ToolRental.Core.Models;
using ToolRental.Data;

namespace berles2
{
    public partial class SettingsDialog : Window
    {
        private ToolRentalDbContext? _context;
        private Setting? _currentSetting;

        public SettingsDialog()
        {
            InitializeComponent();
            InitializeDatabase();
            LoadSettings();
        }

        private void InitializeDatabase()
        {
            // Csak akkor nyitjuk meg az adatbázist, ha már van beállítva connection string
            if (DatabaseConfig.IsConfigured)
            {
                var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();
                optionsBuilder.UseSqlServer(DatabaseConfig.ConnectionString);
                _context = new ToolRentalDbContext(optionsBuilder.Options);
            }
        }

        private void LoadSettings()
        {
            try
            {
                // SQL szerver beállítások betöltése az appsettings.json-ból
                SqlServerTextBox.Text = DatabaseConfig.Server;
                SqlPortTextBox.Text = DatabaseConfig.Port.ToString();
                SqlDatabaseTextBox.Text = DatabaseConfig.Database;
                SqlUserIdTextBox.Text = DatabaseConfig.UserId;
                SqlPasswordBox.Password = DatabaseConfig.Password;
                SqlTrustCertCheckBox.IsChecked = DatabaseConfig.TrustServerCertificate;

                // Ha nincs még SQL kapcsolat konfigurálva, figyelmeztető szín
                if (!DatabaseConfig.IsConfigured)
                {
                    SqlServerGroupBox.BorderBrush = System.Windows.Media.Brushes.OrangeRed;
                    SqlTestResultText.Text = "⚠️ Az SQL szerver még nincs beállítva!";
                    SqlTestResultText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                }

                // Első Setting rekord betöltése (ha van)
                _currentSetting = _context?.Settings.FirstOrDefault();

                if (_currentSetting != null)
                {
                    // Meglévő beállítások betöltése
                    CompanyNameTextBox.Text = _currentSetting.CompanyName;
                    CompanyLogoTextBox.Text = _currentSetting.CompanyLogo ?? "";

                    EmailSmtpTextBox.Text = _currentSetting.EmailSmtp;
                    SmtpPortTextBox.Text = _currentSetting.SmtpPort.ToString();
                    SenderEmailTextBox.Text = _currentSetting.SenderEmail;
                    EmailPasswordBox.Password = CredentialProtection.Unprotect(_currentSetting.EmailPassword);
                    SenderNameTextBox.Text = _currentSetting.SenderName;
                    CcAddressTextBox.Text = _currentSetting.CcAddress ?? "";

                    TemplateContractTextBox.Text = _currentSetting.TemplateContract ?? "";
                    AszfFileTextBox.Text = _currentSetting.AszfFile ?? "";

                    EmailSubjectTextBox.Text = _currentSetting.EmailSubject;
                    ContractEmailTemplateTextBox.Text = _currentSetting.ContractEmailTemplate ?? "";
                    ReviewEmailSubjectTextBox.Text = _currentSetting.ReviewEmailSubject;
                    ReviewEmailTemplateTextBox.Text = _currentSetting.ReviewEmailTemplate ?? "";

                    GoogleReviewTextBox.Text = _currentSetting.GoogleReview ?? "";
                    InvoiceXmlTextBox.Text = _currentSetting.InvoiceXml ?? "";
                    DefaultRentalDaysTextBox.Text = _currentSetting.DefaultRentalDays.ToString();
                }
                else
                {
                    // Alapértelmezett értékek új beállításokhoz
                    CompanyNameTextBox.Text = "Kerékpár Bérlő Kft.";
                    SmtpPortTextBox.Text = "587";
                    DefaultRentalDaysTextBox.Text = "1";
                    EmailSubjectTextBox.Text = "Bérlési szerződés";
                    ReviewEmailSubjectTextBox.Text = "Értékelje szolgáltatásunkat!";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a beállítások betöltésekor: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region SQL kapcsolat teszt

        private void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            string server = SqlServerTextBox.Text.Trim();
            string portText = SqlPortTextBox.Text.Trim();
            string database = SqlDatabaseTextBox.Text.Trim();
            string userId = SqlUserIdTextBox.Text.Trim();
            string password = SqlPasswordBox.Password;
            bool trustCert = SqlTrustCertCheckBox.IsChecked == true;

            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database)
                || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
            {
                SqlTestResultText.Text = "❌ Töltsd ki az összes SQL szerver mezőt!";
                SqlTestResultText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            if (!int.TryParse(portText, out int port) || port <= 0)
            {
                SqlTestResultText.Text = "❌ Érvénytelen port szám!";
                SqlTestResultText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            SqlTestResultText.Text = "⏳ Kapcsolódás...";
            SqlTestResultText.Foreground = System.Windows.Media.Brushes.Gray;
            TestConnectionButton.IsEnabled = false;

            try
            {
                string testConnStr = $"Server={server},{port};Database={database};User Id={userId};Password={password};TrustServerCertificate={trustCert.ToString().ToLower()};Connect Timeout=5;";
                using var conn = new SqlConnection(testConnStr);
                conn.Open();

                SqlTestResultText.Text = "✅ Kapcsolat sikeres!";
                SqlTestResultText.Foreground = System.Windows.Media.Brushes.Green;
                SqlServerGroupBox.BorderBrush = System.Windows.Media.Brushes.Green;
            }
            catch (Exception ex)
            {
                SqlTestResultText.Text = $"❌ Kapcsolat sikertelen: {ex.Message}";
                SqlTestResultText.Foreground = System.Windows.Media.Brushes.Red;
                SqlServerGroupBox.BorderBrush = System.Windows.Media.Brushes.Red;
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
            }
        }

        #endregion

        #region Fájl tallózó gombok

        private void BrowseLogoButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Kép fájlok (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Minden fájl (*.*)|*.*",
                Title = "Válasszon cég logo képet"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                CompanyLogoTextBox.Text = openFileDialog.FileName;
            }
        }

        private void BrowseContractButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Word dokumentum (*.docx)|*.docx|Minden fájl (*.*)|*.*",
                Title = "Válasszon szerződés sablont"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TemplateContractTextBox.Text = openFileDialog.FileName;
            }
        }

        private void BrowseAszfButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PDF fájl (*.pdf)|*.pdf|Minden fájl (*.*)|*.*",
                Title = "Válasszon ÁSZF PDF fájlt"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                AszfFileTextBox.Text = openFileDialog.FileName;
            }
        }

        private void BrowseContractEmailButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "HTML fájl (*.html;*.htm)|*.html;*.htm|Minden fájl (*.*)|*.*",
                Title = "Válasszon szerződés email template-et"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ContractEmailTemplateTextBox.Text = openFileDialog.FileName;
            }
        }

        private void BrowseReviewEmailButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "HTML fájl (*.html;*.htm)|*.html;*.htm|Minden fájl (*.*)|*.*",
                Title = "Válasszon értékelés email template-et"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ReviewEmailTemplateTextBox.Text = openFileDialog.FileName;
            }
        }

        private void BrowseInvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "XML fájl (*.xml)|*.xml|Minden fájl (*.*)|*.*",
                Title = "Válasszon számla XML sablont"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                InvoiceXmlTextBox.Text = openFileDialog.FileName;
            }
        }

        #endregion

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateForm())
            {
                try
                {
                    SaveSettings();
                    MessageBox.Show("Beállítások sikeresen mentve!",
                                  "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true;
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hiba a beállítások mentésekor: {ex.Message}",
                                  "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool ValidateForm()
        {
            // SQL szerver validáció
            if (string.IsNullOrWhiteSpace(SqlServerTextBox.Text))
            {
                MessageBox.Show("Az SQL szerver neve/IP megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                SqlServerTextBox.Focus();
                return false;
            }

            if (!int.TryParse(SqlPortTextBox.Text, out int sqlPort) || sqlPort <= 0)
            {
                MessageBox.Show("Érvényes SQL szerver port megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                SqlPortTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(SqlDatabaseTextBox.Text))
            {
                MessageBox.Show("Az adatbázis neve megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                SqlDatabaseTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(SqlUserIdTextBox.Text))
            {
                MessageBox.Show("Az SQL felhasználónév megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                SqlUserIdTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(SqlPasswordBox.Password))
            {
                MessageBox.Show("Az SQL jelszó megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                SqlPasswordBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(CompanyNameTextBox.Text))
            {
                MessageBox.Show("A cégnév megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                CompanyNameTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(EmailSmtpTextBox.Text))
            {
                MessageBox.Show("Az SMTP szerver megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                EmailSmtpTextBox.Focus();
                return false;
            }

            if (!int.TryParse(SmtpPortTextBox.Text, out int smtpPort) || smtpPort <= 0)
            {
                MessageBox.Show("Érvényes SMTP port megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                SmtpPortTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(SenderEmailTextBox.Text))
            {
                MessageBox.Show("A küldő email cím megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                SenderEmailTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(EmailPasswordBox.Password))
            {
                MessageBox.Show("Az email jelszó megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                EmailPasswordBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(SenderNameTextBox.Text))
            {
                MessageBox.Show("A küldő név megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                SenderNameTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(EmailSubjectTextBox.Text))
            {
                MessageBox.Show("A szerződés email tárgy megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                EmailSubjectTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(ReviewEmailSubjectTextBox.Text))
            {
                MessageBox.Show("Az értékelés email tárgy megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                ReviewEmailSubjectTextBox.Focus();
                return false;
            }

            if (!int.TryParse(DefaultRentalDaysTextBox.Text, out int rentalDays) || rentalDays <= 0)
            {
                MessageBox.Show("Érvényes alapértelmezett bérlési idő megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                DefaultRentalDaysTextBox.Focus();
                return false;
            }

            // Fájlok létezésének ellenőrzése (ha meg vannak adva)
            if (!ValidateFilePath(CompanyLogoTextBox.Text, "Cég logo"))
                return false;
            if (!ValidateFilePath(TemplateContractTextBox.Text, "Szerződés sablon"))
                return false;
            if (!ValidateFilePath(AszfFileTextBox.Text, "ÁSZF fájl"))
                return false;
            if (!ValidateFilePath(ContractEmailTemplateTextBox.Text, "Szerződés email template"))
                return false;
            if (!ValidateFilePath(ReviewEmailTemplateTextBox.Text, "Értékelés email template"))
                return false;
            if (!ValidateFilePath(InvoiceXmlTextBox.Text, "Számla XML sablon"))
                return false;

            return true;
        }

        private bool ValidateFilePath(string filePath, string fileName)
        {
            if (!string.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath))
            {
                MessageBox.Show($"A megadott {fileName} fájl nem található: {filePath}",
                              "Fájl hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void SaveSettings()
        {
            // SQL szerver beállítások mentése appsettings.json-ba
            int.TryParse(SqlPortTextBox.Text, out int sqlPort);
            DatabaseConfig.Save(
                server: SqlServerTextBox.Text.Trim(),
                port: sqlPort > 0 ? sqlPort : 1433,
                database: SqlDatabaseTextBox.Text.Trim(),
                userId: SqlUserIdTextBox.Text.Trim(),
                password: SqlPasswordBox.Password,
                trustServerCertificate: SqlTrustCertCheckBox.IsChecked == true
            );

            // Ha még nincs DB context (első indítás), most inicializáljuk
            if (_context == null)
            {
                var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();
                optionsBuilder.UseSqlServer(DatabaseConfig.ConnectionString);
                _context = new ToolRentalDbContext(optionsBuilder.Options);
                _context.Database.EnsureCreated();
            }

            if (_currentSetting == null)
            {
                // Új Setting létrehozása
                _currentSetting = new Setting();
                _context.Settings.Add(_currentSetting);
            }

            // Adatok frissítése
            _currentSetting.CompanyName = CompanyNameTextBox.Text.Trim();
            _currentSetting.CompanyLogo = string.IsNullOrWhiteSpace(CompanyLogoTextBox.Text) ?
                                         null : CompanyLogoTextBox.Text.Trim();

            _currentSetting.EmailSmtp = EmailSmtpTextBox.Text.Trim();
            _currentSetting.SmtpPort = int.Parse(SmtpPortTextBox.Text);
            _currentSetting.SenderEmail = SenderEmailTextBox.Text.Trim();
            _currentSetting.EmailPassword = CredentialProtection.Protect(EmailPasswordBox.Password);
            _currentSetting.SenderName = SenderNameTextBox.Text.Trim();
            _currentSetting.CcAddress = string.IsNullOrWhiteSpace(CcAddressTextBox.Text) ?
                                       null : CcAddressTextBox.Text.Trim();

            _currentSetting.TemplateContract = string.IsNullOrWhiteSpace(TemplateContractTextBox.Text) ?
                                              null : TemplateContractTextBox.Text.Trim();
            _currentSetting.AszfFile = string.IsNullOrWhiteSpace(AszfFileTextBox.Text) ?
                                      null : AszfFileTextBox.Text.Trim();

            _currentSetting.EmailSubject = EmailSubjectTextBox.Text.Trim();
            _currentSetting.ContractEmailTemplate = string.IsNullOrWhiteSpace(ContractEmailTemplateTextBox.Text) ?
                                                    null : ContractEmailTemplateTextBox.Text.Trim();
            _currentSetting.ReviewEmailSubject = ReviewEmailSubjectTextBox.Text.Trim();
            _currentSetting.ReviewEmailTemplate = string.IsNullOrWhiteSpace(ReviewEmailTemplateTextBox.Text) ?
                                                  null : ReviewEmailTemplateTextBox.Text.Trim();

            _currentSetting.GoogleReview = string.IsNullOrWhiteSpace(GoogleReviewTextBox.Text) ?
                                          null : GoogleReviewTextBox.Text.Trim();
            _currentSetting.InvoiceXml = string.IsNullOrWhiteSpace(InvoiceXmlTextBox.Text) ?
                                        null : InvoiceXmlTextBox.Text.Trim();
            _currentSetting.DefaultRentalDays = int.Parse(DefaultRentalDaysTextBox.Text);

            _context.SaveChanges();
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
    }
}