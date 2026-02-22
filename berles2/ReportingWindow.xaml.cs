using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;
using ToolRental.Core.Models;
using ToolRental.Data;

namespace berles2
{
    public partial class ReportingWindow : Window
    {
        private ToolRentalDbContext _context;
        private int? _selectedFinancialYear;
        private int? _selectedDeviceYear;
        public ReportingWindow()
        {
            InitializeComponent();
            InitializeDatabase();
            ChartCanvas.SizeChanged += ChartCanvas_SizeChanged;
            LoadYearFilters();
            LoadFinancialData();
        }

        private void InitializeDatabase()
        {
            var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();
            optionsBuilder.UseSqlServer(DatabaseConfig.ConnectionString);
            _context = new ToolRentalDbContext(optionsBuilder.Options);
        }

        private void LoadFinancialData()
        {
            try
            {
                var today = DateTime.Today;
                var currentMonth = new DateTime(today.Year, today.Month, 1);
                var currentYear = new DateTime(today.Year, 1, 1);

                // Mai bevételek/kiadások
                var todayRevenue = _context.Financials
                    .Where(f => f.Date.Date == today && f.EntryType == "bevétel")
                    .Sum(f => (decimal?)f.Amount) ?? 0;

                var todayExpense = _context.Financials
                    .Where(f => f.Date.Date == today && f.EntryType == "költség")
                    .Sum(f => (decimal?)f.Amount) ?? 0;

                // Havi bevételek/kiadások
                var monthRevenue = _context.Financials
                    .Where(f => f.Date >= currentMonth && f.EntryType == "bevétel")
                    .Sum(f => (decimal?)f.Amount) ?? 0;

                var monthExpense = _context.Financials
                    .Where(f => f.Date >= currentMonth && f.EntryType == "költség")
                    .Sum(f => (decimal?)f.Amount) ?? 0;

                // Éves bevételek/kiadások
                var yearRevenue = _context.Financials
                    .Where(f => f.Date >= currentYear && f.EntryType == "bevétel")
                    .Sum(f => (decimal?)f.Amount) ?? 0;

                var yearExpense = _context.Financials
                    .Where(f => f.Date >= currentYear && f.EntryType == "költség")
                    .Sum(f => (decimal?)f.Amount) ?? 0;

                // Szép sorok létrehozása
                CreateFinancialRow("🌅 Mai nap", todayRevenue, todayExpense, "#E8F5E8");
                CreateFinancialRow("📅 Aktuális hónap", monthRevenue, monthExpense, "#F0F8FF");
                CreateFinancialRow("🗓️ Aktuális év", yearRevenue, yearExpense, "#FFF8DC");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a pénzügyi adatok betöltésekor: {ex.Message}");
            }
            LoadDeviceData();
        }

        private void CreateFinancialRow(string period, decimal revenue, decimal expense, string backgroundColor)
        {
            var profit = revenue - expense;
            var profitColor = profit >= 0 ? "#4CAF50" : "#F44336";

            var border = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(backgroundColor)),
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new System.Windows.Thickness(1, 0, 1, 1),
                Padding = new System.Windows.Thickness(15, 10, 15, 10)
            };

            var grid = new System.Windows.Controls.Grid();
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(2, System.Windows.GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

            // Időszak
            var periodText = new System.Windows.Controls.TextBlock
            {
                Text = period,
                FontWeight = System.Windows.FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            System.Windows.Controls.Grid.SetColumn(periodText, 0);
            grid.Children.Add(periodText);

            // Bevétel
            var revenueText = new System.Windows.Controls.TextBlock
            {
                Text = $"{revenue:N0} Ft",
                FontWeight = System.Windows.FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.DarkGreen
            };
            System.Windows.Controls.Grid.SetColumn(revenueText, 1);
            grid.Children.Add(revenueText);

            // Kiadás
            var expenseText = new System.Windows.Controls.TextBlock
            {
                Text = $"{expense:N0} Ft",
                FontWeight = System.Windows.FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.DarkRed
            };
            System.Windows.Controls.Grid.SetColumn(expenseText, 2);
            grid.Children.Add(expenseText);

            // Nyereség
            var profitText = new System.Windows.Controls.TextBlock
            {
                Text = $"{profit:N0} Ft",
                FontWeight = System.Windows.FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(profitColor))
            };
            System.Windows.Controls.Grid.SetColumn(profitText, 3);
            grid.Children.Add(profitText);

            border.Child = grid;
            FinancialDataPanel.Children.Add(border);
        }

