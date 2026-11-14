# 🎬 Quote Widget

A tiny, always-on-desktop widget for Windows that rotates through your favorite movie
quotes on every monitor — nicely formatted, resizable, and easy to manage.

![Quote Widget demo: quotes rotating with a fade, then the unlocked widget being resized while the text scales with it](readme_assets/demo.gif)

## 🏷️ Badges

[![CI](https://github.com/pero-grubac/Quote-Widget/actions/workflows/ci.yml/badge.svg)](https://github.com/pero-grubac/Quote-Widget/actions/workflows/ci.yml)
![.NET 8](https://img.shields.io/badge/.NET-8-512BD4)
![C#](https://img.shields.io/badge/language-C%23-239120)
![WPF](https://img.shields.io/badge/UI-WPF-0078D4)
![License: MIT](https://img.shields.io/badge/license-MIT-green)

## ✨ Features

- 🖥️🖥️ **One widget per monitor** — just the quote floating on the desktop: no
  border, no box, fully transparent by default. Never shows up in the taskbar
  or Alt+Tab, never steals focus. Plug/unplug a monitor and its widget
  appears or disappears automatically; its settings are kept either way.
- 🔒 **Lock / unlock, per monitor** — locked (default), a widget is
  click-through and never blocks the desktop underneath it. Unlocked, it
  shows a dashed outline and can be dragged and resized directly.
- 🪟 **Stays on the desktop** — widgets live on your wallpaper, behind every
  program window, and never pop up over your apps (unless you turn on
  *Always on top*).
- 🪶 **Light on memory** — about 25 MB of RAM while only the widgets run; the
  Manager window is released when you close it.
- 🔁 **Auto-rotation** — a new random quote every X minutes (configurable per
  monitor), never repeating the previous one. Optionally show the exact same
  quote on every monitor at once instead.
- 🗂️ **Manager window** — the app's main window (Fluent/Windows 11 style,
  dark theme, Mica backdrop), with a navigation pane:
  - **Quotes** — search, filter by movie/year/character, group the list by
    any of them, add/edit (as a dialog, duplicates refused), delete with
    one-click **Undo**, import and export JSON, with a quick notification
    after each action.
  - ⭐ **Favorites & hidden quotes** — favorites come up 3x as often; hidden
    quotes stay in your list but never appear on a widget.
  - **Widgets** — a card per detected monitor: enabled toggle, its own
    appearance (font sizes, text color, background color/opacity, rotation
    interval), exact position/size, and its own unlock toggle. Apply one
    monitor's look to all of them, or reset one (or all) to defaults.
  - **General** — always-on-top, start-with-Windows, and hiding widgets
    while a fullscreen game or video is running.
- 📥 **Import** — bring in quotes from an external JSON file, with validation,
  duplicate skipping, and a clear summary afterwards.
- 🖥️ **Tray icon** — show/hide the widgets, skip to the next quote, open the
  manager, or exit. Closing a window just hides it; use *Exit* to quit.

## 🧰 Tech Stack

| Layer           | Technology                           |
| --------------- | ------------------------------------- |
| Language        | C#                                     |
| Runtime         | .NET 8                                 |
| UI framework    | WPF                                     |
| Fluent UI       | WPF-UI (FluentWindow, NavigationView, ContentDialog, Snackbar) |
| Tray icon       | H.NotifyIcon.Wpf                        |
| Monitor info    | `System.Windows.Forms.Screen` (multi-monitor + DPI) |
| Storage         | `System.Text.Json` (local files)        |
| Tests           | xUnit                                   |
| CI/CD           | GitHub Actions                          |

## 📁 Repository Structure

```
Quote-Widget/
├── .github/workflows/ci.yml   # CI/CD pipeline
├── QuoteWidget/                # WPF application
│   ├── Assets/                  # app.ico, icon.svg
│   ├── app.manifest              # Per-Monitor V2 DPI awareness
│   ├── Models/                   # Quote, Settings, MonitorWidgetSettings
│   ├── Services/                 # QuoteStore, SettingsStore, MonitorService,
│   │                             # MonitorSettingsService, WidgetManager,
│   │                             # AutostartService, NativeWindowHelper
│   ├── Views/                    # WidgetWindow (one per monitor)
│   │   └── Pages/                 # ManagerWindow's Quotes/Widgets/General pages
│   ├── Data/                     # Sample quotes
│   └── App.xaml(.cs)              # Startup, tray icon, Fluent theme
├── QuoteWidget.Tests/            # xUnit tests
├── tools/icon-generator/         # Script that (re)generates Assets/app.ico
├── readme_assets/                # Screenshots / demo GIF
├── QuoteWidget.sln
├── LICENSE
└── README.md
```

## 🚀 Installation

### Option 1 — Download the release

Grab the latest `QuoteWidget.exe` from the [Releases](../../releases) page and
run it. No installer needed.

### Option 2 — Build from source

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), Windows.

```bash
git clone https://github.com/pero-grubac/Quote-Widget.git
cd Quote-Widget
dotnet run --project QuoteWidget
```

## 📖 Usage

On launch, the **Manager** window opens (your main window — it's in the
taskbar and Alt+Tab), and a **widget** appears on each connected monitor.

**Moving/resizing a widget** — open the *Widgets* page, find that monitor's
card, and switch on its unlock toggle. While unlocked, the widget shows a
dashed outline and can be dragged and resized directly; its X/Y/width/height
also show on the card and can be typed in directly. Switch it back off to
lock the widget down again — it becomes click-through and never blocks the
desktop.

**Quotes page** — search the list, or use *Add* / *Edit* / *Delete* /
*Import...*. Changes save to `quotes.json` immediately and all widgets pick
them up on their next rotation.

**Importing quotes** — click *Import...*, pick a JSON file in the format
below. Invalid files show a clear error; duplicates (same quote text + movie)
are skipped, and you'll get a notification like
`Imported 40, skipped 3 duplicates`.

**Widgets page** — each monitor gets its own card: font sizes, text color,
background color/opacity, and rotation interval, all applied live to that
monitor's widget as you adjust them. *Apply appearance to all monitors*
copies one card's look to every other monitor; *Reset this monitor* /
*Reset all monitors to defaults* restore the transparent-background,
white-text look. The *same quote on all monitors* toggle is also here.

**Favorites & hidden** — click the ☆ star at the start of any row to make it
a favorite (it turns into a filled gold ★; click again to undo). To hide a
quote from the widgets, select it and use the 👁 button. Both are also
checkboxes in the edit dialog, and the *Show* filter lists just your favorites
or just the hidden ones.

**Export** — *Export...* saves the quotes currently shown (all of them, or
just what your filters match) as JSON in the same format *Import...* reads,
handy for backups or moving to another PC.

**General page** — always-on-top, start-with-Windows, and *hide widgets while
a fullscreen app is running* (on by default: only the widget on the monitor
the game/video covers hides, and it comes back on its own).

**Tray icon** — right-click for *Show/hide widgets*, *Next quote*, *Open
manager*, and *Exit*. Closing the Manager keeps the app (and your widgets)
running and frees the Manager's memory; only *Exit* quits the app.

## 🗂️ Quotes file format

Quotes live in `%AppData%\QuoteWidget\quotes.json` and are created with a
handful of sample quotes on first run. `year` and `character` are optional —
this is also the format expected when importing:

```json
[
  {
    "quote": "May the Force be with you.",
    "movie": "Star Wars",
    "year": 1977,
    "character": "Han Solo"
  },
  {
    "quote": "The first rule of Fight Club is: you do not talk about Fight Club.",
    "movie": "Fight Club",
    "favorite": true
  }
]
```

`favorite` and `excluded` (hidden from widgets) are optional too and default
to `false`.

Settings live alongside it in `%AppData%\QuoteWidget\settings.json`: global
options plus a `Monitors` entry per monitor (keyed by its device name),
holding that monitor's appearance, rotation interval and position/size
relative to it. A settings file from an older, single-widget version of the
app is migrated into this shape automatically the first time it's loaded.

## 🧠 Design Decisions

- **WPF** — the app needs borderless, transparent, freely resizable windows
  with smooth text scaling; WPF's `Viewbox` and per-window styling give that
  for free.
- **WPF-UI** — gives the Manager window a modern Fluent/Windows 11 look
  (NavigationView, ContentDialog, Snackbar, Mica) without hand-rolling custom
  control styles.
- **Plain JSON files** — quotes and settings are small, personal datasets with
  no concurrent access and no need for a query engine, so a local JSON file
  (via `System.Text.Json`) is simpler and more transparent than a database.
- **One window per monitor** — each monitor can have its own enabled state,
  appearance and position; a single shared window can't express that cleanly,
  while separate windows map 1:1 onto how Windows itself models monitors.

## 🧪 Tests

```bash
dotnet test
```

Unit tests cover `QuoteStore` (importing and merging quotes, skipping
duplicates, picking a random quote without repeating the previous one,
favorites and hidden quotes, delete + undo, export/import round trips), the
per-monitor settings logic (migrating an old single-widget settings file,
applying one monitor's appearance to all others, resetting to defaults), the
fullscreen-detection geometry, and the rule that keeps widgets below other
windows.

## 🔄 CI/CD

GitHub Actions runs on every push/PR to `main` (docs-only changes are
skipped):

1. **test** — restore, build, `dotnet test`.
2. **build** *(after test passes)* — publishes a self-contained-false,
   single-file `win-x64` build and uploads it as a workflow artifact.
3. **release** *(only on a `v*` tag, after test and build pass)* — publishes
   the exe and attaches it to a GitHub Release with auto-generated notes.

## 📄 License

MIT — see [LICENSE](LICENSE).
