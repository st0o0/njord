using Microsoft.Data.Sqlite;
using Njord.Grpc.V2;

namespace Njord.IntegrationTests.Persistence;

[Collection(HostCollection.Name)]
public sealed class PersistenceRestartTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private static async Task<StatusResponse> StatusAsync(NjordTestHost host, CancellationToken ct) =>
        await new OpsService.OpsServiceClient(host.Channel).GetStatusAsync(new GetStatusRequest(), cancellationToken: ct);

    [Fact(Timeout = 60_000)]
    public async Task Budget_usage_and_poll_state_of_the_first_lifetime_are_recovered_by_the_second()
    {
        var ct = TestContext.Current.CancellationToken;
        long usageBeforeRestart;

        await using (var first = await NjordTestHost.StartAsync(_directory.Path, cancellationToken: ct))
        {
            var ops = new OpsService.OpsServiceClient(first.Channel);

            // One discovery round, then two triggered rounds. Budget grows with each round.
            await Eventually.AssertAsync(async () =>
                Assert.True((await StatusAsync(first, ct)).Budget.MonthlyUsed > 0), ct);
            var afterDiscovery = (await StatusAsync(first, ct)).Budget.MonthlyUsed;
            await ops.TriggerPollAsync(new TriggerPollRequest { Location = "luzern" }, cancellationToken: ct);
            await ops.TriggerPollAsync(new TriggerPollRequest { Location = "luzern" }, cancellationToken: ct);
            await Eventually.AssertAsync(async () =>
            {
                var actual = (await StatusAsync(first, ct)).Budget.MonthlyUsed;
                Assert.True(actual > afterDiscovery, $"usage {actual} should exceed discovery usage {afterDiscovery}");
            }, ct);

            var status = await StatusAsync(first, ct);
            usageBeforeRestart = status.Budget.MonthlyUsed;
        }

        Assert.True(usageBeforeRestart > 0, "The first lifetime never recorded any API usage.");

        await using var second = await NjordTestHost.StartAsync(_directory.Path, cancellationToken: ct);

        await Eventually.AssertAsync(async () =>
        {
            var status = await StatusAsync(second, ct);
            Assert.True(status.Budget.MonthlyUsed >= usageBeforeRestart,
                $"usage {status.Budget.MonthlyUsed} < {usageBeforeRestart}");
        }, ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task The_sqlite_journal_and_snapshot_tables_are_created_by_the_first_start()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = Path.Combine(_directory.Path, "njord.db");
        await using var host = await NjordTestHost.StartAsync(_directory.Path, cancellationToken: ct);

        await Eventually.AssertAsync(async () =>
        {
            Assert.True(File.Exists(database), "The database file does not exist yet.");
            await using var connection = new SqliteConnection($"Data Source={database};Mode=ReadOnly");
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
            var tables = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                tables.Add(reader.GetString(0));
            }

            Assert.True(tables.Any(t => t.Contains("journal", StringComparison.OrdinalIgnoreCase)), "tables: " + string.Join(", ", tables));
            Assert.Contains("snapshot", tables);
        }, ct);
    }
}