        protected override void OnClosed(EventArgs e)
        {
            _context?.Dispose();
            base.OnClosed(e);
        }
        private void LoadYearFilters()
        {
            // Pénzügyek évek
            var financialYears = _context.Financials
                .Select(f => f.Date.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            FinancialYearComboBox.SelectionChanged -= FinancialYearComboBox_SelectionChanged;
            FinancialYearComboBox.Items.Clear();
            FinancialYearComboBox.Items.Add("Összes");
            foreach (var year in financialYears)
                FinancialYearComboBox.Items.Add(year.ToString());

            var currentYearStr = DateTime.Now.Year.ToString();
            var matchF = FinancialYearComboBox.Items.Cast<object>().FirstOrDefault(i => i.ToString() == currentYearStr);
            FinancialYearComboBox.SelectedItem = matchF ?? FinancialYearComboBox.Items[0];
            _selectedFinancialYear = matchF != null ? DateTime.Now.Year : (int?)null;
            FinancialYearComboBox.SelectionChanged += FinancialYearComboBox_SelectionChanged;

            // Eszközök évek (bérlések alapján)
            var deviceYears = _context.Rentals
                .Select(r => r.RentStart.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            DeviceYearComboBox.SelectionChanged -= DeviceYearComboBox_SelectionChanged;
            DeviceYearComboBox.Items.Clear();
            DeviceYearComboBox.Items.Add("Összes");
            foreach (var year in deviceYears)
                DeviceYearComboBox.Items.Add(year.ToString());

            var matchD = DeviceYearComboBox.Items.Cast<object>().FirstOrDefault(i => i.ToString() == currentYearStr);
            DeviceYearComboBox.SelectedItem = matchD ?? DeviceYearComboBox.Items[0];
            _selectedDeviceYear = matchD != null ? DateTime.Now.Year : (int?)null;
            DeviceYearComboBox.SelectionChanged += DeviceYearComboBox_SelectionChanged;
        }
        private void LoadChart()
        {
            try
            {
                ChartCanvas.Children.Clear();
                var chartData = new List<MonthlyData>();

                if (_selectedFinancialYear.HasValue)
                {
                    // Havi nézet – kiválasztott év
                    for (int month = 1; month <= 12; month++)
                    {
                        var monthStart = new DateTime(_selectedFinancialYear.Value, month, 1);
                        var monthEnd = monthStart.AddMonths(1);

                        var revenue = _context.Financials
                            .Where(f => f.Date >= monthStart && f.Date < monthEnd && f.EntryType == "bevétel")
                            .Sum(f => (decimal?)f.Amount) ?? 0;

                        var expense = _context.Financials
                            .Where(f => f.Date >= monthStart && f.Date < monthEnd && f.EntryType == "költség")
                            .Sum(f => (decimal?)f.Amount) ?? 0;

                        chartData.Add(new MonthlyData
                        {
                            Month = month,
                            MonthName = GetHungarianMonthName(month),
                            Revenue = revenue,
                            Expense = expense,
                            Profit = revenue - expense
                        });
                    }

                    if (ChartTitleText != null)
                        ChartTitleText.Text = $"📊 Havi pénzügyi áttekintés - {_selectedFinancialYear.Value}";
                }
                else
                {
                    // Éves összesített nézet – összes év
                    var years = _context.Financials
                        .Select(f => f.Date.Year)
                        .Distinct()
                        .OrderBy(y => y)
                        .ToList();

                    for (int i = 0; i < years.Count; i++)
                    {
                        var year = years[i];
                        var yearStart = new DateTime(year, 1, 1);
                        var yearEnd = yearStart.AddYears(1);

                        var revenue = _context.Financials
                            .Where(f => f.Date >= yearStart && f.Date < yearEnd && f.EntryType == "bevétel")
                            .Sum(f => (decimal?)f.Amount) ?? 0;

                        var expense = _context.Financials
                            .Where(f => f.Date >= yearStart && f.Date < yearEnd && f.EntryType == "költség")
                            .Sum(f => (decimal?)f.Amount) ?? 0;

                        chartData.Add(new MonthlyData
                        {
                            Month = i + 1,
                            MonthName = year.ToString(),
                            Revenue = revenue,
                            Expense = expense,
                            Profit = revenue - expense
                        });
                    }

                    if (ChartTitleText != null)
                        ChartTitleText.Text = "📊 Éves pénzügyi áttekintés - Összes";
                }

                DrawChart(chartData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a grafikon betöltésekor: {ex.Message}");
            }
        }

        private void DrawChart(List<MonthlyData> data)
        {
            if (ChartCanvas.ActualWidth <= 0) return;

            var maxValue = data.SelectMany(d => new[] { d.Revenue, d.Expense, Math.Abs(d.Profit) }).Max();
            if (maxValue == 0) maxValue = 1000; // Minimum skála

            var chartWidth = ChartCanvas.ActualWidth - 60;
            var chartHeight = ChartCanvas.ActualHeight - 40;
            var count = Math.Max(data.Count, 1);
            var barWidth = chartWidth / count / 4;
            var zeroLine = chartHeight / 2; // Középső vonal (0 szint)

            // 0-vonal rajzolása
            var zeroLineShape = new System.Windows.Shapes.Line
            {
                X1 = 20,
                X2 = chartWidth + 40,
                Y1 = zeroLine,
                Y2 = zeroLine,
                Stroke = System.Windows.Media.Brushes.Black,
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(zeroLineShape);

            for (int i = 0; i < data.Count; i++)
            {
                var monthData = data[i];
                var x = 30 + (i * chartWidth / count);

                // Hónap címkéje a 0-vonalon
                var monthLabel = new System.Windows.Controls.TextBlock
                {
                    Text = monthData.MonthName,
                    FontSize = 10,
                    Width = 60
                };
                System.Windows.Controls.Canvas.SetLeft(monthLabel, x - 10);
                System.Windows.Controls.Canvas.SetTop(monthLabel, zeroLine + 5);
                ChartCanvas.Children.Add(monthLabel);

                // Bevétel oszlop (zöld, felfelé)
                if (monthData.Revenue > 0)
                {
                    var revenueHeight = (double)(monthData.Revenue / maxValue) * (chartHeight / 2) * 0.8;
                    var revenueBar = new System.Windows.Shapes.Rectangle
                    {
                        Width = barWidth,
                        Height = revenueHeight,
                        Fill = System.Windows.Media.Brushes.Green
                    };
                    System.Windows.Controls.Canvas.SetLeft(revenueBar, x);
                    System.Windows.Controls.Canvas.SetTop(revenueBar, zeroLine - revenueHeight);
                    ChartCanvas.Children.Add(revenueBar);

                    // Bevétel érték kiírása
                    var revenueText = new System.Windows.Controls.TextBlock
                    {
                        Text = $"{monthData.Revenue:N0}",
                        FontSize = 8,
                        Width = barWidth + 10,
                        TextAlignment = System.Windows.TextAlignment.Center
                    };
                    System.Windows.Controls.Canvas.SetLeft(revenueText, x - 5);
                    System.Windows.Controls.Canvas.SetTop(revenueText, zeroLine - revenueHeight - 15);
                    ChartCanvas.Children.Add(revenueText);
                }

                // Kiadás oszlop (piros, lefelé)
                if (monthData.Expense > 0)
                {
                    var expenseHeight = (double)(monthData.Expense / maxValue) * (chartHeight / 2) * 0.8;
                    var expenseBar = new System.Windows.Shapes.Rectangle
                    {
                        Width = barWidth,
                        Height = expenseHeight,
                        Fill = System.Windows.Media.Brushes.Red
                    };
                    System.Windows.Controls.Canvas.SetLeft(expenseBar, x + barWidth + 2);
                    System.Windows.Controls.Canvas.SetTop(expenseBar, zeroLine);
                    ChartCanvas.Children.Add(expenseBar);

                    // Kiadás érték kiírása
                    var expenseText = new System.Windows.Controls.TextBlock
                    {
                        Text = $"{monthData.Expense:N0}",
                        FontSize = 8,
                        Width = barWidth + 10,
                        TextAlignment = System.Windows.TextAlignment.Center
                    };
                    System.Windows.Controls.Canvas.SetLeft(expenseText, x + barWidth - 3);
                    System.Windows.Controls.Canvas.SetTop(expenseText, zeroLine + expenseHeight + 5);
                    ChartCanvas.Children.Add(expenseText);
                }

                // Nyereség oszlop (kék felfelé ha pozitív, narancs lefelé ha negatív)
                if (monthData.Profit != 0)
                {
                    var profitHeight = (double)(Math.Abs(monthData.Profit) / maxValue) * (chartHeight / 2) * 0.8;
                    var profitBar = new System.Windows.Shapes.Rectangle
                    {
                        Width = barWidth,
                        Height = profitHeight,
                        Fill = monthData.Profit >= 0 ? System.Windows.Media.Brushes.Blue : System.Windows.Media.Brushes.Orange
                    };
                    System.Windows.Controls.Canvas.SetLeft(profitBar, x + (barWidth + 2) * 2);

                    if (monthData.Profit >= 0)
                        System.Windows.Controls.Canvas.SetTop(profitBar, zeroLine - profitHeight);
                    else
                        System.Windows.Controls.Canvas.SetTop(profitBar, zeroLine);

                    ChartCanvas.Children.Add(profitBar);

                    // Nyereség érték kiírása
                    var profitText = new System.Windows.Controls.TextBlock
                    {
                        Text = $"{monthData.Profit:N0}",
                        FontSize = 8,
                        Width = barWidth + 10,
                        TextAlignment = System.Windows.TextAlignment.Center
                    };
                    System.Windows.Controls.Canvas.SetLeft(profitText, x + (barWidth + 2) * 2 - 5);

                    if (monthData.Profit >= 0)
                        System.Windows.Controls.Canvas.SetTop(profitText, zeroLine - profitHeight - 15);
                    else
                        System.Windows.Controls.Canvas.SetTop(profitText, zeroLine + profitHeight + 5);

                    ChartCanvas.Children.Add(profitText);
                }
            }
        }

        private string GetHungarianMonthName(int month)
        {
            string[] months = { "", "Jan", "Feb", "Már", "Ápr", "Máj", "Jún", "Júl", "Aug", "Sze", "Okt", "Nov", "Dec" };
            return months[month];
        }

        private class MonthlyData
        {
            public int Month { get; set; }
            public string MonthName { get; set; }
            public decimal Revenue { get; set; }
            public decimal Expense { get; set; }
            public decimal Profit { get; set; }
        }
        private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            LoadChart();
        }
        private void FinancialYearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FinancialYearComboBox.SelectedItem == null) return;
            var selected = FinancialYearComboBox.SelectedItem.ToString();
            _selectedFinancialYear = selected == "Összes" ? (int?)null : int.Parse(selected);
            LoadChart();
        }

        private void DeviceYearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DeviceYearComboBox.SelectedItem == null) return;
            var selected = DeviceYearComboBox.SelectedItem.ToString();
            _selectedDeviceYear = selected == "Összes" ? (int?)null : int.Parse(selected);
            LoadDeviceData();
        }
        private void LoadDeviceData()
        {
            try
            {
                var devices = _context.Devices
                    .Include(d => d.DeviceTypeNavigation)
                    .Where(d => d.Available)
                    .ToList();

                var deviceStats = new List<DeviceStats>();

                foreach (var device in devices)
                {
                    // Bérlések száma
                    var rentalCountQuery = _context.RentalDevices
    .Where(rd => rd.DeviceId == device.Id);

                    if (_selectedDeviceYear.HasValue)
                        rentalCountQuery = rentalCountQuery.Where(rd => rd.Rental.RentStart.Year == _selectedDeviceYear.Value);

                    var rentalCount = rentalCountQuery.Count();

                    // ÁTMENETI EGYSZERŰ MEGOLDÁS - bérlési bevételek helyes számítása
                    var revenue = 0m;

                    // Bérlések száma és bevétel számítása
                    IQueryable<RentalDevice> deviceRentalsQuery = _context.RentalDevices
    .Where(rd => rd.DeviceId == device.Id)
    .Include(rd => rd.Rental);

                    if (_selectedDeviceYear.HasValue)
                        deviceRentalsQuery = deviceRentalsQuery.Where(rd => rd.Rental.RentStart.Year == _selectedDeviceYear.Value);

                    var deviceRentals = deviceRentalsQuery.ToList();

                    foreach (var rentalDevice in deviceRentals)
                    {
                        var rental = rentalDevice.Rental;

                        // Megkeressük a Financial rekordot ehhez a bérléshez
                        var financial = _context.Financials
                            .FirstOrDefault(f => f.SourceType == "bérlés" && f.SourceId == rental.Id);

                        if (financial != null)
                        {
                            // Eredeti napi összeg kiszámítása (összes eszköz ára ebben a bérlésben)
                            var originalDailyTotal = _context.RentalDevices
                                .Where(rd => rd.RentalId == rental.Id)
                                .Include(rd => rd.Device)
                                .Sum(rd => rd.Device.RentPrice);

                            if (originalDailyTotal > 0)
                            {
                                // Tényleges napi összeg (kedvezménnyel)
                                var actualDailyTotal = financial.Amount / rental.RentalDays;

                                // Arányos elosztás: ez az eszköz milyen arányban részesedik
                                var deviceRatio = device.RentPrice / originalDailyTotal;

                                // Eszköz tényleges bevétele (kedvezménnyel)
                                var deviceRevenue = actualDailyTotal * deviceRatio * rental.RentalDays;

                                revenue += deviceRevenue;
                            }
                        }
                        else
                        {
                            // Ha nincs Financial rekord, akkor az eredeti logika (fallback)
                            revenue += device.RentPrice * rental.RentalDays;
                        }
                    }

                    // Egyéb bevételek (elosztva)
                    var otherRevenue = _context.FinancialDevices
                        .Where(fd => fd.DeviceId == device.Id)
                        .Join(_context.Financials,
                              fd => fd.FinancialId,
                              f => f.Id,
                              (fd, f) => new { fd, f })
                        .Where(x => x.f.EntryType == "bevétel" && x.f.SourceType != "bérlés")
                        .ToList()
                        .Sum(x =>
                        {
                            var deviceCount = _context.FinancialDevices
                                .Count(fd => fd.FinancialId == x.f.Id);
                            return deviceCount > 0 ? x.f.Amount / deviceCount : 0;
                        });

                    revenue += otherRevenue;

                    // JAVÍTOTT költségszámítás - elosztja az összeget
                    var expense = _context.FinancialDevices
    .Where(fd => fd.DeviceId == device.Id)
    .Join(_context.Financials,
          fd => fd.FinancialId,
          f => f.Id,
          (fd, f) => new { fd, f })
    .Where(x => x.f.EntryType == "költség" &&
                (!_selectedDeviceYear.HasValue || x.f.Date.Year == _selectedDeviceYear.Value))
    .ToList()
    .Sum(x =>
    {
        var deviceCount = _context.FinancialDevices
            .Count(fd => fd.FinancialId == x.f.Id);
        return deviceCount > 0 ? x.f.Amount / deviceCount : 0;
    });

                    deviceStats.Add(new DeviceStats
                    {
                        DeviceName = device.DeviceName,
                        RentalCount = rentalCount,
                        Revenue = revenue,
                        Expense = expense,
                        Profit = revenue - expense
                    });
                }

                // Rendezés bérlések száma szerint csökkenő sorrendben
                deviceStats = deviceStats.OrderByDescending(d => d.RentalCount).ToList();

                // Sorok létrehozása
                DeviceDataPanel.Children.Clear();
                for (int i = 0; i < deviceStats.Count; i++)
                {
                    var deviceStat = deviceStats[i];
                    var backgroundColor = i % 2 == 0 ? "#F8F9FA" : "#FFFFFF"; // Váltakozó színek
                    CreateDeviceRow(deviceStat, backgroundColor);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba az eszköz adatok betöltésekor: {ex.Message}");
            }
        }

        private void CreateDeviceRow(DeviceStats deviceStat, string backgroundColor)
        {
            var profitColor = deviceStat.Profit >= 0 ? "#4CAF50" : "#F44336";

            var border = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(backgroundColor)),
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new System.Windows.Thickness(1, 0, 1, 1),
                Padding = new System.Windows.Thickness(15, 10, 15, 10)
            };

            var grid = new System.Windows.Controls.Grid();
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(3, System.Windows.GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

            // Eszköz neve
            var deviceNameText = new System.Windows.Controls.TextBlock
            {
                Text = deviceStat.DeviceName,
                FontWeight = System.Windows.FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            System.Windows.Controls.Grid.SetColumn(deviceNameText, 0);
            grid.Children.Add(deviceNameText);

            // Bérlések száma
            var rentalCountText = new System.Windows.Controls.TextBlock
            {
                Text = $"{deviceStat.RentalCount} db",
                FontWeight = System.Windows.FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.DarkBlue
            };
            System.Windows.Controls.Grid.SetColumn(rentalCountText, 1);
            grid.Children.Add(rentalCountText);

            // Bevétel
            var revenueText = new System.Windows.Controls.TextBlock
            {
                Text = $"{deviceStat.Revenue:N0} Ft",
                FontWeight = System.Windows.FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.DarkGreen
            };
            System.Windows.Controls.Grid.SetColumn(revenueText, 2);
            grid.Children.Add(revenueText);

            // Költség
            var expenseText = new System.Windows.Controls.TextBlock
            {
                Text = $"{deviceStat.Expense:N0} Ft",
                FontWeight = System.Windows.FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.DarkRed
            };
            System.Windows.Controls.Grid.SetColumn(expenseText, 3);
            grid.Children.Add(expenseText);

            // Nyereség
            var profitText = new System.Windows.Controls.TextBlock
            {
                Text = $"{deviceStat.Profit:N0} Ft",
                FontWeight = System.Windows.FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(profitColor))
            };
            System.Windows.Controls.Grid.SetColumn(profitText, 4);
            grid.Children.Add(profitText);

            border.Child = grid;
            DeviceDataPanel.Children.Add(border);
        }

        private class DeviceStats
        {
            public string DeviceName { get; set; }
            public int RentalCount { get; set; }
            public decimal Revenue { get; set; }
            public decimal Expense { get; set; }
            public decimal Profit { get; set; }
        }
    }
}