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
        private ToolRentalDbContext _context;
        private Setting? _currentSetting;

        public SettingsDialog()
        {
            InitializeComponent();
            InitializeDatabase();
            LoadSettings();
        }

        private void InitializeDatabase()
        {
            var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();
            optionsBuilder.UseSqlite("Data Source=ToolRental.db");
            _context = new ToolRentalDbContext(optionsBuilder.Options);
        }

        private void LoadSettings()
        {
            try
            {
                // Első Setting rekord betöltése (ha van)
                _currentSetting = _context.Settings.FirstOrDefault();

                if (_currentSetting != null)
                {
                    // Meglévő beállítások betöltése
                    CompanyNameTextBox.Text = _currentSetting.CompanyName;
                    CompanyLogoTextBox.Text = _currentSetting.CompanyLogo ?? "";

                    EmailSmtpTextBox.Text = _currentSetting.EmailSmtp;
                    SmtpPortTextBox.Text = _currentSetting.SmtpPort.ToString();
                    SenderEmailTextBox.Text = _currentSetting.SenderEmail;
                    EmailPasswordBox.Password = _currentSetting.EmailPassword;
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
            _currentSetting.EmailPassword = EmailPasswordBox.Password;
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