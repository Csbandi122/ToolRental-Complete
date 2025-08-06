using System.Windows;
using ToolRental.Core.Models;

namespace berles2
{
    public partial class CustomerDialog : Window
    {
        public Customer Customer { get; private set; }
        private bool _isEditMode;

        // Konstruktor új ügyfél hozzáadásához
        public CustomerDialog()
        {
            InitializeComponent();
            _isEditMode = false;
            Customer = new Customer();
            TitleTextBlock.Text = "Új ügyfél hozzáadása";
        }

        // Konstruktor meglévő ügyfél szerkesztéséhez
        public CustomerDialog(Customer customer)
        {
            InitializeComponent();
            _isEditMode = true;
            Customer = new Customer
            {
                Id = customer.Id,
                Name = customer.Name,
                Zipcode = customer.Zipcode,
                City = customer.City,
                Address = customer.Address,
                Email = customer.Email,
                IdNumber = customer.IdNumber,
                Comment = customer.Comment
            };

            TitleTextBlock.Text = "Ügyfél szerkesztése";
            LoadCustomerData();
        }

        private void LoadCustomerData()
        {
            NameTextBox.Text = Customer.Name;
            ZipcodeTextBox.Text = Customer.Zipcode;
            CityTextBox.Text = Customer.City;
            AddressTextBox.Text = Customer.Address;
            EmailTextBox.Text = Customer.Email;
            IdNumberTextBox.Text = Customer.IdNumber;
            CommentTextBox.Text = Customer.Comment;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateForm())
            {
                Customer.Name = NameTextBox.Text.Trim();
                Customer.Zipcode = ZipcodeTextBox.Text.Trim();
                Customer.City = CityTextBox.Text.Trim();
                Customer.Address = AddressTextBox.Text.Trim();
                Customer.Email = EmailTextBox.Text.Trim();
                Customer.IdNumber = IdNumberTextBox.Text.Trim();
                Customer.Comment = CommentTextBox.Text.Trim();

                DialogResult = true;
                Close();
            }
        }

        private bool ValidateForm()
        {
            // Név ellenőrzése
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("A név megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return false;
            }

            // E-mail ellenőrzése
            if (string.IsNullOrWhiteSpace(EmailTextBox.Text))
            {
                MessageBox.Show("Az e-mail cím megadása kötelező!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                EmailTextBox.Focus();
                return false;
            }

            // Egyszerű e-mail validáció
            string email = EmailTextBox.Text.Trim();
            if (!email.Contains("@") || !email.Contains("."))
            {
                MessageBox.Show("Kérem adjon meg egy érvényes e-mail címet!", "Hiba",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                EmailTextBox.Focus();
                return false;
            }

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}