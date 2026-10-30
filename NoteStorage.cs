using System;
using System.IO;
using System.Text.Json;

namespace SimpleNote
{
    public class NoteData
    {
        public string Content { get; set; } = string.Empty;
        public bool IsPinned { get; set; } = false;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }

    public static class NoteStorage
    {
        private static readonly object _fileLock = new object();

        private static readonly string StorageFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimpleNote");

        private static readonly string StorageFile = Path.Combine(StorageFolder, "notes.json");
        private static readonly string BackupFile = Path.Combine(StorageFolder, "notes.json.bak");
        private static readonly string TempFile = Path.Combine(StorageFolder, "notes.json.tmp");

        public static NoteData LoadNotes()
        {
            lock (_fileLock)
            {
                // 1. Try reading the primary notes.json file
                if (File.Exists(StorageFile))
                {
                    var data = TryDeserialize(StorageFile);
                    if (data != null)
                    {
                        return data;
                    }
                }

                // 2. If primary file was missing or corrupted (e.g. abrupt power cut), try the backup file
                if (File.Exists(BackupFile))
                {
                    var data = TryDeserialize(BackupFile);
                    if (data != null)
                    {
                        return data;
                    }
                }

                // 3. Fallback only on first-ever launch when neither file exists
                return new NoteData
                {
                    Content = "• Welcome to SimpleNote!\r\n• Hover over the right edge handle anytime to slide this open.\r\n• Press Enter for an instant new bullet.\r\n• Press Backspace on an empty bullet to delete it cleanly.\r\n• Click 📌 Pin at top to keep open while working in other apps.\r\n• Click the system tray icon to toggle visibility.\r\n• Everything is automatically saved.",
                    IsPinned = false,
                    LastModified = DateTime.UtcNow
                };
            }
        }

        private static NoteData? TryDeserialize(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json)) return null;

                var data = JsonSerializer.Deserialize<NoteData>(json);
                if (data != null && data.Content != null)
                {
                    return data;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading {filePath}: {ex.Message}");
            }
            return null;
        }

        public static void SaveNotes(string content, bool isPinned)
        {
            lock (_fileLock)
            {
                try
                {
                    if (!Directory.Exists(StorageFolder))
                    {
                        Directory.CreateDirectory(StorageFolder);
                    }

                    var data = new NoteData
                    {
                        Content = content ?? string.Empty,
                        IsPinned = isPinned,
                        LastModified = DateTime.UtcNow
                    };

                    string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);

                    // Write to temporary file with WriteThrough & synchronous flush directly to physical disk
                    using (var fs = new FileStream(TempFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                    {
                        fs.Write(bytes, 0, bytes.Length);
                        fs.Flush(flushToDisk: true);
                    }

                    // Keep backup of previous good file
                    if (File.Exists(StorageFile))
                    {
                        try
                        {
                            File.Copy(StorageFile, BackupFile, overwrite: true);
                        }
                        catch { }
                    }

                    // Atomic replace
                    File.Move(TempFile, StorageFile, overwrite: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SaveNotes error: {ex.Message}");
                }
            }
        }
    }
}
