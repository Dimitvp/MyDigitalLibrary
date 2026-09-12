using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Editions;

namespace MyDigitalLibrary.Api.Endpoints;

public static class EditionEndpoints
{
    public static void MapEditionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/editions").WithTags("Editions").RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, EditionService service, CancellationToken ct)
            => Results.Ok(await service.GetAsync(id, ct)));

        group.MapPut("/{id:guid}", async (Guid id, UpdateEditionRequest request, EditionService service, CancellationToken ct) =>
        {
            await service.UpdateAsync(id, request, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        group.MapPost("/{id:guid}/cover", async (Guid id, IFormFile file, EditionService service, CancellationToken ct) =>
        {
            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, ct);
            var coverImageUrl = await service.UploadCoverAsync(id, stream.ToArray(), file.ContentType, ct);
            return Results.Ok(new { coverImageUrl });
        }).DisableAntiforgery().AddEndpointFilter<AntiforgeryFilter>();
    }
}
