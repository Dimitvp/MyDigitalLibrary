using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Bookstores;

namespace MyDigitalLibrary.Api.Endpoints;

public static class BookstoreListingEndpoints
{
    public static void MapBookstoreListingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/editions/{editionId:guid}/listings").WithTags("Bookstores").RequireAuthorization();

        group.MapGet("/", async (Guid editionId, BookstoreListingService service, CancellationToken ct)
            => Results.Ok(await service.ListForEditionAsync(editionId, ct)));

        group.MapPost("/", async (Guid editionId, CreateBookstoreListingRequest request, BookstoreListingService service, CancellationToken ct) =>
        {
            var listing = await service.AddManualAsync(editionId, request, ct);
            return Results.Created($"/api/v1/editions/{editionId}/listings/{listing.Id}", listing);
        }).AddEndpointFilter<AntiforgeryFilter>();

        // Plan section 6.3: the manual override always works, even with every adapter disabled.
        group.MapPatch("/{listingId:guid}/discontinued", async (Guid editionId, Guid listingId, BookstoreListingService service, CancellationToken ct) =>
        {
            await service.MarkDiscontinuedAsync(listingId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();
    }
}
