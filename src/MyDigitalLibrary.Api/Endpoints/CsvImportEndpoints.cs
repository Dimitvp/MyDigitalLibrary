using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Import.Csv;

namespace MyDigitalLibrary.Api.Endpoints;

public static class CsvImportEndpoints
{
    public static void MapCsvImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/import").WithTags("Import").RequireAuthorization();

        // .DisableAntiforgery() turns off ASP.NET Core's automatic antiforgery
        // requirement for form-bound endpoints (IFormFile triggers it) — this
        // app validates antiforgery uniformly via AntiforgeryFilter on every
        // mutating endpoint instead, not a mix of two mechanisms.
        group.MapPost("/csv", async (IFormFile file, CsvImportService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var job = await service.ImportAsync(stream, file.FileName, currentUser.UserId, ct);
            return Results.Created($"/api/v1/import/jobs/{job.Id}", job);
        }).DisableAntiforgery().AddEndpointFilter<AntiforgeryFilter>();

        group.MapGet("/jobs/{id:guid}", async (Guid id, CsvImportService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.GetJobAsync(id, currentUser.UserId, ct)));
    }
}
