using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartX.Infrastructure.Persistence;
namespace SmartX.Tests.Operations;
public sealed class MigrationTests
{
    [Fact]
    public void OperationalMigrationIsAdditiveAndSnapshotMatchesModel()
    {
        using var db = new SmartXDbContext(new DbContextOptionsBuilder<SmartXDbContext>().UseSqlServer("Server=localhost;Database=ModelOnly;Integrated Security=true;").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript("20260902131429_InitialCreate");
        Assert.Contains("CREATE TABLE [CommandHistory]", script);
        Assert.Contains("CREATE TABLE [Interactions]", script);
        Assert.Contains("CREATE TABLE [GatewayReceipts]", script);
        Assert.DoesNotContain("DROP TABLE", script); Assert.DoesNotContain("DELETE FROM", script);
    }
}
