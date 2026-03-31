using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace PassNotes;

public partial class MainWindow
{
    private const double HostedDialogHostOuterMargin = 48.0;

    private int AdvanceHostedDialogTransitionVersion()
    {
        unchecked { _hostedDialogTransitionVersion++; }
        return _hostedDialogTransitionVersion;
    }

    private bool IsHostedDialogTransitionCurrent(int version)
        => version == _hostedDialogTransitionVersion;

    private static DependencyObject? GetVisualOrLogicalParent(DependencyObject? child)
    {
        if (child == null)
            return null;

        try
        {
            if (child is Visual || child is Visual3D)
                return VisualTreeHelper.GetParent(child);
        }
        catch
        {
            // ignore
        }

        try
        {
            return LogicalTreeHelper.GetParent(child);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsDescendantOf(DependencyObject? child, DependencyObject? ancestor)
    {
        while (child != null)
        {
            if (ReferenceEquals(child, ancestor))
                return true;

            child = GetVisualOrLogicalParent(child);
        }

        return false;
    }

    private bool TryGetOwnedInputTarget(IInputElement? candidate, out IInputElement? target, out DependencyObject? dependencyObject)
    {
        target = null;
        dependencyObject = null;

        if (candidate is not IInputElement input || candidate is not DependencyObject dep || Window.GetWindow(dep) != this)
            return false;

        target = input;
        dependencyObject = dep;
        return true;
    }

    private bool IsCurrentHostedDialogContentTarget(IInputElement? candidate)
    {
        if (HostedDialogHost.Content is not DependencyObject contentRoot)
            return false;

        return TryGetOwnedInputTarget(candidate, out _, out var dependencyObject)
            && dependencyObject != null
            && IsDescendantOf(dependencyObject, contentRoot);
    }

    private bool IsHostedDialogLayerTarget(IInputElement? candidate)
    {
        if (HostedDialogLayer == null)
            return false;

        return TryGetOwnedInputTarget(candidate, out _, out var dependencyObject)
            && dependencyObject != null
            && !ReferenceEquals(dependencyObject, HostedDialogFocusAnchor)
            && IsDescendantOf(dependencyObject, HostedDialogLayer);
    }

    private bool IsHostedDialogScopeTarget(IInputElement? candidate)
        => ReferenceEquals(candidate, HostedDialogFocusAnchor) || IsHostedDialogLayerTarget(candidate);

    private bool IsShellContentTarget(IInputElement? candidate)
    {
        if (candidate == null || ShellContentRoot == null)
            return false;

        return TryGetOwnedInputTarget(candidate, out _, out var dependencyObject)
            && dependencyObject != null
            && IsDescendantOf(dependencyObject, ShellContentRoot);
    }

    private bool IsMainWindowBackgroundTarget(IInputElement? candidate)
    {
        if (candidate == null || IsHostedDialogScopeTarget(candidate))
            return false;

        // The only truly forbidden targets while a hosted modal is open are controls that belong
        // to the background shell itself. Popup-based routes (ComboBox dropdowns, ContextMenus,
        // Popup content) may live outside the normal visual tree and must not be treated as leaks.
        return IsShellContentTarget(candidate);
    }

    private IInputElement? CaptureWindowFocusTarget()
    {
        if (TryGetOwnedInputTarget(Keyboard.FocusedElement, out var keyboardTarget, out _))
            return keyboardTarget;

        var scopedTarget = FocusManager.GetFocusedElement(this);
        if (TryGetOwnedInputTarget(scopedTarget, out var scopedInput, out _))
            return scopedInput;

        return null;
    }

    private IInputElement? CaptureHostedDialogFocusTarget()
    {
        if (HostedDialogHost.IsOpen)
        {
            if (HostedDialogLayer != null)
            {
                var hostedScopedTarget = FocusManager.GetFocusedElement(HostedDialogLayer);
                if (IsCurrentHostedDialogContentTarget(hostedScopedTarget) || IsHostedDialogLayerTarget(hostedScopedTarget))
                    return hostedScopedTarget as IInputElement;
            }

            if (IsCurrentHostedDialogContentTarget(Keyboard.FocusedElement) || IsHostedDialogLayerTarget(Keyboard.FocusedElement))
                return Keyboard.FocusedElement as IInputElement;

            return null;
        }

        return CaptureWindowFocusTarget();
    }

    private bool TryFocusOwnedInputTarget(IInputElement? target)
    {
        if (target is not DependencyObject dependencyObject || Window.GetWindow(dependencyObject) != this)
            return false;

        try
        {
            Keyboard.Focus(target);
            return Keyboard.FocusedElement == target;
        }
        catch
        {
            return false;
        }
    }

    private bool TryFocusHostedDialogAnchor()
    {
        if (HostedDialogFocusAnchor == null)
            return false;

        try
        {
            HostedDialogFocusAnchor.Focus();
            Keyboard.Focus(HostedDialogFocusAnchor);
            return Keyboard.FocusedElement == HostedDialogFocusAnchor;
        }
        catch
        {
            return false;
        }
    }

    private void ParkHostedDialogFocusForContentTransition()
    {
        if (!HostedDialogHost.IsOpen)
            return;

        try { HostedDialogLayer?.UpdateLayout(); } catch { }
        TryFocusHostedDialogAnchor();
    }

    private bool EnsureHostedDialogFocusIsolation()
    {
        if (!HostedDialogHost.IsOpen)
            return false;

        if (TryFocusHostedDialogPreferredTarget())
            return true;

        return TryFocusHostedDialogAnchor();
    }

    private bool TryFocusHostedDialogPreferredTarget()
    {
        if (HostedDialogHost.PreferContentFocus && TryFocusHostedDialogContent())
            return true;

        if (HostedDialogPrimaryButton != null && HostedDialogPrimaryButton.Visibility == Visibility.Visible)
        {
            try
            {
                HostedDialogPrimaryButton.Focus();
                Keyboard.Focus(HostedDialogPrimaryButton);
                return true;
            }
            catch
            {
                // ignore
            }
        }

        return HostedDialogHost.PreferContentFocus && TryFocusHostedDialogContent();
    }

    private bool TryFocusHostedDialogContent()
    {
        if (HostedDialogHost.Content is not DependencyObject root)
            return false;

        if (root is IInputElement inputRoot && root is UIElement rootElement && rootElement.Focusable && rootElement.IsEnabled && rootElement.Visibility == Visibility.Visible)
        {
            if (TryFocusOwnedInputTarget(inputRoot))
                return true;
        }

        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var childCount = 0;

            try { childCount = VisualTreeHelper.GetChildrenCount(current); } catch { childCount = 0; }

            for (var i = 0; i < childCount; i++)
            {
                DependencyObject? child = null;
                try { child = VisualTreeHelper.GetChild(current, i); } catch { child = null; }
                if (child == null)
                    continue;

                if (child is IInputElement inputChild && child is UIElement element && element.Focusable && element.IsEnabled && element.Visibility == Visibility.Visible)
                {
                    if (TryFocusOwnedInputTarget(inputChild))
                        return true;
                }

                queue.Enqueue(child);
            }
        }

        return false;
    }

    private bool HasInteractiveHostedPopupOpen()
    {
        if (HostedDialogHost.Content is not DependencyObject root)
            return false;

        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current is ComboBox comboBox)
            {
                try
                {
                    if (comboBox.IsDropDownOpen)
                        return true;
                }
                catch
                {
                    // ignore
                }
            }

            if (current is Popup popup)
            {
                try
                {
                    if (popup.IsOpen && popup.Focusable)
                        return true;
                }
                catch
                {
                    // ignore
                }
            }

            if (current is FrameworkElement frameworkElement)
            {
                try
                {
                    if (frameworkElement.ContextMenu?.IsOpen == true)
                        return true;
                }
                catch
                {
                    // ignore
                }
            }

            if (current is FrameworkContentElement frameworkContentElement)
            {
                try
                {
                    if (frameworkContentElement.ContextMenu?.IsOpen == true)
                        return true;
                }
                catch
                {
                    // ignore
                }
            }

            var childCount = 0;
            try { childCount = VisualTreeHelper.GetChildrenCount(current); } catch { childCount = 0; }

            for (var i = 0; i < childCount; i++)
            {
                try
                {
                    var child = VisualTreeHelper.GetChild(current, i);
                    if (child != null)
                        queue.Enqueue(child);
                }
                catch
                {
                    // ignore
                }
            }
        }

