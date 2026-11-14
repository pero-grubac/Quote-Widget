using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Controls;
using Microsoft.Win32;
using QuoteWidget.Models;
using QuoteWidget.Services;
using Wpf.Ui.Controls;

namespace QuoteWidget.Views.Pages;

public partial class QuotesPage : UserControl
{
    private readonly QuoteStore _quoteStore;
    private readonly ContentDialogHost _dialogHost;
    private readonly SnackbarPresenter _snackbarPresenter;
    private const string AllOption = "All";

    private readonly ObservableCollection<Quote> _allQuotes = new();
    private readonly ICollectionView _view;

    // The XAML combo boxes raise SelectionChanged while InitializeComponent() runs (before _view
    // exists) and again while their options are rebuilt; handlers ignore those.
    private bool _isLoading = true;

    public QuotesPage(QuoteStore quoteStore, ContentDialogHost dialogHost, SnackbarPresenter snackbarPresenter)
    {
        InitializeComponent();

        _quoteStore = quoteStore;
        _dialogHost = dialogHost;
        _snackbarPresenter = snackbarPresenter;

        foreach (var quote in _quoteStore.Quotes)
        {
            _allQuotes.Add(quote);
        }

        _view = System.Windows.Data.CollectionViewSource.GetDefaultView(_allQuotes);
        _view.Filter = FilterQuote;
        QuotesDataGrid.ItemsSource = _view;

        RefreshFilterOptions();
        _isLoading = false;
        UpdateStatus();
    }

    private bool FilterQuote(object item)
    {
        if (item is not Quote quote)
        {
            return false;
        }

        if (IsFiltering(MovieFilterComboBox, out var movie) && !string.Equals(quote.Movie, movie, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsFiltering(YearFilterComboBox, out var year) && quote.Year?.ToString() != year)
        {
            return false;
        }

        if (IsFiltering(CharacterFilterComboBox, out var character) && !string.Equals(quote.Character, character, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var show = (ShowFilterComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
        if ((show == "favorites" && !quote.IsFavorite) || (show == "excluded" && !quote.IsExcluded))
        {
            return false;
        }

        var search = SearchTextBox.Text;
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return quote.Text.Contains(search, StringComparison.OrdinalIgnoreCase)
            || quote.Movie.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (quote.Character?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static bool IsFiltering(ComboBox comboBox, out string value)
    {
        value = comboBox.SelectedItem as string ?? AllOption;
        return value != AllOption;
    }

    private void RefreshFilterOptions()
    {
        var wasLoading = _isLoading;
        _isLoading = true;

        SetOptions(MovieFilterComboBox, _allQuotes.Select(q => q.Movie).OrderBy(m => m, StringComparer.OrdinalIgnoreCase));
        SetOptions(YearFilterComboBox, _allQuotes.Where(q => q.Year.HasValue).OrderBy(q => q.Year).Select(q => q.Year!.Value.ToString()));
        SetOptions(CharacterFilterComboBox, _allQuotes
            .Select(q => q.Character)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase));

        _isLoading = wasLoading;
        _view.Refresh();
    }

    private static void SetOptions(ComboBox comboBox, IEnumerable<string> values)
    {
        var previous = comboBox.SelectedItem as string;
        var options = new List<string> { AllOption };
        options.AddRange(values.Distinct(StringComparer.OrdinalIgnoreCase));

        comboBox.ItemsSource = options;
        comboBox.SelectedItem = previous != null && options.Contains(previous) ? previous : AllOption;
    }

    private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        _view.Refresh();
        UpdateStatus();
    }

    private void GroupByComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        _view.GroupDescriptions.Clear();
        _view.SortDescriptions.Clear();

        var property = (GroupByComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
        if (string.IsNullOrEmpty(property))
        {
            return;
        }

        _view.SortDescriptions.Add(new SortDescription(property, ListSortDirection.Ascending));
        _view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(property));
    }

    private void ClearFilters_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _isLoading = true;
        SearchTextBox.Text = string.Empty;
        MovieFilterComboBox.SelectedItem = AllOption;
        YearFilterComboBox.SelectedItem = AllOption;
        CharacterFilterComboBox.SelectedItem = AllOption;
        ShowFilterComboBox.SelectedIndex = 0;
        _isLoading = false;

        _view.Refresh();
        UpdateStatus();
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        _view.Refresh();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var shown = _view.Cast<object>().Count();
        StatusTextBlock.Text = shown == _allQuotes.Count
            ? $"{_allQuotes.Count} quote(s)"
            : $"{shown} of {_allQuotes.Count} quote(s)";
    }

    private void OnQuotesChanged()
    {
        RefreshFilterOptions();
        UpdateStatus();
    }

    private void ShowSnackbar(string title, string message, bool isError)
    {
        var snackbar = new Snackbar(_snackbarPresenter)
        {
            Title = title,
            Content = message,
            Appearance = isError ? ControlAppearance.Danger : ControlAppearance.Success,
            Timeout = TimeSpan.FromSeconds(3)
        };
        _snackbarPresenter.AddToQue(snackbar);
    }

    private async void Add_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var editor = new QuoteEditorControl();
        var dialog = new ContentDialog(_dialogHost)
        {
            Title = "Add quote",
            Content = editor,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        while (true)
        {
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            if (!editor.TryBuildQuote(out var quote, out var error))
            {
                ShowSnackbar("Invalid quote", error!, isError: true);
                continue;
            }

            if (_quoteStore.ContainsDuplicate(quote!))
            {
                ShowSnackbar("Already in your list", "This quote from this movie already exists.", isError: true);
                continue;
            }

            _quoteStore.Add(quote!);
            _allQuotes.Add(quote!);
            OnQuotesChanged();
            ShowSnackbar("Quote added", $"\"{quote!.Movie}\" quote saved.", isError: false);
            return;
        }
    }

    private async void Edit_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (QuotesDataGrid.SelectedItem is not Quote selected)
        {
            ShowSnackbar("No quote selected", "Select a quote to edit.", isError: true);
            return;
        }

        await EditQuoteAsync(selected);
    }

    private async System.Threading.Tasks.Task EditQuoteAsync(Quote selected)
    {
        var editor = new QuoteEditorControl();
        editor.LoadFrom(selected);

        var dialog = new ContentDialog(_dialogHost)
        {
            Title = "Edit quote",
            Content = editor,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        while (true)
        {
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            if (!editor.TryBuildQuote(out var updated, out var error))
            {
                ShowSnackbar("Invalid quote", error!, isError: true);
                continue;
            }

            if (_quoteStore.ContainsDuplicate(updated!, except: selected))
            {
                ShowSnackbar("Already in your list", "Another quote with this text and movie already exists.", isError: true);
                continue;
            }

            _quoteStore.Update(selected, updated!);
            var index = _allQuotes.IndexOf(selected);
            if (index >= 0)
            {
                _allQuotes[index] = updated!;
            }
            OnQuotesChanged();

            ShowSnackbar("Quote updated", $"\"{updated!.Movie}\" quote saved.", isError: false);
            return;
        }
    }

    private async void QuotesDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (QuotesDataGrid.SelectedItem is Quote selected)
        {
            await EditQuoteAsync(selected);
        }
    }

    private void Delete_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (QuotesDataGrid.SelectedItem is not Quote selected)
        {
            ShowSnackbar("No quote selected", "Select a quote to delete.", isError: true);
            return;
        }

        var listIndex = _allQuotes.IndexOf(selected);
        var storeIndex = _quoteStore.Delete(selected);
        _allQuotes.Remove(selected);
        OnQuotesChanged();

        var undone = false;
        var undoButton = new Wpf.Ui.Controls.Button
        {
            Content = "Undo",
            Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowUndo24 },
            Margin = new System.Windows.Thickness(0, 8, 0, 0)
        };
        undoButton.Click += (_, _) =>
        {
            if (undone) return;
            undone = true;

            _quoteStore.Insert(storeIndex, selected);
            _allQuotes.Insert(Math.Clamp(listIndex, 0, _allQuotes.Count), selected);
            OnQuotesChanged();
            _ = _snackbarPresenter.HideCurrent();
            ShowSnackbar("Quote restored", $"\"{selected.Movie}\" quote is back.", isError: false);
        };

