using FluentAssertions;
using MyDigitalLibrary.Domain.Catalog;
using MyDigitalLibrary.Domain.Enums;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Tests.Catalog;

public class BookstoreListingTests
{
    private static readonly Uri ListingUrl = new("https://example-bookstore.test/book/123");

    [Fact]
    public void RecordSuccessfulCheck_updates_availability_price_and_resets_failures()
    {
        var listing = new BookstoreListing(Guid.NewGuid(), Guid.NewGuid(), ListingUrl);
        var price = new Money(19.99m, "BGN");
        var checkedAt = DateTimeOffset.UtcNow;

        listing.RecordSuccessfulCheck(MarketAvailability.InStock, price, checkedAt);

        listing.Availability.Should().Be(MarketAvailability.InStock);
        listing.Price.Should().Be(price);
        listing.LastCheckedAt.Should().Be(checkedAt);
        listing.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void RecordSuccessfulCheck_appends_a_price_history_entry_when_a_price_is_given()
    {
        var listing = new BookstoreListing(Guid.NewGuid(), Guid.NewGuid(), ListingUrl);
        var price = new Money(19.99m, "BGN");

        listing.RecordSuccessfulCheck(MarketAvailability.InStock, price, DateTimeOffset.UtcNow);

        listing.PriceHistory.Should().ContainSingle(e => e.Price == price);
    }

    [Fact]
    public void RecordSuccessfulCheck_does_not_share_the_same_money_instance_between_listing_price_and_history()
    {
        // EF Core's owned-type tracking expects each owned Money instance to
        // belong to exactly one owner (BookstoreListing.Price vs.
        // PriceHistoryEntry.Price are separate owned slots) — sharing one CLR
        // reference between them produced a real "tracked as different entity
        // types" warning when verified against a live deployment.
        var listing = new BookstoreListing(Guid.NewGuid(), Guid.NewGuid(), ListingUrl);
        var price = new Money(19.99m, "BGN");

        listing.RecordSuccessfulCheck(MarketAvailability.InStock, price, DateTimeOffset.UtcNow);

        var historyPrice = listing.PriceHistory.Single().Price;
        ReferenceEquals(listing.Price, historyPrice).Should().BeFalse();
        historyPrice.Should().Be(listing.Price, "still equal by value — Money is a record");
    }

    [Fact]
    public void RecordSuccessfulCheck_with_no_price_does_not_append_price_history()
    {
        var listing = new BookstoreListing(Guid.NewGuid(), Guid.NewGuid(), ListingUrl);

        listing.RecordSuccessfulCheck(MarketAvailability.Unknown, null, DateTimeOffset.UtcNow);

        listing.PriceHistory.Should().BeEmpty();
    }

    [Fact]
    public void RecordFailedCheck_never_touches_availability()
    {
        var listing = new BookstoreListing(Guid.NewGuid(), Guid.NewGuid(), ListingUrl);
        listing.RecordSuccessfulCheck(MarketAvailability.InStock, new Money(10m, "BGN"), DateTimeOffset.UtcNow);

        listing.RecordFailedCheck(DateTimeOffset.UtcNow);

        listing.Availability.Should().Be(MarketAvailability.InStock, "a scrape failure must never look like OutOfStock");
    }

    [Fact]
    public void RecordFailedCheck_increments_consecutive_failures()
    {
        var listing = new BookstoreListing(Guid.NewGuid(), Guid.NewGuid(), ListingUrl);

        listing.RecordFailedCheck(DateTimeOffset.UtcNow);
        listing.RecordFailedCheck(DateTimeOffset.UtcNow);

        listing.ConsecutiveFailures.Should().Be(2);
    }

    [Fact]
    public void A_successful_check_resets_consecutive_failures_to_zero()
    {
        var listing = new BookstoreListing(Guid.NewGuid(), Guid.NewGuid(), ListingUrl);
        listing.RecordFailedCheck(DateTimeOffset.UtcNow);
        listing.RecordFailedCheck(DateTimeOffset.UtcNow);

        listing.RecordSuccessfulCheck(MarketAvailability.InStock, null, DateTimeOffset.UtcNow);

        listing.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void MarkUnknownDueToRepeatedFailures_sets_availability_to_unknown_without_touching_the_failure_count()
    {
        var listing = new BookstoreListing(Guid.NewGuid(), Guid.NewGuid(), ListingUrl);
        listing.RecordSuccessfulCheck(MarketAvailability.InStock, null, DateTimeOffset.UtcNow);
        listing.RecordFailedCheck(DateTimeOffset.UtcNow);
        listing.RecordFailedCheck(DateTimeOffset.UtcNow);

        listing.MarkUnknownDueToRepeatedFailures();

        listing.Availability.Should().Be(MarketAvailability.Unknown);
        listing.ConsecutiveFailures.Should().Be(2);
    }

    [Fact]
    public void SetDiscontinuedManually_works_regardless_of_prior_state()
    {
        var listing = new BookstoreListing(Guid.NewGuid(), Guid.NewGuid(), ListingUrl);

        listing.SetDiscontinuedManually();

        listing.Availability.Should().Be(MarketAvailability.Discontinued);
    }

    [Fact]
    public void A_new_listing_starts_with_unknown_availability()
    {
        var listing = new BookstoreListing(Guid.NewGuid(), Guid.NewGuid(), ListingUrl);

        listing.Availability.Should().Be(MarketAvailability.Unknown);
        listing.PriceHistory.Should().BeEmpty();
    }
}
