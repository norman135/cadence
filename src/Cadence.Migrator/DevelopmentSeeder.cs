using System.Globalization;
using Cadence.Domain.Issues;
using Cadence.Domain.Organizations;
using Cadence.Domain.Projects;
using Cadence.Domain.Workflows;
using Cadence.Infrastructure.Identity;
using Cadence.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cadence.Migrator;

/// <summary>
/// Fills a database with a realistic organization for development and performance testing: the
/// people of the design boards in "Lumen Labs", three projects, nine labels, and as many issues as
/// asked for, with sub-issues, comments and six months of history. Deterministic (fixed seed), and
/// skipped when the organization already exists.
/// </summary>
internal static partial class DevelopmentSeeder
{
    public const string OrganizationSlug = "lumen-labs";
    public const string EmailDomain = "lumenlabs.test";

    private const int BatchSize = 1_000;

    private static readonly (string Name, OrganizationRole Role)[] s_people =
    [
        ("Ada Lovelace", OrganizationRole.Owner),
        ("Grace Hopper", OrganizationRole.Admin),
        ("Alan Turing", OrganizationRole.Member),
        ("Katherine Johnson", OrganizationRole.Member),
        ("Margaret Hamilton", OrganizationRole.Member),
        ("Dennis Ritchie", OrganizationRole.Member),
    ];

    private static readonly (string Key, string Name, double Share)[] s_projects =
    [
        ("ATL", "Atlas Mobile", 0.5),
        ("NIM", "Nimbus API", 0.3),
        ("WEB", "Website", 0.2),
    ];

    private static readonly (string Name, LabelColor Color)[] s_labels =
    [
        ("Backend", LabelColor.Teal),
        ("Frontend", LabelColor.Blue),
        ("Design", LabelColor.Violet),
        ("Research", LabelColor.Pink),
        ("Bug", LabelColor.Red),
        ("Performance", LabelColor.Orange),
        ("Tech debt", LabelColor.Amber),
        ("Feature", LabelColor.Green),
        ("Chore", LabelColor.Slate),
    ];

    private static readonly string[] s_verbs =
        ["Fix", "Add", "Improve", "Remove", "Speed up", "Redesign", "Document", "Test", "Refactor", "Support"];

    private static readonly string[] s_subjects =
    [
        "offline sync", "push notifications", "the sign-in flow", "rate limiting", "the board view", "search results",
        "webhook retries", "the billing page", "image uploads", "dark mode", "the onboarding tour", "CSV export",
        "audit logging", "deep links", "the settings page", "keyboard shortcuts", "error reporting", "the release notes",
    ];

    private static readonly string[] s_contexts =
    [
        "on slow connections", "for large teams", "on Android", "on iOS", "in the API", "after a reconnect",
        "for guests", "in Safari", "under load", "for archived projects", "on first launch", "with many labels",
    ];

    private static readonly int[] s_estimates = [1, 2, 3, 5, 8, 13];

    private static readonly string[] s_comments =
    [
        "Reproduced on a throttled connection.", "I'll take this one.", "Is this still happening on the latest build?",
        "Pushed a fix to a branch; needs review.", "Blocked on the API change.", "Looks good to me.",
        "Can we split this into smaller issues?", "Added a test that fails before the fix.",
    ];

