using System.Text.Json;
using System.Text.Json.Serialization;
using Cadence.Api.Endpoints;
using Cadence.Application.Common.Paging;
using Cadence.Application.Features.Auth;
using Cadence.Application.Features.Invitations;
using Cadence.Application.Features.Me;
using Cadence.Application.Features.Members;
using Cadence.Application.Features.Organizations;
using Cadence.Application.Features.System;

namespace Cadence.Api.Serialization;

/// <summary>
/// Compile-time JSON metadata for every API contract. Serializers are generated ahead of time,
/// which removes reflection warm-up and reduces allocations per request.
/// Add each new request and response type here.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SystemInfoResponse))]
[JsonSerializable(typeof(RegisterCommand))]
[JsonSerializable(typeof(RegisterResponse))]
[JsonSerializable(typeof(ConfirmEmailCommand))]
[JsonSerializable(typeof(ResendConfirmationCommand))]
[JsonSerializable(typeof(LoginCommand))]
[JsonSerializable(typeof(AccessTokenResponse))]
[JsonSerializable(typeof(ForgotPasswordCommand))]
[JsonSerializable(typeof(ResetPasswordCommand))]
[JsonSerializable(typeof(CurrentUserResponse))]
[JsonSerializable(typeof(UpdateProfileCommand))]
[JsonSerializable(typeof(ChangePasswordCommand))]
[JsonSerializable(typeof(CreateOrganizationCommand))]
[JsonSerializable(typeof(OrganizationResponse))]
[JsonSerializable(typeof(RenameOrganizationCommand))]
[JsonSerializable(typeof(KeysetPage<MemberResponse>))]
[JsonSerializable(typeof(ChangeMemberRoleRequest))]
[JsonSerializable(typeof(CreateInvitationCommand))]
[JsonSerializable(typeof(InvitationResponse))]
[JsonSerializable(typeof(IReadOnlyList<InvitationResponse>))]
[JsonSerializable(typeof(InvitationPreviewResponse))]
[JsonSerializable(typeof(AcceptInvitationResponse))]
internal sealed partial class ApiJsonSerializerContext : JsonSerializerContext;
