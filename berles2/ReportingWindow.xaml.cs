using Microsoft.EntityFrameworkCore;
using System.Windows;
using ToolRental.Data;

namespace berles2
{
    public partial class ReportingWindow : Window
    {
        private ToolRentalDbContext _context;

        public ReportingWindow()
        {
            InitializeComponent();
            InitializeDatabase();
            LoadFinancialData();
            
            ChartCanvas.SizeChanged += ChartCanvas_SizeChanged;
        }

        private void InitializeDatabase()
        {
            var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();
            optionsBuilder.UseSqlite("Data Source=ToolRental.db");
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
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(200) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(150) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(150) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(150) });

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
        private void LoadChart()
        {
            try
            {
                ChartCanvas.Children.Clear();

                var currentYear = DateTime.Now.Year;
                var monthlyData = new List<MonthlyData>();

                // 12 hónap adatainak lekérdezése
                for (int month = 1; month <= 12; month++)
                {
                    var monthStart = new DateTime(currentYear, month, 1);
                    var monthEnd = monthStart.AddMonths(1);

                    var revenue = _context.Financials
                        .Where(f => f.Date >= monthStart && f.Date < monthEnd && f.EntryType == "bevétel")
                        .Sum(f => (decimal?)f.Amount) ?? 0;

                    var expense = _context.Financials
                        .Where(f => f.Date >= monthStart && f.Date < monthEnd && f.EntryType == "költség")
                        .Sum(f => (decimal?)f.Amount) ?? 0;

                    monthlyData.Add(new MonthlyData
                    {
                        Month = month,
                        MonthName = GetHungarianMonthName(month),
                        Revenue = revenue,
                        Expense = expense,
                        Profit = revenue - expense
                    });
                }

                DrawChart(monthlyData);
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
            var barWidth = chartWidth / 12 / 4; // 12 hónap, 4 hely oszloponként
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
                var x = 30 + (i * chartWidth / 12);

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
                    var rentalCount = _context.RentalDevices
                        .Where(rd => rd.DeviceId == device.Id)
                        .Count();

                    // JAVÍTOTT bevételszámítás - elosztja az összeget
                    var revenue = _context.FinancialDevices
                        .Where(fd => fd.DeviceId == device.Id)
                        .Join(_context.Financials,
                              fd => fd.FinancialId,
                              f => f.Id,
                              (fd, f) => new { fd, f })
                        .Where(x => x.f.EntryType == "bevétel")
                        .ToList() // Memóriába töltés a további számításokhoz
                        .Sum(x =>
                        {
                            // Hány eszközre vonatkozik ez a Financial rekord?
                            var deviceCount = _context.FinancialDevices
                                .Count(fd => fd.FinancialId == x.f.Id);

                            // Elosztjuk az összeget az eszközök számával
                            return deviceCount > 0 ? x.f.Amount / deviceCount : 0;
                        });

                    // JAVÍTOTT költségszámítás - elosztja az összeget
                    var expense = _context.FinancialDevices
                        .Where(fd => fd.DeviceId == device.Id)
                        .Join(_context.Financials,
                              fd => fd.FinancialId,
                              f => f.Id,
                              (fd, f) => new { fd, f })
                        .Where(x => x.f.EntryType == "költség")
                        .ToList() // Memóriába töltés a további számításokhoz
                        .Sum(x =>
                        {
                            // Hány eszközre vonatkozik ez a Financial rekord?
                            var deviceCount = _context.FinancialDevices
                                .Count(fd => fd.FinancialId == x.f.Id);

                            // Elosztjuk az összeget az eszközök számával
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
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(250) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(120) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(120) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(120) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(120) });

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