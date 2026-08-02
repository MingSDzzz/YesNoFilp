using System;
using System.Collections.Generic;

namespace DecisionDisc
{
    public enum DecisionMode { Fair5050, StrengthInfluences }

    [Serializable]
    public sealed class DecisionRecord
    {
        public string id;
        public string question;
        public string result;
        public float strength;
        public string strengthSource;
        public string mode;
        public string timestampUtc;
        public string note;
        public string badgeId;
    }

    [Serializable]
    public sealed class HistoryFile
    {
        public int version = 1;
        public List<DecisionRecord> records = new List<DecisionRecord>();
    }

    [Serializable]
    public sealed class HistoryExport
    {
        public string format = "decision-disc-history";
        public int version = 1;
        public string exportedAtUtc;
        public List<DecisionRecord> records = new List<DecisionRecord>();
    }

    [Serializable]
    public sealed class BadgeDefinition
    {
        public string id;
        public string name;
        public string yesImagePath;
        public string noImagePath;
        public bool builtIn;
    }

    [Serializable]
    public sealed class BadgeFile
    {
        public int version = 1;
        public string selectedBadgeId = "classic";
        public List<BadgeDefinition> badges = new List<BadgeDefinition>();
    }

    public sealed class PendingDecision
    {
        public string Question;
        public bool IsYes;
        public float Strength;
        public string StrengthSource;
        public DecisionMode Mode;
        public DateTime TimestampUtc;
        public string BadgeId;
    }
}
