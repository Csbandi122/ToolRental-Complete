using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
using ToolRental.Core.Models;

namespace ToolRental.Data
{
    public class ToolRentalDbContext : DbContext
    {
        public ToolRentalDbContext(DbContextOptions<ToolRentalDbContext> options)
            : base(options) { }

        // DbSet properties - ezek lesznek a táblák
        public DbSet<Device> Devices { get; set; }
        public DbSet<DeviceType> DeviceTypes { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Rental> Rentals { get; set; }
        public DbSet<RentalDevice> RentalDevices { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Financial> Financials { get; set; }
        public DbSet<FinancialDevice> FinancialDevices { get; set; }
        public DbSet<Setting> Settings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Kapcsolatok definiálása

            // Device - DeviceType kapcsolat
            modelBuilder.Entity<Device>()
                .HasOne(d => d.DeviceTypeNavigation)
                .WithMany(dt => dt.Devices)
                .HasForeignKey(d => d.DeviceType);

            // Rental - Customer kapcsolat
            modelBuilder.Entity<Rental>()
                .HasOne(r => r.Customer)
                .WithMany(c => c.Rentals)
                .HasForeignKey(r => r.CustomerId);

            // RentalDevice many-to-many kapcsolat
            modelBuilder.Entity<RentalDevice>()
                .HasOne(rd => rd.Rental)
                .WithMany(r => r.RentalDevices)
                .HasForeignKey(rd => rd.RentalId);

            modelBuilder.Entity<RentalDevice>()
                .HasOne(rd => rd.Device)
                .WithMany(d => d.RentalDevices)
                .HasForeignKey(rd => rd.DeviceId);

            // FinancialDevice many-to-many kapcsolat
            modelBuilder.Entity<FinancialDevice>()
                .HasOne(fd => fd.Financial)
                .WithMany(f => f.FinancialDevices)
                .HasForeignKey(fd => fd.FinancialId);

            modelBuilder.Entity<FinancialDevice>()
                .HasOne(fd => fd.Device)
                .WithMany()
                .HasForeignKey(fd => fd.DeviceId);

            base.OnModelCreating(modelBuilder);
        }
    }
}