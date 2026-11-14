using System.Windows.Controls;
using QuoteWidget.Models;

namespace QuoteWidget.Views.Pages;

public partial class QuoteEditorControl : UserControl
{
    public QuoteEditorControl()
    {
        InitializeComponent();
    }

    public void LoadFrom(Quote existing)
    {
        QuoteTextBox.Text = existing.Text;
        MovieTextBox.Text = existing.Movie;
        YearTextBox.Text = existing.Year?.ToString() ?? string.Empty;
        CharacterTextBox.Text = existing.Character ?? string.Empty;
        FavoriteCheckBox.IsChecked = existing.IsFavorite;
        ExcludedCheckBox.IsChecked = existing.IsExcluded;
    }

    public bool TryBuildQuote(out Quote? quote, out string? error)
    {
        var quoteText = QuoteTextBox.Text.Trim();
        var movie = MovieTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(quoteText) || string.IsNullOrWhiteSpace(movie))
        {
            quote = null;
            error = "Quote text and movie are required.";
            return false;
        }

        int? year = null;
        var yearText = YearTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(yearText))
        {
            if (!int.TryParse(yearText, out var parsedYear))
            {
                quote = null;
                error = "Year must be a number.";
                return false;
            }
            year = parsedYear;
        }

        var character = CharacterTextBox.Text.Trim();

        quote = new Quote
        {
            Text = quoteText,
            Movie = movie,
            Year = year,
            Character = string.IsNullOrWhiteSpace(character) ? null : character,
            IsFavorite = FavoriteCheckBox.IsChecked == true,
            IsExcluded = ExcludedCheckBox.IsChecked == true
        };
        error = null;
        return true;
    }
}
