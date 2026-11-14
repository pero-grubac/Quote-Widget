using QuoteWidget.Services;
using QuoteWidget.Views.Pages;
using Wpf.Ui.Controls;

namespace QuoteWidget.Views;

public partial class ManagerWindow : FluentWindow
{
    private readonly QuotesPage _quotesPage;
    private readonly WidgetsPage _widgetsPage;
    private readonly GeneralPage _generalPage;

    public ManagerWindow(QuoteStore quoteStore, SettingsStore settingsStore, WidgetManager widgetManager)
    {
        InitializeComponent();

        _quotesPage = new QuotesPage(quoteStore, RootContentDialog, RootSnackbar);
        _widgetsPage = new WidgetsPage(settingsStore, widgetManager, RootSnackbar);
        _generalPage = new GeneralPage(settingsStore, widgetManager);

        // NavigationView's content presenter isn't ready until its template is applied, so the
        // initial page can only be set once the control has loaded.
        Loaded += (_, _) => RootNavigation.ReplaceContent(_quotesPage, null);
    }

    // NavigationViewItem is a ButtonBase under the hood, so Click fires reliably on the exact
    // item clicked - simpler and more robust than going through SelectionChanged/SelectedItem.
    private void QuotesNavItem_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        RootNavigation.ReplaceContent(_quotesPage, null);
    }

    private void WidgetsNavItem_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _widgetsPage.Reload();
        RootNavigation.ReplaceContent(_widgetsPage, null);
    }

    private void GeneralNavItem_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        RootNavigation.ReplaceContent(_generalPage, null);
    }
}
