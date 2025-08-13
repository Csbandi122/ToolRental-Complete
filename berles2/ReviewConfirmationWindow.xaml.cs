using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ToolRental.Core.Models;

namespace berles2
{
    public partial class ReviewConfirmationWindow : Window
    {
        private List<Rental> _rentals;

        public ReviewConfirmationWindow(List<Rental> rentals)
        {
            InitializeComponent();
            _rentals = rentals;
            LoadData();
        }

        private void LoadData()
        {
            // RentEnd számítása a megjelenítéshez
            var displayRentals = _rentals.Select(r => new
            {
                r.TicketNr,
                r.Customer,
                r.RentStart,
                RentEnd = r.RentStart.AddDays(r.RentalDays - 1)
            }).ToList();

            RentalsDataGrid.ItemsSource = displayRentals;
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}