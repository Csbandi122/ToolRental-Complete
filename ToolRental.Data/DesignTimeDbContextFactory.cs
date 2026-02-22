using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ToolRental.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ToolRentalDbContext>
    {
        public ToolRentalDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ToolRentalDbContext>();

            // SQLITE connection string (nem SQL Server!)
            optionsBuilder.UseSqlServer(@"Server=192.168.68.241,1433;Database=ToolRentalDB;User Id=toolrentaluser;Password=ToolRental2025!;TrustServerCertificate=True;");
            return new ToolRentalDbContext(optionsBuilder.Options);
        }
    }
}