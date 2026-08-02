using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public static class IA_RuntimeTextTrace
    {
        private sealed class Session
        {
            public string Path;
            public StreamWriter Writer;
        }

        private static readonly object _gate = new object();
        private static Session _session;

        // Rastrear cada frame/modulo abre escrita sincronizada no arquivo e
        // compete diretamente com a main thread do jogo. Eventos de negocio
        // continuam registrados; o detalhamento pesado fica opt-in.
        public static bool FrameTraceEnabled { get; set; }
        public static bool ModuleTraceEnabled { get; set; }

        public static string CurrentPath
        {
            get
            {
                lock (_gate)
                {
                    return _session != null ? _session.Path : string.Empty;
                }
            }
        }

        public static void EnsureSession(int teamId)
        {
            lock (_gate)
            {
                EnsureSessionUnlocked(teamId);
            }
        }

        public static void CloseSession()
        {
            lock (_gate)
            {
                if (_session == null)
                {
                    return;
                }

                try
                {
                    _session.Writer.Flush();
                    _session.Writer.Dispose();
                }
                catch
                {
                    // Ignora falhas de fechamento para nao interromper o jogo.
                }
                finally
                {
                    _session = null;
                }
            }
        }

        public static void LogFrame(int teamId, string component, string phase, string message)
        {
            if (!FrameTraceEnabled)
            {
                return;
            }

            Write(teamId, component, phase, message);
        }

        public static void LogText(int teamId, string component, string category, string message)
        {
            Write(teamId, component, category, message);
        }

        public static void LogCommand(int teamId, string component, string transition, IA_CommandRequest request, string message)
        {
            Write(teamId, component, transition, BuildCommandMessage(request, message));
        }

        public static void LogModule(int teamId, string moduleName, string state, float costMs, float budgetMs, string message)
        {
            if (!ModuleTraceEnabled)
            {
                return;
            }

            string payload = string.Format(
                CultureInfo.InvariantCulture,
                "{0} | cost={1:0.00}ms | budget={2:0.00}ms{3}",
                state,
                costMs,
                budgetMs,
                string.IsNullOrWhiteSpace(message) ? string.Empty : " | " + message);
            Write(teamId, moduleName, "MODULE", payload);
        }

        private static void Write(int teamId, string component, string category, string message)
        {
            lock (_gate)
            {
                EnsureSessionUnlocked(teamId);
                WriteLineUnlocked(teamId, component, category, message);
            }
        }

        private static void EnsureSessionUnlocked(int teamId)
        {
            if (_session != null)
            {
                return;
            }

            string root = Path.Combine(Application.persistentDataPath, "IA_Traces");
            Directory.CreateDirectory(root);

            string fileName = string.Format(
                CultureInfo.InvariantCulture,
                "ia_partida_{0}_team_{1}.txt",
                DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture),
                Mathf.Max(-1, teamId));
            string path = Path.Combine(root, fileName);
            FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            _session = new Session { Path = path, Writer = writer };

            WriteLineUnlocked(
                teamId,
                "TRACE",
                "SESSION",
                "Arquivo criado em " + path);
        }

        private static void WriteLineUnlocked(int teamId, string component, string category, string message)
        {
            if (_session == null || _session.Writer == null)
            {
                return;
            }

            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0} | frame={1} | t={2:0.000} | team={3} | {4} | {5} | {6}",
                DateTime.Now.ToString("O", CultureInfo.InvariantCulture),
                Time.frameCount,
                Time.time,
                teamId,
                Normalize(component, "IA"),
                Normalize(category, "EVENT"),
                Normalize(message, string.Empty));

            _session.Writer.WriteLine(line);
        }

        private static string BuildCommandMessage(IA_CommandRequest request, string message)
        {
            if (request == null)
            {
                return Normalize(message, "request nula");
            }

            IA_BuildOrderData build = request.Payload as IA_BuildOrderData;
            IA_ProduceOrderData produce = request.Payload as IA_ProduceOrderData;
            IA_MoveOrderData move = request.Payload as IA_MoveOrderData;
            IA_AttackOrderData attack = request.Payload as IA_AttackOrderData;

            StringBuilder sb = new StringBuilder(256);
            sb.Append("id=").Append(request.Id)
              .Append(" | type=").Append(request.Type)
              .Append(" | origin=").Append(request.Origin)
              .Append(" | domain=").Append(request.Domain)
              .Append(" | family=").Append(request.Family)
              .Append(" | priority=").Append(request.Priority)
              .Append(" | dedup=").Append(request.DedupKey)
              .Append(" | cooldown=").Append(request.CooldownSeconds.ToString("0.00", CultureInfo.InvariantCulture))
              .Append(" | attempts=").Append(request.AttemptCount);

            if (!string.IsNullOrWhiteSpace(request.Reason))
            {
                sb.Append(" | reason=").Append(request.Reason);
            }

            if (build != null)
            {
                sb.Append(" | item=").Append(build.ItemKey)
                  .Append(" | zone=").Append(build.Zone)
                  .Append(" | pos=").Append(FormatVector3(build.Position));
            }
            else if (produce != null)
            {
                sb.Append(" | item=").Append(produce.ItemKey)
                  .Append(" | qty=").Append(produce.Quantity);
            }
            else if (move != null)
            {
                sb.Append(" | units=").Append(move.Units != null ? move.Units.Count : 0)
                  .Append(" | dest=").Append(FormatVector3(move.Destination));
            }
            else if (attack != null)
            {
                sb.Append(" | units=").Append(attack.Units != null ? attack.Units.Count : 0)
                  .Append(" | target=").Append(FormatVector3(attack.TargetPosition));
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                sb.Append(" | ").Append(message);
            }

            return sb.ToString();
        }

        private static string FormatVector3(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:0.0},{1:0.0},{2:0.0})",
                value.x,
                value.y,
                value.z);
        }

        private static string Normalize(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            return value.Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}
