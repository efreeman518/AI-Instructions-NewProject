# Gateway (YARP)

## Overview

The Gateway project is a **YARP reverse proxy** that sits in front of the API. It handles user-facing authentication (Entra External/B2C), CORS, token relay (acquires service-to-service tokens for downstream calls), and forwards original user claims to the API.

> **UI-Framework Agnostic:** The Gateway is a pure reverse proxy — it serves any front-end client (SPA, mobile app, desktop app) that can issue HTTP requests with a Bearer token. The planned UI framework is **Uno Platform** (see [uno-ui.md](uno-ui.md)), but the Gateway requires zero changes to support a different front-end technology.

## Project Structure

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Gateway/`

```
{Gateway}.Gateway/
├── Program.cs
├── RegisterServices.cs
├── WebApplicationBuilderExtensions.cs
├── TokenService.cs
├── Auth/
│   ├── GatewayClaimsTransformer.cs
│   ├── GatewayClaimsPayload.cs
│   ├── GatewayClaimsTransformSettings.cs
│   └── TenantMatchHandler.cs
├── ExceptionHandlers/
├── HealthChecks/
│   └── AggregateGatewayHealthCheck.cs
├── StartupTasks/
│   └── WarmupDependencies.cs
├── appsettings.json
└── Dockerfile
```

## YARP Configuration (appsettings.json)

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Gateway/appsettings.json`

```json
{
  "ReverseProxy": {
    "Routes": {
      "api-route": {
        "ClusterId": "api-cluster",
        "Match": { "Path": "api/{**catch-all}" },
        "Transforms": [{ "PathRemovePrefix": "/api" }]
      }
    },
    "Clusters": {
      "api-cluster": {
        "Destinations": {
          "api": { "Address": "https://localhost:7065" }
        }
      }
    }
  }
}
```

With Aspire service discovery, destination addresses are resolved automatically at runtime.

## YARP Registration with Token Relay

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Gateway/RegisterServices.cs` and `sampleapp/src/TaskFlow/TaskFlow.Gateway/TokenService.cs`

```csharp
// Compact pattern — see sampleapp for full implementation
private static void AddReverseProxy(IServiceCollection services, IConfiguration config)
{
    services.AddSingleton<TokenService>();
    services.AddReverseProxy()
        .LoadFromConfig(config.GetSection("ReverseProxy"))
        .AddTransforms(ConfigureProxyTransforms)
        .AddServiceDiscoveryDestinationResolver();
}

// Token relay: acquire service token + forward original user claims
private static void ConfigureProxyTransforms(TransformBuilderContext context)
{
    context.AddRequestTransform(async ctx =>
    {
        AddOriginalUserClaimsHeader(ctx);  // X-Orig-Request header
        var token = await tokenService.GetAccessTokenAsync(clusterId);
        ctx.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    });
}
```

## TokenService

Acquires client credential tokens for service-to-service auth:

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Gateway/TokenService.cs`

```csharp
// Compact pattern — caches tokens with expiry buffer
public async Task<string> GetAccessTokenAsync(string clusterId)
{
    if (_cache.TryGetValue(clusterId, out var cached) && cached.Expiry > DateTimeOffset.UtcNow.AddMinutes(5))
        return cached.Token;
    var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
    var tokenResult = await credential.GetTokenAsync(new TokenRequestContext([scope]));
    _cache[clusterId] = (tokenResult.Token, tokenResult.ExpiresOn);
    return tokenResult.Token;
}
```

## Authentication (User-Facing)

The Gateway authenticates users (e.g., via Entra External/B2C), while the downstream API uses service-to-service Entra ID tokens:

```
User → [B2C/Entra External Token] → Gateway → [Client Credential Token + X-Orig-Request] → API
```

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Gateway/RegisterServices.cs` (auth registration), `sampleapp/src/TaskFlow/TaskFlow.Gateway/Auth/GatewayClaimsTransformer.cs` (claims transformation), and `sampleapp/src/TaskFlow/TaskFlow.Api/Auth/GatewayClaimsTransformer.cs` (API-side claims deserialization).

```csharp
// Compact pattern — see sampleapp for full implementation
private static void AddAuthentication(IServiceCollection services, IConfiguration config)
{
    services.AddAuthentication(options => { options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme; })
        .AddMicrosoftIdentityWebApi(config.GetSection("Gateway_EntraExt"));
    services.AddSingleton<IAuthorizationHandler, TenantMatchHandler>();
    services.AddTransient<IClaimsTransformation, GatewayClaimsTransformer>();
}
```

## Pipeline Configuration

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Gateway/WebApplicationBuilderExtensions.cs`

