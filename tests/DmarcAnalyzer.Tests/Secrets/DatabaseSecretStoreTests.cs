using DmarcAnalyzer.Infrastructure.Data;
using DmarcAnalyzer.Infrastructure.Secrets;
using DmarcAnalyzer.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace DmarcAnalyzer.Tests.Secrets;

public class DatabaseSecretStoreTests
{
    private static DmarcAnalyzerDbContext BuildDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<DmarcAnalyzerDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new DmarcAnalyzerDbContext(options);
    }

    [Fact]
    public async Task SetThenGet_ReturnsOriginalValue()
    {
        var db = BuildDb(nameof(SetThenGet_ReturnsOriginalValue));
        var store = new DatabaseSecretStore(db, new FakeDataProtectionProvider());

        await store.SetSecretAsync("GraphClientSecret", "super-secret-value");
        var result = await store.GetSecretAsync("GraphClientSecret");

        Assert.Equal("super-secret-value", result);
    }

    [Fact]
    public async Task Set_StoresCipherTextNotPlaintext()
    {
        var db = BuildDb(nameof(Set_StoresCipherTextNotPlaintext));
        var store = new DatabaseSecretStore(db, new FakeDataProtectionProvider());

        await store.SetSecretAsync("GraphClientSecret", "super-secret-value");

        var stored = await db.Secrets.SingleAsync(s => s.Name == "GraphClientSecret");
        Assert.DoesNotContain("super-secret-value", stored.CipherText);
    }

    [Fact]
    public async Task SetTwice_OverwritesPreviousValue()
    {
        var db = BuildDb(nameof(SetTwice_OverwritesPreviousValue));
        var store = new DatabaseSecretStore(db, new FakeDataProtectionProvider());

        await store.SetSecretAsync("GraphClientSecret", "first-value");
        await store.SetSecretAsync("GraphClientSecret", "second-value");
        var result = await store.GetSecretAsync("GraphClientSecret");

        Assert.Equal("second-value", result);
        Assert.Equal(1, await db.Secrets.CountAsync());
    }

    [Fact]
    public async Task Get_UnknownName_ReturnsNull()
    {
        var db = BuildDb(nameof(Get_UnknownName_ReturnsNull));
        var store = new DatabaseSecretStore(db, new FakeDataProtectionProvider());

        var result = await store.GetSecretAsync("DoesNotExist");

        Assert.Null(result);
    }
}
