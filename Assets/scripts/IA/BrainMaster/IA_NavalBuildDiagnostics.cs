using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hegemonia.AI.BrainMaster
{
    public static class IA_NavalBuildDiagnostics
    {
        public sealed class DiagnosticPoint
        {
            public Vector3 Position;
            public string Label;
            public Color Color;
            public float Size;
            public bool Wire;
        }

        private sealed class DiagnosticReport
        {
            public string Title = string.Empty;
            public string Status = string.Empty;
            public float UpdatedAt;
            public readonly List<string> Lines = new List<string>();
            public readonly List<DiagnosticPoint> Points = new List<DiagnosticPoint>();
        }

        private const int MaxLines = 24;
        private const int MaxPoints = 40;
        private static readonly Dictionary<int, DiagnosticReport> _reports = new Dictionary<int, DiagnosticReport>();

        public static void Begin(IA_BrainMaster brain, string title, string status = "")
        {
            if (brain == null)
            {
                return;
            }

            DiagnosticReport report = GetOrCreateReport(brain);
            report.Title = title ?? string.Empty;
            report.Status = status ?? string.Empty;
            report.UpdatedAt = Time.time;
            report.Lines.Clear();
            report.Points.Clear();
        }

        public static void Clear(IA_BrainMaster brain)
        {
            if (brain == null)
            {
                return;
            }

            _reports.Remove(brain.GetInstanceID());
        }

        public static void ClearAll()
        {
            _reports.Clear();
        }

        public static bool HasReport(IA_BrainMaster brain)
        {
            return TryGetReport(brain, out _);
        }

        public static void SetStatus(IA_BrainMaster brain, string status)
        {
            if (brain == null)
            {
                return;
            }

            DiagnosticReport report = GetOrCreateReport(brain);
            report.Status = status ?? string.Empty;
            report.UpdatedAt = Time.time;
        }

        public static void AddLine(IA_BrainMaster brain, string line)
        {
            if (brain == null || string.IsNullOrEmpty(line))
            {
                return;
            }

            DiagnosticReport report = GetOrCreateReport(brain);
            if (report.Lines.Count > 0 && report.Lines[report.Lines.Count - 1] == line)
            {
                report.UpdatedAt = Time.time;
                return;
            }

            if (report.Lines.Count >= MaxLines)
            {
                report.Lines.RemoveAt(0);
            }

            report.Lines.Add(line);
            report.UpdatedAt = Time.time;
        }

        public static void AddPoint(
            IA_BrainMaster brain,
            Vector3 position,
            string label,
            Color color,
            float size = 3.5f,
            bool wire = true)
        {
            if (brain == null)
            {
                return;
            }

            DiagnosticReport report = GetOrCreateReport(brain);
            for (int i = 0; i < report.Points.Count; i++)
            {
                DiagnosticPoint existing = report.Points[i];
                if ((existing.Position - position).sqrMagnitude <= 4f && existing.Label == label)
                {
                    existing.Color = color;
                    existing.Size = size;
                    existing.Wire = wire;
                    report.UpdatedAt = Time.time;
                    return;
                }
            }

            if (report.Points.Count >= MaxPoints)
            {
                report.Points.RemoveAt(0);
            }

            report.Points.Add(new DiagnosticPoint
            {
                Position = position,
                Label = label ?? string.Empty,
                Color = color,
                Size = size,
                Wire = wire
            });
            report.UpdatedAt = Time.time;
        }

        public static string GetInspectorSummary(IA_BrainMaster brain)
        {
            if (!TryGetReport(brain, out DiagnosticReport report))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            if (!string.IsNullOrEmpty(report.Title))
            {
                builder.Append(report.Title);
            }

            if (!string.IsNullOrEmpty(report.Status))
            {
                if (builder.Length > 0)
                {
                    builder.Append(" | ");
                }

                builder.Append(report.Status);
            }

            int lineCount = Mathf.Min(report.Lines.Count, 8);
            for (int i = Mathf.Max(0, report.Lines.Count - lineCount); i < report.Lines.Count; i++)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append("- ").Append(report.Lines[i]);
            }

            if (report.Points.Count > 0)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append("Pontos: ").Append(report.Points.Count);
            }

            return builder.ToString();
        }

        public static string BuildReport(IA_BrainMaster brain)
        {
            if (!TryGetReport(brain, out DiagnosticReport report))
            {
                return "[IA_NavalDiagnostics] Nenhum relatorio naval registrado.";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("[IA_NavalDiagnostics][Team ")
                .Append(brain != null ? brain.TeamId : -1)
                .Append("] ");

            if (!string.IsNullOrEmpty(report.Title))
            {
                builder.Append(report.Title);
            }
            else
            {
                builder.Append("Relatorio naval");
            }

            if (!string.IsNullOrEmpty(report.Status))
            {
                builder.Append(" | ").Append(report.Status);
            }

            builder.AppendLine();
            builder.Append("Atualizado em t=").Append(report.UpdatedAt.ToString("0.0")).AppendLine("s");

            if (report.Lines.Count > 0)
            {
                builder.AppendLine("Eventos:");
                for (int i = 0; i < report.Lines.Count; i++)
                {
                    builder.Append("- ").AppendLine(report.Lines[i]);
                }
            }

            if (report.Points.Count > 0)
            {
                builder.AppendLine("Pontos:");
                for (int i = 0; i < report.Points.Count; i++)
                {
                    DiagnosticPoint point = report.Points[i];
                    builder.Append("- ")
                        .Append(point.Label)
                        .Append(" @ ")
                        .Append(point.Position.ToString("F2"))
                        .AppendLine();
                }
            }

            return builder.ToString().TrimEnd();
        }

        public static void DrawGizmos(IA_BrainMaster brain)
        {
            if (!TryGetReport(brain, out DiagnosticReport report))
            {
                return;
            }

            for (int i = 0; i < report.Points.Count; i++)
            {
                DiagnosticPoint point = report.Points[i];
                Vector3 drawPosition = point.Position + Vector3.up * 1.25f;
                Gizmos.color = point.Color;
                if (point.Wire)
                {
                    Gizmos.DrawWireSphere(drawPosition, point.Size);
                }
                else
                {
                    Gizmos.DrawSphere(drawPosition, point.Size * 0.45f);
                }

                if (brain != null)
                {
                    Gizmos.DrawLine(brain.transform.position + Vector3.up * 2f, drawPosition);
                }

#if UNITY_EDITOR
                Handles.color = point.Color;
                Handles.Label(drawPosition + Vector3.up * Mathf.Max(1.2f, point.Size * 0.25f), point.Label);
#endif
            }
        }

        private static bool TryGetReport(IA_BrainMaster brain, out DiagnosticReport report)
        {
            report = null;
            if (brain == null)
            {
                return false;
            }

            return _reports.TryGetValue(brain.GetInstanceID(), out report);
        }

        private static DiagnosticReport GetOrCreateReport(IA_BrainMaster brain)
        {
            if (!_reports.TryGetValue(brain.GetInstanceID(), out DiagnosticReport report))
            {
                report = new DiagnosticReport();
                _reports.Add(brain.GetInstanceID(), report);
            }

            return report;
        }
    }
}
