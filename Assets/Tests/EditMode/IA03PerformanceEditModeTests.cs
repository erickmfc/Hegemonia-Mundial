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
    private const float AtrasoFixoRegistroIA03 = 0.187f;

    [Test]
    public void GovernorAtualizaInstanciasReutilizaveisSemTrocarReferencia()
    {
        Type governorType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_PerformanceGovernor");
        Type bandType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_PerformanceGovernorBand");
        Type stateType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_PerformanceStateData");
        Type budgetType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_EngagementBudget");
        Type decisionType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BattleGovernorDecision");
        object governor = Activator.CreateInstance(governorType);
        object sourceState = governorType.GetProperty("State").GetValue(governor);
        SetField(sourceState, "Band", Enum.Parse(bandType, "Critico"));
        SetField(sourceState, "FpsSmoothed", 19f);
        SetField(sourceState, "CpuMainSmoothed", 42f);
        SetField(sourceState, "GcPressure", true);
        SetField(sourceState, "StableHealthySeconds", 3f);
        SetField(sourceState, "LastUpdatedTime", 12f);

        object stateTarget = Activator.CreateInstance(stateType);
        SetField(stateTarget, "FpsSmoothed", -1f);
        MethodInfo copyState = governorType.GetMethod("CreateStateSnapshot", new[] { stateType });
        Assert.That(copyState, Is.Not.Null);
        Assert.That(copyState.Invoke(governor, new[] { stateTarget }), Is.SameAs(stateTarget));
        Assert.That(GetField(stateTarget, "FpsSmoothed"), Is.EqualTo(19f));
        Assert.That(GetField(stateTarget, "CpuMainSmoothed"), Is.EqualTo(42f));
        Assert.That(GetField(stateTarget, "GcPressure"), Is.EqualTo(true));

        object budgetTarget = Activator.CreateInstance(budgetType);
        SetField(budgetTarget, "UsedPoints", 9);
        SetField(budgetTarget, "LandUsed", 4);
        MethodInfo fillBudget = governorType.GetMethod("CreateEngagementBudget", new[] { budgetType });
        Assert.That(fillBudget, Is.Not.Null);
        Assert.That(fillBudget.Invoke(governor, new[] { budgetTarget }), Is.SameAs(budgetTarget));
        Assert.That(GetField(budgetTarget, "TotalPoints"), Is.EqualTo(20));
        Assert.That(GetField(budgetTarget, "LandPoints"), Is.EqualTo(12));
        Assert.That(GetField(budgetTarget, "UsedPoints"), Is.EqualTo(0));
        Assert.That(GetField(budgetTarget, "LandUsed"), Is.EqualTo(0));

        object decisionTarget = Activator.CreateInstance(decisionType);
        SetField(decisionTarget, "AllowBuild", true);
        SetField(decisionTarget, "MaxLandAttackers", 1);
        MethodInfo fillDecision = governorType.GetMethod("CreateBattleDecision", new[] { typeof(int), decisionType });
        Assert.That(fillDecision, Is.Not.Null);
        Assert.That(fillDecision.Invoke(governor, new object[] { 5, decisionTarget }), Is.SameAs(decisionTarget));
        Assert.That(GetField(decisionTarget, "Band"), Is.EqualTo(Enum.Parse(bandType, "Critico")));
        Assert.That(GetField(decisionTarget, "AllowBuild"), Is.EqualTo(false));
        Assert.That(GetField(decisionTarget, "MaxLandAttackers"), Is.EqualTo(16));

        SetField(sourceState, "Band", Enum.Parse(bandType, "Saudavel"));
        Assert.That(fillDecision.Invoke(governor, new object[] { 5, decisionTarget }), Is.SameAs(decisionTarget));
        Assert.That(GetField(decisionTarget, "AllowBuild"), Is.EqualTo(true));
        Assert.That(GetField(decisionTarget, "AllowHeavyBuild"), Is.EqualTo(false));
        Assert.That(GetField(decisionTarget, "SuppressEconomicExpansion"), Is.EqualTo(false));
        Assert.That(GetField(decisionTarget, "MaxActiveFronts"), Is.EqualTo(1));
        Assert.That(GetField(decisionTarget, "MaxAirPackages"), Is.EqualTo(1));
        Assert.That(GetField(decisionTarget, "MaxLandAttackers"), Is.EqualTo(48));

        Assert.That(fillBudget.Invoke(governor, new[] { budgetTarget }), Is.SameAs(budgetTarget));
        Assert.That(GetField(budgetTarget, "TotalPoints"), Is.EqualTo(56));
        Assert.That(GetField(budgetTarget, "LandPoints"), Is.EqualTo(32));
        Assert.That(GetField(budgetTarget, "AirPoints"), Is.EqualTo(20));
        Assert.That(GetField(budgetTarget, "NavalPoints"), Is.EqualTo(24));
    }

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
        FieldInfo schedulerPhaseOffset = schedulerType.GetField("PhaseOffsetSeconds", BindingFlags.Instance | BindingFlags.Public);

        Assert.That(schedulerTick, Is.Not.Null);
        Assert.That(schedulerRegister, Is.Not.Null);
        Assert.That(schedulerPhaseOffset, Is.Not.Null);

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
                schedulerPhaseOffset.SetValue(scheduler, CalcularPhaseOffset(brain, teamId));
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
                schedulerRegister.Invoke(scheduler, new object[] { strategist, 0f, AtrasoFixoRegistroIA03 + delay });
                aquecimentoTicks.Add((Action<float, float>)Delegate.CreateDelegate(typeof(Action<float, float>), scheduler, schedulerTick));
            }

            Aquecer(aquecimentoTicks);

            var measuredSchedulers = new List<object>(NumeroDePaises);
            var measuredTicks = new List<Action<float, float>>(NumeroDePaises);
            for (int indice = 0; indice < strategists.Count; indice++)
            {
                object scheduler = Activator.CreateInstance(schedulerType);
                Component strategist = strategists[indice];
                Component brain = objetos[indice].GetComponent(brainType);
                int teamId = indice + 1;
                schedulerPhaseOffset.SetValue(scheduler, CalcularPhaseOffset(brain, teamId));
                float delay = (float)strategistType.GetProperty("DelayInicialEscalonado").GetValue(strategist);
                schedulerRegister.Invoke(scheduler, new object[] { strategist, 15f, AtrasoFixoRegistroIA03 + delay });
                measuredSchedulers.Add(scheduler);
                measuredTicks.Add((Action<float, float>)Delegate.CreateDelegate(typeof(Action<float, float>), scheduler, schedulerTick));
            }

            Medicao baseline = Medir(baseTicks, null, null, 15f);
            Medicao comIA03 = Medir(measuredTicks, measuredSchedulers, schedulerType, 15f);

            UnityEngine.Debug.Log(Formatar("baseline: 15 agendadores sem módulos", baseline));
            UnityEngine.Debug.Log(Formatar("15 IA03 em N1, aquecidas e escalonadas como BrainMaster, sem unidades ou cena de campanha", comIA03));

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
            ExecucoesDeModulo = execucoes,
            PicoDeModuloMs = picoDeModuloMs,
            ExcedeuBudget = excedeuBudget
        };
    }

    private static string Formatar(string nome, Medicao medicao)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "[IA03PERF] {0}; countries={1}; simulatedFrames={2}; totalMs={3:0.000}; meanFrameMs={4:0.000000}; p95FrameMs={5:0.000000}; peakFrameMs={6:0.000000}; moduleRuns={7}; peakModuleMs={8:0.000000}; overBudget={9}",
            nome,
            NumeroDePaises,
            medicao.Quadros,
            medicao.TempoTotalMs,
            medicao.MediaPorQuadroMs,
            medicao.Percentil95Ms,
            medicao.PicoPorQuadroMs,
            medicao.ExecucoesDeModulo,
            medicao.PicoDeModuloMs,
            medicao.ExcedeuBudget);
    }

    private static float CalcularPhaseOffset(Component brain, int teamId)
    {
        int seed = Mathf.Abs((teamId * 31) + (brain.GetInstanceID() * 17));
        return 0.03f * (seed % 11);
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

    private static object GetField(object alvo, string nome)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(campo, Is.Not.Null, "Campo não encontrado: " + nome);
        return campo.GetValue(alvo);
    }

    private struct Medicao
    {
        public int Quadros;
        public double TempoTotalMs;
        public double MediaPorQuadroMs;
        public double Percentil95Ms;
        public double PicoPorQuadroMs;
        public int ExecucoesDeModulo;
        public float PicoDeModuloMs;
        public int ExcedeuBudget;
    }
}
#endif
