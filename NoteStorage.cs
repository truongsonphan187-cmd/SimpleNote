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
        private static readonly string StorageFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimpleNote");

        private static readonly string StorageFile = Path.Combine(StorageFolder, "notes.json");

        public static NoteData LoadNotes()
        {
            try
            {
                if (!Directory.Exists(StorageFolder))
                {
                    Directory.CreateDirectory(StorageFolder);
                }

                if (File.Exists(StorageFile))
                {
                    string json = File.ReadAllText(StorageFile);
                    var data = JsonSerializer.Deserialize<NoteData>(json);
                    if (data != null && !string.IsNullOrEmpty(data.Content))
                    {
                        return data;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback gracefully on any IO/Deserialization error
            }

            return new NoteData
            {
                Content = "• Welcome to SimpleNote!\r\n• Hover over the right edge handle anytime to slide this open.\r\n• Press Enter for an instant new bullet.\r\n• Press Backspace on an empty bullet to delete it cleanly.\r\n• Click 📌 Pin at top to keep open while working in other apps.\r\n• Click the system tray icon to toggle visibility.\r\n• Everything is automatically saved.",
                IsPinned = false,
                LastModified = DateTime.UtcNow
            };
        }

        public static void SaveNotes(string content, bool isPinned)
        {
            try
            {
                if (!Directory.Exists(StorageFolder))
                {
                    Directory.CreateDirectory(StorageFolder);
                }

                var data = new NoteData
                {
                    Content = content,
                    IsPinned = isPinned,
                    LastModified = DateTime.UtcNow
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StorageFile, json);
            }
            catch (Exception)
            {
                // Best effort save
            }
        }
    }
}
