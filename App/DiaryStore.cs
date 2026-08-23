using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace FamilyPlanner
{
    [DataContract]
    public sealed class DiaryEntry
    {
        [DataMember] public DateTime Date;
        [DataMember] public string Title;
        [DataMember] public string Content;
        [DataMember] public DateTime UpdatedAt;
    }

    public static class DiaryStore
    {
        static readonly string Folder = AppDataPaths.Root;
        static readonly string FilePath = Path.Combine(Folder, "diary.json");
        static readonly string BackupFolder = AppDataPaths.Backups;
        static readonly Mutex FileMutex = new Mutex(false, "Local\\Onharu.DiaryFileLock");

        public static List<DiaryEntry> Load()
        {
            var entered = EnterMutex();
            try
            {
                if (!File.Exists(FilePath)) return new List<DiaryEntry>();
                using (var stream = File.OpenRead(FilePath))
                    return Normalize((List<DiaryEntry>)new DataContractJsonSerializer(typeof(List<DiaryEntry>)).ReadObject(stream));
            }
            catch (Exception ex) { ErrorLog.Write("Load diary data", ex); return new List<DiaryEntry>(); }
            finally { if (entered) FileMutex.ReleaseMutex(); }
        }

        public static void Upsert(DiaryEntry entry, DateTime? previousDate = null)
        {
            if (entry == null) return;
            var entries = Load();
            if (previousDate.HasValue) entries.RemoveAll(x => x.Date.Date == previousDate.Value.Date);
            entries.RemoveAll(x => x.Date.Date == entry.Date.Date);
            entry.Date = entry.Date.Date; entry.UpdatedAt = DateTime.Now;
            entries.Add(entry); Save(entries);
        }

        public static void Delete(DateTime date)
        {
            var entries = Load();
            if (entries.RemoveAll(x => x.Date.Date == date.Date) > 0) Save(entries);
        }

        static void Save(List<DiaryEntry> entries)
        {
            var entered = EnterMutex();
            try
            {
                Directory.CreateDirectory(Folder);
                WriteAtomic(FilePath, Normalize(entries));
                Directory.CreateDirectory(BackupFolder);
                WriteAtomic(Path.Combine(BackupFolder, "diary-" + DateTime.Today.ToString("yyyyMMdd") + ".json"), Normalize(entries));
            }
            catch (Exception ex) { ErrorLog.Write("Save diary data", ex); throw; }
            finally { if (entered) FileMutex.ReleaseMutex(); }
        }

        static List<DiaryEntry> Normalize(IEnumerable<DiaryEntry> source)
        {
            return (source ?? Enumerable.Empty<DiaryEntry>()).Where(x => x != null && x.Date.Year >= 1900 && x.Date.Year <= 9998)
                .GroupBy(x => x.Date.Date).Select(x => x.OrderByDescending(y => y.UpdatedAt).First()).OrderByDescending(x => x.Date).ToList();
        }

        static void WriteAtomic(string path, List<DiaryEntry> entries)
        {
            var temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                new DataContractJsonSerializer(typeof(List<DiaryEntry>)).WriteObject(stream, entries);
            if (File.Exists(path))
            {
                var replaced = false;
                try { File.Replace(temp, path, null); replaced = true; } catch { }
                if (!replaced) { File.Copy(temp, path, true); File.Delete(temp); }
            }
            else File.Move(temp, path);
        }

        static bool EnterMutex()
        {
            try { return FileMutex.WaitOne(TimeSpan.FromSeconds(5)); }
            catch (AbandonedMutexException) { return true; }
        }
    }
}
