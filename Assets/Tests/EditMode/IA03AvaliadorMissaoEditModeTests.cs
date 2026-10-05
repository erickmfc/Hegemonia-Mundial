#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
        Type creatyKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoCreaty");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        Type domainType = ResolverTipo("Hegemonia.AI.IA03.IA03DominioEstrategico");
        Type registryType = ResolverTipo("Hegemonia.AI.IA03.RegistroCreatysEstrategicos");
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
            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "Tensao"));

            object context = Activator.CreateInstance(contextType);
            object queue = Activator.CreateInstance(queueType);
            SetField(context, "CommandQueue", queue);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });
            SetField(strategist, "brain", brain);

            Component first = firstObject.AddComponent(creatyType);
            Component next = nextObject.AddComponent(creatyType);
            ConfigureRouteCreaty(first, "route-first", 1, 2);
            ConfigureRouteCreaty(next, "route-second", 1, 2);
            SetField(first, "tipo", Enum.Parse(creatyKindType, "PontoGenerico"));
            SetField(next, "tipo", Enum.Parse(creatyKindType, "PontoGenerico"));
            SetField(first, "nivelDeConflito", Enum.Parse(levelType, "Tensao"));
            SetField(next, "nivelDeConflito", Enum.Parse(levelType, "Tensao"));
            SetField(first, "dominio", Enum.Parse(domainType, "Terrestre"));
            SetField(next, "dominio", Enum.Parse(domainType, "Terrestre"));
            nextObject.transform.position = Vector3.right * 500f;
            SetField(first, "proximoPonto", next);
            firstObject.SetActive(true);
            nextObject.SetActive(true);
            RegistrarCreatyParaTeste(first);
            RegistrarCreatyParaTeste(next);

            SetField(mission, "idMissao", "route-arrival-test");
            SetField(mission, "tipoMissao", Enum.Parse(ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao"), "Patrulha"));
            SetField(mission, "dominio", Enum.Parse(domainType, "Terrestre"));
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

            Assert.That(missionType.GetMethod("Aceita").Invoke(mission, new object[] { Enum.Parse(levelType, "Tensao"), Enum.Parse(creatyKindType, "PontoGenerico") }), Is.EqualTo(true));
            Assert.That(creatyType.GetProperty("Ativo").GetValue(next), Is.EqualTo(true), "O ponto de rota precisa estar ativo.");
            Assert.That(creatyType.GetMethod("PodeReservar").Invoke(next, new object[] { 1, 2, 1 }), Is.EqualTo(true), "O ponto de rota precisa aceitar a reserva do grupo.");

            var routeCandidates = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(creatyType));
            object ownerTeamId = Field(brain, "TeamId");
            object targetTeamId = Field(strategist, "equipeAlvoAtiva");
            object currentLevel = Field(strategist, "nivelDeConflito");
            object currentDomain = missionType.GetProperty("Dominio").GetValue(mission);
            registryType.GetMethod("PreencherCandidatos").Invoke(null, new[]
            {
                routeCandidates,
                ownerTeamId,
                targetTeamId,
                Enum.Parse(creatyKindType, "PontoGenerico"),
                currentLevel,
                currentDomain
            });
            Assert.That(routeCandidates.Contains(next), Is.EqualTo(true), "O Creaty ativo precisa estar indexado como candidato para os filtros atuais.");

            MethodInfo validateNextPoint = strategistType.GetMethod("ProximoPontoCompativel", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(validateNextPoint, Is.Not.Null);
            Assert.That(validateNextPoint.Invoke(strategist, new object[] { mission, next, 1 }), Is.EqualTo(true),
                "Creaty compatível foi recusado. ativo=" + creatyType.GetProperty("Ativo").GetValue(next)
                + ", podeReservar=" + creatyType.GetMethod("PodeReservar").Invoke(next, new object[] { 1, 2, 1 })
                + ", candidatos=" + routeCandidates.Count
                + ", registrados=" + registryType.GetProperty("QuantidadeRegistrada").GetValue(null)
                + ", proprietário=" + ownerTeamId + ", alvo=" + targetTeamId
                + ", nível=" + currentLevel + ", domínio=" + currentDomain);

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
            RemoverCreatyParaTeste(firstObject.GetComponent(creatyType));
            RemoverCreatyParaTeste(nextObject.GetComponent(creatyType));
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(nextObject);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void AlteracaoDeTeamIdNotificadaReconstruiSnapshotDoBrainMaster()
    {
        Type worldStateType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_WorldState");
        Type identityType = ResolverTipo("IdentidadeUnidade");
        Type entityRegistryType = ResolverTipo("RegistroEntidadesJogo");
        GameObject unit = new GameObject("IA03 snapshot version invalidation test");
        Component identity = unit.AddComponent(identityType);
        object worldState = Activator.CreateInstance(worldStateType, new object[] { 912301 });
        MethodInfo register = worldStateType.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
        MethodInfo unregister = worldStateType.GetMethod("Unregister", BindingFlags.Public | BindingFlags.Static);
        MethodInfo rebuild = worldStateType.GetMethod("RebuildRegistrySnapshotIfNeeded", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo notifyChanged = entityRegistryType.GetMethod("NotificarAlteracao", BindingFlags.Public | BindingFlags.Static);

        try
        {
            SetField(identity, "teamID", 912301);
            Assert.That(register, Is.Not.Null);
            Assert.That(unregister, Is.Not.Null);
            Assert.That(rebuild, Is.Not.Null);
            Assert.That(notifyChanged, Is.Not.Null);
            register.Invoke(null, new object[] { identity });
            rebuild.Invoke(worldState, new object[] { 0f });
            Assert.That(ReadSnapshotTeamId(), Is.EqualTo(912301));

            SetField(identity, "teamID", 912302);
            notifyChanged.Invoke(null, null);
            rebuild.Invoke(worldState, new object[] { 1f });

            Assert.That(ReadSnapshotTeamId(), Is.EqualTo(912302), "a versão de RegistroEntidadesJogo deve invalidar o snapshot mesmo sem mudar o registry interno do BrainMaster");
        }
        finally
        {
            unregister.Invoke(null, new object[] { identity });
            entityRegistryType.GetMethod("Unregister", BindingFlags.Public | BindingFlags.Static, null, new[] { identityType }, null).Invoke(null, new object[] { identity });
            UnityEngine.Object.DestroyImmediate(unit);
        }

        int ReadSnapshotTeamId()
        {
            foreach (object entry in (System.Collections.IEnumerable)Field(worldState, "_registrySnapshot"))
            {
                if (ReferenceEquals(Field(entry, "Identity"), identity))
                {
                    return (int)Field(entry, "TeamId");
                }
            }

            Assert.Fail("A identidade registrada não apareceu no snapshot do BrainMaster.");
            return -1;
        }
    }

    private static void ConfigureRouteCreaty(Component creaty, string id, int ownerTeamId, int targetTeamId)
    {
        SetField(creaty, "id", id);
        SetField(creaty, "paisProprietarioTeamId", ownerTeamId);
        SetField(creaty, "paisAlvoTeamId", targetTeamId);
        SetField(creaty, "maximoDeUnidades", 12);
    }

    private static void RegistrarCreatyParaTeste(Component creaty)
    {
        Type registryType = ResolverTipo("Hegemonia.AI.IA03.RegistroCreatysEstrategicos");
        registryType.GetMethod("Registrar").Invoke(null, new object[] { creaty });
    }

    private static void RemoverCreatyParaTeste(Component creaty)
    {
        if (creaty == null)
        {
            return;
        }

        Type registryType = ResolverTipo("Hegemonia.AI.IA03.RegistroCreatysEstrategicos");
        registryType.GetMethod("Remover").Invoke(null, new object[] { creaty });
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
    public void TorresEmPaisesSobUmaRaizCompartilhadaMantemAutoriaDeProjeteisEMisseis()
    {
        GameObject scenario = new GameObject("IA03_TestScenario");
        scenario.SetActive(false);
        GameObject projectileObject = new GameObject("TestProjectile");
        GameObject missileObject = new GameObject("TestNavalMissile");
        missileObject.SetActive(false);
        try
        {
            Component identidadeA = CriarUnidadeComTorre(scenario.transform, "Valdoria", "Tank_A", 1, out Component torreA);
            Component identidadeB = CriarUnidadeComTorre(scenario.transform, "Karsovia", "Tank_B", 3, out Component torreB);
            Assert.That(identidadeA.transform.root, Is.SameAs(scenario.transform));
            Assert.That(identidadeB.transform.root, Is.SameAs(scenario.transform));

            MethodInfo obterDono = torreA.GetType().GetMethod("ObterDonoDoDisparo", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(obterDono, Is.Not.Null);
            MethodInfo setDono = ResolverTipo("Projetil").GetMethod("SetDono", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(setDono, Is.Not.Null);
            Component projectile = projectileObject.AddComponent(ResolverTipo("Projetil"));
            Component[] identities = { identidadeA, identidadeB };
            Component[] turrets = { torreA, torreB };
            int[] teamIds = { 1, 3 };
            PropertyInfo projectileTeam = projectile.GetType().GetProperty("TeamDono", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(projectileTeam, Is.Not.Null);
            MethodInfo resolveIdentity = ResolverTipo("SistemaDeDanos").GetMethod(
                "ResolverIdentidade",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolveIdentity, Is.Not.Null);
            Component missile = missileObject.AddComponent(ResolverTipo("MisselNaval"));
            MethodInfo resolveLauncher = missile.GetType().GetMethod("ResolverLancador", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo ignoreCollider = missile.GetType().GetMethod("DeveIgnorarTrigger", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(resolveLauncher, Is.Not.Null);
            Assert.That(ignoreCollider, Is.Not.Null);
            Collider[] unitColliders =
            {
                identidadeA.gameObject.AddComponent<BoxCollider>(),
                identidadeB.gameObject.AddComponent<BoxCollider>()
            };

            for (int i = 0; i < identities.Length; i++)
            {
                Component identidade = identities[i];
                Component torre = turrets[i];
                SetField(torre, "minhaIdentidade", identidade);
                GameObject dono = (GameObject)obterDono.Invoke(torre, null);
                setDono.Invoke(projectile, new object[] { dono });

                Assert.That(dono, Is.SameAs(identidade.gameObject));
                Assert.That(projectileTeam.GetValue(projectile), Is.EqualTo(teamIds[i]));
                Component identidadeResolvida = (Component)resolveIdentity.Invoke(null, new object[] { dono.transform });
                Assert.That(identidadeResolvida, Is.SameAs(identidade));
                Assert.That(Field(identidadeResolvida, "teamID"), Is.EqualTo(teamIds[i]));

                Transform lancadorMissel = (Transform)resolveLauncher.Invoke(null, new object[] { torre.transform });
                Assert.That(lancadorMissel, Is.SameAs(identidade.transform));
                SetField(missile, "lancador", lancadorMissel);
                Assert.That(ignoreCollider.Invoke(missile, new object[] { unitColliders[i] }), Is.EqualTo(true));
                Assert.That(ignoreCollider.Invoke(missile, new object[] { unitColliders[1 - i] }), Is.EqualTo(false));
            }

            SetField(torreB, "minhaIdentidade", null);
            Assert.That(obterDono.Invoke(torreB, null), Is.SameAs(scenario));
            Assert.That(resolveLauncher.Invoke(null, new object[] { scenario.transform }), Is.SameAs(scenario.transform));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(projectileObject);
            UnityEngine.Object.DestroyImmediate(missileObject);
            UnityEngine.Object.DestroyImmediate(scenario);
        }
    }

    [Test]
    public void ProducaoAereaIAFalhaQuandoAeroportoBloqueiaSpawnEmAgua()
    {
        Vector3 spawn = new Vector3(50000f, 0f, 50000f);
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = "IA03 water spawn blocker";
        water.transform.position = spawn;
        water.transform.localScale = new Vector3(1000f, 10f, 1000f);
        water.AddComponent(ResolverTipo("MarcadorSuperficieMapa"));

        GameObject airportObject = new GameObject("IA03 airport spawn rejection test");
        airportObject.SetActive(false);
        airportObject.transform.position = spawn;
        GameObject prefab = new GameObject("IA03 fighter test prefab");
        ScriptableObject item = ScriptableObject.CreateInstance(ResolverTipo("DadosConstrucao"));
        Component airport = null;
        object backend = null;
        try
        {
            Component identity = airportObject.AddComponent(ResolverTipo("IdentidadeUnidade"));
            SetField(identity, "teamID", 987654);
            airport = airportObject.AddComponent(ResolverTipo("GerenciadorAeroporto"));
            MethodInfo register = ResolverTipo("RegistroEntidadesJogo").GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(method => method.Name == "Register"
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == airport.GetType());
            register.Invoke(null, new object[] { airport });

            SetField(item, "nomeItem", "IA03 fighter test");
            SetField(item, "prefabDaUnidade", prefab);

            Type backendType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BackendBridge");
            backend = Activator.CreateInstance(backendType, new object[] { 987654 });
            object productionService = backendType.GetProperty("ProductionService").GetValue(backend);
            MethodInfo produceAircraft = productionService.GetType().GetMethod(
                "ProduceAircraft",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(produceAircraft, Is.Not.Null);

            LogAssert.Expect(LogType.Error, new Regex("\\[Aeroporto\\] Spawn aéreo bloqueado em água: .*"));
            object[] arguments = { item, null, "ia03-water-blocked-aircraft" };
            GameObject produced = produceAircraft.Invoke(productionService, arguments) as GameObject;

            Assert.That(produced, Is.Null);
            Assert.That(arguments[1], Is.EqualTo("aeroporto recusou o spawn"));
        }
        finally
        {
            if (airport != null)
            {
                MethodInfo unregister = ResolverTipo("RegistroEntidadesJogo").GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(method => method.Name == "Unregister"
                        && method.GetParameters().Length == 1
                        && method.GetParameters()[0].ParameterType == airport.GetType());
                unregister.Invoke(null, new object[] { airport });
            }

            UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(prefab);
            UnityEngine.Object.DestroyImmediate(airportObject);
            UnityEngine.Object.DestroyImmediate(water);
        }
    }

    [Test]
    public void ProducaoAereaIABloqueiaSpawnQuandoAeroportoEstaSemEnergia()
    {
        Vector3 spawn = new Vector3(50000f, 0f, 50000f);
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = "IA03 blackout water spawn blocker";
        water.transform.position = spawn;
        water.transform.localScale = new Vector3(1000f, 10f, 1000f);
        water.AddComponent(ResolverTipo("MarcadorSuperficieMapa"));

        GameObject airportObject = new GameObject("IA03 unpowered airport spawn test");
        airportObject.SetActive(false);
        airportObject.transform.position = spawn;
        GameObject prefab = new GameObject("IA03 unpowered fighter test prefab");
        Component airport = null;
        try
        {
            airport = airportObject.AddComponent(ResolverTipo("GerenciadorAeroporto"));
            SetField(airport, "semEnergia", true);
            MethodInfo trySpawn = airport.GetType().GetMethod(
                "TryComprarAviaoIAImediato",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(trySpawn, Is.Not.Null);

            bool accepted = (bool)trySpawn.Invoke(airport, new object[] { prefab, "ia03-blackout-aircraft" });

            Assert.That(accepted, Is.False);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(prefab);
            UnityEngine.Object.DestroyImmediate(airportObject);
            UnityEngine.Object.DestroyImmediate(water);
        }
    }

    [Test]
    public void ProducaoDeHelicopteroIAFalhaQuandoHeliportoEstaSemEnergia()
    {
        const int teamId = 987655;
        GameObject heliportObject = new GameObject("IA03 unpowered heliport test");
        heliportObject.SetActive(false);
        GameObject prefab = new GameObject("IA03 heli test prefab");
        ScriptableObject item = ScriptableObject.CreateInstance(ResolverTipo("DadosConstrucao"));
        Component heliport = null;
        object backend = null;
        try
        {
            Component identity = heliportObject.AddComponent(ResolverTipo("IdentidadeUnidade"));
            SetField(identity, "teamID", teamId);
            heliport = heliportObject.AddComponent(ResolverTipo("Heliporto"));
            Component property = heliportObject.AddComponent(ResolverTipo("Imovel"));
            SetField(property, "semEnergia", true);

            Type registryType = ResolverTipo("RegistroEntidadesJogo");
            MethodInfo register = registryType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(method => method.Name == "Register"
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == heliport.GetType());
            register.Invoke(null, new object[] { heliport });

            SetField(item, "nomeItem", "IA03 helicopter test");
            SetField(item, "prefabDaUnidade", prefab);

            Type backendType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BackendBridge");
            backend = Activator.CreateInstance(backendType, new object[] { teamId });
            object productionService = backendType.GetProperty("ProductionService").GetValue(backend);
            MethodInfo produceHelicopter = productionService.GetType().GetMethod(
                "ProduceHelicopter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(produceHelicopter, Is.Not.Null);

            object[] arguments = { item, null, "ia03-unpowered-heliport" };
            GameObject produced = produceHelicopter.Invoke(productionService, arguments) as GameObject;

            Assert.That(produced, Is.Null);
            Assert.That(arguments[1], Is.EqualTo("heliporto sem energia"));
            Assert.That((bool)heliport.GetType().GetMethod("TemEspacoParaPousar").Invoke(heliport, null), Is.True,
                "O teste precisa comprovar que o bloqueio foi energia, não lotação.");
        }
        finally
        {
            if (heliport != null)
            {
                Type registryType = ResolverTipo("RegistroEntidadesJogo");
                MethodInfo unregister = registryType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(method => method.Name == "Unregister"
                        && method.GetParameters().Length == 1
                        && method.GetParameters()[0].ParameterType == heliport.GetType());
                unregister.Invoke(null, new object[] { heliport });
            }

            UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(prefab);
            UnityEngine.Object.DestroyImmediate(heliportObject);
        }
    }

    private static Component CriarUnidadeComTorre(
        Transform root,
        string pais,
        string unidade,
        int teamId,
        out Component torre)
    {
        GameObject paisObject = new GameObject(pais);
        paisObject.transform.SetParent(root, false);
        GameObject unidadeObject = new GameObject(unidade);
        unidadeObject.transform.SetParent(paisObject.transform, false);
        Component identidade = unidadeObject.AddComponent(ResolverTipo("IdentidadeUnidade"));
        SetField(identidade, "teamID", teamId);
        GameObject torreObject = new GameObject("Torre");
        torreObject.transform.SetParent(unidadeObject.transform, false);
        torre = torreObject.AddComponent(ResolverTipo("ControleTorreta"));
        return identidade;
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
    public void RotaNaoDespachaParaCreatyDeOutroNivel()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type registryType = ResolverTipo("Hegemonia.AI.IA03.RegistroCreatysEstrategicos");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type queueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type conditionType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        Type creatyKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoCreaty");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        Type resultType = ResolverTipo("Hegemonia.AI.IA03.IA03ResultadoMissao");
        GameObject strategistObject = new GameObject("IA03 incompatible route test");
        GameObject firstObject = new GameObject("IA03 compatible route start");
        GameObject nextObject = new GameObject("IA03 incompatible route target");
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
            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "Tensao"));

            Component first = firstObject.AddComponent(creatyType);
            Component next = nextObject.AddComponent(creatyType);
            ConfigureRouteCreaty(first, "route-compatible-start", 1, 2);
            ConfigureRouteCreaty(next, "route-incompatible-next", 1, 2);
            SetField(next, "tipo", Enum.Parse(creatyKindType, "AvancoN3"));
            SetField(next, "nivelDeConflito", Enum.Parse(levelType, "AvancoMilitar"));
            firstObject.SetActive(true);
            nextObject.SetActive(true);
            RegistrarCreatyParaTeste(first);
            RegistrarCreatyParaTeste(next);
            SetField(first, "proximoPonto", next);

            SetField(mission, "idMissao", "route-incompatible-level-test");
            SetField(mission, "condicaoDeSucesso", Enum.Parse(conditionType, "ChegarAoDestino"));
            SetField(mission, "condicaoDeFracasso", Enum.Parse(conditionType, "SobreviverAteOPrazo"));
            SetField(mission, "tempoMaximoSegundos", 600f);

            unit = new GameObject("IA03 unit at valid route start");
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

            Assert.That(Field(strategist, "missaoAtiva"), Is.Null, "A missão deve ser concluída no ponto válido alcançado.");
            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("Sucesso"));
            Assert.That(creatyType.GetProperty("UnidadesReservadas").GetValue(first), Is.EqualTo(0));
            Assert.That(creatyType.GetProperty("UnidadesReservadas").GetValue(next), Is.EqualTo(0));
            Assert.That(queueType.GetProperty("PendingCount").GetValue(queue), Is.EqualTo(0), "Nenhuma ordem deve ser enviada ao ponto incompatível.");
        }
        finally
        {
            if (unit != null) UnityEngine.Object.DestroyImmediate(unit);
            UnityEngine.Object.DestroyImmediate(strategistObject);
            RemoverCreatyParaTeste(firstObject.GetComponent(creatyType));
            RemoverCreatyParaTeste(nextObject.GetComponent(creatyType));
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(nextObject);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void DesativarIA03NoProximoTickLiberaMissaoEPreservaOrdemExterna()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type queueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type requestType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandRequest");
        Type commandType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandType");
        GameObject strategistObject = new GameObject("IA03 disable cleanup test");
        GameObject creatyObject = new GameObject("IA03 disable cleanup Creaty");
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
            object context = Activator.CreateInstance(contextType);
            object queue = Activator.CreateInstance(queueType);
            SetField(context, "CommandQueue", queue);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });
            SetField(strategist, "brain", brain);

            string ia03OrderId = "ia03-disable-cleanup";
            object ia03Request = Activator.CreateInstance(requestType);
            SetField(ia03Request, "Id", ia03OrderId);
            SetField(ia03Request, "Origin", "IA03EstrategaNacional");
            SetField(ia03Request, "Domain", "tactical");
            SetField(ia03Request, "Reason", "missão de teste");
            SetField(ia03Request, "Family", "tactical");
            SetField(ia03Request, "Type", Enum.Parse(commandType, "Move"));
            SetField(ia03Request, "Priority", 10);
            SetField(ia03Request, "DedupKey", ia03OrderId);
            object externalRequest = Activator.CreateInstance(requestType);
            SetField(externalRequest, "Id", "external-disable-cleanup");
            SetField(externalRequest, "Origin", "OtherSystem");
            SetField(externalRequest, "Domain", "tactical");
            SetField(externalRequest, "Reason", "ordem de outro sistema");
            SetField(externalRequest, "Family", "tactical");
            SetField(externalRequest, "Type", Enum.Parse(commandType, "Move"));
            SetField(externalRequest, "Priority", 10);
            SetField(externalRequest, "DedupKey", "external-disable-cleanup");
            Assert.That(queueType.GetMethod("Enqueue").Invoke(queue, new object[] { ia03Request, 1f, null }), Is.EqualTo(true));
            Assert.That(queueType.GetMethod("Enqueue").Invoke(queue, new object[] { externalRequest, 1f, null }), Is.EqualTo(true));

            Component creaty = creatyObject.AddComponent(creatyType);
            ConfigureRouteCreaty(creaty, "disable-cleanup-creaty", 1, 2);
            SetField(creaty, "unidadesReservadas", 1);
            SetField(mission, "idMissao", ia03OrderId);
            unit = new GameObject("IA03 unit released from mission");
            SetField(strategist, "missaoAtiva", mission);
            SetField(strategist, "creatyAtivo", creaty);
            SetField(strategist, "unidadesReservadas", 1);
            SetField(strategist, "idOrdemAtivaDaMissao", ia03OrderId);
            ((List<GameObject>)Field(strategist, "unidadesAtivasNaMissao")).Add(unit);
            SetField(strategist, "ativo", false);

            MethodInfo tick = strategistType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(tick, Is.Not.Null);
            tick.Invoke(strategist, new object[] { 2f, 0.02f });

            Assert.That(Field(strategist, "missaoAtiva"), Is.Null);
            Assert.That(Field(strategist, "creatyAtivo"), Is.Null);
            Assert.That(Field(strategist, "unidadesReservadas"), Is.EqualTo(0));
            Assert.That(((List<GameObject>)Field(strategist, "unidadesAtivasNaMissao")).Count, Is.EqualTo(0));
            Assert.That(creatyType.GetProperty("UnidadesReservadas").GetValue(creaty), Is.EqualTo(0));
            Assert.That(queueType.GetProperty("PendingCount").GetValue(queue), Is.EqualTo(1));
            Assert.That(queueType.GetMethod("GetStatus").Invoke(queue, new object[] { ia03OrderId }).ToString(), Is.EqualTo("Cancelled"));
            Assert.That(queueType.GetMethod("GetStatus").Invoke(queue, new object[] { "external-disable-cleanup" }).ToString(), Is.EqualTo("Queued"));
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
    public void ComponenteDesativadoIgnoraTickDiretoDoScheduler()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type profileType = ResolverTipo("Hegemonia.AI.IA03.PerfilPaisSO");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        GameObject owner = new GameObject("IA03 disabled component scheduler test");
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        try
        {
            Component strategist = owner.AddComponent(strategistType);
            Component brain = owner.GetComponent(brainType);
            SetField(strategist, "perfilPais", profile);
            SetField(strategist, "ativo", true);
            SetField(strategist, "perfilAplicado", true);
            SetField(strategist, "brain", brain);
            SetField(strategist, "ultimaAnaliseEm", -1f);
            object context = Activator.CreateInstance(contextType);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });

            ((Behaviour)strategist).enabled = false;
            MethodInfo tick = strategistType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(tick, Is.Not.Null);
            tick.Invoke(strategist, new object[] { 123f, 0.02f });

            Assert.That(Field(strategist, "ultimaAnaliseEm"), Is.EqualTo(-1f),
                "O BrainMaster pode reter a referência do módulo; Tick precisa respeitar o enabled do componente.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void SomenteResultadoTerminalDeMissaoOfensivaContaBatalha()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type missionKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao");
        Type conditionType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        Type resultType = ResolverTipo("Hegemonia.AI.IA03.IA03ResultadoMissao");
        GameObject strategistObject = new GameObject("IA03 mission battle result test");
        strategistObject.SetActive(false);
        var missions = new List<ScriptableObject>();
        try
        {
            Component strategist = strategistObject.AddComponent(strategistType);
            MethodInfo finalize = strategistType.GetMethod("FinalizarMissao", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(finalize, Is.Not.Null);

            Finalizar("AtaqueLimitado", "Sucesso");
            object report = strategistType.GetProperty("RelatorioAtual").GetValue(strategist);
            Assert.That(Field(report, "BatalhasVencidas"), Is.EqualTo(1));
            Assert.That(Field(report, "BatalhasPerdidas"), Is.EqualTo(0));

            Finalizar("GuerraTotal", "Fracasso");
            Assert.That(Field(report, "BatalhasVencidas"), Is.EqualTo(1));
            Assert.That(Field(report, "BatalhasPerdidas"), Is.EqualTo(1));

            Finalizar("AtaqueLimitado", "Expirada");
            Finalizar("GuerraTotal", "Cancelada");
            Finalizar("Patrulha", "Sucesso");
            Assert.That(Field(report, "BatalhasVencidas"), Is.EqualTo(1), "Timeout, cancelamento e patrulha são inconclusivos para batalhas.");
            Assert.That(Field(report, "BatalhasPerdidas"), Is.EqualTo(1));

            void Finalizar(string tipo, string resultado)
            {
                ScriptableObject activeMission = ScriptableObject.CreateInstance(missionType);
                missions.Add(activeMission);
                SetField(activeMission, "tipoMissao", Enum.Parse(missionKindType, tipo));
                SetField(activeMission, "condicaoDeSucesso", Enum.Parse(conditionType, "DestruirAlvo"));
                SetField(strategist, "missaoAtiva", activeMission);
                finalize.Invoke(strategist, new object[] { Enum.Parse(resultType, resultado), "teste controlado" });
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(strategistObject);
            for (int i = 0; i < missions.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(missions[i]);
            }
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

    [Test]
    public void BrainMasterNaoSubstituiOrdemDaMissaoIA03AposConcluirMovimento()
    {
        Type backendType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        Type controleType = ResolverTipo("ControleUnidade");
        Type runtimeType = ResolverTipo("ControleOrdemMovimentoRuntime");
        Type orderType = ResolverTipo("TipoOrdemMovimento");
        object backend = Activator.CreateInstance(backendType, new object[] { 1 });
        GameObject unit = new GameObject("IA03 reserved mission unit");

        try
        {
            Component control = unit.AddComponent(controleType);
            object runtimeOrder = Activator.CreateInstance(runtimeType, new object[] { 2f });
            MethodInfo startOrder = runtimeType.GetMethod("TentarIniciar");
            Assert.That(startOrder, Is.Not.Null);
            object[] startArguments =
            {
                "IA03:mission:unit",
                "IA03EstrategaNacional",
                unit,
                new Vector3(20f, 0f, 0f),
                Enum.Parse(orderType, "Terrestre"),
                1f,
                false
            };
            Assert.That(startOrder.Invoke(runtimeOrder, startArguments), Is.EqualTo(true));
            MethodInfo completeOrder = runtimeType.GetMethod("Concluir");
            Assert.That(completeOrder.Invoke(runtimeOrder, new object[] { 2f }), Is.EqualTo(true));
            SetField(control, "controleOrdemMovimento", runtimeOrder);

            object commandService = backendType.GetProperty("CommandService").GetValue(backend);
            MethodInfo tryIssueMove = commandService.GetType().GetMethod("TryIssueMove", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(tryIssueMove, Is.Not.Null);
            bool accepted = (bool)tryIssueMove.Invoke(commandService, new object[]
            {
                unit,
                new Vector3(80f, 0f, 0f),
                "IA_TacticalDirector",
                "tactical-overwrite"
            });

            object currentOrder = controleType.GetProperty("OrdemMovimentoAtual").GetValue(control);
            Assert.That(accepted, Is.EqualTo(false), "ordem tática não deve assumir unidade reservada pela missão IA03");
            Assert.That(Field(currentOrder, "Dono"), Is.EqualTo("IA03EstrategaNacional"));
            Assert.That(Field(currentOrder, "Id"), Is.EqualTo("IA03:mission:unit"));
            Assert.That(Field(currentOrder, "Destino"), Is.EqualTo(new Vector3(20f, 0f, 0f)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unit);
        }
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
