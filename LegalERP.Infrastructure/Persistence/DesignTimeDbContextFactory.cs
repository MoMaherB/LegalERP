using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LegalERP.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Local dev connection string only — replace YOUR_PASSWORD with the
        // postgres superuser password you set during installation.
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=legalerp_dev;Username=postgres;Password=5573758");

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}