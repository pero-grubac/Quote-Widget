using System.Text.Json;
using QuoteWidget.Models;
using QuoteWidget.Services;

namespace QuoteWidget.Tests;

public class QuoteStoreTests : IDisposable
{
    private readonly string _quotesFilePath;
    private readonly List<string> _tempFiles = new();

    public QuoteStoreTests()
    {
        _quotesFilePath = NewTempFilePath();
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private string NewTempFilePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"quotewidget-test-{Guid.NewGuid()}.json");
        _tempFiles.Add(path);
        return path;
    }

    private QuoteStore CreateStoreWithQuotes(IEnumerable<Quote> quotes)
    {
        File.WriteAllText(_quotesFilePath, JsonSerializer.Serialize(quotes));
        return new QuoteStore(_quotesFilePath);
    }

    [Fact]
    public void Load_CreatesSampleQuotes_WhenFileDoesNotExist()
    {
        var store = new QuoteStore(_quotesFilePath);

        Assert.True(store.Quotes.Count > 0);
        Assert.True(File.Exists(_quotesFilePath));
    }

    [Fact]
    public void Load_RecreatesDefaults_WhenFileIsCorrupt()
    {
        File.WriteAllText(_quotesFilePath, "{ this is not valid json ");

        var store = new QuoteStore(_quotesFilePath);

        Assert.True(store.Quotes.Count > 0);
    }

    [Fact]
    public void Import_AddsNewQuotes_AndSkipsDuplicates()
    {
        var store = CreateStoreWithQuotes(new List<Quote>
        {
            new() { Text = "Quote A", Movie = "Movie A", Year = 2000 },
        });

        var importFilePath = NewTempFilePath();
        File.WriteAllText(importFilePath, JsonSerializer.Serialize(new List<Quote>
        {
            new() { Text = "Quote A", Movie = "Movie A", Year = 2000 }, // duplicate of existing
            new() { Text = "Quote B", Movie = "Movie B", Year = 2010 }, // new
        }));

        var result = store.Import(importFilePath);

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(2, store.Quotes.Count);
    }

    [Fact]
    public void Import_DuplicateDetection_IsCaseInsensitiveAndIgnoresSurroundingWhitespace()
    {
        var store = CreateStoreWithQuotes(new List<Quote>
        {
            new() { Text = "Say hello to my little friend!", Movie = "Scarface" },
        });

        var importFilePath = NewTempFilePath();
        File.WriteAllText(importFilePath, JsonSerializer.Serialize(new List<Quote>
        {
            new() { Text = "  SAY HELLO TO MY LITTLE FRIEND!  ", Movie = " scarface " },
        }));

        var result = store.Import(importFilePath);

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Skipped);
        Assert.Single(store.Quotes);
    }

    [Fact]
    public void Import_Throws_WhenJsonIsInvalid()
    {
        var store = CreateStoreWithQuotes(new List<Quote> { new() { Text = "A", Movie = "B" } });

        var importFilePath = NewTempFilePath();
        File.WriteAllText(importFilePath, "this is not valid json");

        Assert.Throws<QuoteImportException>(() => store.Import(importFilePath));
    }

    [Fact]
    public void Import_Throws_WhenRequiredFieldsAreMissing()
    {
        var store = CreateStoreWithQuotes(new List<Quote> { new() { Text = "A", Movie = "B" } });

        var importFilePath = NewTempFilePath();
        File.WriteAllText(importFilePath, JsonSerializer.Serialize(new List<Quote>
        {
            new() { Text = "", Movie = "Movie without quote text" },
        }));

        Assert.Throws<QuoteImportException>(() => store.Import(importFilePath));
    }

    [Fact]
    public void GetRandom_NeverReturnsSameQuoteTwiceInARow()
    {
        var quotes = Enumerable.Range(1, 5)
            .Select(i => new Quote { Text = $"Quote {i}", Movie = $"Movie {i}" });
        var store = CreateStoreWithQuotes(quotes);

        Quote? previous = null;
        for (var i = 0; i < 200; i++)
        {
            var next = store.GetRandom(previous);

            Assert.NotNull(next);
            if (previous != null)
            {
                Assert.False(previous.Text == next!.Text && previous.Movie == next.Movie);
            }

            previous = next;
        }
    }

    [Fact]
    public void GetRandom_ReturnsTheOnlyQuote_WhenJustOneExists()
    {
        var store = CreateStoreWithQuotes(new List<Quote> { new() { Text = "Only", Movie = "Movie" } });

        var first = store.GetRandom(null);
        var second = store.GetRandom(first);

        Assert.Equal("Only", first!.Text);
        Assert.Equal("Only", second!.Text);
    }

    [Fact]
    public void GetRandom_ReturnsNull_WhenNoQuotesExist()
    {
        var store = CreateStoreWithQuotes(new List<Quote>());

        Assert.Null(store.GetRandom(null));
    }

    [Fact]
    public void AddUpdateDelete_ModifyTheQuoteListAndPersist()
    {
        var store = new QuoteStore(_quotesFilePath);
        var quote = new Quote { Text = "New", Movie = "New Movie" };

        store.Add(quote);
        Assert.Contains(store.Quotes, q => q.Text == "New");

        var updated = new Quote { Text = "Updated", Movie = "New Movie" };
        store.Update(quote, updated);
        Assert.Contains(store.Quotes, q => q.Text == "Updated");
        Assert.DoesNotContain(store.Quotes, q => q.Text == "New");

        store.Delete(updated);
        Assert.DoesNotContain(store.Quotes, q => q.Text == "Updated");

        var reloaded = new QuoteStore(_quotesFilePath);
        Assert.DoesNotContain(reloaded.Quotes, q => q.Text == "Updated");
    }

    [Fact]
    public void ContainsDuplicate_MatchesSameTextAndMovie_IgnoringCaseAndTheExceptedQuote()
    {
        var existing = new Quote { Text = "I'll be back.", Movie = "The Terminator" };
        var store = CreateStoreWithQuotes(new List<Quote> { existing });
        var stored = store.Quotes[0];

        Assert.True(store.ContainsDuplicate(new Quote { Text = " i'll BE back. ", Movie = "the terminator" }));
        Assert.False(store.ContainsDuplicate(new Quote { Text = "I'll be back.", Movie = "Terminator 2" }));

        // Editing a quote without changing its text/movie must not count as a duplicate of itself.
        Assert.False(store.ContainsDuplicate(new Quote { Text = "I'll be back.", Movie = "The Terminator" }, except: stored));
    }

    [Fact]
    public void Delete_ThenInsertAtReturnedIndex_RestoresOriginalPosition()
    {
        var store = CreateStoreWithQuotes(new List<Quote>
        {
            new() { Text = "A", Movie = "M" },
            new() { Text = "B", Movie = "M" },
            new() { Text = "C", Movie = "M" },
        });
        var middle = store.Quotes[1];

        var index = store.Delete(middle);
        Assert.Equal(1, index);
        Assert.Equal(2, store.Quotes.Count);

        store.Insert(index, middle);

        var reloaded = new QuoteStore(_quotesFilePath);
        Assert.Equal(new[] { "A", "B", "C" }, reloaded.Quotes.Select(q => q.Text));
    }

    [Fact]
    public void Delete_ReturnsMinusOne_WhenQuoteIsNotInTheStore()
    {
        var store = CreateStoreWithQuotes(new List<Quote> { new() { Text = "A", Movie = "M" } });

        Assert.Equal(-1, store.Delete(new Quote { Text = "Not stored", Movie = "M" }));
        Assert.Single(store.Quotes);
    }

    [Fact]
    public void GetRandom_NeverReturnsExcludedQuotes()
    {
        var store = CreateStoreWithQuotes(new List<Quote>
        {
            new() { Text = "Shown 1", Movie = "M" },
            new() { Text = "Shown 2", Movie = "M" },
            new() { Text = "Hidden", Movie = "M", IsExcluded = true },
        });

        Quote? previous = null;
        for (var i = 0; i < 300; i++)
        {
            previous = store.GetRandom(previous);
            Assert.NotEqual("Hidden", previous!.Text);
        }
    }

    [Fact]
    public void GetRandom_ReturnsNull_WhenEveryQuoteIsExcluded()
    {
        var store = CreateStoreWithQuotes(new List<Quote>
        {
            new() { Text = "A", Movie = "M", IsExcluded = true },
            new() { Text = "B", Movie = "M", IsExcluded = true },
        });

        Assert.Null(store.GetRandom(null));
    }

    [Fact]
    public void GetRandom_PicksFavoritesMoreOften()
    {
        var store = CreateStoreWithQuotes(new List<Quote>
        {
            new() { Text = "Favorite", Movie = "M", IsFavorite = true },
            new() { Text = "Normal", Movie = "M" },
        });

        // Weight 3 vs 1 -> the favorite should win about 75% of independent picks.
        var favoritePicks = Enumerable.Range(0, 4000).Count(_ => store.GetRandom(null)!.Text == "Favorite");

        Assert.InRange(favoritePicks, 2700, 3300);
    }

    [Fact]
    public void Export_ThenImport_RoundTripsQuotesAndFlags()
    {
        var store = CreateStoreWithQuotes(new List<Quote>
        {
            new() { Text = "A", Movie = "M1", Year = 1999, Character = "X", IsFavorite = true },
            new() { Text = "B", Movie = "M2", IsExcluded = true },
            new() { Text = "C", Movie = "M3" },
        });

        var exportPath = NewTempFilePath();
        QuoteStore.Export(exportPath, store.Quotes);

        var otherStore = new QuoteStore(NewTempFilePath());
        var result = otherStore.Import(exportPath);

        Assert.Equal(3, result.Added);
        var a = otherStore.Quotes.Single(q => q.Text == "A");
        Assert.True(a.IsFavorite);
        Assert.Equal(1999, a.Year);
        Assert.Equal("X", a.Character);
        Assert.True(otherStore.Quotes.Single(q => q.Text == "B").IsExcluded);
    }

    [Fact]
    public void Export_OmitsFlagsThatAreOff_SoTheFileMatchesTheImportFormat()
    {
        var exportPath = NewTempFilePath();

        QuoteStore.Export(exportPath, new[] { new Quote { Text = "A", Movie = "M" } });

        var json = File.ReadAllText(exportPath);
        Assert.DoesNotContain("favorite", json);
        Assert.DoesNotContain("excluded", json);
    }
}
