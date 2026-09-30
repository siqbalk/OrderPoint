using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace Reporting.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> target Reporting.Infrastructure directly, without the Host.</summary>
public sealed class ReportingDbContextFactory : IDesignTimeDbContextFactory<ReportingDbContext>
{
    public ReportingDbContext CreateDbContext(string[] args)
        => new(ModulePersistenceExtensions.DesignTimeOptions<ReportingDbContext>(ReportingDbContext.SchemaName),
            ModulePersistenceExtensions.DesignTimeTenant);
}
