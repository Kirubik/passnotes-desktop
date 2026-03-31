using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace PassNotes;

/// <summary>
/// Lightweight system-tray integration for WPF.
/// Uses WinForms NotifyIcon under the hood.
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _openItem;
    private readonly ToolStripMenuItem _lockItem;
    private readonly ToolStripMenuItem _exitItem;

    private readonly Action _onOpen;
    private readonly Action _onLock;
    private readonly Action _onExit;

    public bool NotificationsEnabled { get; set; } = true;

    public TrayService(Action onOpen, Action onLock, Action onExit)
    {
        _onOpen = onOpen ?? throw new ArgumentNullException(nameof(onOpen));
        _onLock = onLock ?? throw new ArgumentNullException(nameof(onLock));
        _onExit = onExit ?? throw new ArgumentNullException(nameof(onExit));

        _openItem = new ToolStripMenuItem();
        _lockItem = new ToolStripMenuItem();
        _exitItem = new ToolStripMenuItem();

        _openItem.Click += (_, _) => DispatchToUi(_onOpen);
        _lockItem.Click += (_, _) => DispatchToUi(_onLock);
        _exitItem.Click += (_, _) => DispatchToUi(_onExit);

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _openItem,
            _lockItem,
            new ToolStripSeparator(),
            _exitItem
        });

        _icon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            ContextMenuStrip = menu,
            Visible = false,
            Text = Loc.Instance["AppTitle"]
        };

        _icon.DoubleClick += (_, _) => DispatchToUi(_onOpen);

        UpdateTexts();
    }

    public void SetVisible(bool visible)
    {
        try { _icon.Visible = visible; } catch { }
    }

    public void SetLockEnabled(bool enabled)
    {
        try { _lockItem.Enabled = enabled; } catch { }
    }

    public void UpdateTexts()
    {
        try
        {
            _icon.Text = Loc.Instance["AppTitle"];
            _openItem.Text = Loc.Instance["TrayMenuOpen"];
            _lockItem.Text = Loc.Instance["TrayMenuLock"];
            _exitItem.Text = Loc.Instance["TrayMenuExit"];
        }
        catch
        {
            // Never crash due to localization/resource issues.
        }
    }

    public void ShowInfo(string title, string message)
    {
        if (!NotificationsEnabled)
            return;

        try
        {
            _icon.BalloonTipTitle = title ?? "";
            _icon.BalloonTipText = message ?? "";
            _icon.ShowBalloonTip(2500);
        }
        catch { }
    }

    public void Dispose()
    {
        try
        {
            _icon.Visible = false;
            _icon.Dispose();
        }
        catch { }
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "PassNotesApp.ico");
            if (File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }
        }
        catch
        {
            // Fall through to the next fallback.
        }

        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
            {
                var extracted = Icon.ExtractAssociatedIcon(processPath);
                if (extracted != null)
                {
                    return (Icon)extracted.Clone();
                }
            }
        }
        catch
        {
            // Fall through to SystemIcons fallback.
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private static void DispatchToUi(Action action)
    {
        try
        {
            var app = System.Windows.Application.Current;
            var dispatcher = app?.Dispatcher;

            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.BeginInvoke(action);
        }
        catch { }
    }
}
