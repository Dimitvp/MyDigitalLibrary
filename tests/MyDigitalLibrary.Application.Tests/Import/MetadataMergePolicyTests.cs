using FluentAssertions;
using MyDigitalLibrary.Application.Import;

namespace MyDigitalLibrary.Application.Tests.Import;

public class MetadataMergePolicyTests
{
    [Fact]
    public void Merge_of_no_candidates_returns_null()
    {
        MetadataMergePolicy.Merge([]).Should().BeNull();
    }

    [Fact]
    public void Merge_of_one_candidate_returns_it_unchanged()
    {
        var candidate = Candidate("open-library", title: "Dune");

        MetadataMergePolicy.Merge([candidate]).Should().Be(candidate);
    }

    [Fact]
    public void Earlier_candidate_wins_a_field_both_provide()
    {
        var first = Candidate("open-library", title: "Dune (Open Library)");
        var second = Candidate("google-books", title: "Dune (Google Books)");

        var merged = MetadataMergePolicy.Merge([first, second]);

        merged!.Title.Should().Be("Dune (Open Library)");
    }

    [Fact]
    public void A_later_candidate_fills_a_field_the_earlier_one_left_empty()
    {
        var first = Candidate("open-library", title: "Dune", description: null);
        var second = Candidate("google-books", title: "Dune (Google Books)", description: "A desert planet epic.");

        var merged = MetadataMergePolicy.Merge([first, second]);

        merged!.Title.Should().Be("Dune", "the earlier provider's non-empty field always wins");
        merged.Description.Should().Be("A desert planet epic.", "the earlier provider left this field empty");
    }

    [Fact]
    public void Empty_author_list_does_not_win_over_a_later_non_empty_one()
    {
        var first = Candidate("open-library", authorNames: []);
        var second = Candidate("google-books", authorNames: ["Frank Herbert"]);

        var merged = MetadataMergePolicy.Merge([first, second]);

        merged!.AuthorNames.Should().BeEquivalentTo(["Frank Herbert"]);
    }

    private static BookMetadataCandidate Candidate(
        string providerKey, string? title = "Dune", string? description = null, IReadOnlyList<string>? authorNames = null)
        => new(providerKey, title, null, authorNames ?? ["Frank Herbert"], null, null, null, null, description, null, null, null, []);
}
