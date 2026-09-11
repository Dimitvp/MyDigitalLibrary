using MyDigitalLibrary.Api.Filters;
using MyDigitalLibrary.Application.Abstractions;
using MyDigitalLibrary.Application.Quotes;

namespace MyDigitalLibrary.Api.Endpoints;

public static class QuoteEndpoints
{
    public static void MapQuoteEndpoints(this IEndpointRouteBuilder app)
    {
        var forWork = app.MapGroup("/api/v1/works/{workId:guid}/quotes").WithTags("Quotes").RequireAuthorization();

        forWork.MapGet("/", async (Guid workId, QuoteService service, ICurrentUser currentUser, CancellationToken ct)
            => Results.Ok(await service.ListForWorkAsync(workId, currentUser.UserId, ct)));

        forWork.MapPost("/", async (Guid workId, CreateQuoteRequest request, QuoteService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            var quote = await service.CreateAsync(workId, request, currentUser.UserId, ct);
            return Results.Created($"/api/v1/quotes/{quote.Id}", quote);
        }).AddEndpointFilter<AntiforgeryFilter>();

        var byId = app.MapGroup("/api/v1/quotes").WithTags("Quotes").RequireAuthorization();

        byId.MapPut("/{id:guid}", async (Guid id, UpdateQuoteRequest request, QuoteService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.UpdateAsync(id, request, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();

        byId.MapDelete("/{id:guid}", async (Guid id, QuoteService service, ICurrentUser currentUser, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, currentUser.UserId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();
    }
}
