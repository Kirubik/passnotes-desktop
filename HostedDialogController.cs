using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PassNotes;

public sealed class HostedDialogController : INotifyPropertyChanged
{
    private sealed class HostedDialogState
    {
        public bool IsOpen { get; init; }
        public string Title { get; init; } = string.Empty;
        public object? Content { get; init; }
        public string PrimaryButtonText { get; init; } = string.Empty;
        public string SecondaryButtonText { get; init; } = string.Empty;
        public string TertiaryButtonText { get; init; } = string.Empty;
        public bool CloseOnOverlay { get; init; }
        public double RequestedWidth { get; init; } = 580;
        public double RequestedMinWidth { get; init; } = 420;
        public double RequestedMaxWidth { get; init; } = 580;
        public double RequestedHeight { get; init; } = double.NaN;
        public double RequestedMinHeight { get; init; }
        public double Width { get; init; } = 580;
        public double MinWidth { get; init; } = 420;
        public double MaxWidth { get; init; } = 580;
        public double Height { get; init; } = double.NaN;
        public double MinHeight { get; init; }
        public Action? PrimaryAction { get; init; }
        public Action? SecondaryAction { get; init; }
        public Action? TertiaryAction { get; init; }
        public Action? ClosedAction { get; init; }
        public bool PreferContentFocus { get; init; }
    }

    private HostedDialogState _current = CreateClosedState();
    private readonly System.Collections.Generic.Stack<HostedDialogState> _stack = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsOpen => _current.IsOpen;

    public string Title => _current.Title;

    public object? Content => _current.Content;

    public string PrimaryButtonText => _current.PrimaryButtonText;

    public string SecondaryButtonText => _current.SecondaryButtonText;

    public string TertiaryButtonText => _current.TertiaryButtonText;

    public bool CloseOnOverlay => _current.CloseOnOverlay;

    public double Width => _current.Width;

    public double MinWidth => _current.MinWidth;

    public double MaxWidth => _current.MaxWidth;

    public double Height => _current.Height;

    public double MinHeight => _current.MinHeight;

    public bool PreferContentFocus => _current.PreferContentFocus;

    public bool IsPrimaryButtonVisible => !string.IsNullOrWhiteSpace(PrimaryButtonText);
    public bool IsSecondaryButtonVisible => !string.IsNullOrWhiteSpace(SecondaryButtonText);
    public bool IsTertiaryButtonVisible => !string.IsNullOrWhiteSpace(TertiaryButtonText);
    public bool HasVisibleButtons => IsPrimaryButtonVisible || IsSecondaryButtonVisible || IsTertiaryButtonVisible;

    internal void Show(HostedDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (IsOpen)
        {
            _stack.Push(_current);
        }

        ApplyState(CreateStateFromRequest(request));
    }

    internal void ReplaceCurrent(HostedDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsOpen)
        {
            Show(request);
            return;
        }

