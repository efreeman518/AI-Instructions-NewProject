# Gateway (YARP)

## Purpose

Gateway is a YARP reverse proxy in front of API/backends. It handles user-facing auth, CORS, downstream token relay, and forwarding original user claims.

## Non-Negotiables

1. Keep proxy routes/clusters in configuration and load through YARP.
2. Relay service-to-service bearer token per cluster via `TokenService`.
3. Forward original user claims metadata (`X-Orig-Request`) for downstream context.
4. Keep pipeline order deterministic (security -> routing/auth -> endpoints -> proxy).
5. Normalize path prefixes consistently between UI, gateway transforms, and backend routes.

Reference implementation: `sampleapp/src/TaskFlow/TaskFlow.Gateway/`.

---

## Project Shape

```
{Gateway}.Gateway/
├── Program.cs
├── RegisterServices.cs
├── WebApplicationBuilderExtensions.cs
├── TokenService.cs
├── Auth/
├── HealthChecks/
├── StartupTasks/
├── appsettings.json
└── Dockerfile
```

---

## YARP Configuration

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

With Aspire, destination resolution can be service-discovery driven.

---

## Service Registration Pattern

```csharp
private static void AddReverseProxy(IServiceCollection services, IConfiguration config)
{
    services.AddSingleton<TokenService>();

    services.AddReverseProxy()
        .LoadFromConfig(config.GetSection("ReverseProxy"))
        .AddTransforms(ConfigureProxyTransforms)
        .AddServiceDiscoveryDestinationResolver();
}
```

Transform pattern:

```csharp
private static void ConfigureProxyTransforms(TransformBuilderContext context)
{
    context.AddRequestTransform(async ctx =>
    {
        AddOriginalUserClaimsHeader(ctx);
        var token = await tokenService.GetAccessTokenAsync(clusterId);
        ctx.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    });
}
```

---

## TokenService Contract

`TokenService` acquires and caches client-credential tokens per cluster with an expiry buffer.

```csharp
public async Task<string> GetAccessTokenAsync(string clusterId)
{
    if (_cache.TryGetValue(clusterId, out var cached) && cached.Expiry > DateTimeOffset.UtcNow.AddMinutes(5))
        return cached.Token;

    var tokenResult = await credential.GetTokenAsync(new TokenRequestContext([scope]));
    _cache[clusterId] = (tokenResult.Token, tokenResult.ExpiresOn);
    return tokenResult.Token;
}
```

---

## Authentication Model

Typical split:

- Gateway authenticates user token (for example Entra External/B2C).
- Gateway acquires service token for downstream API.
- API receives gateway service token + forwarded user claims payload.

```csharp
private static void AddAuthentication(IServiceCollection services, IConfiguration config)
{
    services.AddAuthentication(options =>
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddMicrosoftIdentityWebApi(config.GetSection("Gateway_EntraExt"));

    services.AddSingleton<IAuthorizationHandler, TenantMatchHandler>();
    services.AddTransient<IClaimsTransformation, GatewayClaimsTransformer>();
}
```

---

## Pipeline Order

```csharp
public static WebApplication ConfigurePipeline(this WebApplication app)
{
    ConfigureSecurity(app);
    ConfigureCors(app);
    ConfigureMiddleware(app);   // routing, limiter, auth
    ConfigureEndpoints(app);    // health/liveness
    ConfigureReverseProxy(app);
    return app;
}
```

Order matters; proxy should execute after auth/routing middleware is ready.

---

## Path Prefix Normalization Rule

When using `PathRemovePrefix`, ensure UI base path + gateway transform + backend route prefix are aligned.

If gateway strips `/api`:

- client `/api/v1/todoitems` -> backend `/v1/todoitems`

Pick one convention and keep it consistent. Avoid dual-prefix probing logic in production.

---

## Health and Startup Tasks

- Add aggregated downstream health checks.
- Add startup warmup tasks for token acquisition/dependency checks before live traffic.

---

## Verification

- [ ] YARP routes/clusters load from config
- [ ] transform adds original-user header + downstream bearer token
- [ ] `TokenService` caches cluster tokens with expiry buffer
- [ ] gateway auth section matches intended identity provider config
- [ ] pipeline order is security -> middleware -> endpoints -> reverse proxy
- [ ] CORS origins match UI local/deployed origins
- [ ] path-prefix convention is documented and consistent across UI/gateway/API
- [ ] health checks and startup warmup are registered
- [ ] cross-check with [aspire.md](aspire.md) and [iac.md](iac.md)