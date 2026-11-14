using System.Text.Json.Serialization;

namespace QuoteWidget.Models;

public class Quote
{
    [JsonPropertyName("quote")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("movie")]
    public string Movie { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("character")]
    public string? Character { get; set; }

    /// <summary>Favorites come up in the rotation more often than other quotes.</summary>
    [JsonPropertyName("favorite")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsFavorite { get; set; }

    /// <summary>Excluded quotes stay in the list but are never shown on a widget.</summary>
    [JsonPropertyName("excluded")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsExcluded { get; set; }
}
