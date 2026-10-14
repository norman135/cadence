using Cadence.Api.IntegrationTests.Infrastructure;
using Cadence.Application.Features.Board;
using Cadence.Domain.Issues;
using Cadence.Infrastructure.Persistence;
using Cadence.Infrastructure.Ranking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cadence.Api.IntegrationTests.Board;

[Collection(nameof(ApiCollection))]
public sealed class RankRebalancerTests(CadenceApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateApiClient();

    private static BoardColumn Column(BoardResponse board, string name) => board.Columns.Single(column => column.Name == name);

    private async Task<(TestSession Owner, Guid OrganizationId, Guid ProjectId)> ProjectAsync()
    {
        var owner = await factory.SignUpAsync();
        var organization = await _client.CreateOrganizationAsync(owner);
        var project = await _client.CreateProjectAsync(owner, organization.Id, "Atlas", "ATL");
        return (owner, organization.Id, project.Id);
    }

    private async Task SqlAsync(FormattableString sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CadenceDbContext>().Database.ExecuteSqlAsync(sql, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Long_ranks_are_rebalanced_without_changing_the_order()
    {
        var (owner, organizationId, projectId) = await ProjectAsync();
        for (var i = 0; i < 3; i++)
        {
            await _client.CreateIssueAsync(owner, organizationId, projectId, $"Issue {i}");
        }

        var before = Column(await _client.GetBoardAsync(owner, organizationId, projectId), "Todo").Cards;
        var longRank = Rank.Between(before[0].Rank, before[1].Rank) + "0000000000001";
        await SqlAsync($"UPDATE issues SET rank = {longRank} WHERE id = {before[2].Id}");

        var rebalanced = await factory.Services.GetRequiredService<RankRebalancer>().RebalanceAsync(TestContext.Current.CancellationToken);

        var after = Column(await _client.GetBoardAsync(owner, organizationId, projectId), "Todo").Cards;
        Assert.True(rebalanced >= 1);
        Assert.Equal([before[0].Id, before[2].Id, before[1].Id], after.Select(card => card.Id));
        Assert.All(after, card => Assert.True(card.Rank.Length <= 5 && Rank.IsValid(card.Rank), card.Rank));
    }
}
