using System.Windows;

namespace PassNotes;

internal static class HelpWindowManager
{
    private static HelpNavState? _lastState;

    public static void ShowOrActivate(Window? owner, string? topic)
    {
        var state = ParseTopicOrLast(topic);
        var main = ResolveMainWindow(owner);
        if (main is null)
        {
            _lastState = state;
            return;
        }

        main.ShowHostedHelpDialog(state);
    }

    internal static void UpdateLastState(HelpNavState state)
    {
        _lastState = state;
    }

    private static MainWindow? ResolveMainWindow(Window? owner)
    {
        for (var current = owner; current is not null; current = current.Owner)
        {
            if (current is MainWindow main)
                return main;
        }

        return Application.Current?.MainWindow as MainWindow;
    }

    private static HelpNavState ParseTopicOrLast(string? topic)
    {
        if (!string.IsNullOrWhiteSpace(topic))
        {
            var t = topic.Trim();
            var file = t;
            string? anchor = null;

            var hash = t.IndexOf('#');
            if (hash >= 0)
            {
                file = t[..hash];
                anchor = t[(hash + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(anchor))
                    anchor = null;
            }

            file = System.IO.Path.GetFileName(file);
            if (string.IsNullOrWhiteSpace(file))
                file = "index.md";

            return new HelpNavState(file, anchor);
        }

        return _lastState ?? new HelpNavState("index.md", null);
    }
}
