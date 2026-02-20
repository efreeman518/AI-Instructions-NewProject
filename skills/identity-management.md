# Identity Management

## Overview

Identity management covers two distinct scenarios:

1. **Entra External ID** — for public-facing applications where end-users (customers, patients, external parties) sign up and authenticate. Uses Entra External ID (formerly Azure AD B2C).
2. **Entra ID (Enterprise)** — for internal/enterprise applications where users are employees or partners managed in the organization's Azure AD tenant.

Both scenarios are optional. The domain inputs determine which to scaffold.

## When to Use

| Scenario | Auth Provider | Use Case |
|----------|--------------|----------|
| Public-facing app | Entra External ID | Customer portals, patient portals, self-service apps |
| Enterprise app | Entra ID | Internal tools, admin dashboards, line-of-business apps |
| Both | Entra External ID + Entra ID | Public portal with admin back-office |

## Project Structure

### Infrastructure Service (external user management)

When the solution needs to **manage** external users programmatically (create, invite, assign roles):

```
src/Infrastructure/
└── {Project}.Infrastructure.EntraExt/
    ├── Infrastructure.EntraExt.csproj
    ├── IEntraExtService.cs
    ├── EntraExtService.cs
    └── EntraExtServiceSettings.cs
```

### Infrastructure Service (enterprise user management)

When the solution needs to query or manage enterprise users via Microsoft Graph:

```
src/Infrastructure/
└── {Project}.Infrastructure.Graph/
    ├── Infrastructure.Graph.csproj
    ├── IGraphService.cs
    ├── GraphService.cs
    └── GraphServiceSettings.cs
```

## Entra External ID (Public-Facing)

### Gateway Authentication

The Gateway validates external user tokens and performs token relay to backend services:

```csharp
// File: src/{Gateway}/{Gateway}.Gateway/Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://{tenantName}.ciamlogin.com/{tenantId}/v2.0";
        options.Audience = configuration["AzureAd:ClientId"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://{tenantName}.ciamlogin.com/{tenantId}/v2.0",
            ValidateAudience = true,
            ValidateLifetime = true
        };
    });
```

### External User Management Service

For admin operations — creating external users, sending invitations, assigning app roles:

```csharp
// File: src/Infrastructure/{Project}.Infrastructure.EntraExt/IEntraExtService.cs
namespace {Project}.Infrastructure.EntraExt;

public interface IEntraExtService
{
    Task<Result<string>> CreateUserAsync(string email, string displayName, CancellationToken ct = default);
    Task<Result> InviteUserAsync(string email, string redirectUrl, CancellationToken ct = default);
    Task<Result> AssignAppRoleAsync(string userId, string appRoleId, CancellationToken ct = default);
    Task<Result> RemoveAppRoleAsync(string userId, string appRoleId, CancellationToken ct = default);
    Task<Result<ExternalUserInfo>> GetUserAsync(string userId, CancellationToken ct = default);
    Task<Result> DisableUserAsync(string userId, CancellationToken ct = default);
}
```

```csharp
// File: src/Infrastructure/{Project}.Infrastructure.EntraExt/EntraExtService.cs
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace {Project}.Infrastructure.EntraExt;

public class EntraExtService(
    ILogger<EntraExtService> logger,
    IOptions<EntraExtServiceSettings> settings,
    GraphServiceClient graphClient) : IEntraExtService
{
    public async Task<Result<string>> CreateUserAsync(string email, string displayName, CancellationToken ct = default)
    {
        try
        {
            var user = new User
            {
                DisplayName = displayName,
                Mail = email,
                AccountEnabled = true,
                Identities =
                [
                    new ObjectIdentity
                    {
                        SignInType = "emailAddress",
                        Issuer = settings.Value.TenantDomain,
                        IssuerAssignedId = email
                    }
                ]
            };

            var created = await graphClient.Users.PostAsync(user, cancellationToken: ct);
            return Result<string>.Success(created!.Id!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create external user {Email}", email);
            return Result<string>.Failure(ex.Message);
        }
    }

    public async Task<Result> AssignAppRoleAsync(string userId, string appRoleId, CancellationToken ct = default)
    {
        try
        {
            var assignment = new AppRoleAssignment
            {
                PrincipalId = Guid.Parse(userId),
                ResourceId = Guid.Parse(settings.Value.ServicePrincipalId),
                AppRoleId = Guid.Parse(appRoleId)
            };

            await graphClient.Users[userId].AppRoleAssignments.PostAsync(assignment, cancellationToken: ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to assign app role {AppRoleId} to user {UserId}", appRoleId, userId);
            return Result.Failure(ex.Message);
        }
    }

    // ... other method implementations follow same pattern
}
```

