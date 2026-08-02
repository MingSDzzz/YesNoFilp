using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DecisionDisc
{
    public sealed class DecisionStore
    {
        public const int CurrentVersion = 1;
        private readonly string historyPath;
        private readonly string badgePath;
        private readonly string badgeDirectory;

        public HistoryFile History { get; private set; }
        public BadgeFile Badges { get; private set; }

        public DecisionStore()
        {
            historyPath = Path.Combine(Application.persistentDataPath, "history-v1.json");
            badgePath = Path.Combine(Application.persistentDataPath, "badges-v1.json");
            badgeDirectory = Path.Combine(Application.persistentDataPath, "Badges");
            Directory.CreateDirectory(badgeDirectory);
            History = Load<HistoryFile>(historyPath) ?? new HistoryFile();
            Badges = Load<BadgeFile>(badgePath) ?? new BadgeFile();
            EnsureClassicBadge();
        }

        public void SaveExplicit(PendingDecision pending, string note)
        {
            if (pending == null) throw new InvalidOperationException("There is no result to save.");
            History.records.Insert(0, new DecisionRecord
            {
                id = Guid.NewGuid().ToString("N"), question = pending.Question,
                result = pending.IsYes ? "YES" : "NO", strength = pending.Strength,
                strengthSource = pending.StrengthSource, mode = pending.Mode.ToString(),
                timestampUtc = pending.TimestampUtc.ToString("o"), note = note ?? string.Empty,
                badgeId = pending.BadgeId
            });
            Write(historyPath, History);
        }

        public void DeleteRecord(string id)
        {
            History.records.RemoveAll(record => record.id == id);
            Write(historyPath, History);
        }

        public string CreateExportJson()
        {
            return JsonUtility.ToJson(new HistoryExport
            {
                exportedAtUtc = DateTime.UtcNow.ToString("o"),
                records = new List<DecisionRecord>(History.records)
            }, true);
        }

        public HistoryExport ParseImport(string json)
        {
            var parsed = JsonUtility.FromJson<HistoryExport>(json);
            if (parsed == null || parsed.format != "decision-disc-history")
                throw new InvalidDataException("This is not a Decision Disc history export.");
            if (parsed.version != CurrentVersion)
                throw new InvalidDataException("Unsupported history version: " + parsed.version);
            if (parsed.records == null) parsed.records = new List<DecisionRecord>();
            return parsed;
        }

        public void ApplyImport(HistoryExport imported, bool replace)
        {
            if (replace) History.records = new List<DecisionRecord>(imported.records);
            else
            {
                var ids = new HashSet<string>();
                foreach (var record in History.records) ids.Add(record.id);
                foreach (var record in imported.records)
                {
                    if (string.IsNullOrEmpty(record.id)) record.id = Guid.NewGuid().ToString("N");
                    if (ids.Add(record.id)) History.records.Add(record);
                }
            }
            Write(historyPath, History);
        }

        public BadgeDefinition CreateBadge(string name)
        {
            var badge = new BadgeDefinition { id = Guid.NewGuid().ToString("N"), name = name, builtIn = false };
            Badges.badges.Add(badge);
            Badges.selectedBadgeId = badge.id;
            Directory.CreateDirectory(Path.Combine(badgeDirectory, badge.id));
            SaveBadges();
            return badge;
        }

        public void SelectBadge(string id) { Badges.selectedBadgeId = id; SaveBadges(); }

        public void CopyBadgeImage(BadgeDefinition badge, bool yesFace, string sourcePath)
        {
            if (badge == null || badge.builtIn || !File.Exists(sourcePath)) return;
            string extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrEmpty(extension)) extension = ".img";
            string directory = Path.Combine(badgeDirectory, badge.id);
            Directory.CreateDirectory(directory);
            string destination = Path.Combine(directory, yesFace ? "yes" + extension : "no" + extension);
            File.Copy(sourcePath, destination, true); // App-owned copy survives source deletion.
            if (yesFace) badge.yesImagePath = destination; else badge.noImagePath = destination;
            SaveBadges();
        }

        public void DeleteBadge(string id)
        {
            BadgeDefinition badge = Badges.badges.Find(item => item.id == id);
            if (badge == null || badge.builtIn) return;
            Badges.badges.Remove(badge);
            if (Badges.selectedBadgeId == id) Badges.selectedBadgeId = "classic";
            string directory = Path.Combine(badgeDirectory, id);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            SaveBadges();
        }

        public BadgeDefinition SelectedBadge()
        {
            return Badges.badges.Find(item => item.id == Badges.selectedBadgeId) ?? Badges.badges[0];
        }

        private void EnsureClassicBadge()
        {
            if (Badges.badges.Find(item => item.id == "classic") == null)
                Badges.badges.Insert(0, new BadgeDefinition { id = "classic", name = "Classic YES / NO", builtIn = true });
            SaveBadges();
        }

        private void SaveBadges() { Write(badgePath, Badges); }

        private static T Load<T>(string path) where T : class
        {
            try { return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path)) : null; }
            catch (Exception exception) { Debug.LogWarning("Could not load " + path + ": " + exception.Message); return null; }
        }

        private static void Write<T>(string path, T value)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(value, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
        }
    }
}
