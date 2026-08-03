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

        public DecisionRecord SaveExplicit(PendingDecision pending, string note)
        {
            if (pending == null) throw new InvalidOperationException("当前没有可保存的投掷结果。");
            var record = new DecisionRecord
            {
                id = Guid.NewGuid().ToString("N"), question = pending.Question,
                result = pending.IsYes ? "YES" : "NO", strength = pending.Strength,
                strengthSource = pending.StrengthSource, mode = pending.Mode.ToString(),
                timestampUtc = pending.TimestampUtc.ToString("o"), note = note ?? string.Empty,
                badgeId = pending.BadgeId, yesProbabilityUsed = pending.YesProbabilityUsed,
                seriesLength = pending.SeriesLength, yesWins = pending.YesWins, noWins = pending.NoWins
            };
            History.records.Insert(0, record);
            Write(historyPath, History);
            return record;
        }

        public void UpdateRecordNote(string id, string note)
        {
            DecisionRecord record = History.records.Find(item => item.id == id);
            if (record == null) return;
            record.note = note ?? string.Empty;
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
                throw new InvalidDataException("这不是有效的 YES/NO 决策历史导出文件。");
            if (parsed.version != CurrentVersion)
                throw new InvalidDataException("不支持的历史文件版本：" + parsed.version);
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
            var badge = new BadgeDefinition { id = Guid.NewGuid().ToString("N"), name = name, builtIn = false, yesProbability = 0.5f, probabilityConfigured = true };
            // Put a newly created badge first so it is immediately visible without scrolling.
            Badges.badges.Insert(0, badge);
            Directory.CreateDirectory(Path.Combine(badgeDirectory, badge.id));
            SaveBadges();
            return badge;
        }

        public void RenameBadge(BadgeDefinition badge, string name)
        {
            if (badge == null || badge.builtIn || string.IsNullOrWhiteSpace(name)) return;
            badge.name = name.Trim();
            SaveBadges();
        }

        public void SetBadgeProbability(BadgeDefinition badge, float yesProbability)
        {
            if (badge == null) return;
            badge.yesProbability = Mathf.Clamp01(yesProbability);
            badge.probabilityConfigured = true;
            SaveBadges();
        }

        public static bool IsBadgeComplete(BadgeDefinition badge)
        {
            // Every badge has valid generated YES/NO text faces. Images are optional replacements.
            return badge != null;
        }

        public void SelectBadge(string id) { Badges.selectedBadgeId = id; SaveBadges(); }

        public void CopyBadgeImage(BadgeDefinition badge, bool yesFace, string sourcePath, float zoom = 1f, float offsetX = 0f, float offsetY = 0f)
        {
            if (badge == null || badge.builtIn || !File.Exists(sourcePath)) return;
            string directory = Path.Combine(badgeDirectory, badge.id);
            Directory.CreateDirectory(directory);
            string destination = Path.Combine(directory, yesFace ? "yes.png" : "no.png");
            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(sourcePath))) throw new InvalidDataException("无法读取所选图片。");
            const int outputSize = 512;
            var output = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false);
            float cropSize = Mathf.Min(source.width, source.height) / Mathf.Clamp(zoom, 1f, 3f);
            float availableX = Mathf.Max(0f, source.width - cropSize);
            float availableY = Mathf.Max(0f, source.height - cropSize);
            float startX = availableX * Mathf.Clamp01((offsetX + 1f) * 0.5f);
            float startY = availableY * Mathf.Clamp01((offsetY + 1f) * 0.5f);
            for (int y = 0; y < outputSize; y++)
            for (int x = 0; x < outputSize; x++)
            {
                float nx = (x + 0.5f) / outputSize;
                float ny = (y + 0.5f) / outputSize;
                Color pixel = source.GetPixelBilinear((startX + nx * cropSize) / source.width, (startY + ny * cropSize) / source.height);
                float dx = nx - 0.5f, dy = ny - 0.5f;
                if (dx * dx + dy * dy > 0.25f) pixel.a = 0f;
                output.SetPixel(x, y, pixel);
            }
            output.Apply();
            File.WriteAllBytes(destination, output.EncodeToPNG()); // Fixed-size app-owned circular copy.
            UnityEngine.Object.Destroy(source);
            UnityEngine.Object.Destroy(output);
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
                Badges.badges.Insert(0, new BadgeDefinition { id = "classic", name = "经典 YES / NO", builtIn = true, yesProbability = 0.5f, probabilityConfigured = true });
            foreach (BadgeDefinition badge in Badges.badges)
                if (!badge.probabilityConfigured) { badge.yesProbability = 0.5f; badge.probabilityConfigured = true; }
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
