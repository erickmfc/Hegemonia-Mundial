#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IA03AvaliadorMissaoEditModeTests
{
    [Test]
    public void ChegadaAoDestinoConcluiAMissao()
    {
        Assert.That(Avaliar("ChegarAoDestino", chegou: true), Is.EqualTo("Sucesso"));
    }

    [Test]
    public void PrazoSemChegadaExpiraAMissao()
    {
        Assert.That(Avaliar("ChegarAoDestino", prazo: true), Is.EqualTo("Expirada"));
    }

    [TestCase(25f, 75f, 25f)]
    [TestCase(100f, -90f, 10f)]
    [TestCase(0f, 0f, 0f)]
    [TestCase(-5f, 45f, 0f)]
    public void PrejuizoEstruturalContaApenasVidaRealmenteRemovida(
        float danoSolicitado,
        float vidaPosteriorAoImpacto,
        float danoEsperado)
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        MethodInfo calcular = strategistType.GetMethod(
            "CalcularDanoEstruturalEfetivo",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(calcular, Is.Not.Null);
        float danoEfetivo = (float)calcular.Invoke(
            null,
            new object[] { danoSolicitado, vidaPosteriorAoImpacto });
        Assert.That(danoEfetivo, Is.EqualTo(danoEsperado).Within(0.001f));
    }

    [Test]
    public void CicloForcadoDeDebugPercorrePazN4N3N2N1()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        GameObject owner = new GameObject("IA03 debug cycle test");
        owner.SetActive(false);
        try
        {
            Component strategist = owner.AddComponent(strategistType);
            Component brain = owner.GetComponent(ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster"));
            SetField(strategist, "brain", brain);
            MethodInfo forceLevel = strategistType.GetMethod("DebugForcarNivel");
            Assert.That(forceLevel, Is.Not.Null, "Os botões de desenvolvimento precisam ter o mesmo caminho testável.");

            string[] levels = { "Paz", "Tensao", "AvancoMilitar", "ConflitoLimitado", "GuerraTotal" };
            string[] states = { "Paz", "Tensao", "Mobilizacao", "ConflitoLimitado", "GuerraTotal" };
            for (int i = 0; i < levels.Length; i++)
            {
                forceLevel.Invoke(strategist, new[] { Enum.Parse(levelType, levels[i]) });
                Assert.That(Field(strategist, "nivelDeConflito").ToString(), Is.EqualTo(levels[i]));
                Assert.That(Field(strategist, "estadoNacional").ToString(), Is.EqualTo(states[i]));
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void DestruicaoDoAlvoExigeConfirmacaoDoProdutorDeCombate()
    {
        Assert.That(Avaliar("DestruirAlvo", alvoDestruido: false), Is.EqualTo("EmAndamento"));
        Assert.That(Avaliar("DestruirAlvo", alvoDestruido: true), Is.EqualTo("Sucesso"));
    }

    [Test]
    public void CapturaDeTerritorioConcluiAMissao()
    {
        Assert.That(Avaliar("CapturarTerritorio", territorioCapturado: true), Is.EqualTo("Sucesso"));
        Assert.That(Avaliar("CapturarTerritorio", prazo: true), Is.EqualTo("Expirada"));
    }

    [Test]
    public void PermanenciaConcluiEAbandonoDoPontoFalha()
    {
        Assert.That(Avaliar("PermanecerNoDestino", permaneceu: true), Is.EqualTo("Sucesso"));
        Assert.That(Avaliar("ChegarAoDestino", falha: "PermanecerNoDestino", saiu: true), Is.EqualTo("Fracasso"));
    }

    [Test]
    public void PermanenciaFalhaQuandoGrupoSaiMesmoComOrdemConcluida()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type conditionType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        GameObject strategistObject = new GameObject("IA03 residence exit test");
        GameObject creatyObject = new GameObject("IA03 residence exit Creaty");
        strategistObject.SetActive(false);
        creatyObject.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);
        GameObject unit = null;
        try
        {
            Component strategist = strategistObject.AddComponent(strategistType);
            Component brain = strategistObject.GetComponent(brainType);
            FieldInfo integrationMode = brainType.GetField("IntegrationMode");
            SetField(brain, "IntegrationMode", Enum.Parse(integrationMode.FieldType, "Hybrid"));
            Component creaty = creatyObject.AddComponent(creatyType);

            SetField(mission, "condicaoDeSucesso", Enum.Parse(conditionType, "PermanecerNoDestino"));
            SetField(mission, "condicaoDeFracasso", Enum.Parse(conditionType, "PermanecerNoDestino"));
            SetField(mission, "tempoMinimoDePermanenciaSegundos", 30f);
            SetField(mission, "tempoMaximoSegundos", 600f);

            unit = new GameObject("IA03 unit that left destination");
            unit.transform.position = creaty.transform.position + Vector3.right * 100f;
            SetField(strategist, "brain", brain);
            SetField(strategist, "missaoAtiva", mission);
            SetField(strategist, "creatyAtivo", creaty);
            SetField(strategist, "inicioMissaoEm", 0f);
            SetField(strategist, "inicioPermanenciaNoDestinoEm", 0f);
            SetField(strategist, "unidadesOriginaisNaMissao", 1);
            SetField(strategist, "grupoChegouAoDestino", true);
            ((List<GameObject>)Field(strategist, "unidadesAtivasNaMissao")).Add(unit);
            ((HashSet<int>)Field(strategist, "unidadesComOrdemConcluidaNaMissao")).Add(unit.GetInstanceID());

            MethodInfo process = strategistType.GetMethod("ProcessarMissaoAtiva", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(process, Is.Not.Null);
            process.Invoke(strategist, new object[] { 31f });

            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("Fracasso"));
            Assert.That(Field(strategist, "missaoAtiva"), Is.Null);
        }
        finally
        {
            if (unit != null) UnityEngine.Object.DestroyImmediate(unit);
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(creatyObject);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void ProximoTrechoEsperaNovaChegadaAposConfirmacaoAnterior()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type queueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type conditionType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        GameObject strategistObject = new GameObject("IA03 chained route test");
        GameObject firstObject = new GameObject("IA03 first route Creaty");
        GameObject nextObject = new GameObject("IA03 second route Creaty");
        strategistObject.SetActive(false);
        firstObject.SetActive(false);
        nextObject.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);
        GameObject unit = null;
        try
        {
            Component strategist = strategistObject.AddComponent(strategistType);
            Component brain = strategistObject.GetComponent(brainType);
            FieldInfo integrationMode = brainType.GetField("IntegrationMode");
            SetField(brain, "IntegrationMode", Enum.Parse(integrationMode.FieldType, "Hybrid"));
            SetField(brain, "TeamId", 1);

            object context = Activator.CreateInstance(contextType);
            object queue = Activator.CreateInstance(queueType);
            SetField(context, "CommandQueue", queue);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });
            SetField(strategist, "brain", brain);

            Component first = firstObject.AddComponent(creatyType);
            Component next = nextObject.AddComponent(creatyType);
            ConfigureRouteCreaty(first, "route-first", 1, 2);
            ConfigureRouteCreaty(next, "route-second", 1, 2);
            nextObject.transform.position = Vector3.right * 500f;
            SetField(first, "proximoPonto", next);
            firstObject.SetActive(true);
            nextObject.SetActive(true);

            SetField(mission, "idMissao", "route-arrival-test");
            SetField(mission, "condicaoDeSucesso", Enum.Parse(conditionType, "ChegarAoDestino"));
            SetField(mission, "condicaoDeFracasso", Enum.Parse(conditionType, "SobreviverAteOPrazo"));
            SetField(mission, "tempoMaximoSegundos", 600f);

            unit = new GameObject("IA03 route unit");
            unit.transform.position = first.transform.position;
            SetField(strategist, "missaoAtiva", mission);
            SetField(strategist, "creatyAtivo", first);
            SetField(strategist, "equipeAlvoAtiva", 2);
            SetField(strategist, "inicioMissaoEm", 0f);
            SetField(strategist, "unidadesReservadas", 1);
            SetField(strategist, "unidadesOriginaisNaMissao", 1);
            SetField(first, "unidadesReservadas", 1);
            ((List<GameObject>)Field(strategist, "unidadesAtivasNaMissao")).Add(unit);

            MethodInfo process = strategistType.GetMethod("ProcessarMissaoAtiva", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(process, Is.Not.Null);
            process.Invoke(strategist, new object[] { 1f });
            Assert.That(Field(strategist, "creatyAtivo"), Is.SameAs(next));
            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("EmAndamento"));
            Assert.That(((HashSet<int>)Field(strategist, "unidadesComOrdemConcluidaNaMissao")).Count, Is.EqualTo(0));
            Assert.That(Field(strategist, "missaoAtiva"), Is.SameAs(mission), "avançar a rota não deve encerrar a missão");
            Assert.That(Vector3.Distance(unit.transform.position, next.transform.position), Is.GreaterThan(30f));

            process.Invoke(strategist, new object[] { 2f });
            Assert.That(Field(strategist, "missaoAtiva"), Is.SameAs(mission), "a missão foi encerrada antes da chegada ao segundo Creaty");
            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("EmAndamento"), "estado após o segundo trecho");
        }
        finally
        {
            if (unit != null) UnityEngine.Object.DestroyImmediate(unit);
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(nextObject);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    private static void ConfigureRouteCreaty(Component creaty, string id, int ownerTeamId, int targetTeamId)
    {
        SetField(creaty, "id", id);
        SetField(creaty, "paisProprietarioTeamId", ownerTeamId);
        SetField(creaty, "paisAlvoTeamId", targetTeamId);
        SetField(creaty, "maximoDeUnidades", 12);
    }

    [Test]
    public void SobrevivenciaNoPrazoDependeDeTodoOGrupoOriginal()
    {
        Assert.That(Avaliar("SobreviverAteOPrazo", prazo: true, sobreviveu: true), Is.EqualTo("Sucesso"));
        Assert.That(Avaliar("SobreviverAteOPrazo", perdeuUnidade: true), Is.EqualTo("Fracasso"));
    }

    [Test]
    public void ConfirmacaoExternaEsperaOHookConfigurado()
    {
        Assert.That(Avaliar("ConfirmacaoExterna"), Is.EqualTo("EmAndamento"));
        Assert.That(Avaliar("ConfirmacaoExterna", confirmouSucesso: true), Is.EqualTo("Sucesso"));
        Assert.That(Avaliar("ChegarAoDestino", confirmouFracasso: true), Is.EqualTo("EmAndamento"));
        Assert.That(Avaliar("ChegarAoDestino", falha: "ConfirmacaoExterna", confirmouFracasso: true), Is.EqualTo("Fracasso"));
    }

    [Test]
    public void FalhasObservadasSaoDiferentesDeExpiracao()
    {
        Assert.That(Avaliar("PermanecerNoDestino", falha: "PermanecerNoDestino", saiu: true), Is.EqualTo("Fracasso"));
        Assert.That(Avaliar("CapturarTerritorio", falha: "CapturarTerritorio", territorioPerdido: true), Is.EqualTo("Fracasso"));
        Assert.That(Avaliar("SobreviverAteOPrazo", perdeuUnidade: true), Is.EqualTo("Fracasso"));
    }

    [Test]
    public void RegraDeDominioExigeOMinimoDeBatalhasConfigurado()
    {
        Type relatorioType = ResolverTipo("Hegemonia.AI.IA03.IA03RelatorioConflito");
        object relatorio = Activator.CreateInstance(relatorioType);
        MethodInfo registrar = relatorioType.GetMethod("RegistrarResultadoCombate");
        MethodInfo avaliar = relatorioType.GetMethod("AtingiuDominioMinimo");
        registrar.Invoke(relatorio, new object[] { true });
        Assert.That(avaliar.Invoke(relatorio, new object[] { 3, 0.67f }), Is.EqualTo(false));
        registrar.Invoke(relatorio, new object[] { true });
        registrar.Invoke(relatorio, new object[] { false });
        Assert.That(avaliar.Invoke(relatorio, new object[] { 3, 0.67f }), Is.EqualTo(false));
        registrar.Invoke(relatorio, new object[] { true });
        Assert.That(avaliar.Invoke(relatorio, new object[] { 3, 0.67f }), Is.EqualTo(true));
    }

    [Test]
    public void MobilizacaoN1PreservaDefesaReservaERespeitaCapacidadeDoCreaty()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        MethodInfo calculate = strategistType.GetMethod("CalcularLimiteDeMobilizacao", BindingFlags.Public | BindingFlags.Static);
        MethodInfo calculateMission = strategistType.GetMethod("CalcularLimitePorMissaoN1", BindingFlags.Public | BindingFlags.Static);
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        object war = Enum.Parse(levelType, "GuerraTotal");
        Assert.That(calculate.Invoke(null, new[] { (object)100, 12, war, 0.1f, 0.9f }), Is.EqualTo(12));
        Assert.That(calculate.Invoke(null, new[] { (object)100, 100, war, 0.1f, 0.9f }), Is.EqualTo(90));
        Assert.That(calculateMission.Invoke(null, new[] { (object)100, 100, war, 0.1f, 0.9f, 0.5f }), Is.EqualTo(50));
        Assert.That(calculateMission.Invoke(null, new[] { (object)100, 12, war, 0.1f, 0.9f, 0.5f }), Is.EqualTo(12));
        Assert.That(calculateMission.Invoke(null, new[] { (object)100, 100, war, 0.1f, 0.9f, 0f }), Is.EqualTo(0));
    }

    [Test]
    public void MetasMilitaresDoPerfilPodemSerReaplicadasAposAlteracaoDasMetas()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type profileType = ResolverTipo("Hegemonia.AI.IA03.PerfilPaisSO");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelEconomico");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        GameObject owner = new GameObject("IA03 profile target refresh test");
        owner.SetActive(false);
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        try
        {
            Component strategist = owner.AddComponent(strategistType);
            Component brain = owner.GetComponent(brainType);
            SetField(strategist, "brain", brain);
            SetField(strategist, "perfilPais", profile);
            SetField(profile, "nivelEconomico", Enum.Parse(levelType, "Fraco"));

            MethodInfo applyTargets = strategistType.GetMethod("AplicarMetasMilitaresDoPerfil");
            Assert.That(applyTargets, Is.Not.Null);

            applyTargets.Invoke(strategist, null);
            Assert.That(brainType.GetField("TargetFleet").GetValue(brain), Is.EqualTo(2));
            Assert.That(brainType.GetField("TargetAircraft").GetValue(brain), Is.EqualTo(4));

            brainType.GetField("TargetFleet").SetValue(brain, 32);
            brainType.GetField("TargetAircraft").SetValue(brain, 32);
            applyTargets.Invoke(strategist, null);
            Assert.That(brainType.GetField("TargetFleet").GetValue(brain), Is.EqualTo(2));
            Assert.That(brainType.GetField("TargetAircraft").GetValue(brain), Is.EqualTo(4));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void DefasagemInicialDistribuiQuinzePaisEScalonados()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        MethodInfo calculate = strategistType.GetMethod("CalcularAtrasoInicialEscalonado", BindingFlags.Public | BindingFlags.Static);
        var delays = new HashSet<float>();
        for (int teamId = 1; teamId <= 15; teamId++)
        {
            delays.Add((float)calculate.Invoke(null, new object[] { teamId }));
        }

        Assert.That(delays.Count, Is.EqualTo(15));
        Assert.That(delays.Contains(0f), Is.True);
        Assert.That(delays.Contains(7f), Is.True);
    }

    [Test]
    public void MissaoN4NaoAceitaAtaqueInvasaoOuTipoCreatyN1()
    {
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);
        try
        {
            Type missionKind = ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao");
            Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
            Type creatyType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoCreaty");
            MethodInfo aceita = missionType.GetMethod("Aceita");
            SetField(mission, "tipoMissao", Enum.Parse(missionKind, "AtaqueLimitado"));
            SetField(mission, "tipoOrdem", Enum.Parse(ResolverTipo("Hegemonia.AI.IA03.IA03TipoOrdem"), "Atacar"));
            Assert.That(aceita.Invoke(mission, new[] { Enum.Parse(levelType, "Tensao"), Enum.Parse(creatyType, "TensaoN4") }), Is.EqualTo(false));

            SetField(mission, "tipoMissao", Enum.Parse(missionKind, "InvasaoAnfibia"));
            Assert.That(aceita.Invoke(mission, new[] { Enum.Parse(levelType, "GuerraTotal"), Enum.Parse(creatyType, "GuerraN1") }), Is.EqualTo(false));

            SetField(mission, "tipoMissao", Enum.Parse(missionKind, "Patrulha"));
            SetField(mission, "tipoOrdem", Enum.Parse(ResolverTipo("Hegemonia.AI.IA03.IA03TipoOrdem"), "Patrulhar"));
            Assert.That(aceita.Invoke(mission, new[] { Enum.Parse(levelType, "Tensao"), Enum.Parse(creatyType, "PatrulhaAerea") }), Is.EqualTo(true));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [TestCase("Sucesso")]
    [TestCase("Fracasso")]
    [TestCase("Cancelada")]
    [TestCase("Expirada")]
    public void EncerrarMissaoLiberaCreatyEGrupoReservado(string resultadoNome)
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type resultType = ResolverTipo("Hegemonia.AI.IA03.IA03ResultadoMissao");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type queueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type requestType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandRequest");
        GameObject strategistObject = new GameObject("IA03 lifecycle test");
        GameObject creatyObject = new GameObject("Creaty lifecycle test");
        strategistObject.SetActive(false);
        creatyObject.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);
        GameObject unit = null;
        try
        {
            Component strategist = strategistObject.AddComponent(strategistType);
            Component creaty = creatyObject.AddComponent(creatyType);
            Component brain = strategistObject.GetComponent(brainType);
            object context = Activator.CreateInstance(contextType);
            object queue = Activator.CreateInstance(queueType);
            SetField(context, "CommandQueue", queue);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });
            SetField(strategist, "brain", brain);

            string orderId = "ia03-lifecycle-" + resultadoNome;
            object request = Activator.CreateInstance(requestType);
            SetField(request, "Id", orderId);
            SetField(request, "Origin", "IA03EstrategaNacional");
            SetField(request, "Domain", "tactical");
            SetField(request, "Reason", "missão de teste");
            SetField(request, "Family", "tactical");
            SetField(request, "Type", Enum.Parse(ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandType"), "Move"));
            SetField(request, "Priority", 10);
            SetField(request, "DedupKey", orderId);
            object[] enqueueArguments = { request, 1f, null };
            Assert.That(queueType.GetMethod("Enqueue").Invoke(queue, enqueueArguments), Is.EqualTo(true));

            unit = new GameObject("Reserved unit lifecycle test");
            unit.SetActive(false);

            SetField(strategist, "missaoAtiva", mission);
            SetField(strategist, "creatyAtivo", creaty);
            SetField(strategist, "unidadesReservadas", 1);
            SetField(strategist, "idOrdemAtivaDaMissao", orderId);
            ((List<GameObject>)Field(strategist, "unidadesAtivasNaMissao")).Add(unit);
            SetField(creaty, "unidadesReservadas", 1);

            MethodInfo finalizar = strategistType.GetMethod("FinalizarMissao", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(finalizar, Is.Not.Null);
            finalizar.Invoke(strategist, new[] { Enum.Parse(resultType, resultadoNome), (object)"teste de ciclo de vida" });

            Assert.That(Field(strategist, "missaoAtiva"), Is.Null);
            Assert.That(Field(strategist, "creatyAtivo"), Is.Null);
            Assert.That(Field(strategist, "unidadesReservadas"), Is.EqualTo(0));
            Assert.That(((List<GameObject>)Field(strategist, "unidadesAtivasNaMissao")).Count, Is.EqualTo(0));
            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo(resultadoNome));
            Assert.That(creatyType.GetProperty("UnidadesReservadas").GetValue(creaty), Is.EqualTo(0));
            Assert.That(queueType.GetProperty("PendingCount").GetValue(queue), Is.EqualTo(0));
            Assert.That(queueType.GetMethod("GetStatus").Invoke(queue, new object[] { orderId }).ToString(), Is.EqualTo("Cancelled"));

        }
        finally
        {
            if (unit != null) UnityEngine.Object.DestroyImmediate(unit);
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(creatyObject);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void MissaoUrgenteFicaNoTopoEPreservaMissaoInterrompidaSemDuplicar()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type queueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type requestType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandRequest");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        Type commandType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandType");
        GameObject strategistObject = new GameObject("IA03 urgent mission queue test");
        GameObject creatyObject = new GameObject("IA03 urgent mission Creaty test");
        strategistObject.SetActive(false);
        creatyObject.SetActive(false);
        ScriptableObject interruptedMission = ScriptableObject.CreateInstance(missionType);
        ScriptableObject urgentMission = ScriptableObject.CreateInstance(missionType);
        GameObject unit = null;
        try
        {
            Component strategist = strategistObject.AddComponent(strategistType);
            Component brain = strategistObject.GetComponent(brainType);
            Component creaty = creatyObject.AddComponent(creatyType);
            object context = Activator.CreateInstance(contextType);
            object queue = Activator.CreateInstance(queueType);
            SetField(context, "CommandQueue", queue);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });
            SetField(strategist, "brain", brain);
            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "ConflitoLimitado"));

            SetField(interruptedMission, "idMissao", "patrulha-prioritaria-baixa");
            SetField(interruptedMission, "nomeMissao", "Patrulha interrompida");
            SetField(interruptedMission, "prioridade", 1000);
            SetField(urgentMission, "idMissao", "defender-estaleiro");
            SetField(urgentMission, "nomeMissao", "Defender estaleiro");
            SetField(urgentMission, "prioridade", 1);

            const string orderId = "ia03-urgent-interrupted-order";
            object request = Activator.CreateInstance(requestType);
            SetField(request, "Id", orderId);
            SetField(request, "Origin", "IA03EstrategaNacional");
            SetField(request, "Domain", "tactical");
            SetField(request, "Reason", "patrulha interrompida pelo teste");
            SetField(request, "Family", "tactical");
            SetField(request, "Type", Enum.Parse(commandType, "Move"));
            SetField(request, "Priority", 1000);
            SetField(request, "DedupKey", orderId);
            object[] enqueueArguments = { request, 1f, null };
            Assert.That(queueType.GetMethod("Enqueue").Invoke(queue, enqueueArguments), Is.EqualTo(true));

            unit = new GameObject("IA03 urgent mission reserved unit");
            unit.SetActive(false);
            SetField(strategist, "missaoAtiva", interruptedMission);
            SetField(strategist, "creatyAtivo", creaty);
            SetField(strategist, "unidadesReservadas", 1);
            SetField(strategist, "idOrdemAtivaDaMissao", orderId);
            ((List<GameObject>)Field(strategist, "unidadesAtivasNaMissao")).Add(unit);
            SetField(creaty, "unidadesReservadas", 1);

            MethodInfo requestMission = strategistType.GetMethod("SolicitarMissaoEstrategica");
            Assert.That(requestMission, Is.Not.Null);
            Assert.That(requestMission.Invoke(strategist, new object[] { urgentMission, "estaleiro atacado", true }), Is.EqualTo(true));

            MethodInfo refreshQueue = strategistType.GetMethod("AtualizarFilaMissoes", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refreshQueue, Is.Not.Null);
            refreshQueue.Invoke(strategist, null);

            System.Collections.IList missions = (System.Collections.IList)Field(strategist, "filaMissoes");
            Assert.That(missions.Count, Is.EqualTo(2));
            Assert.That(missions[0], Is.SameAs(urgentMission), "A missão urgente deve ultrapassar até uma missão de prioridade maior.");
            Assert.That(missions[1], Is.SameAs(interruptedMission), "A missão interrompida deve permanecer disponível para retomar depois.");
            Assert.That(requestMission.Invoke(strategist, new object[] { urgentMission, "duplicação de teste", true }), Is.EqualTo(false));
            Assert.That(missions.Count, Is.EqualTo(2));
        }
        finally
        {
            if (unit != null) UnityEngine.Object.DestroyImmediate(unit);
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(creatyObject);
            UnityEngine.Object.DestroyImmediate(interruptedMission);
            UnityEngine.Object.DestroyImmediate(urgentMission);
        }
    }

    [Test]
    public void TimeoutRealExpiraMissaoELiberaFilaReservaECelulaDeGrupo()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type queueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type requestType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandRequest");
        GameObject strategistObject = new GameObject("IA03 timeout integration test");
        GameObject creatyObject = new GameObject("Creaty timeout integration test");
        strategistObject.SetActive(false);
        creatyObject.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);
        GameObject unit = null;
        try
        {
            Component strategist = strategistObject.AddComponent(strategistType);
            Component brain = strategistObject.GetComponent(brainType);
            Component creaty = creatyObject.AddComponent(creatyType);
            object context = Activator.CreateInstance(contextType);
            object queue = Activator.CreateInstance(queueType);
            SetField(context, "CommandQueue", queue);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });
            SetField(strategist, "brain", brain);

            SetField(mission, "tempoMaximoSegundos", 5f);
            SetField(mission, "condicaoDeSucesso", Enum.Parse(ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao"), "ConfirmacaoExterna"));
            SetField(mission, "condicaoDeFracasso", Enum.Parse(ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao"), "SobreviverAteOPrazo"));

            const string orderId = "ia03-timeout-integration";
            object request = Activator.CreateInstance(requestType);
            SetField(request, "Id", orderId);
            SetField(request, "Origin", "IA03EstrategaNacional");
            SetField(request, "Domain", "tactical");
            SetField(request, "Reason", "timeout de teste");
            SetField(request, "Family", "tactical");
            SetField(request, "Type", Enum.Parse(ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandType"), "Move"));
            SetField(request, "DedupKey", orderId);
            object[] enqueueArguments = { request, 1f, null };
            Assert.That(queueType.GetMethod("Enqueue").Invoke(queue, enqueueArguments), Is.EqualTo(true));

            unit = new GameObject("Reserved timeout test unit");
            unit.transform.position = Vector3.one * 1000f;
            SetField(strategist, "missaoAtiva", mission);
            SetField(strategist, "creatyAtivo", creaty);
            SetField(strategist, "unidadesReservadas", 1);
            SetField(strategist, "unidadesOriginaisNaMissao", 1);
            SetField(strategist, "inicioMissaoEm", 0f);
            SetField(strategist, "idOrdemAtivaDaMissao", orderId);
            ((List<GameObject>)Field(strategist, "unidadesAtivasNaMissao")).Add(unit);
            SetField(creaty, "unidadesReservadas", 1);

            MethodInfo process = strategistType.GetMethod("ProcessarMissaoAtiva", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(process, Is.Not.Null);
            process.Invoke(strategist, new object[] { 5f });

            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("Expirada"));
            Assert.That(Field(strategist, "missaoAtiva"), Is.Null);
            Assert.That(Field(strategist, "creatyAtivo"), Is.Null);
            Assert.That(Field(strategist, "unidadesReservadas"), Is.EqualTo(0));
            Assert.That(((List<GameObject>)Field(strategist, "unidadesAtivasNaMissao")).Count, Is.EqualTo(0));
            Assert.That(creatyType.GetProperty("UnidadesReservadas").GetValue(creaty), Is.EqualTo(0));
            Assert.That(queueType.GetProperty("PendingCount").GetValue(queue), Is.EqualTo(0));
            Assert.That(queueType.GetMethod("GetStatus").Invoke(queue, new object[] { orderId }).ToString(), Is.EqualTo("Cancelled"));
        }
        finally
        {
            if (unit != null) UnityEngine.Object.DestroyImmediate(unit);
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(creatyObject);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void TermosTerritoriaisLegadosMantemCompatibilidadeDeSave()
    {
        Type propostaType = ResolverTipo("PropostaInternacional");
        object original = Activator.CreateInstance(propostaType);
        SetField(original, "tipo", Enum.Parse(ResolverTipo("TipoPropostaInternacional"), "CessaoTerritorialTemporaria"));
        SetField(original, "status", Enum.Parse(ResolverTipo("StatusPropostaInternacional"), "Executada"));
        SetField(original, "origemTeamId", 2);
        SetField(original, "alvoTeamId", 3);
        SetField(original, "duracaoDias", 30);
        SetField(original, "terminaEmDiaDeJogo", 41);
        SetField(original, "territoriosConcedidos", new List<string> { "regiao-teste" });

        string json = JsonUtility.ToJson(original);
        object restaurada = JsonUtility.FromJson(json, propostaType);

        Type tipoProposta = ResolverTipo("TipoPropostaInternacional");
        Assert.That(Convert.ToInt32(Enum.Parse(tipoProposta, "CessaoTerritorial")), Is.EqualTo(13));
        Assert.That(Convert.ToInt32(Enum.Parse(tipoProposta, "CessaoTerritorialTemporaria")), Is.EqualTo(14));
        Assert.That(Convert.ToInt32(Enum.Parse(tipoProposta, "Desmilitarizacao")), Is.EqualTo(15));
        Assert.That(Field(restaurada, "tipo").ToString(), Is.EqualTo("CessaoTerritorialTemporaria"));
        Assert.That(Field(restaurada, "duracaoDias"), Is.EqualTo(30));
        Assert.That(Field(restaurada, "terminaEmDiaDeJogo"), Is.EqualTo(41));
        CollectionAssert.AreEqual((List<string>)Field(original, "territoriosConcedidos"), (List<string>)Field(restaurada, "territoriosConcedidos"));
    }

    private static string Avaliar(
        string sucesso,
        string falha = "SobreviverAteOPrazo",
        bool chegou = false,
        bool permaneceu = false,
        bool saiu = false,
        bool alvoDestruido = false,
        bool territorioCapturado = false,
        bool territorioPerdido = false,
        bool sobreviveu = false,
        bool perdeuUnidade = false,
        bool prazo = false,
        bool confirmouSucesso = false,
        bool confirmouFracasso = false)
    {
        Type condicaoType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        Type resultadoType = ResolverTipo("Hegemonia.AI.IA03.IA03ResultadoMissao");
        Type avaliadorType = ResolverTipo("Hegemonia.AI.IA03.IA03AvaliadorMissao");
        MethodInfo metodo = avaliadorType.GetMethod("Avaliar", BindingFlags.Public | BindingFlags.Static);
        object[] argumentos =
        {
            Enum.Parse(condicaoType, sucesso),
            Enum.Parse(condicaoType, falha),
            chegou,
            permaneceu,
            saiu,
            alvoDestruido,
            territorioCapturado,
            territorioPerdido,
            sobreviveu,
            perdeuUnidade,
            prazo,
            confirmouSucesso,
            confirmouFracasso
        };

        Assert.That(metodo, Is.Not.Null);
        object resultado = metodo.Invoke(null, argumentos);
        Assert.That(resultado.GetType(), Is.EqualTo(resultadoType));
        return resultado.ToString();
    }

    private static Type ResolverTipo(string nome)
    {
        Type tipo = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(nome, false))
            .FirstOrDefault(candidato => candidato != null);
        Assert.That(tipo, Is.Not.Null, "Tipo não encontrado: " + nome);
        return tipo;
    }

    private static void SetField(object alvo, string nome, object valor)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(campo, Is.Not.Null, "Campo não encontrado: " + nome);
        campo.SetValue(alvo, valor);
    }

    private static object Field(object alvo, string nome)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(campo, Is.Not.Null, "Campo não encontrado: " + nome);
        return campo.GetValue(alvo);
    }
}
#endif