### Settings

```csharp
// File: src/Infrastructure/{Project}.Infrastructure.EntraExt/EntraExtServiceSettings.cs
namespace {Project}.Infrastructure.EntraExt;

public class EntraExtServiceSettings
{
    public string TenantId { get; set; } = null!;
    public string TenantDomain { get; set; } = null!;        // e.g., "contoso.onmicrosoft.com"
    public string ClientId { get; set; } = null!;             // App registration client ID
    public string ClientSecret { get; set; } = null!;         // From Key Vault
    public string ServicePrincipalId { get; set; } = null!;   // For app role assignments
}
```

## Entra ID (Enterprise)

### Enterprise Authentication

For internal apps, authenticate against the organization's Entra ID tenant:

```csharp
// File: src/{Host}/{Host}.Api/Program.cs (or Gateway)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
        options.Audience = configuration["AzureAd:ClientId"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://login.microsoftonline.com/{tenantId}/v2.0",
            ValidateAudience = true,
            ValidateLifetime = true,
            RoleClaimType = "roles"  // Enterprise app roles from Entra ID
        };
    });
```

### Enterprise User Lookup via Microsoft Graph

```csharp
// File: src/Infrastructure/{Project}.Infrastructure.Graph/IGraphService.cs
namespace {Project}.Infrastructure.Graph;

public interface IGraphService
{
    Task<Result<EnterpriseUserInfo>> GetUserAsync(string userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EnterpriseUserInfo>>> SearchUsersAsync(string query, CancellationToken ct = default);
    Task<Result<IReadOnlyList<string>>> GetUserGroupsAsync(string userId, CancellationToken ct = default);
    Task<Result<byte[]>> GetUserPhotoAsync(string userId, CancellationToken ct = default);
}
```

```csharp
// File: src/Infrastructure/{Project}.Infrastructure.Graph/GraphService.cs
using Azure.Identity;
using Microsoft.Graph;

namespace {Project}.Infrastructure.Graph;

public class GraphService(
    ILogger<GraphService> logger,
    IOptions<GraphServiceSettings> settings,
    GraphServiceClient graphClient) : IGraphService
{
    public async Task<Result<EnterpriseUserInfo>> GetUserAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            var user = await graphClient.Users[userId]
                .GetAsync(r => r.QueryParameters.Select =
                    ["id", "displayName", "mail", "jobTitle", "department"], cancellationToken: ct);

            return user is null
                ? Result<EnterpriseUserInfo>.None()
                : Result<EnterpriseUserInfo>.Success(new EnterpriseUserInfo(
                    user.Id!, user.DisplayName!, user.Mail, user.JobTitle, user.Department));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get enterprise user {UserId}", userId);
            return Result<EnterpriseUserInfo>.Failure(ex.Message);
        }
    }
}
```

## NuGet Packages

```xml
<!-- Infrastructure.EntraExt or Infrastructure.Graph -->
<ItemGroup>
    <PackageReference Include="Microsoft.Graph" />
    <PackageReference Include="Azure.Identity" />
</ItemGroup>
```

Add to `Directory.Packages.props`:

```xml
<PackageVersion Include="Microsoft.Graph" Version="5.x" />
```

## DI Registration

