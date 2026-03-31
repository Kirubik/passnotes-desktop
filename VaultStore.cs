using System;
using System.IO;
using System.Text.Json;

namespace PassNotes;

public sealed class VaultStore
{
    private string _path;

    public VaultStore(string? vaultPath = null)
    {
        _path = NormalizePath(vaultPath);
    }

    public string Path => _path;

    public void SetPath(string vaultPath)
    {
        _path = NormalizePath(vaultPath);
    }

    private static string NormalizePath(string? p)
    {
        if (string.IsNullOrWhiteSpace(p))
            return SettingsStore.GetDefaultVaultPath();
        p = p.Trim();
        // Treat relative paths as relative to the app data folder.
        if (!System.IO.Path.IsPathRooted(p))
            return System.IO.Path.Combine(SettingsStore.GetAppDir(), p);
        return p;
    }

    public bool Exists => File.Exists(_path);

    public VaultData Load(string masterPassword)
    {
        return VaultIoGate.Run("VaultStore.Load", () =>
        {
            var blob = File.ReadAllBytes(_path);
            var jsonBytes = VaultCrypto.Decrypt(masterPassword, blob);

            try
            {
                var data = JsonSerializer.Deserialize<VaultData>(jsonBytes) ?? new VaultData();

                data.Entries ??= Array.Empty<VaultEntry>();
                data.Folders ??= Array.Empty<VaultFolder>();
                data.Attachments ??= Array.Empty<VaultAttachment>();

                // Upgrade older vaults
                if (data.Version < 2)
                {
                    data.Version = 2;
                    // FolderId will be null by default for older entries (=> "No folder")
                }

                // v3: trash / soft-delete fields (defaults are fine; bump version to indicate schema)
                if (data.Version < 3)
                {
                    data.Version = 3;
                }

                // v4: attachments
                if (data.Version < 4)
                {
                    data.Version = 4;
                    data.Attachments ??= Array.Empty<VaultAttachment>();
                }

return data;
            }
            finally
            {
                Array.Clear(jsonBytes, 0, jsonBytes.Length);
            }
        });
    }

    public void Save(string masterPassword, VaultData data)
    {
        VaultIoGate.Run("VaultStore.Save", () =>
        {
            data.Version = Math.Max(data.Version, 4);
            data.Entries ??= Array.Empty<VaultEntry>();
            data.Folders ??= Array.Empty<VaultFolder>();
            data.Attachments ??= Array.Empty<VaultAttachment>();

            var json = JsonSerializer.SerializeToUtf8Bytes(data);
            byte[]? blob = null;
            string? tempPath = null;

            try
            {
                blob = VaultCrypto.Encrypt(masterPassword, json);

                var vaultDir = System.IO.Path.GetDirectoryName(_path) ?? SettingsStore.GetAppDir();
                Directory.CreateDirectory(vaultDir);

                // Write to a temp file in the same directory, then atomically replace.
                tempPath = System.IO.Path.Combine(vaultDir, $".vault_write_{Guid.NewGuid():N}.tmp");

                using (var fs = new FileStream(
                           tempPath,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.None,
                           bufferSize: 4096,
                           options: FileOptions.WriteThrough))
                {
                    fs.Write(blob, 0, blob.Length);
                    fs.Flush(flushToDisk: true);
                }

                if (File.Exists(_path))
                {
                    var prevPath = _path + ".prev";

                    // Try atomic replace first.
                    try
                    {
                        File.Replace(tempPath, _path, destinationBackupFileName: prevPath, ignoreMetadataErrors: true);
                        // File.Replace removes tempPath.
                        tempPath = null;
                    }
                    catch
                    {
                        // Fallback to overwrite copy (still safe-ish because the temp file is complete).
                        // Keep a best-effort previous copy.
                        try { File.Copy(_path, prevPath, overwrite: true); } catch { }

                        if (tempPath is not null)
                        {
                            File.Copy(tempPath, _path, overwrite: true);
                            try { File.Delete(tempPath); } catch { }
                            tempPath = null;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                else
                {
                    File.Move(tempPath, _path);
                    tempPath = null;
                }
            }
            finally
            {
                if (tempPath is not null && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }

                Array.Clear(json, 0, json.Length);

                if (blob is { Length: > 0 })
                    Array.Clear(blob, 0, blob.Length);
            }
        });
    }
}