        ApplyState(CreateStateFromRequest(request));
    }

    private static HostedDialogState CreateStateFromRequest(HostedDialogRequest request)
    {
        var baseState = new HostedDialogState
        {
            IsOpen = true,
            Title = request.Title?.Trim() ?? string.Empty,
            Content = request.Content,
            PrimaryButtonText = request.PrimaryButtonText?.Trim() ?? string.Empty,
            SecondaryButtonText = request.SecondaryButtonText?.Trim() ?? string.Empty,
            TertiaryButtonText = request.TertiaryButtonText?.Trim() ?? string.Empty,
            CloseOnOverlay = request.CloseOnOverlay,
            PrimaryAction = request.PrimaryAction,
            SecondaryAction = request.SecondaryAction,
            TertiaryAction = request.TertiaryAction,
            ClosedAction = request.OnClosed,
            PreferContentFocus = request.PreferContentFocus
        };

        return CreateAdaptiveState(
            baseState,
            request.Width,
            request.MinWidth,
            request.MaxWidth,
            request.Height,
            request.MinHeight,
            double.PositiveInfinity,
            double.PositiveInfinity);
    }

    internal void InvokePrimary()
        => _current.PrimaryAction?.Invoke();

    internal void InvokeSecondary()
        => _current.SecondaryAction?.Invoke();

    internal void InvokeTertiary()
        => _current.TertiaryAction?.Invoke();

    internal void UpdateCurrentSize(double width, double minWidth, double maxWidth, double height, double minHeight)
    {
        if (!IsOpen)
            return;

        ApplyState(CreateAdaptiveState(
            _current,
            width > 0 ? width : _current.RequestedWidth,
            minWidth > 0 ? minWidth : _current.RequestedMinWidth,
            maxWidth > 0 ? maxWidth : _current.RequestedMaxWidth,
            double.IsNaN(height) ? _current.RequestedHeight : height,
            minHeight > 0 ? minHeight : _current.RequestedMinHeight,
            double.PositiveInfinity,
            double.PositiveInfinity));
    }

    internal void ApplyAvailableBounds(double availableWidth, double availableHeight)
    {
        if (!IsOpen)
            return;

        ApplyState(CreateAdaptiveState(
            _current,
            _current.RequestedWidth,
            _current.RequestedMinWidth,
            _current.RequestedMaxWidth,
            _current.RequestedHeight,
            _current.RequestedMinHeight,
            availableWidth,
            availableHeight));
    }

    internal void Close()
    {
        var closedAction = _current.ClosedAction;

        if (_stack.Count > 0)
        {
            ApplyState(_stack.Pop());
        }
        else
        {
            ApplyState(CreateClosedState());
        }

        closedAction?.Invoke();
    }

    private static HostedDialogState CreateClosedState()
    {
        return new HostedDialogState
        {
            IsOpen = false,
            Title = string.Empty,
            Content = null,
            PrimaryButtonText = string.Empty,
            SecondaryButtonText = string.Empty,
            TertiaryButtonText = string.Empty,
            CloseOnOverlay = false,
            Width = 580,
            MinWidth = 420,
            MaxWidth = 580,
            Height = double.NaN,
            MinHeight = 0,
            PrimaryAction = null,
            SecondaryAction = null,
            TertiaryAction = null,
            ClosedAction = null,
            PreferContentFocus = false
        };
    }

    private static HostedDialogState CreateAdaptiveState(
        HostedDialogState baseState,
        double requestedWidth,
        double requestedMinWidth,
        double requestedMaxWidth,
        double requestedHeight,
        double requestedMinHeight,
        double availableWidth,
        double availableHeight)
    {
        var resolvedRequestedWidth = requestedWidth > 0 ? requestedWidth : 580;
        var resolvedRequestedMinWidth = requestedMinWidth > 0 ? requestedMinWidth : resolvedRequestedWidth;
        var resolvedRequestedMaxWidth = requestedMaxWidth > 0 ? requestedMaxWidth : resolvedRequestedWidth;
        resolvedRequestedMaxWidth = Math.Max(resolvedRequestedMaxWidth, resolvedRequestedMinWidth);

        var effectiveMaxWidth = resolvedRequestedMaxWidth;
        if (availableWidth > 0 && !double.IsInfinity(availableWidth))
            effectiveMaxWidth = Math.Min(effectiveMaxWidth, availableWidth);

        var effectiveMinWidth = Math.Min(resolvedRequestedMinWidth, effectiveMaxWidth);
        var effectiveWidth = Math.Clamp(resolvedRequestedWidth, effectiveMinWidth, effectiveMaxWidth);

        var effectiveHeight = requestedHeight;
        var effectiveMinHeight = requestedMinHeight > 0 ? requestedMinHeight : 0;

        if (!double.IsNaN(requestedHeight) && availableHeight > 0 && !double.IsInfinity(availableHeight))
        {
            effectiveHeight = Math.Min(requestedHeight, availableHeight);
            effectiveMinHeight = Math.Min(effectiveMinHeight, effectiveHeight);
        }
        else if (double.IsNaN(requestedHeight) && availableHeight > 0 && !double.IsInfinity(availableHeight))
        {
            effectiveMinHeight = Math.Min(effectiveMinHeight, availableHeight);
        }

        return new HostedDialogState
        {
            IsOpen = baseState.IsOpen,
            Title = baseState.Title,
            Content = baseState.Content,
            PrimaryButtonText = baseState.PrimaryButtonText,
            SecondaryButtonText = baseState.SecondaryButtonText,
            TertiaryButtonText = baseState.TertiaryButtonText,
            CloseOnOverlay = baseState.CloseOnOverlay,
            RequestedWidth = resolvedRequestedWidth,
            RequestedMinWidth = resolvedRequestedMinWidth,
            RequestedMaxWidth = resolvedRequestedMaxWidth,
            RequestedHeight = requestedHeight,
            RequestedMinHeight = requestedMinHeight,
            Width = effectiveWidth,
            MinWidth = effectiveMinWidth,
            MaxWidth = effectiveMaxWidth,
            Height = effectiveHeight,
            MinHeight = effectiveMinHeight,
            PrimaryAction = baseState.PrimaryAction,
            SecondaryAction = baseState.SecondaryAction,
            TertiaryAction = baseState.TertiaryAction,
            ClosedAction = baseState.ClosedAction,
            PreferContentFocus = baseState.PreferContentFocus
        };
    }

    private void ApplyState(HostedDialogState state)
    {
        if (ReferenceEquals(_current, state))
            return;

        _current = state;
        OnPropertyChanged(string.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed class HostedDialogRequest
{
    public string Title { get; set; } = string.Empty;
    public object? Content { get; set; }
    public Action? AfterShown { get; set; }
    public string PrimaryButtonText { get; set; } = string.Empty;
    public Action? PrimaryAction { get; set; }
    public string SecondaryButtonText { get; set; } = string.Empty;
    public Action? SecondaryAction { get; set; }
    public string TertiaryButtonText { get; set; } = string.Empty;
    public Action? TertiaryAction { get; set; }
    public bool CloseOnOverlay { get; set; }
    public double Width { get; set; } = 580;
    public double MinWidth { get; set; } = 420;
    public double MaxWidth { get; set; } = 580;
    public double Height { get; set; } = double.NaN;
    public double MinHeight { get; set; }
    public Action? OnClosed { get; set; }
    public bool PreferContentFocus { get; set; }
}



