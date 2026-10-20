using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class GlobalWorldDiagnosticsEditModeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void MemorySamplingDoesNotPushBenchmarkDeadlineForward()
    {
        GameObject host = new GameObject("GlobalWorldDiagnostics_Deadline_Test");
        Component diagnostics = host.AddComponent(ResolveDiagnosticsType());
        try
        {
            float now = Time.unscaledTime;
            float expectedDeadline = now + 60f;
            SetField(diagnostics, "benchmarkRecording", true);
            SetField(diagnostics, "nextSampleAt", now - 1f);
            SetField(diagnostics, "nextBenchmarkSampleAt", expectedDeadline);

            Invoke(diagnostics, "Update");

            Assert.That(GetField<float>(diagnostics, "nextBenchmarkSampleAt"), Is.EqualTo(expectedDeadline));
            Assert.That(GetField<int>(diagnostics, "benchmarkWindowFrameCount"), Is.EqualTo(1));
        }
        finally
        {
            SetField(diagnostics, "benchmarkRecording", false);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BenchmarkUsesRecentWorldCountsEvenWhenSamplingFramesDiffer()
    {
        GameObject host = new GameObject("GlobalWorldDiagnostics_Counts_Test");
        Component diagnostics = host.AddComponent(ResolveDiagnosticsType());
        try
        {
            float now = Time.unscaledTime;
            SetField(diagnostics, "sampleInterval", 1f);
            SetField(diagnostics, "benchmarkStartedAt", now - 2f);
            SetField(diagnostics, "benchmarkMemorySampleAt", now - 0.25f);
            SetField(diagnostics, "benchmarkWindowFrameCount", 1);
            SetField(diagnostics, "benchmarkWindowFrameMillisecondsTotal", 16d);
            SetField(diagnostics, "benchmarkWindowWorstFrameMilliseconds", 16f);
            ((IList)GetField<object>(diagnostics, "benchmarkWindowFrameMilliseconds")).Add(16f);
            SetField(diagnostics, "activeGameObjects", 42);
            SetField(diagnostics, "activeUnits", 3);
            SetField(diagnostics, "activeNavalUnits", 2);
            SetField(diagnostics, "activeProjectiles", 5);
            SetField(diagnostics, "activeParticleSystems", 6);
            SetField(diagnostics, "activeTrailRenderers", 7);

            Invoke(diagnostics, "FinalizeBenchmarkSample");

            IList samples = (IList)GetField<object>(diagnostics, "benchmarkSamples");
            Assert.That(samples.Count, Is.EqualTo(1));
            object sample = samples[0];
            Assert.That(GetSampleField<int>(sample, "activeObjects"), Is.EqualTo(42));
            Assert.That(GetSampleField<int>(sample, "units"), Is.EqualTo(3));
            Assert.That(GetSampleField<int>(sample, "navalUnits"), Is.EqualTo(2));
            Assert.That(GetSampleField<int>(sample, "projectiles"), Is.EqualTo(5));
            Assert.That(GetSampleField<int>(sample, "particleSystems"), Is.EqualTo(6));
            Assert.That(GetSampleField<int>(sample, "trailRenderers"), Is.EqualTo(7));
        }
        finally
        {
            SetField(diagnostics, "benchmarkRecording", false);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BenchmarkSkipsNegativeElapsedSampleAfterPlayModeClockReset()
    {
        GameObject host = new GameObject("GlobalWorldDiagnostics_ClockReset_Test");
        Component diagnostics = host.AddComponent(ResolveDiagnosticsType());
        try
        {
            SetField(diagnostics, "benchmarkStartedAt", Time.unscaledTime + 10f);
            SetField(diagnostics, "benchmarkWindowFrameCount", 1);
            SetField(diagnostics, "benchmarkWindowFrameMillisecondsTotal", 16d);
            SetField(diagnostics, "benchmarkWindowWorstFrameMilliseconds", 16f);
            ((IList)GetField<object>(diagnostics, "benchmarkWindowFrameMilliseconds")).Add(16f);

            Invoke(diagnostics, "FinalizeBenchmarkSample");

            IList samples = (IList)GetField<object>(diagnostics, "benchmarkSamples");
            Assert.That(samples.Count, Is.Zero);
        }
        finally
        {
            SetField(diagnostics, "benchmarkRecording", false);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BenchmarkCanRetainAtLeastNinetyMinutesOfOneSecondIntervals()
    {
        GameObject host = new GameObject("GlobalWorldDiagnostics_Capacity_Test");
        Component diagnostics = host.AddComponent(ResolveDiagnosticsType());
        try
        {
            SetField(diagnostics, "benchmarkRecording", true);
            SetField(diagnostics, "benchmarkStartedAt", Time.unscaledTime);
            IList samples = (IList)GetField<object>(diagnostics, "benchmarkSamples");

            const int ninetyMinutesAtOneSecondIntervals = 90 * 60;
            for (int i = 0; i < ninetyMinutesAtOneSecondIntervals; i++)
            {
                SetField(diagnostics, "nextBenchmarkSampleAt", Time.unscaledTime - 0.1f);
                Invoke(diagnostics, "CaptureBenchmarkFrame");
            }

            Assert.That(samples.Count, Is.EqualTo(ninetyMinutesAtOneSecondIntervals));
            Assert.That(GetField<bool>(diagnostics, "benchmarkRecording"), Is.True);
        }
        finally
        {
            // Avoid writing a benchmark file from teardown.
            SetField(diagnostics, "benchmarkRecording", false);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BenchmarkDoesNotEmitMoreThanOneSampleAtTheSameTimestamp()
    {
        GameObject host = new GameObject("GlobalWorldDiagnostics_Interval_Test");
        Component diagnostics = host.AddComponent(ResolveDiagnosticsType());
        try
        {
            SetField(diagnostics, "benchmarkRecording", true);
            SetField(diagnostics, "sampleInterval", 1f);
            SetField(diagnostics, "nextBenchmarkSampleAt", Time.unscaledTime);
            IList samples = (IList)GetField<object>(diagnostics, "benchmarkSamples");

            Invoke(diagnostics, "CaptureBenchmarkFrame");
            Assert.That(samples.Count, Is.EqualTo(1));
            Assert.That(GetField<float>(diagnostics, "nextBenchmarkSampleAt"), Is.GreaterThan(Time.unscaledTime));

            Invoke(diagnostics, "CaptureBenchmarkFrame");
            Assert.That(samples.Count, Is.EqualTo(1));
        }
        finally
        {
            SetField(diagnostics, "benchmarkRecording", false);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static T GetField<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, PrivateInstance);
        Assert.That(field, Is.Not.Null, "Missing field " + name);
        return (T)field.GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, PrivateInstance | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, "Missing field " + name);
        field.SetValue(target, value);
    }

    private static T GetSampleField<T>(object sample, string name)
    {
        FieldInfo field = sample.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, "Missing sample field " + name);
        return (T)field.GetValue(sample);
    }

    private static void Invoke(object target, string name)
    {
        MethodInfo method = target.GetType().GetMethod(name, PrivateInstance);
        Assert.That(method, Is.Not.Null, "Missing method " + name);
        method.Invoke(target, null);
    }

    private static Type ResolveDiagnosticsType()
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType("GlobalWorldDiagnostics", false);
            if (type != null) return type;
        }

        Assert.Fail("GlobalWorldDiagnostics was not loaded.");
        return null;
    }
}
