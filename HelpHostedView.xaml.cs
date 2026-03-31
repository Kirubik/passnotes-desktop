using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace PassNotes;

public partial class HelpHostedView : UserControl, IHostedDialogCloseRequestHandler
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .Build();

    private readonly List<HelpTocItem> _tocItems = new();
    private readonly Stack<HelpNavState> _back = new();
    private readonly Stack<HelpNavState> _forward = new();
    private readonly HashSet<string> _loggedLinkClicks = new(StringComparer.OrdinalIgnoreCase);

    private HelpNavState? _current;
    private string? _pendingAnchor;
    private bool _suppressTocSelection;

    public event Action? CloseRequested;

    public HelpHostedView()
    {
        InitializeComponent();

        BackBtn.IsEnabled = false;
        ForwardBtn.IsEnabled = false;

        Loaded += HelpHostedView_Loaded;
        PreviewKeyDown += HelpHostedView_PreviewKeyDown;

        EnsureTocLoaded();
    }

    internal void NavigateTo(HelpNavState state)
    {
        NavigateTo(state, addToHistory: true);
    }

    internal void RequestClose()
        => CloseRequested?.Invoke();

    public bool TryHandleHostedDialogCloseRequest()
    {
        CloseRequested?.Invoke();
        return true;
    }

    private void HelpHostedView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Keyboard.Focus(TocList);
        }
        catch
        {
            // ignore
        }
    }

    private void NavigateTo(HelpNavState state, bool addToHistory)
    {
        EnsureTocLoaded();

        if (addToHistory && _current is not null)
        {
            _back.Push(_current);
            _forward.Clear();
        }

        if (!HelpContentService.TryReadMarkdown(state.FileName, out var md, out var usedLang, out var error))
        {
            ShowError(error);
            UpdateNavButtons();
            return;
        }

        HideError();

        md = HelpTokenReplacer.Replace(md);

        var htmlBody = Markdown.ToHtml(md, Pipeline);
        htmlBody = RewriteMdLinksToPnHelp(htmlBody, state.FileName);
        var baseUri = HelpContentService.GetBaseUri(usedLang).AbsoluteUri;
        var html = WrapHtml(htmlBody, baseUri);

        _pendingAnchor = state.Anchor;
        _current = new HelpNavState(state.FileName, state.Anchor);
        HelpWindowManager.UpdateLastState(_current);

        TrySelectTocItem(_current);

        void DoNavigate()
        {
            try
            {
                Browser.NavigateToString(html);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        if (IsLoaded && Browser.IsLoaded)
        {
            DoNavigate();
        }
        else
        {
            Dispatcher.BeginInvoke((Action)DoNavigate, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        UpdateNavButtons();
    }

    private void EnsureTocLoaded()
    {
        if (_tocItems.Count > 0)
            return;

        if (HelpContentService.TryReadMarkdown("navigation.md", out var navMd, out _, out _))
        {
            var items = HelpNavParser.ParseNavigationMarkdown(navMd);
            if (items.Count > 0)
                _tocItems.AddRange(items);
        }

        if (_tocItems.Count == 0)
        {
            _tocItems.Add(new HelpTocItem(Loc.Instance["HelpTocHome"], "index.md", null));
            _tocItems.Add(new HelpTocItem(Loc.Instance["HelpTocHotkeys"], "hotkeys.md", null));
        }

        TocList.ItemsSource = _tocItems;

        _suppressTocSelection = true;
        TocList.SelectedIndex = 0;
        _suppressTocSelection = false;
    }

    private void TrySelectTocItem(HelpNavState state)
    {
        try
        {
            if (TocList.ItemsSource is null)
                return;

            var match = _tocItems.FirstOrDefault(x => string.Equals(x.FileName, state.FileName, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return;

            _suppressTocSelection = true;
            TocList.SelectedItem = match;
        }
        catch
        {
            // best-effort
        }
        finally
        {
            _suppressTocSelection = false;
        }
    }

    private void TocList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTocSelection)
            return;

        if (TocList.SelectedItem is HelpTocItem item)
        {
            var next = new HelpNavState(item.FileName, item.Anchor);
            NavigateTo(next, addToHistory: true);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_back.Count == 0 || _current is null)
            return;

        var prev = _back.Pop();
        _forward.Push(_current);
        NavigateTo(prev, addToHistory: false);
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_forward.Count == 0 || _current is null)
            return;

        var next = _forward.Pop();
        _back.Push(_current);
        NavigateTo(next, addToHistory: false);
    }

    private void HelpHostedView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Left)
        {
            e.Handled = true;
            Back_Click(this, new RoutedEventArgs());
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Right)
        {
            e.Handled = true;
            Forward_Click(this, new RoutedEventArgs());
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseRequested?.Invoke();
        }
    }

    private void Browser_LoadCompleted(object sender, NavigationEventArgs e)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_pendingAnchor))
                ScrollToAnchor(_pendingAnchor);
        }
        catch
        {
            // best-effort
        }
        finally
        {
            _pendingAnchor = null;
        }
    }

    private void Browser_Navigating(object sender, NavigatingCancelEventArgs e)
    {
        if (e.Uri is null)
            return;

        try
        {
            var uri = e.Uri;

            if (uri.Scheme.Equals("pnhelp", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;

                var original = uri.OriginalString ?? "";
                LogHelpLinkClick(original);

                var rest = original;
                var colon = rest.IndexOf(':');
                if (colon >= 0)
                    rest = rest[(colon + 1)..];
                rest = rest.TrimStart('/');

                var filePart = rest;
                string? anchor = null;

                var hash = rest.IndexOf('#');
                if (hash >= 0)
                {
                    filePart = rest[..hash];
                    anchor = rest[(hash + 1)..];
                }

                if (string.IsNullOrWhiteSpace(filePart))
                    filePart = _current?.FileName ?? "index.md";

                var file = System.IO.Path.GetFileName(filePart);
                if (string.IsNullOrWhiteSpace(file))
                    file = _current?.FileName ?? "index.md";

                if (!file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    file += ".md";

                if (string.IsNullOrWhiteSpace(anchor))
                    anchor = null;

                if (_current is not null && string.Equals(_current.FileName, file, StringComparison.OrdinalIgnoreCase))
                {
                    if (anchor is not null)
                    {
                        _pendingAnchor = anchor;
                        ScrollToAnchor(anchor);
                        _current = new HelpNavState(file, anchor);
                        HelpWindowManager.UpdateLastState(_current);
                    }
                    return;
                }

                NavigateTo(new HelpNavState(file, anchor), addToHistory: true);
                return;
            }

            if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                try
                {
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                }
                catch
                {
                    // best-effort
                }
                return;
            }

            if (uri.Scheme.Equals("about", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(uri.Fragment))
            {
                e.Cancel = true;
                var anchor = uri.Fragment.TrimStart('#');
                if (!string.IsNullOrWhiteSpace(anchor))
                {
                    _pendingAnchor = anchor;
                    ScrollToAnchor(anchor);
                    if (_current is not null)
                    {
                        _current = new HelpNavState(_current.FileName, anchor);
                        HelpWindowManager.UpdateLastState(_current);
                    }
                }
                return;
            }

            if (uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                var localPath = uri.LocalPath;
                if (localPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    e.Cancel = true;

                    var file = System.IO.Path.GetFileName(localPath);
                    var anchor = uri.Fragment;
                    var a = string.IsNullOrWhiteSpace(anchor) ? null : anchor.TrimStart('#');

                    if (_current is not null && string.Equals(_current.FileName, file, StringComparison.OrdinalIgnoreCase) && a is not null)
                    {
                        _pendingAnchor = a;
                        ScrollToAnchor(a);
                        _current = new HelpNavState(file, a);
                        HelpWindowManager.UpdateLastState(_current);
                        return;
                    }

                    NavigateTo(new HelpNavState(file, a), addToHistory: true);
                }
                return;
            }
        }
        catch
        {
            // Best-effort: if navigation fails, do nothing.
        }
    }

    private void LogHelpLinkClick(string href)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(href))
                return;

            if (_loggedLinkClicks.Count >= 10)
                return;

            if (_loggedLinkClicks.Add(href))
                DiagnosticsLog.AppendLine("HELP_LINK_CLICK", href);
        }
        catch
        {
            // best-effort
        }
    }

    private static string RewriteMdLinksToPnHelp(string html, string currentFileName)
    {
        if (string.IsNullOrWhiteSpace(html))
            return html;

        currentFileName = System.IO.Path.GetFileName(currentFileName ?? "") ?? "";
        if (string.IsNullOrWhiteSpace(currentFileName))
            currentFileName = "index.md";

        return Regex.Replace(
            html,
            "href\\s*=\\s*([\"'])(?<href>[^\"']+)\\1",
            m =>
            {
                var quote = m.Groups[1].Value;
                var href = (m.Groups["href"].Value ?? "").Trim();

                if (string.IsNullOrWhiteSpace(href))
                    return m.Value;

                if (href.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
                    || href.StartsWith("https:", StringComparison.OrdinalIgnoreCase)
                    || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                    || href.StartsWith("pnhelp:", StringComparison.OrdinalIgnoreCase))
                    return m.Value;

                if (href.StartsWith("#", StringComparison.Ordinal))
                {
                    var a = href.TrimStart('#');
                    if (string.IsNullOrWhiteSpace(a))
                        return m.Value;

                    var rewrittenAnchor = $"pnhelp:{currentFileName}#{a}";
                    return $"href={quote}{rewrittenAnchor}{quote}";
                }

                var raw = href;
                if (raw.StartsWith("./", StringComparison.Ordinal))
                    raw = raw[2..];

                var filePart = raw;
                string? anchor = null;

                var hash = raw.IndexOf('#');
                if (hash >= 0)
                {
                    filePart = raw[..hash];
                    anchor = raw[(hash + 1)..];
                }

                filePart = System.IO.Path.GetFileName(filePart);
                if (!filePart.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    return m.Value;

                var rewritten = $"pnhelp:{filePart}{(string.IsNullOrWhiteSpace(anchor) ? "" : "#" + anchor)}";
                return $"href={quote}{rewritten}{quote}";
            },
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private void UpdateNavButtons()
    {
        try { BackBtn.IsEnabled = _back.Count > 0; } catch { }
        try { ForwardBtn.IsEnabled = _forward.Count > 0; } catch { }
    }

    private void ShowError(string error)
    {
        try
        {
            ErrorText.Text = string.IsNullOrWhiteSpace(error) ? Loc.Instance["HelpLoadError"] : error;
            ErrorOverlay.Visibility = Visibility.Visible;
        }
        catch { }

        try
        {
            DiagnosticsLog.AppendLine("HELP_LOAD_ERROR", error);
        }
        catch { }
    }

    private void HideError()
    {
        try { ErrorOverlay.Visibility = Visibility.Collapsed; } catch { }
        try { ErrorText.Text = ""; } catch { }
    }

    private static string WrapHtml(string bodyHtml, string baseHref)
    {
        return $@"<!doctype html>
<html>
<head>
    <meta charset=""utf-8""/>
    <base href=""{baseHref}""/>
<style>
body {{ font-family: ""Segoe UI"", Arial, sans-serif; font-size: 14px; line-height: 1.45; margin: 16px; }}
h1 {{ margin: 0 0 12px 0; }}
h2 {{ margin-top: 22px; }}
p {{ margin: 10px 0; }}

table {{ border-collapse: collapse; width: 100%; margin: 10px 0 18px 0; }}
th, td {{ padding: 6px 10px; vertical-align: top; }}
thead th {{ border-bottom: 1px solid #cfcfcf; }}
tbody tr + tr td {{ border-top: 1px solid #ededed; }}
th {{ text-align: left; }}

td:nth-child(2), th:nth-child(2) {{ white-space: nowrap; }}

.hotkeys-table {{ table-layout: fixed; margin: 8px 0 24px 0; }}
.hotkeys-table col.hotkeys-col-action {{ width: 76%; }}
.hotkeys-table col.hotkeys-col-keys {{ width: 24%; }}
.hotkeys-table th:nth-child(2),
.hotkeys-table td:nth-child(2) {{ white-space: nowrap; }}
.hotkeys-table td:nth-child(1) {{ overflow-wrap: anywhere; word-break: normal; }}
.hotkeys-table .hotkeys-action-main {{ font-weight: 400; }}
.hotkeys-table .hotkeys-action-note {{
    margin-top: 2px;
    color: #555555;
    font-size: 12px;
    line-height: 1.35;
}}
.hotkeys-table .hotkeys-gesture {{
    font-family: Consolas, ""Courier New"", monospace;
    font-size: 13px;
    font-weight: 600;
}}

code {{ font-family: Consolas, ""Courier New"", monospace; font-size: 13px; }}
pre {{ padding: 10px; overflow: auto; }}

a {{ text-decoration: underline; }}
</style>
</head>
<body>
{bodyHtml}
</body>
</html>";
    }

    private void ScrollToAnchor(string anchor)
    {
        if (string.IsNullOrWhiteSpace(anchor))
            return;

        var safe = EscapeJs(anchor.Trim());
        var js = $"try{{var id='{safe}';var el=document.getElementById(id);if(el){{el.scrollIntoView();}}location.hash='#'+id;}}catch(e){{}}";
        try { Browser.InvokeScript("eval", js); } catch { }
    }

    private static string EscapeJs(string s)
    {
        return s
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", "")
            .Replace("\n", "");
    }
}