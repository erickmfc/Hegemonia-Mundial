#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IA03PerformanceEditModeTests
{
    private const int NumeroDePaises = 15;
    private const int QuadrosSimulados = 1800;
    private const int QuadrosAquecimento = 900;
    private const float Delta = 1f / 60f;

    [Test]
    public void MicrobenchmarkIsoladoComparaQuinzeAgendadoresComESemIA03()
    {
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type worldStateType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_WorldState");
        Type commandQueueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type schedulerType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_PerformanceScheduler");
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type profileType = ResolverTipo("Hegemonia.AI.IA03.PerfilPaisSO");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");

        var baseTicks = new List<Action<float, float>>(NumeroDePaises);
        var aquecimentoTicks = new List<Action<float, float>>(NumeroDePaises);
        var strategists = new List<Component>(NumeroDePaises);
        var objetos = new List<GameObject>(NumeroDePaises);
        var perfis = new List<ScriptableObject>(NumeroDePaises);
        MethodInfo schedulerTick = schedulerType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public);
        MethodInfo schedulerRegister = schedulerType.GetMethod("Register", BindingFlags.Instance | BindingFlags.Public);

        Assert.That(schedulerTick, Is.Not.Null);
        Assert.That(schedulerRegister, Is.Not.Null);

        try
        {
            for (int indice = 0; indice < NumeroDePaises; indice++)
            {
                int teamId = indice + 1;

                object schedulerBase = Activator.CreateInstance(schedulerType);
                baseTicks.Add((Action<float, float>)Delegate.CreateDelegate(typeof(Action<float, float>), schedulerBase, schedulerTick));

                object scheduler = Activator.CreateInstance(schedulerType);

                var objeto = new GameObject("IA03 perf harness " + teamId);
                objeto.SetActive(false);
                objetos.Add(objeto);

                Component brain = objeto.AddComponent(brainType);
                SetField(brain, "TeamId", teamId);
                Component strategist = objeto.AddComponent(strategistType);
                strategists.Add(strategist);
                ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
                perfis.Add(profile);
                SetField(profile, "intervaloDecisaoSegundos", 5f);
                SetField(strategist, "perfilPais", profile);
                SetField(strategist, "ativo", true);
                SetField(strategist, "brain", brain);

                object context = Activator.CreateInstance(contextType);
                object worldState = Activator.CreateInstance(worldStateType, new object[] { teamId });
                SetField(context, "Brain", brain);
                SetField(context, "WorldState", worldState);
                SetField(context, "CommandQueue", Activator.CreateInstance(commandQueueType));
                SetField(context, "Scheduler", scheduler);
                PropertyInfo contextProperty = brainType.GetProperty("Context", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(contextProperty, Is.Not.Null);
                contextProperty.GetSetMethod(true).Invoke(brain, new[] { context });

                object guerraTotal = Enum.Parse(levelType, "GuerraTotal");
                strategistType.GetMethod("DebugForcarNivel", BindingFlags.Instance | BindingFlags.Public)
                    .Invoke(strategist, new[] { guerraTotal });

                float delay = (float)strategistType.GetProperty("DelayInicialEscalonado").GetValue(strategist);
                schedulerRegister.Invoke(scheduler, new object[] { strategist, 0f, delay });
                aquecimentoTicks.Add((Action<float, float>)Delegate.CreateDelegate(typeof(Action<float, float>), scheduler, schedulerTick));
            }

            Aquecer(aquecimentoTicks);

            var measuredSchedulers = new List<object>(NumeroDePaises);
            var measuredTicks = new List<Action<float, float>>(NumeroDePaises);
            for (int indice = 0; indice < strategists.Count; indice++)
            {
                object scheduler = Activator.CreateInstance(schedulerType);
                Component strategist = strategists[indice];
                float delay = (float)strategistType.GetProperty("DelayInicialEscalonado").GetValue(strategist);
                schedulerRegister.Invoke(scheduler, new object[] { strategist, 15f, delay });
                measuredSchedulers.Add(scheduler);
                measuredTicks.Add((Action<float, float>)Delegate.CreateDelegate(typeof(Action<float, float>), scheduler, schedulerTick));
            }

            Medicao baseline = Medir(baseTicks, null, null, 15f);
            Medicao comIA03 = Medir(measuredTicks, measuredSchedulers, schedulerType, 15f);

            UnityEngine.Debug.Log(Formatar("baseline: 15 agendadores sem módulos", baseline));
            UnityEngine.Debug.Log(Formatar("15 IA03 em N1, aquecidas, sem unidades ou cena de campanha", comIA03));

            Assert.That(baseline.Quadros, Is.EqualTo(QuadrosSimulados));
            Assert.That(comIA03.Quadros, Is.EqualTo(QuadrosSimulados));
            Assert.That(comIA03.ExecucoesDeModulo, Is.GreaterThan(NumeroDePaises));
        }
        finally
        {
            for (int i = 0; i < objetos.Count; i++)
            {
                if (objetos[i] != null) UnityEngine.Object.DestroyImmediate(objetos[i]);
            }
            for (int i = 0; i < perfis.Count; i++)
            {
                if (perfis[i] != null) UnityEngine.Object.DestroyImmediate(perfis[i]);
            }
        }
    }

    private static void Aquecer(List<Action<float, float>> ticks)
    {
        for (int quadro = 0; quadro < QuadrosAquecimento; quadro++)
        {
            float agora = quadro * Delta;
            for (int i = 0; i < ticks.Count; i++)
            {
                ticks[i](agora, Delta);
            }
        }
    }

    private static Medicao Medir(
        List<Action<float, float>> ticks,
        List<object> schedulers,
        Type schedulerType,
        float inicioSimulacao)
    {
        var temposPorQuadro = new double[QuadrosSimulados];
        GC.Collect();
        long bytesAntes = GC.GetAllocatedBytesForCurrentThread();
        var cronometroTotal = Stopwatch.StartNew();

        for (int quadro = 0; quadro < QuadrosSimulados; quadro++)
        {
            float agora = inicioSimulacao + quadro * Delta;
            long inicioQuadro = Stopwatch.GetTimestamp();
            for (int i = 0; i < ticks.Count; i++)
            {
                ticks[i](agora, Delta);
            }
            long fimQuadro = Stopwatch.GetTimestamp();
            temposPorQuadro[quadro] = (fimQuadro - inicioQuadro) * 1000d / Stopwatch.Frequency;
        }

        cronometroTotal.Stop();
        long bytesAlocados = GC.GetAllocatedBytesForCurrentThread() - bytesAntes;
        Array.Sort(temposPorQuadro);

        int execucoes = 0;
        float picoDeModuloMs = 0f;
        int excedeuBudget = 0;
        if (schedulers != null)
        {
            MethodInfo getSnapshot = schedulerType.GetMethod("GetSnapshot", BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < schedulers.Count; i++)
            {
                var snapshot = (System.Collections.IEnumerable)getSnapshot.Invoke(schedulers[i], null);
                foreach (object slot in snapshot)
                {
                    Type snapshotType = slot.GetType();
                    execucoes += (int)snapshotType.GetField("RunCount").GetValue(slot);
                    picoDeModuloMs = Mathf.Max(picoDeModuloMs, (float)snapshotType.GetField("PeakCostMs").GetValue(slot));
                    excedeuBudget += (int)snapshotType.GetField("OverBudgetCount").GetValue(slot);
                }
            }
        }

        return new Medicao
        {
            Quadros = QuadrosSimulados,
            TempoTotalMs = cronometroTotal.Elapsed.TotalMilliseconds,
            MediaPorQuadroMs = cronometroTotal.Elapsed.TotalMilliseconds / QuadrosSimulados,
            Percentil95Ms = temposPorQuadro[(int)((QuadrosSimulados - 1) * 0.95)],
            PicoPorQuadroMs = temposPorQuadro[temposPorQuadro.Length - 1],
            BytesAlocados = bytesAlocados,
            ExecucoesDeModulo = execucoes,
            PicoDeModuloMs = picoDeModuloMs,
            ExcedeuBudget = excedeuBudget
        };
    }

    private static string Formatar(string nome, Medicao medicao)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "[IA03PERF] {0}; countries={1}; simulatedFrames={2}; totalMs={3:0.000}; meanFrameMs={4:0.000000}; p95FrameMs={5:0.000000}; peakFrameMs={6:0.000000}; gcBytes={7}; moduleRuns={8}; peakModuleMs={9:0.000000}; overBudget={10}",
            nome,
            NumeroDePaises,
            medicao.Quadros,
            medicao.TempoTotalMs,
            medicao.MediaPorQuadroMs,
            medicao.Percentil95Ms,
            medicao.PicoPorQuadroMs,
            medicao.BytesAlocados,
            medicao.ExecucoesDeModulo,
            medicao.PicoDeModuloMs,
            medicao.ExcedeuBudget);
    }

    private static Type ResolverTipo(string nome)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type tipo = assembly.GetType(nome, false);
            if (tipo != null) return tipo;
        }

        Assert.Fail("Tipo não encontrado no domínio Unity: " + nome);
        return null;
    }

    private static void SetField(object alvo, string nome, object valor)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(campo, Is.Not.Null, "Campo não encontrado: " + nome);
        campo.SetValue(alvo, valor);
    }

    private struct Medicao
    {
        public int Quadros;
        public double TempoTotalMs;
        public double MediaPorQuadroMs;
        public double Percentil95Ms;
        public double PicoPorQuadroMs;
        public long BytesAlocados;
        public int ExecucoesDeModulo;
        public float PicoDeModuloMs;
        public int ExcedeuBudget;
    }
}
#endif
