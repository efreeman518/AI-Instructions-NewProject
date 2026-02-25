# Identity Management

Use this skill when domain inputs enable `authProvider` and the solution needs Entra-based authentication or Graph-backed user management.

Reference implementation: `sampleapp/src/TaskFlow/TaskFlow.Api/Auth/`, `sampleapp/src/TaskFlow/TaskFlow.Gateway/Auth/`.

## Modes

| Scenario | Provider | Typical use |
|---|---|---|
| Public-facing users | Entra External ID | Customer/self-service portals |
| Enterprise/internal users | Entra ID | Internal apps and admin portals |
| Mixed | Both | Public UI + enterprise back-office |

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

## Verification

- [ ] `Infrastructure.EntraExt` and/or `Infrastructure.Graph` builds cleanly
- [ ] `Microsoft.Graph` and `Azure.Identity` are in `Directory.Packages.props`
- [ ] Settings POCOs match configuration sections
- [ ] No hardcoded secrets
