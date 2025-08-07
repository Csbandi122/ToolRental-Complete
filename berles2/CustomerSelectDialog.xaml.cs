using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using ToolRental.Data;
using ToolRental.Core.Models;

namespace berles2
{
    public partial class CustomerSelectDialog : Window
    {
        private ToolRentalDbContext _context;
        private ObservableCollection<Customer> _customers;
        private List<Customer> _allCustomers;

        public Customer? SelectedCustomer { get; private set; }

        public CustomerSelectDialog()
        {
            InitializeComponent();
            InitializeDatabase();
            LoadCustomers();
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
                _allCustomers = _context.Customers
                    .OrderBy(c => c.Name)
                    .ToList();

                _customers = new ObservableCollection<Customer>(_allCustomers);
                CustomersDataGrid.ItemsSource = _customers;

                // Ha nincs ügyfél
                if (_allCustomers.Count == 0)
                {
                    MessageBox.Show("Még nincsenek ügyfelek az adatbázisban!\n\nElőször hozz létre új ügyfeleket.",
                                  "Nincs ügyfél", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = false;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba az ügyfelek betöltésekor: {ex.Message}",
                              "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                this.DialogResult = false;
                this.Close();
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.ToLower();

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
                // Szűrés név, e-mail vagy város alapján
                var filteredCustomers = _allCustomers.Where(c =>
                    c.Name.ToLower().Contains(searchText) ||
                    c.Email.ToLower().Contains(searchText) ||
                    c.City.ToLower().Contains(searchText) ||
                    c.Address.ToLower().Contains(searchText)
                ).ToList();

                _customers.Clear();
                foreach (var customer in filteredCustomers)
                {
                    _customers.Add(customer);
                }
            }
        }

        private void CustomersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Kiválasztás gomb engedélyezése ha van kijelölt sor
            SelectButton.IsEnabled = CustomersDataGrid.SelectedItem != null;
        }

        private void CustomersDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Dupla kattintás = kiválasztás
            if (CustomersDataGrid.SelectedItem is Customer selectedCustomer)
            {
                SelectedCustomer = selectedCustomer;
                this.DialogResult = true;
                this.Close();
            }
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (CustomersDataGrid.SelectedItem is Customer selectedCustomer)
            {
                SelectedCustomer = selectedCustomer;
                this.DialogResult = true;
                this.Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedCustomer = null;
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