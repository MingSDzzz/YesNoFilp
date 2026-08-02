using System;
using System.Collections.Generic;
using System.Text;

namespace DecisionDisc
{
    /// <summary>In-memory diagnostic log. It is written only when the user explicitly exports it.</summary>
    public static class UserActionLog
    {
        private const int Capacity = 300;
        private static readonly List<string> Entries = new List<string>();

        public static void Add(string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message;
            Entries.Add(line);
            if (Entries.Count > Capacity) Entries.RemoveAt(0);
        }

        public static string ExportText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("YesNoFilp 用户操作日志");
            builder.AppendLine("生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            builder.AppendLine("说明：日志仅在内存中收集，点击导出后才写入用户选择的文件。");
            builder.AppendLine(new string('-', 48));
            foreach (string entry in Entries) builder.AppendLine(entry);
            return builder.ToString();
        }

        public static string Preview(int maxLines = 12)
        {
            if (Entries.Count == 0) return "暂无操作日志。";
            int start = Math.Max(0, Entries.Count - maxLines);
            return string.Join("\n", Entries.GetRange(start, Entries.Count - start).ToArray());
        }
    }
}
