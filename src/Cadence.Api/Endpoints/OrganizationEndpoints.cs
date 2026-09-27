using Cadence.Api.Hosting;
using Cadence.Application.Common.Paging;
using Cadence.Application.Features.Invitations;
using Cadence.Application.Features.Members;
using Cadence.Application.Features.Organizations;
using Cadence.Domain.Organizations;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cadence.Api.Endpoints;

/// <summary>Body of the change-role request.</summary>
public sealed record ChangeMemberRoleRequest(OrganizationRole Role);

internal static class OrganizationEndpoints
{
    public static RouteGroupBuilder MapOrganizationEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/organizations", CreateOrganization)
            .WithTags("Organizations")
            .WithName(nameof(CreateOrganization))
            .WithSummary("Create an organization with you as its owner");

        // Every route below resolves {organizationId} to the caller's membership first (404 for outsiders).
        var organization = api.MapGroup($"/organizations/{{{TenantResolutionMiddleware.RouteParameter}:guid}}")
            .WithTags("Organizations");

        organization.MapGet("/", GetOrganization)
            .WithName(nameof(GetOrganization))
            .WithSummary("Get an organization");

        organization.MapPatch("/", RenameOrganization)
            .WithName(nameof(RenameOrganization))
            .WithSummary("Rename an organization")
            .RequirePermission(Permissions.OrganizationUpdate);

        organization.MapDelete("/", DeleteOrganization)
            .WithName(nameof(DeleteOrganization))
            .WithSummary("Delete an organization and everything in it")
            .RequirePermission(Permissions.OrganizationDelete);

        var members = organization.MapGroup("/members").WithTags("Members");

        members.MapGet("/", ListMembers)
            .WithName(nameof(ListMembers))
            .WithSummary("List members, one page at a time")
            .RequirePermission(Permissions.MembersRead);

        members.MapPatch("/{userId:guid}", ChangeMemberRole)
            .WithName(nameof(ChangeMemberRole))
            .WithSummary("Change a member's role")
            .RequirePermission(Permissions.MembersManage);

        // Leaving needs no permission; removing someone else is checked in the handler.
        members.MapDelete("/{userId:guid}", RemoveMember)
            .WithName(nameof(RemoveMember))
            .WithSummary("Remove a member, or leave the organization");

        var invitations = organization.MapGroup("/invitations").WithTags("Invitations")
            .RequirePermission(Permissions.MembersInvite);

        invitations.MapGet("/", ListInvitations)
            .WithName(nameof(ListInvitations))
            .WithSummary("List pending invitations");

        invitations.MapPost("/", CreateInvitation)
            .WithName(nameof(CreateInvitation))
            .WithSummary("Invite someone by email");

        invitations.MapDelete("/{invitationId:guid}", RevokeInvitation)
            .WithName(nameof(RevokeInvitation))
            .WithSummary("Revoke a pending invitation");

        return api;
    }

    private static async Task<Results<Created<OrganizationResponse>, ProblemHttpResult>> CreateOrganization(
        CreateOrganizationCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Created($"/api/v1/organizations/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<OrganizationResponse>, ProblemHttpResult>> GetOrganization(
        ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetOrganizationQuery(), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RenameOrganization(
        RenameOrganizationCommand command, ISender sender, CancellationToken cancellationToken) =>
        ToNoContent(await sender.Send(command, cancellationToken));

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteOrganization(
        ISender sender, CancellationToken cancellationToken) =>
        ToNoContent(await sender.Send(new DeleteOrganizationCommand(), cancellationToken));

    private static async Task<Ok<KeysetPage<MemberResponse>>> ListMembers(
        string? after, int? limit, ISender sender, CancellationToken cancellationToken) =>
        TypedResults.Ok(await sender.Send(new ListMembersQuery(after, limit), cancellationToken));

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeMemberRole(
        Guid userId, ChangeMemberRoleRequest request, ISender sender, CancellationToken cancellationToken) =>
        ToNoContent(await sender.Send(new ChangeMemberRoleCommand(userId, request.Role), cancellationToken));

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveMember(
        Guid userId, ISender sender, CancellationToken cancellationToken) =>
        ToNoContent(await sender.Send(new RemoveMemberCommand(userId), cancellationToken));

    private static async Task<Ok<IReadOnlyList<InvitationResponse>>> ListInvitations(
        ISender sender, CancellationToken cancellationToken) =>
        TypedResults.Ok(await sender.Send(new ListInvitationsQuery(), cancellationToken));

    private static async Task<Results<Created<InvitationResponse>, ProblemHttpResult>> CreateInvitation(
        CreateInvitationCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? TypedResults.Created((string?)null, result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RevokeInvitation(
        Guid invitationId, ISender sender, CancellationToken cancellationToken) =>
        ToNoContent(await sender.Send(new RevokeInvitationCommand(invitationId), cancellationToken));

    private static Results<NoContent, ProblemHttpResult> ToNoContent(Domain.Common.Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
}