        var snackbar = new Snackbar(_snackbarPresenter)
        {
            Title = "Quote deleted",
            Content = new StackPanel
            {
                Children =
                {
                    new System.Windows.Controls.TextBlock { Text = $"\"{selected.Movie}\" quote removed." },
                    undoButton
                }
            },
            Appearance = ControlAppearance.Secondary,
            Timeout = TimeSpan.FromSeconds(8)
        };

        // Replace whatever is showing so Undo always refers to the latest delete.
        _ = _snackbarPresenter.ImmediatelyDisplay(snackbar);
    }

    private void FavoriteStar_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if ((sender as System.Windows.FrameworkElement)?.DataContext is not Quote quote)
        {
            return;
        }

        quote.IsFavorite = !quote.IsFavorite;
        _quoteStore.Save();
        RefreshKeepingSelection(quote);

        ShowSnackbar(
            quote.IsFavorite ? "Added to favorites" : "Removed from favorites",
            quote.IsFavorite ? $"Favorites come up {QuoteStore.FavoriteWeight}x as often." : "It's back to the normal chance.",
            isError: false);
    }

    private void Exclude_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (QuotesDataGrid.SelectedItem is not Quote selected)
        {
            ShowSnackbar("No quote selected", "Select a quote to hide or unhide.", isError: true);
            return;
        }

        selected.IsExcluded = !selected.IsExcluded;
        _quoteStore.Save();
        RefreshKeepingSelection(selected);

        ShowSnackbar(
            selected.IsExcluded ? "Hidden from widgets" : "Shown on widgets again",
            selected.IsExcluded ? "It stays in the list but won't be shown." : "It's back in the rotation.",
            isError: false);
    }

    // Quote has no change notifications, so rows only redraw on a view refresh, which drops the
    // selection - restore it so toggling a few times in a row stays smooth.
    private void RefreshKeepingSelection(Quote selected)
    {
        _view.Refresh();
        UpdateStatus();
        QuotesDataGrid.SelectedItem = selected;
        QuotesDataGrid.ScrollIntoView(selected);
    }

    private void Export_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            FileName = "quotes-export.json",
            Title = "Export quotes"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var quotes = _view.Cast<Quote>().ToList();
        try
        {
            QuoteStore.Export(dialog.FileName, quotes);
            var scope = quotes.Count == _allQuotes.Count ? string.Empty : " (only the ones matching the current filters)";
            ShowSnackbar("Export complete", $"Exported {quotes.Count} quote(s){scope}.", isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowSnackbar("Export failed", ex.Message, isError: true);
        }
    }

    private void Import_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Import quotes"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var result = _quoteStore.Import(dialog.FileName);

            _allQuotes.Clear();
            foreach (var quote in _quoteStore.Quotes)
            {
                _allQuotes.Add(quote);
            }
            OnQuotesChanged();

            ShowSnackbar("Import complete", $"Imported {result.Added}, skipped {result.Skipped} duplicates.", isError: false);
        }
        catch (QuoteImportException ex)
        {
            ShowSnackbar("Import failed", ex.Message, isError: true);
        }
    }
}
