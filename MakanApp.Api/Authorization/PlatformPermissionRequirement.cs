using Microsoft.AspNetCore.Authorization;

namespace MakanApp.Api.Authorization;

public sealed record PlatformPermissionRequirement(string Permission) : IAuthorizationRequirement;
