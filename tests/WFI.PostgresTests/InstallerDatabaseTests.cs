using Npgsql;
using System.Text.RegularExpressions;
using Xunit;

namespace WFI.PostgresTests;

public sealed class InstallerDatabaseTests
{
    [Fact]
    public async Task InstallerApplicationRoleCanReadSchemaButCannotCreateTablesOrDatabases()
    {
        var source=Environment.GetEnvironmentVariable("WFI_TEST_POSTGRES")??throw new InvalidOperationException("Isolated loopback test PostgreSQL required.");
        var settings=new NpgsqlConnectionStringBuilder(source){Database="postgres",Pooling=false};
        Assert.Contains(settings.Host,new[]{"127.0.0.1","localhost","::1"});
        var name="wfi_test_"+Guid.NewGuid().ToString("N");var role=name+"_app";
        var password=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        await using var admin=new NpgsqlConnection(settings.ConnectionString);await admin.OpenAsync();
        async Task Run(NpgsqlConnection c,string sql){await using var cmd=new NpgsqlCommand(sql,c);await cmd.ExecuteNonQueryAsync();}
        await Run(admin,$"CREATE DATABASE {name}");
        try
        {
            await Run(admin,$"CREATE ROLE {role} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}'");
            settings.Database=name;
            await using(var db=new NpgsqlConnection(settings.ConnectionString))
            {
                await db.OpenAsync();
                var root=new DirectoryInfo(AppContext.BaseDirectory);
                while(root!=null&&!File.Exists(Path.Combine(root.FullName,"deploy","schema.sql")))root=root.Parent;
                Assert.NotNull(root);
                await Run(db,await File.ReadAllTextAsync(Path.Combine(root.FullName,"deploy","schema.sql")));
                var script=await File.ReadAllTextAsync(Path.Combine(root.FullName,"scripts","Install-ServerAutomatic.ps1"));
                var grant=Regex.Match(script,"Sql 'wfi' \"(REVOKE ALL[^\"]+)\"");Assert.True(grant.Success);
                await Run(db,grant.Groups[1].Value.Replace("wfi_app",role).Replace("DATABASE wfi ","DATABASE "+name+" "));
            }
            settings.Username=role;settings.Password=password;
            await using(var application=new NpgsqlConnection(settings.ConnectionString))
            {
                await application.OpenAsync();
                await Run(application,"SELECT * FROM \"Devices\" LIMIT 1");
                await Run(application,"UPDATE \"Devices\" SET \"Hostname\"=\"Hostname\" WHERE false");
                var ddl=await Assert.ThrowsAsync<PostgresException>(()=>Run(application,"CREATE TABLE forbidden(id integer)"));Assert.Equal("42501",ddl.SqlState);
                var createDb=await Assert.ThrowsAsync<PostgresException>(()=>Run(application,"CREATE DATABASE forbidden"));Assert.Equal("42501",createDb.SqlState);
            }
        }
        finally
        {
            await Run(admin,$"DROP DATABASE {name} WITH (FORCE)");
            await Run(admin,$"DROP ROLE IF EXISTS {role}");
        }
    }
}