    public static async Task<int> RunAsync(IServiceProvider services, int issueCount, string password, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Cadence.Migrator.Seeder");
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CadenceDbContext>();

        if (await db.Organizations.AnyAsync(organization => organization.Slug == OrganizationSlug, cancellationToken))
        {
            LogSkipped(logger, OrganizationSlug);
            return 0;
        }

        var random = new Random(42);
        var now = DateTimeOffset.UtcNow;
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        var users = CreateUsers(password, now);
        db.Users.AddRange(users);

        var organization = Organization.Create("Lumen Labs", OrganizationSlug, now.AddDays(-200)).Value;
        db.Organizations.Add(organization);
        for (var index = 0; index < users.Count; index++)
        {
            db.Memberships.Add(Membership.Create(organization.Id, users[index].Id, s_people[index].Role, now.AddDays(-200)));
        }

        var projects = s_projects
            .Select(spec => Project.Create(organization.Id, spec.Key, spec.Name, $"Everything about {spec.Name}.", users[0].Id, now.AddDays(-190)).Value)
            .ToList();
        db.Projects.AddRange(projects);
        var workflows = projects.ToDictionary(project => project.Id, Workflow.CreateDefault);
        db.Workflows.AddRange(workflows.Values);
        foreach (var project in projects)
        {
            foreach (var user in users)
            {
                var role = user == users[0] ? ProjectRole.Admin : ProjectRole.Member;
                db.ProjectMembers.Add(ProjectMember.Add(project, user.Id, role, now.AddDays(-190)));
            }
        }

        var labels = s_labels.Select(spec => Label.Create(organization.Id, spec.Name, spec.Color).Value).ToList();
        db.Labels.AddRange(labels);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var created = 0;
        var numbers = projects.ToDictionary(project => project.Id, _ => 0);
        var epics = projects.ToDictionary(project => project.Id, _ => new List<Guid>());

        while (created < issueCount)
        {
            var batch = Math.Min(BatchSize, issueCount - created);
            for (var index = 0; index < batch; index++)
            {
                var project = PickProject(random, projects);
                var number = ++numbers[project.Id];
                // Issues get evenly spaced ranks in creation order, as the migration and rebalancing do.
                var issue = CreateIssue(db, random, project, workflows[project.Id], number, users, labels, epics[project.Id], now);
                if (issue.Type == IssueType.Epic)
                {
                    epics[project.Id].Add(issue.Id);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
            created += batch;
            LogProgress(logger, created, issueCount);
        }

        await RefreshCountersAsync(db, organization.Id, cancellationToken);
        LogSeeded(logger, OrganizationSlug, issueCount, users.Count, EmailDomain);
        return 0;
    }

    private static List<ApplicationUser> CreateUsers(string password, DateTimeOffset now)
    {
        var hasher = new PasswordHasher<ApplicationUser>();
        return
        [
            .. s_people.Select(person =>
            {
                var email = $"{person.Name.Split(' ')[0].ToLowerInvariant()}@{EmailDomain}";
                var user = new ApplicationUser
                {
                    Id = Guid.CreateVersion7(),
                    UserName = email,
                    NormalizedUserName = email.ToUpperInvariant(),
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    EmailConfirmed = true,
                    DisplayName = person.Name,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                    CreatedAt = now.AddDays(-200),
                };
                user.PasswordHash = hasher.HashPassword(user, password);
                return user;
            }),
        ];
    }

    private static Project PickProject(Random random, List<Project> projects)
    {
        var roll = random.NextDouble();
        for (var index = 0; index < projects.Count; index++)
        {
            roll -= s_projects[index].Share;
            if (roll < 0)
            {
                return projects[index];
            }
        }

        return projects[^1];
    }

    private static Issue CreateIssue(
        CadenceDbContext db,
        Random random,
        Project project,
        Workflow workflow,
        int number,
        List<ApplicationUser> users,
        List<Label> labels,
        List<Guid> epics,
        DateTimeOffset now)
    {
        var type = Weighted(random, (IssueType.Task, 45), (IssueType.Bug, 25), (IssueType.Story, 27), (IssueType.Epic, 3));
        var status = Weighted(random, (IssueStatus.Backlog, 15), (IssueStatus.Todo, 25), (IssueStatus.InProgress, 15), (IssueStatus.InReview, 8), (IssueStatus.Done, 30), (IssueStatus.Canceled, 7));
        var priority = Weighted(random, (IssuePriority.None, 20), (IssuePriority.Low, 25), (IssuePriority.Medium, 30), (IssuePriority.High, 18), (IssuePriority.Urgent, 7));
        var createdAt = now.AddMinutes(-random.Next(10, 180 * 24 * 60));
        var updatedAt = createdAt.AddMinutes(random.Next(0, (int)Math.Max(1, (now - createdAt).TotalMinutes)));
        var title = string.Create(
            CultureInfo.InvariantCulture,
            $"{s_verbs[random.Next(s_verbs.Length)]} {s_subjects[random.Next(s_subjects.Length)]} {s_contexts[random.Next(s_contexts.Length)]}");

        var issue = Issue.Create(
            project,
            number,
            type,
            title,
            "Seeded for development and performance testing.",
            workflow.FirstIn(status)!,
            Rank.AtPosition(number - 1),
            priority,
            users[random.Next(users.Count)].Id,
            createdAt).Value;
        if (random.NextDouble() < 0.8)
        {
            issue.AssignTo(users[random.Next(users.Count)].Id, createdAt);
        }

        if (random.NextDouble() < 0.6)
        {
            issue.EstimateAt(s_estimates[random.Next(s_estimates.Length)], createdAt);
        }

        if (random.NextDouble() < 0.25)
        {
            issue.DueOn(DateOnly.FromDateTime(now.UtcDateTime).AddDays(random.Next(-60, 60)), createdAt);
        }

        db.Issues.Add(issue);
        var entry = db.Entry(issue);

        // Some issues belong to an epic of their project.
        if (type != IssueType.Epic && epics.Count > 0 && random.NextDouble() < 0.15)
        {
            entry.Property(nameof(Issue.ParentId)).CurrentValue = epics[random.Next(epics.Count)];
        }

        // Times in the past, as if the issue had a history.
        entry.Property(nameof(Issue.UpdatedAt)).CurrentValue = updatedAt;
        if (status.IsClosed())
        {
            entry.Property(nameof(Issue.CompletedAt)).CurrentValue = updatedAt;
        }

        foreach (var label in labels.OrderBy(_ => random.Next()).Take(random.Next(0, 4)))
        {
            db.IssueLabels.Add(new IssueLabel(issue.OrganizationId, issue.Id, label.Id));
        }

        for (var count = random.Next(0, 3); count > 0; count--)
        {
            var comment = Comment.Create(issue, users[random.Next(users.Count)].Id, s_comments[random.Next(s_comments.Length)], updatedAt).Value;
            db.Comments.Add(comment);
        }

        return issue;
    }

    private static T Weighted<T>(Random random, params (T Value, int Weight)[] options)
    {
        var roll = random.Next(options.Sum(option => option.Weight));
        foreach (var (value, weight) in options)
        {
            roll -= weight;
            if (roll < 0)
            {
                return value;
            }
        }

        return options[^1].Value;
    }

    /// <summary>Issue numbers, open counts, sub-issue and comment counts, all from the rows.</summary>
    private static async Task RefreshCountersAsync(CadenceDbContext db, Guid organizationId, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE projects AS p SET
                last_issue_number = (SELECT coalesce(max(number), 0) FROM issues WHERE project_id = p.id),
                open_issue_count = (SELECT count(*) FROM issues WHERE project_id = p.id AND status < 4)
            WHERE p.organization_id = {organizationId}
            """,
            cancellationToken);

        await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE issues AS i SET
                sub_issue_count = (SELECT count(*) FROM issues c WHERE c.parent_id = i.id),
                sub_issue_closed_count = (SELECT count(*) FROM issues c WHERE c.parent_id = i.id AND c.status >= 4),
                comment_count = (SELECT count(*) FROM comments m WHERE m.issue_id = i.id)
            WHERE i.organization_id = {organizationId}
            """,
            cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Organization {Slug} already exists; nothing to seed")]
    private static partial void LogSkipped(ILogger logger, string slug);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Created} of {Total} issues")]
    private static partial void LogProgress(ILogger logger, int created, int total);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Slug}: {Issues} issues, {Users} people (sign in as ada@{Domain})")]
    private static partial void LogSeeded(ILogger logger, string slug, int issues, int users, string domain);
}
