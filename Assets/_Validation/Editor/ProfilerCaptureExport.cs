using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hegemonia.EditorTools
{
    /// <summary>
    /// Read-only exporter for Unity Profiler raw captures. Loads a saved capture
    /// and writes the slowest frames and their sample trees to a text report.
    /// </summary>
    public static class ProfilerCaptureExport
    {
        private sealed class Marker
        {
            public string Name;
            public int Depth;
            public int ChildrenRemaining;
            public double InclusiveMs;
            public double ChildrenMs;
            public double SelfMs => Math.Max(0d, InclusiveMs - ChildrenMs);
        }

        private sealed class OpenMarker
        {
            public int MarkerIndex;
            public int ChildrenRemaining;
        }

        private sealed class FrameSummary
        {
            public int Index;
            public double Milliseconds;
            public int SampleCount;
        }

        public static void ExportFromEnvironment()
        {
            string input = Environment.GetEnvironmentVariable("HEGEMONIA_PROFILE_INPUT");
            string output = Environment.GetEnvironmentVariable("HEGEMONIA_PROFILE_OUTPUT");
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
                throw new InvalidOperationException("HEGEMONIA_PROFILE_INPUT must point to an existing .raw file.");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("HEGEMONIA_PROFILE_OUTPUT must point to an output report.");

            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.profileEditor = true;
            ProfilerDriver.enabled = true;
            Profiler.enabled = true;
            Profiler.AddFramesFromFile(input);

            int first = ProfilerDriver.firstFrameIndex;
            int last = ProfilerDriver.lastFrameIndex;
            List<FrameSummary> frames = new List<FrameSummary>();
            for (int frameIndex = first; frameIndex <= last; frameIndex++)
            {
                using (RawFrameDataView view = ProfilerDriver.GetRawFrameDataView(frameIndex, 0))
                {
                    if (view == null || !view.valid)
                        continue;
                    frames.Add(new FrameSummary
                    {
                        Index = frameIndex,
                        Milliseconds = view.frameTimeMs,
                        SampleCount = view.sampleCount
                    });
                }
            }

            StringBuilder report = new StringBuilder(64 * 1024);
            report.AppendLine("Unity Profiler Raw Capture Export");
            report.AppendLine("Capture: " + input);
            report.AppendLine("Frame indices: " + first + ".." + last);
            report.AppendLine("Valid main-thread frames: " + frames.Count);
            report.AppendLine("FrameTime values are from Profiler RawFrameDataView.");
            report.AppendLine();

            List<FrameSummary> slowestFrames = frames
                .OrderByDescending(frame => frame.Milliseconds)
                .Take(5)
                .ToList();

            report.AppendLine("Slowest captured frames:");
            foreach (FrameSummary frame in slowestFrames)
            {
                report.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "frame={0} frameMs={1:F3} mainThreadSamples={2}",
                    frame.Index,
                    frame.Milliseconds,
                    frame.SampleCount));
            }

            foreach (FrameSummary frame in slowestFrames.Take(3))
            {
                report.AppendLine();
                report.AppendLine("FRAME " + frame.Index + " (" +
                    frame.Milliseconds.ToString("F3", CultureInfo.InvariantCulture) + " ms)");

                for (int threadIndex = 0; threadIndex < 64; threadIndex++)
                {
                    using (RawFrameDataView view = ProfilerDriver.GetRawFrameDataView(frame.Index, threadIndex))
                    {
                        if (view == null || !view.valid)
                            break;

                        List<Marker> markers = ReadMarkers(view);
                        report.AppendLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "THREAD {0} group=\"{1}\" name=\"{2}\" samples={3}",
                            threadIndex,
                            view.threadGroupName,
                            view.threadName,
                            view.sampleCount));

                        IEnumerable<Marker> topSelf = markers
                            .Where(marker => marker.Depth <= 8 && marker.SelfMs >= 0.05)
                            .OrderByDescending(marker => marker.SelfMs)
                            .Take(40);
                        foreach (Marker marker in topSelf)
                        {
                            report.AppendLine(string.Format(
                                CultureInfo.InvariantCulture,
                                "  depth={0} selfMs={1:F3} inclusiveMs={2:F3} children={3} name={4}",
                                marker.Depth,
                                marker.SelfMs,
                                marker.InclusiveMs,
                                marker.InclusiveMs > 0 ? marker.ChildrenMs.ToString("F3", CultureInfo.InvariantCulture) : "0",
                                marker.Name));
                        }
                    }
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, report.ToString(), new UTF8Encoding(false));
            Debug.Log("[ProfilerCaptureExport] Wrote " + output + " frames=" + frames.Count);
            Profiler.enabled = false;
            ProfilerDriver.enabled = false;
            EditorApplication.Exit(0);
        }

        private static List<Marker> ReadMarkers(RawFrameDataView view)
        {
            List<Marker> markers = new List<Marker>(Math.Max(0, view.sampleCount - 1));
            Stack<OpenMarker> open = new Stack<OpenMarker>();

            for (int sampleIndex = 1; sampleIndex < view.sampleCount; sampleIndex++)
            {
                while (open.Count > 0 && open.Peek().ChildrenRemaining == 0)
                    open.Pop();

                float duration = view.GetSampleTimeMs(sampleIndex);
                int children = view.GetSampleChildrenCount(sampleIndex);
                Marker marker = new Marker
                {
                    Name = view.GetSampleName(sampleIndex),
                    Depth = open.Count + 1,
                    ChildrenRemaining = children,
                    InclusiveMs = duration
                };

                if (open.Count > 0)
                {
                    OpenMarker parent = open.Peek();
                    markers[parent.MarkerIndex].ChildrenMs += duration;
                    parent.ChildrenRemaining--;
                }

                int markerIndex = markers.Count;
                markers.Add(marker);
                if (children > 0)
                {
                    open.Push(new OpenMarker
                    {
                        MarkerIndex = markerIndex,
                        ChildrenRemaining = children
                    });
                }
            }

            return markers;
        }
    }
}
