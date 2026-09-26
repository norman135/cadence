using Cadence.Application.Common.Abstractions;
using Cadence.Application.Features.Auth;
using Cadence.Domain.Common;
using Mediator;

namespace Cadence.Application.Features.Me;

/// <summary>Returns the signed-in user's profile.</summary>
public sealed record GetCurrentUserQuery : IQuery<Result<CurrentUserResponse>>;

/// <summary>The signed-in user.</summary>
public sealed record CurrentUserResponse(Guid Id, string Email, string DisplayName);

public sealed class GetCurrentUserQueryHandler(IIdentityService identity, ICurrentUser currentUser)
    : IQueryHandler<GetCurrentUserQuery, Result<CurrentUserResponse>>
{
    public async ValueTask<Result<CurrentUserResponse>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var user = await identity.FindByIdAsync(currentUser.RequiredUserId, cancellationToken);
        if (user is null)
        {
            return AuthErrors.UserNotFound;
        }

        return new CurrentUserResponse(user.Id, user.Email, user.DisplayName);
    }
}
