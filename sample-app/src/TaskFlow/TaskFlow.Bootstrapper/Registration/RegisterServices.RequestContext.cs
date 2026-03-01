using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using EF.Common.Contracts;
using System.Security.Claims;

namespace TaskFlow.Bootstrapper;

public static partial class RegisterServices
{
    private static void AddRequestContextServices(IServiceCollection services)
    {
        services.AddScoped<IRequestContext<string, Guid?>>(provider =>
        {
            var httpContext = provider.GetService<IHttpContextAccessor>()?.HttpContext;
            var correlationId = Guid.NewGuid().ToString();
            if (httpContext != null)
            {
                var headers = httpContext.Request?.Headers;
                if (headers != null && headers.TryGetValue("X-Correlation-ID", out var headerValues))
                {
                    var headerValue = headerValues.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(headerValue))
                    {
                        correlationId = headerValue;
                    }
                }
            }

            if (httpContext == null)
            {
                return new RequestContext<string, Guid?>(correlationId, $"BackgroundService-{correlationId}", null, []);
            }

            var user = httpContext.User;
            var auditId = user.Claims.FirstOrDefault(c => c.Type == "oid")?.Value
                ?? user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
                ?? user.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                ?? "NoAuditClaim";

            var tenantIdClaim = user.Claims.FirstOrDefault(c => c.Type == "userTenantId")?.Value;
            var tenantId = Guid.TryParse(tenantIdClaim, out var tenantGuid) ? tenantGuid : (Guid?)null;
            var rolesList = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();

            return new RequestContext<string, Guid?>(correlationId, auditId, tenantId, rolesList);
        });
    }
}
