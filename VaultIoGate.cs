using System;
using System.Diagnostics;
using System.Threading;

namespace PassNotes;

/// <summary>
/// Serializes all vault file I/O within the process.
///
/// This gate is shared by VaultStore (Save/Load) and BackupService (Restore/backup copies)
/// so that operations do not overlap and cause rare IO races.
/// </summary>
internal static class VaultIoGate
{
    // Re-entrant gate: some operations (e.g. orphan cleanup) may call into VaultStore.Save/Load
    // while already holding the gate. Using a re-entrant lock avoids self-deadlocks.
    private static readonly object _gate = new();

    // Log only when an operation had to wait noticeably to enter the gate.
    // This is best-effort diagnostics that should never break the app.
    private const int SlowWaitThresholdMs = 500;

    private static string? _currentOperation;

    public static void Run(string operation, Action action)
    {
        var holderBeforeWait = Volatile.Read(ref _currentOperation);

        var sw = Stopwatch.StartNew();
        var lockTaken = false;
        try
        {
            Monitor.Enter(_gate, ref lockTaken);
            sw.Stop();

            if (sw.ElapsedMilliseconds >= SlowWaitThresholdMs)
                LogSlowWait(operation, sw.ElapsedMilliseconds, holderBeforeWait);

            // Keep the first operation name while holding the gate.
            // Nested calls should not overwrite it (helps diagnostics for slow waits).
            var setOp = false;
            if (string.IsNullOrWhiteSpace(Volatile.Read(ref _currentOperation)))
            {
                Volatile.Write(ref _currentOperation, operation);
                setOp = true;
            }

            try { action(); }
            finally
            {
                if (setOp)
                    Volatile.Write(ref _currentOperation, null);
            }
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_gate);
        }
    }

    public static T Run<T>(string operation, Func<T> func)
    {
        var holderBeforeWait = Volatile.Read(ref _currentOperation);

        var sw = Stopwatch.StartNew();
        var lockTaken = false;
        try
        {
            Monitor.Enter(_gate, ref lockTaken);
            sw.Stop();

            if (sw.ElapsedMilliseconds >= SlowWaitThresholdMs)
                LogSlowWait(operation, sw.ElapsedMilliseconds, holderBeforeWait);

            var setOp = false;
            if (string.IsNullOrWhiteSpace(Volatile.Read(ref _currentOperation)))
            {
                Volatile.Write(ref _currentOperation, operation);
                setOp = true;
            }

            try { return func(); }
            finally
            {
                if (setOp)
                    Volatile.Write(ref _currentOperation, null);
            }
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_gate);
        }
    }

    // Backward-compatible overloads (kept to avoid touching every call site if needed).
    public static void Run(Action action) => Run("(unnamed)", action);

    public static T Run<T>(Func<T> func) => Run("(unnamed)", func);

    /// <summary>
    /// Best-effort, non-blocking entry to the gate.
    /// Returns false if the gate is currently held by another operation.
    /// </summary>
    public static bool TryRun(string operation, Action action)
    {
        var lockTaken = false;
        try
        {
            // Re-entrant: returns true immediately if the current thread already holds the lock.
            lockTaken = Monitor.TryEnter(_gate);
            if (!lockTaken)
                return false;

            // Keep the first operation name while holding the gate.
            var setOp = false;
            if (string.IsNullOrWhiteSpace(Volatile.Read(ref _currentOperation)))
            {
                Volatile.Write(ref _currentOperation, operation);
                setOp = true;
            }

            try { action(); }
            finally
            {
                if (setOp)
                    Volatile.Write(ref _currentOperation, null);
            }

            return true;
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_gate);
        }
    }

    private static void LogSlowWait(string operation, long waitMs, string? holderBeforeWait)
    {
        try
        {
            var holder = string.IsNullOrWhiteSpace(holderBeforeWait) ? "unknown" : holderBeforeWait;
            DiagnosticsLog.AppendLine(
                "IO_GATE_WAIT",
                $"waitMs={waitMs} operation={operation} heldBy={holder} thread=T{Environment.CurrentManagedThreadId}");
        }
        catch
        {
            // best-effort
        }
    }
}
