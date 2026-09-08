using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Team11.AI
{
    /// One line per exchange. This file is the latency measurement, the bug report,
    /// the test evidence and the weekly-submission artefact. Roughly an hour to write,
    /// and without it every claim about the system in Sprint 2 is an opinion.
    ///
    /// 2026-09-24T14:03:11Z | live   | "how much is the plan" | ttfa 1284ms | dur 4.2s | ok
    public static class TranscriptLog
    {
        private static readonly string Path =
            System.IO.Path.Combine(Application.persistentDataPath, "transcript.log");

        public static string FilePath => Path;

        public static void Write(string source, string question, long ttfaMs,
                                 float durationSec, string outcome)
        {
            var line = new StringBuilder()
                .Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")).Append(" | ")
                .Append(source.PadRight(6)).Append(" | ")
                .Append('"').Append(question).Append('"').Append(" | ")
                .Append("ttfa ").Append(ttfaMs).Append("ms | ")
                .Append("dur ").Append(durationSec.ToString("0.0")).Append("s | ")
                .Append(outcome)
                .ToString();

            try   { File.AppendAllText(Path, line + Environment.NewLine); }
            catch (Exception e) { Debug.LogWarning($"[TranscriptLog] {e.Message}"); }

            Debug.Log($"[transcript] {line}");
        }
    }
}