```csharp
// Compact pattern — pipeline order matters
public static WebApplication ConfigurePipeline(this WebApplication app)
{
    ConfigureSecurity(app);           // HTTPS redirect
    ConfigureCors(app);               // CORS policy
    ConfigureMiddleware(app);         // Routing → RateLimiter → Auth
    ConfigureEndpoints(app);          // Health, liveness
    ConfigureReverseProxy(app);       // YARP proxy with error logging
    return app;
}
```

## Health Checks

Gateway aggregates health from downstream services:

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Gateway/HealthChecks/AggregateGatewayHealthCheck.cs`

## Startup Tasks

Pre-warm YARP cluster tokens and downstream dependencies before accepting traffic:

> **Reference implementation:** See `sampleapp/src/TaskFlow/TaskFlow.Gateway/StartupTasks/WarmupDependencies.cs`

## Gateway vs. API Auth Summary

| Concern | Gateway | API |
|---------|---------|-----|
| Auth scheme | Entra External / B2C | Entra ID (client credentials) |
| Token source | User browser | Gateway's TokenService |
| User identity | In JWT claims | In X-Orig-Request header |
| Tenant check | TenantMatchPolicy | TenantBoundaryValidator (service layer) |
| CORS | Configured here | Not needed (internal) |

---

## Path Prefix Normalization for UI Clients

When the Gateway uses YARP with a `PathRemovePrefix` transform, the effective request path seen by the backend differs from what the UI client sends. This is a common source of routing bugs.

### How YARP PathRemovePrefix Works

Given this YARP configuration:

```json
{
  "Routes": {
    "api-route": {
      "Match": { "Path": "api/{**catch-all}" },
      "Transforms": [{ "PathRemovePrefix": "/api" }]
    }
  }
}
```

The Gateway strips `/api` from the incoming path before forwarding to the backend:

| Client sends | Gateway strips | Backend receives |
|-------------|---------------|-----------------|
| `/api/v1/todoitems` | `/api` | `/v1/todoitems` |
| `/api/todoitems` | `/api` | `/todoitems` |

### The Double-Prefix Problem

If the UI client **also** prepends `/api` in its base path configuration, and the backend endpoints are routed at `/api/v1/...`, the resulting request path becomes:

```
UI sends:      /api/api/v1/todoitems
Gateway strips: /api
Backend sees:   /api/v1/todoitems  ✅ (works, but the client path looks wrong)
```

If the UI only sends `/api/v1/todoitems`:

```
UI sends:      /api/v1/todoitems
Gateway strips: /api
Backend sees:   /v1/todoitems  ❌ (404 — backend expects /api/v1/...)
```

### Recommendation

Define **one** configurable UI setting for the Gateway API base path (e.g., `GatewayApiBasePath`). Keep a single normalized path in config rather than probing multiple prefixes at runtime.

- If the backend expects requests at `/api/v1/...` and the Gateway strips `/api`, the UI should send requests to `/api/api/v1/...` — or adjust the backend route prefix so stripping `/api` produces the correct downstream path.
- The cleanest pattern: backend routes at `/v1/{entity}` (no `/api` prefix), Gateway matches `api/{**catch-all}` and strips `/api`, UI sends `/api/v1/{entity}`.
- Document the chosen path convention in `appsettings.json` and avoid dual-prefix probing logic in production code.

---

## Verification

After generating the Gateway project, confirm:

- [ ] `RegisterServices.cs` registers YARP, auth, CORS, and health checks
- [ ] `WebApplicationBuilderExtensions.cs` pipeline order: HTTPS → Config → CORS → Routing → RateLimiter → Auth → Endpoints → ReverseProxy
- [ ] `TokenService` acquires client-credential tokens and caches them with expiry buffer
- [ ] `ConfigureProxyTransforms` adds `X-Orig-Request` header with user claims for downstream API
- [ ] `appsettings.json` has routes/clusters for API and Scheduler (addresses resolved by Aspire service discovery at runtime)
- [ ] `AggregateGatewayHealthCheck` checks downstream `/alive` endpoints
- [ ] CORS origins configured for the UI project's local and deployed URLs
- [ ] Auth section name matches Entra External (`Gateway_EntraExt`) or Entra ID (`Gateway_EntraID`) config
- [ ] Cross-references: [aspire.md](aspire.md) `WaitFor(api)` on gateway, [iac.md](iac.md) Container App for gateway
