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
            optionsBuilder.UseSqlite("Data Source=ToolRental.db");

            return new ToolRentalDbContext(optionsBuilder.Options);
        }
    }
}