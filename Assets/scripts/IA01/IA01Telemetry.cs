using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    public sealed class IA01Telemetry
    {
        private sealed class NationTelemetryState
        {
            public int InstanceId;
            public int NationId;
            public int TeamId;
            public string NationName = string.Empty;
            public IA01ExecutionMode ExecutionMode;
            public IA01NationMode NationMode;
            public IA01NationStage Stage;
            public IA01NationPosture Posture;
            public float TotalSliceMs;
            public float LastSliceMs;
            public int SliceCount;
            public int DirtyCount;
            public int EventCount;
            public int RegistryEntries;
            public string LastResult = string.Empty;
            public string LastServiceReport = string.Empty;
        }

        private readonly Dictionary<int, NationTelemetryState> states = new Dictionary<int, NationTelemetryState>();
        private readonly Stopwatch frameStopwatch = new Stopwatch();
        private readonly StringBuilder summaryBuilder = new StringBuilder(512);

        private float totalFrameMs;
        private float peakFrameMs;
        private int frameCount;
        private int sliceCount;
        private int eventCount;
        private string serviceReport = string.Empty;

        public float LastFrameMs { get; private set; }
        public float AverageFrameMs => frameCount > 0 ? totalFrameMs / frameCount : 0f;
        public int FrameCount => frameCount;
        public int SliceCount => sliceCount;
        public int EventCount => eventCount;
        public string ServiceReport => serviceReport;

        public void SetServiceReport(string report)
        {
            serviceReport = report ?? string.Empty;
            try
            {
                global::DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("ia01.services", serviceReport);
            }
            catch
            {
                // The telemetry logger is optional at runtime.
            }
        }

        public void RegisterController(IA01Controller controller)
        {
            if (controller == null)
            {
                return;
            }

            NationTelemetryState state = GetOrCreateState(controller.InstanceId);
            state.InstanceId = controller.InstanceId;
            state.NationId = controller.NationId;
            state.TeamId = controller.TeamId;
            state.NationName = controller.NationName;
            state.ExecutionMode = controller.ExecutionMode;
            state.NationMode = controller.NationMode;
            state.Stage = controller.CurrentStage;
            state.Posture = controller.CurrentPosture;
            state.LastServiceReport = serviceReport;
        }

        public void UnregisterController(int instanceId)
        {
            if (instanceId <= 0)
            {
                return;
            }

            states.Remove(instanceId);
        }

        public void RecordSlice(IA01Controller controller, IA01WorkResult result, int registryEntries, int dirtyCount)
        {
            if (controller == null)
            {
                return;
            }

            NationTelemetryState state = GetOrCreateState(controller.InstanceId);
            state.InstanceId = controller.InstanceId;
            state.NationId = controller.NationId;
            state.TeamId = controller.TeamId;
            state.NationName = controller.NationName;
            state.ExecutionMode = controller.ExecutionMode;
            state.NationMode = controller.NationMode;
            state.Stage = controller.CurrentStage;
            state.Posture = controller.CurrentPosture;
            state.LastSliceMs = result.ConsumedMilliseconds;
            state.TotalSliceMs += result.ConsumedMilliseconds;
            state.SliceCount++;
            state.DirtyCount = Mathf.Max(0, dirtyCount);
            state.RegistryEntries = Mathf.Max(0, registryEntries);
            state.LastResult = result.LastMessage ?? string.Empty;
            state.LastServiceReport = serviceReport;

            sliceCount++;
            if (result.Events > 0)
            {
                RecordEvent(controller.InstanceId, result.Events);
            }

            try
            {
                global::DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("ia01.slice." + controller.NationId, result.ConsumedMilliseconds);
                global::DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("ia01.slice.count");
            }
            catch
            {
                // Optional logger.
            }
        }

        public void RecordEvent(int instanceId, int count = 1)
        {
            if (instanceId <= 0 || count <= 0)
            {
                return;
            }

            eventCount += count;
            if (states.TryGetValue(instanceId, out NationTelemetryState state))
            {
                state.EventCount += count;
            }
        }

        public void RecordNationEvent(int nationId, int count = 1)
        {
            if (nationId <= 0 || count <= 0)
            {
                return;
            }

            eventCount += count;
            foreach (NationTelemetryState state in states.Values)
            {
                if (state != null && state.NationId == nationId)
                {
                    state.EventCount += count;
                }
            }
        }

        public void RecordFrame(float frameMs)
        {
            LastFrameMs = Mathf.Max(0f, frameMs);
            totalFrameMs += LastFrameMs;
            frameCount++;
            peakFrameMs = Mathf.Max(peakFrameMs, LastFrameMs);

            try
            {
                global::DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("ia01.frame", LastFrameMs);
                global::DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("ia01.frame.count");
            }
            catch
            {
                // Optional logger.
            }
        }

        public IA01TelemetrySnapshot CaptureSnapshot(float now)
        {
            IA01TelemetrySnapshot snapshot = new IA01TelemetrySnapshot
            {
                CaptureTime = now,
                LastFrameMs = LastFrameMs,
                AverageFrameMs = AverageFrameMs,
                PeakFrameMs = peakFrameMs,
                FrameCount = FrameCount,
                SliceCount = SliceCount,
                EventCount = EventCount,
                ControllerCount = states.Count,
                ServiceReport = serviceReport
            };

            List<NationTelemetryState> ordered = new List<NationTelemetryState>(states.Values);
            ordered.Sort((a, b) => a.NationId.CompareTo(b.NationId));

            for (int i = 0; i < ordered.Count; i++)
            {
                NationTelemetryState state = ordered[i];
                snapshot.Nations.Add(new IA01NationTelemetrySnapshot
                {
                    InstanceId = state.InstanceId,
                    NationId = state.NationId,
                    TeamId = state.TeamId,
                    NationName = state.NationName,
                    ExecutionMode = state.ExecutionMode,
                    NationMode = state.NationMode,
                    Stage = state.Stage,
                    Posture = state.Posture,
                    LastSliceMs = state.LastSliceMs,
                    AverageSliceMs = state.SliceCount > 0 ? state.TotalSliceMs / state.SliceCount : 0f,
                    SliceCount = state.SliceCount,
                    DirtyCount = state.DirtyCount,
                    EventCount = state.EventCount,
                    RegistryEntries = state.RegistryEntries,
                    LastResult = state.LastResult,
                    LastServiceReport = state.LastServiceReport
                });
            }

            return snapshot;
        }

        public string BuildSummary(float now)
        {
            IA01TelemetrySnapshot snapshot = CaptureSnapshot(now);
            summaryBuilder.Clear();
            summaryBuilder.Append("frames=").Append(snapshot.FrameCount);
            summaryBuilder.Append(" slices=").Append(snapshot.SliceCount);
            summaryBuilder.Append(" events=").Append(snapshot.EventCount);
            summaryBuilder.Append(" avgMs=").Append(snapshot.AverageFrameMs.ToString("0.000"));
            summaryBuilder.Append(" peakMs=").Append(snapshot.PeakFrameMs.ToString("0.000"));
            summaryBuilder.Append(" nations=").Append(snapshot.ControllerCount);
            return summaryBuilder.ToString();
        }

        private NationTelemetryState GetOrCreateState(int instanceId)
        {
            if (!states.TryGetValue(instanceId, out NationTelemetryState state))
            {
                state = new NationTelemetryState { InstanceId = instanceId };
                states[instanceId] = state;
            }

            return state;
        }
    }
}
