using System.IO;
using System.Text.Json;
using QuoteWidget.Data;
using QuoteWidget.Models;

namespace QuoteWidget.Services;

public class QuoteImportException : Exception
{
    public QuoteImportException(string message) : base(message) { }
}

public record ImportResult(int Added, int Skipped);

public class QuoteStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>A favorite is this many times as likely to be picked as a normal quote.</summary>
    public const int FavoriteWeight = 3;

    private readonly string _filePath;
    private readonly List<Quote> _quotes = new();
    private readonly Random _random = new();

    public QuoteStore(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    public static string GetDefaultFilePath()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuoteWidget");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "quotes.json");
    }

    public IReadOnlyList<Quote> Quotes => _quotes;

    public void Load()
    {
        _quotes.Clear();

        List<Quote>? loaded = null;
        if (File.Exists(_filePath))
        {
            try
            {
                var json = File.ReadAllText(_filePath);
                loaded = JsonSerializer.Deserialize<List<Quote>>(json, JsonOptions);
            }
            catch (JsonException)
            {
                loaded = null;
            }
        }

        if (loaded == null)
        {
            _quotes.AddRange(SampleQuotes.All);
            Save();
        }
        else
        {
            _quotes.AddRange(loaded);
        }
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_quotes, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    public void Add(Quote quote)
    {
        _quotes.Add(quote);
        Save();
    }

    public void Update(Quote original, Quote updated)
    {
        var index = _quotes.IndexOf(original);
        if (index < 0)
        {
            return;
        }

        _quotes[index] = updated;
        Save();
    }

    /// <summary>Removes the quote and returns the index it had (for undo), or -1 if it wasn't there.</summary>
    public int Delete(Quote quote)
    {
        var index = _quotes.IndexOf(quote);
        if (index < 0)
        {
            return -1;
        }

        _quotes.RemoveAt(index);
        Save();
        return index;
    }

    public void Insert(int index, Quote quote)
    {
        _quotes.Insert(Math.Clamp(index, 0, _quotes.Count), quote);
        Save();
    }

    /// <summary>True if another quote (other than <paramref name="except"/>) has the same text and movie.</summary>
    public bool ContainsDuplicate(Quote candidate, Quote? except = null)
    {
        return _quotes.Any(existing => !ReferenceEquals(existing, except) && IsDuplicate(existing, candidate));
    }

    public Quote? GetRandom(Quote? previous)
    {
        var pool = new List<Quote>();
        foreach (var quote in _quotes)
        {
            if (quote.IsExcluded)
            {
                continue;
            }

            var weight = quote.IsFavorite ? FavoriteWeight : 1;
            for (var i = 0; i < weight; i++)
            {
                pool.Add(quote);
            }
        }

        if (pool.Count == 0)
        {
            return null;
        }

        if (previous != null)
        {
            var withoutPrevious = pool.Where(quote => !IsDuplicate(quote, previous)).ToList();
            if (withoutPrevious.Count > 0)
            {
                pool = withoutPrevious;
            }
        }

        return pool[_random.Next(pool.Count)];
    }

    public static void Export(string filePath, IEnumerable<Quote> quotes)
    {
        var json = JsonSerializer.Serialize(quotes.ToList(), JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public ImportResult Import(string filePath)
    {
        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (IOException ex)
        {
            throw new QuoteImportException($"Could not read file: {ex.Message}");
        }

        List<Quote>? imported;
        try
        {
            imported = JsonSerializer.Deserialize<List<Quote>>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new QuoteImportException($"Invalid JSON format: {ex.Message}");
        }

        if (imported == null)
        {
            throw new QuoteImportException("The file does not contain a list of quotes.");
        }

        foreach (var quote in imported)
        {
            if (string.IsNullOrWhiteSpace(quote.Text) || string.IsNullOrWhiteSpace(quote.Movie))
            {
                throw new QuoteImportException("Each quote must have a non-empty 'quote' and 'movie' field.");
            }
        }

        var added = 0;
        var skipped = 0;
        foreach (var quote in imported)
        {
            if (_quotes.Any(existing => IsDuplicate(existing, quote)))
            {
                skipped++;
                continue;
            }

            _quotes.Add(quote);
            added++;
        }

        if (added > 0)
        {
            Save();
        }

        return new ImportResult(added, skipped);
    }

    private static bool IsDuplicate(Quote a, Quote b)
    {
        return string.Equals(a.Text.Trim(), b.Text.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Movie.Trim(), b.Movie.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
