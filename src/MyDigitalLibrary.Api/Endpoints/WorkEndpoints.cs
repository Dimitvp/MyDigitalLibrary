using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Catalog;
using MyDigitalLibrary.Application.Works;

namespace MyDigitalLibrary.Api.Endpoints;

public static class WorkEndpoints
{
    public static void MapWorkEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/works").WithTags("Works").RequireAuthorization();

        group.MapGet("/", async (WorkService service, string? q, Guid? authorId, Guid? seriesId, int? page, int? pageSize, CancellationToken ct)
            => Results.Ok(await service.ListAsync(q, authorId, seriesId, page, pageSize, ct)));

        group.MapGet("/{id:guid}", async (Guid id, WorkService service, CancellationToken ct)
            => Results.Ok(await service.GetAsync(id, ct)));

        group.MapPut("/{id:guid}", async (Guid id, UpdateWorkRequest request, WorkService service, CancellationToken ct) =>
        {
            await service.UpdateAsync(id, request, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapGet("/{id:guid}/editions", async (Guid id, WorkService service, CancellationToken ct)
            => Results.Ok(await service.ListEditionsAsync(id, ct)));

        group.MapPost("/{id:guid}/editions", async (Guid id, CreateEditionRequest request, WorkService service, CancellationToken ct) =>
        {
            var edition = await service.AddEditionAsync(id, request, ct);
            return Results.Created($"/api/v1/works/{id}/editions/{edition.Id}", edition);
        }).AddEndpointFilter<AntiforgeryFilter>();
    }
}
