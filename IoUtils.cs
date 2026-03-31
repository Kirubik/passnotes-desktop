using System;
using System.IO;

namespace PassNotes;

internal static class IoUtils
{
    /// <summary>
    /// Reads all bytes from a file allowing other processes to keep the file open.
    /// </summary>
    internal static byte[] ReadAllBytesShared(string filePath)
    {
        using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var ms = new MemoryStream();
        fs.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Writes bytes to a destination file using a temp file + replace/move.
    /// Best-effort atomicity.
    /// </summary>
    internal static void WriteBytesSafely(byte[] bytes, string destinationFilePath)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));

        if (string.IsNullOrWhiteSpace(destinationFilePath))
            throw new ArgumentException("Destination file path is empty", nameof(destinationFilePath));

        var dir = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var tempPath = destinationFilePath + ".tmp_" + Guid.NewGuid().ToString("N");
        try
        {
            using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }

            if (File.Exists(destinationFilePath))
            {
                try
                {
                    File.Replace(tempPath, destinationFilePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                    tempPath = "";
                }
                catch
                {
                    File.Copy(tempPath, destinationFilePath, overwrite: true);
                    try { File.Delete(tempPath); } catch { }
                    tempPath = "";
                }
            }
            else
            {
                File.Move(tempPath, destinationFilePath);
                tempPath = "";
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }
}
