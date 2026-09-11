using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Import;

namespace MyDigitalLibrary.Api.Endpoints;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/import").WithTags("Import").RequireAuthorization();

        // Plan section 4: "import by link doesn't get its own save endpoints — it
        // assembles the body" for the ordinary composite POST /library-items /
        // POST /wishlist. This endpoint is read-only: it never touches the catalog.
        group.MapPost("/lookup", async (ImportLookupRequest request, ImportLookupService service, CancellationToken ct)
            => Results.Ok(await service.LookupAsync(request, ct))).AddEndpointFilter<AntiforgeryFilter>();
    }
}