        return false;
    }

    private void RunDeferredHostedUiAction(string context, Action action)
    {
        if (action == null)
            return;

        // Run after the current key event completes, but before the next render pass.
        // Background was safe for reentrancy, but it allowed one stale frame of the
        // previous focused control to be rendered under the hosted overlay.
        Dispatcher.BeginInvoke(new Action(() => SafeUi(context, action)), DispatcherPriority.Input);
    }

    private void MainWindow_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!HostedDialogHost.IsOpen)
            return;

        if (!IsMainWindowBackgroundTarget(e.NewFocus as IInputElement))
            return;

        e.Handled = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                EnsureHostedDialogFocusIsolation();
            }
            catch
            {
                // ignore
            }
        }), DispatcherPriority.Input);
    }

    internal void RunHostedKeyboardActionDeferred(string context, Action action)
        => RunDeferredHostedUiAction(context, action);

    private void ShowHostedDialogModal(HostedDialogRequest request)
    {
        var frame = new DispatcherFrame();
        _hostedDialogModalFrames.Push(frame);

        WrapHostedDialogModalRequest(request, frame);

        try
        {
            ShowHostedDialog(request);
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            if (_hostedDialogModalFrames.Count > 0 && ReferenceEquals(_hostedDialogModalFrames.Peek(), frame))
                _hostedDialogModalFrames.Pop();
        }
    }

    private void ReplaceHostedDialogModal(HostedDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var frame = new DispatcherFrame();
        _hostedDialogModalFrames.Push(frame);

        WrapHostedDialogModalRequest(request, frame);

        try
        {
            ReplaceHostedDialog(request);
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            if (_hostedDialogModalFrames.Count > 0 && ReferenceEquals(_hostedDialogModalFrames.Peek(), frame))
                _hostedDialogModalFrames.Pop();
        }
    }

    private static void WrapHostedDialogModalRequest(HostedDialogRequest request, DispatcherFrame frame)
    {
        var closed = request.OnClosed;
        request.OnClosed = () =>
        {
            try
            {
                closed?.Invoke();
            }
            finally
            {
                frame.Continue = false;
            }
        };
    }

    private void ShowHostedDialog(HostedDialogRequest request)
    {
        var transitionVersion = AdvanceHostedDialogTransitionVersion();
        _hostedDialogFocusStack.Push(CaptureHostedDialogFocusTarget());
        ParkHostedDialogFocusForContentTransition();
        HostedDialogHost.Show(request);
        RefreshHostedDialogLayout();
        try { HostedDialogLayer?.UpdateLayout(); } catch { }
        TryFocusHostedDialogAnchor();
        request.AfterShown?.Invoke();
        FocusHostedDialogAfterShow(transitionVersion);
    }

    private void ReplaceHostedDialog(HostedDialogRequest request)
    {
        var transitionVersion = AdvanceHostedDialogTransitionVersion();
        _hostedDialogFocusStack.Push(CaptureHostedDialogFocusTarget());
        ParkHostedDialogFocusForContentTransition();
        HostedDialogHost.ReplaceCurrent(request);
        RefreshHostedDialogLayout();
        try { HostedDialogLayer?.UpdateLayout(); } catch { }
        TryFocusHostedDialogAnchor();
        request.AfterShown?.Invoke();
        FocusHostedDialogAfterShow(transitionVersion);
    }

    private void FocusHostedDialogAfterShow(int transitionVersion)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (!IsHostedDialogTransitionCurrent(transitionVersion) || !HostedDialogHost.IsOpen)
                    return;

                RefreshHostedDialogLayout();
                TryFocusHostedDialogPreferredTarget();
            }
            catch
            {
                // ignore
            }
        }), DispatcherPriority.Input);
    }

    private void HostedDialogLayer_SizeChanged(object sender, SizeChangedEventArgs e)
        => RefreshHostedDialogLayout();

    private void RefreshHostedDialogLayout()
    {
        if (!HostedDialogHost.IsOpen)
            return;

        var hostWidth = HostedDialogLayer?.ActualWidth > 0 ? HostedDialogLayer.ActualWidth : ActualWidth;
        var hostHeight = HostedDialogLayer?.ActualHeight > 0 ? HostedDialogLayer.ActualHeight : ActualHeight;

        var availableWidth = Math.Max(320, hostWidth - HostedDialogHostOuterMargin);
        var availableHeight = Math.Max(240, hostHeight - HostedDialogHostOuterMargin);

        HostedDialogHost.ApplyAvailableBounds(availableWidth, availableHeight);

        if (_hostedHelpView != null && ReferenceEquals(HostedDialogHost.Content, _hostedHelpView))
            TryUpdateHostedHelpDialogSize();
    }

    private void CloseHostedDialog()
    {
        var transitionVersion = AdvanceHostedDialogTransitionVersion();
        ParkHostedDialogFocusForContentTransition();
        HostedDialogHost.Close();
        TryCommitPendingVisualUnlockIfReady();
        TryCommitPendingHostedUiCommitIfReady();

        if (HostedDialogHost.IsOpen)
        {
            RestoreFocusForRevealedHostedDialog(transitionVersion);
            return;
        }

        RestoreFocusAfterHostedDialogClosed(transitionVersion);
    }

    private void RestoreFocusForRevealedHostedDialog(int transitionVersion)
    {
        var target = _hostedDialogFocusStack.Count > 0 ? _hostedDialogFocusStack.Pop() : null;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (!IsHostedDialogTransitionCurrent(transitionVersion) || !HostedDialogHost.IsOpen)
                    return;

                if (IsCurrentHostedDialogContentTarget(target) && TryFocusOwnedInputTarget(target))
                    return;

                TryFocusHostedDialogPreferredTarget();
            }
            catch
            {
                // ignore
            }
        }), DispatcherPriority.ContextIdle);
    }

    private void RestoreFocusAfterHostedDialogClosed(int transitionVersion)
    {
        var target = _hostedDialogFocusStack.Count > 0 ? _hostedDialogFocusStack.Pop() : null;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (!IsHostedDialogTransitionCurrent(transitionVersion) || HostedDialogHost.IsOpen)
                    return;

                if (target is DependencyObject d && target is IInputElement input && Window.GetWindow(d) == this)
                {
                    Keyboard.Focus(input);
                    return;
                }

                Focus();
            }
            catch
            {
                // ignore
            }
        }), DispatcherPriority.ContextIdle);
    }

    private void HostedDialogPrimaryButton_Click(object sender, RoutedEventArgs e)
        => RunDeferredHostedUiAction(nameof(HostedDialogPrimaryButton_Click), HostedDialogHost.InvokePrimary);

    private void HostedDialogSecondaryButton_Click(object sender, RoutedEventArgs e)
        => RunDeferredHostedUiAction(nameof(HostedDialogSecondaryButton_Click), HostedDialogHost.InvokeSecondary);

    private void HostedDialogTertiaryButton_Click(object sender, RoutedEventArgs e)
        => RunDeferredHostedUiAction(nameof(HostedDialogTertiaryButton_Click), HostedDialogHost.InvokeTertiary);

    private void HostedDialogBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!HostedDialogHost.IsOpen || !HostedDialogHost.CloseOnOverlay)
            return;

        CloseHostedDialog();
        e.Handled = true;
    }
}