```csharp
// File: src/{Host}/{Host}.Bootstrapper/RegisterServices.cs

// Entra External ID (public-facing)
public static IServiceCollection RegisterEntraExtServices(
    this IServiceCollection services, IConfiguration configuration)
{
    services.Configure<EntraExtServiceSettings>(
        configuration.GetSection("EntraExt"));

    var settings = configuration.GetSection("EntraExt").Get<EntraExtServiceSettings>()!;
    var credential = new ClientSecretCredential(
        settings.TenantId, settings.ClientId, settings.ClientSecret);
    var graphClient = new GraphServiceClient(credential);

    services.AddSingleton(graphClient);
    services.AddScoped<IEntraExtService, EntraExtService>();
    return services;
}

// Entra ID (enterprise)
public static IServiceCollection RegisterGraphServices(
    this IServiceCollection services, IConfiguration configuration)
{
    services.Configure<GraphServiceSettings>(
        configuration.GetSection("Graph"));

    var settings = configuration.GetSection("Graph").Get<GraphServiceSettings>()!;
    var credential = new ClientSecretCredential(
        settings.TenantId, settings.ClientId, settings.ClientSecret);
    var graphClient = new GraphServiceClient(credential);

    services.AddSingleton(graphClient);
    services.AddScoped<IGraphService, GraphService>();
    return services;
}
```

## Configuration

```json
// appsettings.json — Entra External ID
{
  "EntraExt": {
    "TenantId": "{{from-keyvault}}",
    "TenantDomain": "contoso.onmicrosoft.com",
    "ClientId": "{{from-keyvault}}",
    "ClientSecret": "{{from-keyvault}}",
    "ServicePrincipalId": "{{from-keyvault}}"
  }
}

// appsettings.json — Entra ID (Enterprise)
{
  "Graph": {
    "TenantId": "{{from-keyvault}}",
    "ClientId": "{{from-keyvault}}",
    "ClientSecret": "{{from-keyvault}}"
  }
}
```

## Summary: Which to Scaffold

| Domain Input | Infrastructure Project | Gateway Config | Bootstrapper Method |
|---|---|---|---|
| `auth: entraExternal` | `Infrastructure.EntraExt` | Entra External ID issuer | `RegisterEntraExtServices` |
| `auth: entraId` | `Infrastructure.Graph` | Entra ID issuer | `RegisterGraphServices` |
| Both | Both projects | Dual authentication schemes | Both methods |
| Neither | None | No identity management | Skip this skill |

## Lite Mode

When scaffolding in lite mode:
- Skip identity management infrastructure entirely.
- Use hardcoded/mock `IRequestContext` for local development.
- Add TODO comments for identity integration.

## Rules

1. **Separation** — identity management infrastructure projects must NOT reference Domain or Application projects. They are pure infrastructure.
2. **Credentials** — all secrets (client secrets, tenant IDs) must come from Key Vault or user secrets, never hardcoded.
3. **Graph SDK** — use `Microsoft.Graph` v5+ with `GraphServiceClient`. Do not use raw HTTP calls to Graph API.
4. **Error handling** — all Graph operations must return `Result<T>` or `Result`. Never throw from identity operations.
5. **Scoped registration** — register identity services as `Scoped`, not `Singleton`, to support per-request credential resolution in multi-tenant scenarios.
6. **Token relay** — the Gateway handles authentication token validation. Identity management services are for admin-side operations (user creation, role assignment), not for request authentication.
7. **Placeholder tokens** — see [placeholder-tokens.md](../placeholder-tokens.md) for all token definitions.

## Verification

1. `dotnet build src/Infrastructure/{Project}.Infrastructure.EntraExt/` — confirm clean build (if created)
2. `dotnet build src/Infrastructure/{Project}.Infrastructure.Graph/` — confirm clean build (if created)
3. Verify no references to Domain.Model or Application.Services from identity projects
4. Verify `Microsoft.Graph` NuGet is declared in `Directory.Packages.props`
5. Verify settings POCOs match the configuration section structure
