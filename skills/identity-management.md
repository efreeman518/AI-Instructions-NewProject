# Identity Management

Use this skill when domain inputs enable `authProvider` and the solution needs authentication or identity-backed user management. **This skill is applied as the final phase (Phase 4f)** — earlier phases use auth stubs so the project compiles and runs without identity configuration.

Reference implementation: `sampleapp/src/TaskFlow/TaskFlow.Api/Auth/`, `sampleapp/src/TaskFlow/TaskFlow.Gateway/Auth/`.

## Identity Provider Scenarios

Prompt the user at the start of this phase to select the appropriate scenario:

| Scenario | Provider(s) | Typical use |
|---|---|---|
| Enterprise / internal users | Microsoft Entra ID | Internal apps, admin portals, SSO, conditional access, group-based roles |
| External / consumer users | Microsoft Entra External ID, Google, Facebook, Apple, OAuth2/OIDC | Customer-facing apps, self-service portals |
| Hybrid | Entra ID + Entra External ID / social providers | Public UI for external users + enterprise back-office for internal users |

## Pre-Auth Stub Pattern (Phases 4a–4e)

Until this phase is reached, authentication must be **stubbed** so the project compiles and runs:

```csharp
// File: {Host}.Api/Auth/AuthStub.cs
// TODO: [CONFIGURE] Authentication — replace this stub with real identity provider configuration (see skills/identity-management.md)

public static class AuthStub
{
    public static IServiceCollection AddAuthStub(this IServiceCollection services)
    {
        // No-op auth — all endpoints accessible without authentication
        // Remove this stub and wire real auth in Phase 4f
        return services;
    }
}
```

- Register `builder.Services.AddAuthStub()` in the host's `Program.cs`
- Do **not** add `[Authorize]` attributes or `RequireAuthorization()` until real auth is wired
- All endpoints should work without authentication during development

## Projects

- External user administration: `src/Infrastructure/{Project}.Infrastructure.EntraExt`
- Enterprise Graph access: `src/Infrastructure/{Project}.Infrastructure.Graph`

---

## Auth Configuration

### Entra External ID (gateway)

```csharp
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

### Entra ID (enterprise)

```csharp
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
            RoleClaimType = "roles"
        };
    });
```

---

## Service Contracts

### External user admin

```csharp
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

### Enterprise Graph access

```csharp
public interface IGraphService
{
    Task<Result<EnterpriseUserInfo>> GetUserAsync(string userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EnterpriseUserInfo>>> SearchUsersAsync(string query, CancellationToken ct = default);
    Task<Result<IReadOnlyList<string>>> GetUserGroupsAsync(string userId, CancellationToken ct = default);
    Task<Result<byte[]>> GetUserPhotoAsync(string userId, CancellationToken ct = default);
}
```

Implementation rule: return `Result`/`Result<T>` from all Graph operations; do not leak exceptions.

---

## Graph Client + DI

```csharp
services.Configure<EntraExtServiceSettings>(configuration.GetSection("EntraExt"));

var entra = configuration.GetSection("EntraExt").Get<EntraExtServiceSettings>()!;
var entraCredential = new ClientSecretCredential(entra.TenantId, entra.ClientId, entra.ClientSecret);
services.AddSingleton(new GraphServiceClient(entraCredential));
services.AddScoped<IEntraExtService, EntraExtService>();

services.Configure<GraphServiceSettings>(configuration.GetSection("Graph"));

var graph = configuration.GetSection("Graph").Get<GraphServiceSettings>()!;
var graphCredential = new ClientSecretCredential(graph.TenantId, graph.ClientId, graph.ClientSecret);
services.AddSingleton(new GraphServiceClient(graphCredential));
services.AddScoped<IGraphService, GraphService>();
```

NuGet:

```xml
<PackageReference Include="Microsoft.Graph" />
<PackageReference Include="Azure.Identity" />
```

---

## Configuration

```json
{
  "EntraExt": {
    "TenantId": "{{from-keyvault}}",
    "TenantDomain": "contoso.onmicrosoft.com",
    "ClientId": "{{from-keyvault}}",
    "ClientSecret": "{{from-keyvault}}",
    "ServicePrincipalId": "{{from-keyvault}}"
  },
  "Graph": {
    "TenantId": "{{from-keyvault}}",
    "ClientId": "{{from-keyvault}}",
    "ClientSecret": "{{from-keyvault}}"
  }
}
```

Rule: secrets come from Key Vault/User Secrets only.

---

## Rules

1. Identity infrastructure projects stay infrastructure-only (no Domain/Application references).
2. Gateway handles token validation; Graph services handle admin/user management operations.
3. Register identity services as `Scoped`.
4. Use `Microsoft.Graph` v5+ with `GraphServiceClient`.
5. In lite mode, skip identity infrastructure and use a local/mock request context.
6. For regulated/sensitive classifications, enforce least-privilege roles and trace access decisions with auditable correlation IDs.

## Verification

- [ ] `Infrastructure.EntraExt` and/or `Infrastructure.Graph` builds cleanly
- [ ] `Microsoft.Graph` and `Azure.Identity` are in `Directory.Packages.props`
- [ ] Settings POCOs match configuration sections
- [ ] No hardcoded secrets
