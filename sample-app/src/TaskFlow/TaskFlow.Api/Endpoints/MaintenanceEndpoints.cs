namespace TaskFlow.Api.Endpoints;

public static class MaintenanceEndpoints
{
    public static RouteGroupBuilder MapMaintenanceEndpoints(this RouteGroupBuilder group)
    {
        var maintGroup = group.MapGroup("/maintenance").WithTags("Maintenance");

        maintGroup.MapPost("/purge-history", async (int? retentionDays, IMaintenanceService service, CancellationToken ct) =>
        {
            var result = await service.PurgeHistoryAsync(retentionDays ?? 90, ct);
            return result.Match<IResult>(
                count => Results.Ok(new { PurgedCount = count }),
                errors => Results.BadRequest(errors),
                () => Results.NotFound());
        });

        return group;
    }
}
