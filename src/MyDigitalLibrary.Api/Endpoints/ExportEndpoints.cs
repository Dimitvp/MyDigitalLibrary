using System.Text;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Export;

namespace MyDigitalLibrary.Api.Endpoints;

public static class ExportEndpoints
{
    public static void MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/export", async (string? format, ExportService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            var archive = await service.BuildArchiveAsync(currentUser.UserId, ct);

            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = ExportCsvFormatter.ToCsv(archive.LibraryItems);
                return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", "library-export.csv");
            }

            return Results.Ok(archive);
        }).WithTags("Export").RequireAuthorization();
    }
}
