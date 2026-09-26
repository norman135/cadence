using System.Text.Json;
using System.Text.Json.Serialization;
using Cadence.Api.Endpoints;
using Cadence.Application.Features.Auth;
using Cadence.Application.Features.Me;
using Cadence.Application.Features.System;

namespace Cadence.Api.Serialization;

/// <summary>
/// Compile-time JSON metadata for every API contract. Serializers are generated ahead of time,
/// which removes reflection warm-up and reduces allocations per request.
/// Add each new request and response type here.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
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
internal sealed partial class ApiJsonSerializerContext : JsonSerializerContext;
