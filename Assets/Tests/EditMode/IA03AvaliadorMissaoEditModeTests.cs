#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class IA03AvaliadorMissaoEditModeTests
{
    [Test]
    public void GrupoOfensivoNaoConsideraUnidadeSemArmamentoConfigurado()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        MethodInfo capability = strategistType.GetMethod(
            "PossuiCapacidadeOfensiva",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(capability, Is.Not.Null);

        GameObject unit = new GameObject("IA03 unarmed candidate test");
        GameObject projectile = new GameObject("IA03 projectile configuration test");
        unit.SetActive(false);
        projectile.SetActive(false);
        try
        {
            Component launcher = unit.AddComponent(ResolverTipo("LancadorNaval"));
            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(false));

            SetField(launcher, "prefabMissel", projectile);
            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(true));

            SetField(launcher, "municaoTotal", 0);
            SetField(launcher, "torpedosTotal", 0);
            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(false),
                "Capacidade máxima configurada não deve tornar operacional um lançador sem munição disponível.");

            SetField(launcher, "municaoTotal", 1);
            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(true));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(projectile);
            UnityEngine.Object.DestroyImmediate(unit);
        }
    }

    [TestCase("LancadorNaval", "municaoTotal", "prefabMissel")]
    [TestCase("LancadorNaval", "torpedosTotal", "prefabTorpedo")]
    [TestCase("LancadorMisselCaca", "municaoAtual", "missilCacaPrefab")]
    [TestCase("LancadorMisseis", "municaoAtual", "missilPrefab")]
    public void GrupoOfensivoNaoConsideraLancadorDeMissilSemMunicaoAtual(
        string launcherTypeName,
        string currentAmmoField,
        string projectileField)
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        MethodInfo capability = strategistType.GetMethod(
            "PossuiCapacidadeOfensiva",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(capability, Is.Not.Null);

        GameObject unit = new GameObject("IA03 empty missile launcher " + launcherTypeName);
        GameObject projectile = new GameObject("IA03 projectile marker " + launcherTypeName);
        unit.SetActive(false);
        projectile.SetActive(false);
        try
        {
            Component launcher = unit.AddComponent(ResolverTipo(launcherTypeName));
            SetField(launcher, projectileField, projectile);
            SetField(launcher, currentAmmoField, 0);

            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(false),
                "Um lançador com capacidade máxima, mas sem munição atual, não pode ser tratado como pronto para a missão.");

            SetField(launcher, currentAmmoField, 1);
            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(true));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(projectile);
            UnityEngine.Object.DestroyImmediate(unit);
        }
    }

    [Test]
    public void GrupoOfensivoNaoConsideraTorretaModularVaziaComoPronta()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type moduleType = ResolverTipo("ModuloArma");
        MethodInfo capability = strategistType.GetMethod(
            "PossuiCapacidadeOfensiva",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(capability, Is.Not.Null);
        Assert.That(moduleType, Is.Not.Null);

        GameObject unit = new GameObject("IA03 empty modular turret candidate");
        GameObject projectile = new GameObject("IA03 modular turret projectile marker");
        unit.SetActive(false);
        projectile.SetActive(false);
        try
        {
            Component turret = unit.AddComponent(ResolverTipo("ControleTorretaModular"));
            object module = Activator.CreateInstance(moduleType);
            SetField(module, "tamanhoCartucho", 10);
            SetField(module, "municaoAtual", 0);
            SetField(module, "municaoPrefab", projectile);
            Array firingPoints = Array.CreateInstance(typeof(Transform), 1);
            firingPoints.SetValue(unit.transform, 0);
            SetField(module, "pontosDisparo", firingPoints);
            Type modulesType = typeof(List<>).MakeGenericType(moduleType);
            IList modules = (IList)Activator.CreateInstance(modulesType);
            modules.Add(module);
            SetField(turret, "armas", modules);

            SetField(module, "tamanhoCartucho", 0);
            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(true),
                "Módulo configurado com tamanho de cartucho zero usa munição infinita no próprio contrato de disparo.");

            SetField(module, "tamanhoCartucho", 10);
            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(false));

            SetField(module, "municaoAtual", 1);
            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(true));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(projectile);
            UnityEngine.Object.DestroyImmediate(unit);
        }
    }

    [Test]
    public void GrupoOfensivoConsideraPenteAtualDoSistemaDeTiro()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type fireSystemType = ResolverTipo("SistemaDeTiro");
        MethodInfo capability = strategistType.GetMethod(
            "PossuiCapacidadeOfensiva",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(capability, Is.Not.Null);
        Assert.That(fireSystemType, Is.Not.Null);

        GameObject unit = new GameObject("IA03 empty direct-fire candidate");
        GameObject projectile = new GameObject("IA03 direct-fire projectile marker");
        unit.SetActive(false);
        projectile.SetActive(false);
        try
        {
            Component fireSystem = unit.AddComponent(fireSystemType);
            SetField(fireSystem, "capacidadePente", 10);
            SetField(fireSystem, "balasAtuais", 0);
            SetField(fireSystem, "recarregando", false);
            SetField(fireSystem, "prefabProjetil", projectile);

            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(false));

            SetField(fireSystem, "balasAtuais", 1);
            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(true));

            SetField(fireSystem, "balasAtuais", 0);
            SetField(fireSystem, "recarregando", true);
            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(true),
                "Um pente vazio em recarga mantém capacidade de combate para a missão já planejada.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(projectile);
            UnityEngine.Object.DestroyImmediate(unit);
        }
    }

    [TestCase("canhao")]
    [TestCase("missil")]
    public void GrupoOfensivoConsideraMunicaoAtualDaTorretaConvencional(string modoArma)
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type turretType = ResolverTipo("ControleTorreta");
        MethodInfo capability = strategistType.GetMethod(
            "PossuiCapacidadeOfensiva",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(capability, Is.Not.Null);
        Assert.That(turretType, Is.Not.Null);

        GameObject unit = new GameObject("IA03 empty conventional turret candidate " + modoArma);
        GameObject projectile = new GameObject("IA03 conventional turret projectile marker " + modoArma);
        unit.SetActive(false);
        projectile.SetActive(false);
        try
        {
            Component turret = unit.AddComponent(turretType);
            Array firingPoints = Array.CreateInstance(typeof(Transform), 1);
            firingPoints.SetValue(unit.transform, 0);

            if (modoArma == "canhao")
            {
                SetField(turret, "tamanhoCartucho", 10);
                SetField(turret, "municaoPrefab", projectile);
                SetField(turret, "locaisDoTiro", firingPoints);
                SetField(turret, "balasAtuais", 0);
                SetField(turret, "estaRecarregando", false);
                SetField(turret, "capacidadeMisseis", 0);

                Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(false));
                SetField(turret, "balasAtuais", 1);
                Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(true));
                SetField(turret, "balasAtuais", 0);
                SetField(turret, "estaRecarregando", true);
            }
            else
            {
                SetField(turret, "capacidadeMisseis", 10);
                SetField(turret, "misselPrefab", projectile);
                SetField(turret, "locaisDoMissel", firingPoints);
                SetField(turret, "misseisAtuais", 0);
                SetField(turret, "estaRecarregandoMisseis", false);
                SetField(turret, "tamanhoCartucho", 0);

                Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(false));
                SetField(turret, "misseisAtuais", 1);
                Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(true));
                SetField(turret, "misseisAtuais", 0);
                SetField(turret, "estaRecarregandoMisseis", true);
            }

            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(true),
                "A recarga ativa mantém a plataforma válida para uma missão em andamento.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(projectile);
            UnityEngine.Object.DestroyImmediate(unit);
        }
    }

    [Test]
    public void GrupoOfensivoIgnoraArmaDeHeliceVaziaForaDeRecarga()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type weaponType = ResolverTipo("SistemaArmamentoHelice");
        MethodInfo capability = strategistType.GetMethod(
            "PossuiCapacidadeOfensiva",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(capability, Is.Not.Null);

        GameObject unit = new GameObject("IA03 empty propeller gun candidate");
        GameObject projectile = new GameObject("IA03 propeller gun projectile marker");
        unit.SetActive(false);
        projectile.SetActive(false);
        try
        {
            Component weapon = unit.AddComponent(weaponType);
            Transform[] firingPoints = { unit.transform };
            SetField(weapon, "cartuchoMaximo", 100);
            SetField(weapon, "balasAtuais", 0);
            SetField(weapon, "recarregando", false);
            SetField(weapon, "prefabProjetilTrassante", projectile);
            SetField(weapon, "canosDeTiro", firingPoints);

            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(false),
                "A capacidade máxima não deve substituir a munição atual no grupo de missão.");

            SetField(weapon, "balasAtuais", 1);
            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(true));

            SetField(weapon, "balasAtuais", 0);
            SetField(weapon, "recarregando", true);
            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(true),
                "Uma arma em recarga permanece apta a voltar a disparar durante a missão.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(projectile);
            UnityEngine.Object.DestroyImmediate(unit);
        }
    }

    [Test]
    public void GrupoOfensivoConsideraTorpedoDisponivelDoSubmarinoSomenteContraAlvoNaval()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type submarineType = ResolverTipo("ControleSubmarino");
        Type torpedoType = ResolverTipo("Torpedo");
        MethodInfo capability = strategistType.GetMethod(
            "PossuiCapacidadeOfensiva",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(capability, Is.Not.Null);
        Assert.That(submarineType, Is.Not.Null);

        GameObject unit = new GameObject("IA03 torpedo-only submarine candidate");
        GameObject projectile = new GameObject("IA03 submarine torpedo marker");
        Transform tube = unit.transform;
        unit.SetActive(false);
        projectile.SetActive(false);
        try
        {
            projectile.AddComponent(torpedoType);
            Component submarine = unit.AddComponent(submarineType);
            SetField(submarine, "misseisDisponiveis", 0);
            SetField(submarine, "prefabTorpedo", projectile);
            SetField(submarine, "torpedosDisponiveis", 2);
            SetField(submarine, "tubosTorpedo", new[] { tube });
            SetField(submarine, "tubosTorpedoUsados", null);
            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(false),
                "Estado do tubo não inicializado não deve ser tratado como uma arma operacional.");

            SetField(submarine, "tubosTorpedoUsados", new[] { false });

            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(true),
                "Um torpedo disponível com tubo livre deve qualificar o submarino contra alvos de superfície.");
            Assert.That(capability.Invoke(null, new object[] { unit, true }), Is.EqualTo(false),
                "Torpedo não deve qualificar o submarino contra alvo aéreo.");

            SetField(submarine, "tubosTorpedoUsados", new[] { true });
            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(false),
                "Munição nominal não deve qualificar o submarino se todos os tubos estiverem ocupados.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(projectile);
            UnityEngine.Object.DestroyImmediate(unit);
        }
    }

    [Test]
    public void GrupoOfensivoNaoContaTorpedoNavalComoArmaContraAlvoAereo()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type launcherType = ResolverTipo("LancadorNaval");
        MethodInfo capability = strategistType.GetMethod(
            "PossuiCapacidadeOfensiva",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(capability, Is.Not.Null);
        Assert.That(launcherType, Is.Not.Null);

        GameObject unit = new GameObject("IA03 torpedo-only naval candidate");
        GameObject projectile = new GameObject("IA03 naval torpedo marker");
        unit.SetActive(false);
        projectile.SetActive(false);
        try
        {
            Component launcher = unit.AddComponent(launcherType);
            SetField(launcher, "municaoTotal", 0);
            SetField(launcher, "prefabMissel", null);
            SetField(launcher, "torpedosTotal", 2);
            SetField(launcher, "prefabTorpedo", projectile);

            Assert.That(capability.Invoke(null, new object[] { unit, false }), Is.EqualTo(true),
                "Torpedo naval disponível continua válido contra alvo de superfície.");
            Assert.That(capability.Invoke(null, new object[] { unit, true }), Is.EqualTo(false),
                "Torpedo naval sozinho não torna o lançador capaz de cumprir ataque aéreo.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(projectile);
            UnityEngine.Object.DestroyImmediate(unit);
        }
    }

    [Test]
    public void AutoridadeDeProducaoNaoBloqueiaComandosDoBrainMaster()
    {
        const int teamId = 9703;
        string productionOwner = "IA01Production:" + teamId;
        string commandOwner = "BrainMaster:" + teamId;

        Type authorityType = ResolverTipo("Hegemonia.AI.Sovereign.AIControlAuthority");
        InvokeStatic(authorityType, "ReleaseProduction", teamId, productionOwner);
        InvokeStatic(authorityType, "Release", teamId, commandOwner);
        try
        {
            Assert.That(
                (bool)InvokeStatic(authorityType, "ClaimProduction", teamId, productionOwner, 400),
                Is.True);
            Assert.That(
                (bool)InvokeStatic(authorityType, "Claim", teamId, commandOwner, 300),
                Is.True);
            Assert.That(
                (bool)InvokeStatic(authorityType, "CanIssueProduction", teamId, productionOwner),
                Is.True);
            Assert.That(
                (bool)InvokeStatic(authorityType, "CanIssue", teamId, commandOwner),
                Is.True);
        }
        finally
        {
            InvokeStatic(authorityType, "ReleaseProduction", teamId, productionOwner);
            InvokeStatic(authorityType, "Release", teamId, commandOwner);
        }
    }

    [Test]
    public void DelegacaoDeConstrucaoExigeAutoridadeDeProducaoEfetiva()
    {
        const int teamId = 9704;
        GameObject owner = new GameObject("IA production authority ownership test");
        owner.SetActive(false);

        try
        {
            Component ia01 = owner.AddComponent(ResolverTipo("Hegemonia.AI.IA01.IA01Controller"));
            SetField(ia01, "teamId", teamId);
            SetField(ia01, "autoRegisterWithManager", false);
            owner.SetActive(true);

            Type ia01AuthorityType = ResolverTipo("Hegemonia.AI.IA01.IA01ConstructionAuthority");
            Assert.That(
                (bool)InvokeStatic(ia01AuthorityType, "IsOwner", teamId),
                Is.False,
                "Modo Full sem claim registrado não deve bloquear o BrainMaster.");

            owner.SetActive(false);
            Component ia02 = owner.AddComponent(ResolverTipo("Hegemonia.AI.IA02.IA02Controller"));
            SetField(ia02, "teamId", teamId);
            SetField(ia02, "autoRegisterWithManager", false);
            owner.SetActive(true);

            Type ia02AuthorityType = ResolverTipo("Hegemonia.AI.IA02.IA02ConstructionAuthority");
            Assert.That(
                (bool)InvokeStatic(ia02AuthorityType, "IsOwner", teamId),
                Is.False,
                "Modo Full sem claim registrado não deve bloquear o BrainMaster.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

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
        Assert.That(Avaliar("DestruirAlvo", alvoDestruido: true, perdeuUnidade: true), Is.EqualTo("Sucesso"),
            "A conclusão do objetivo ofensivo tem precedência sobre a perda do grupo.");
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

            SetField(strategist, "nivelDeConflito", Enum.Parse(
                ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito"), "Tensao"));
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
        const int teamId = 9705;
        string commandOwner = "BrainMaster:" + teamId;
        Type authorityType = ResolverTipo("Hegemonia.AI.Sovereign.AIControlAuthority");
        try
        {
            Component strategist = strategistObject.AddComponent(strategistType);
            Component brain = strategistObject.GetComponent(brainType);
            FieldInfo integrationMode = brainType.GetField("IntegrationMode");
            SetField(brain, "IntegrationMode", Enum.Parse(integrationMode.FieldType, "Hybrid"));
            SetField(brain, "TeamId", teamId);
            SetField(brain, "_authorityOwnerKey", commandOwner);
            InvokeStatic(authorityType, "Release", teamId, commandOwner);
            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "Tensao"));

            object context = Activator.CreateInstance(contextType);
            object queue = Activator.CreateInstance(queueType);
            SetField(context, "CommandQueue", queue);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });
            SetField(strategist, "brain", brain);

            Component first = firstObject.AddComponent(creatyType);
            Component next = nextObject.AddComponent(creatyType);
            ConfigureRouteCreaty(first, "route-first", teamId, 2);
            ConfigureRouteCreaty(next, "route-second", teamId, 2);
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
            Assert.That(creatyType.GetMethod("PodeReservar").Invoke(next, new object[] { teamId, 2, 1 }), Is.EqualTo(true), "O ponto de rota precisa aceitar a reserva do grupo.");

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
                + ", podeReservar=" + creatyType.GetMethod("PodeReservar").Invoke(next, new object[] { teamId, 2, 1 })
                + ", candidatos=" + routeCandidates.Count
                + ", registrados=" + registryType.GetProperty("QuantidadeRegistrada").GetValue(null)
                + ", proprietário=" + ownerTeamId + ", alvo=" + targetTeamId
                + ", nível=" + currentLevel + ", domínio=" + currentDomain);

            MethodInfo process = strategistType.GetMethod("ProcessarMissaoAtiva", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(process, Is.Not.Null);
            process.Invoke(strategist, new object[] { 1f });
            Assert.That(Field(strategist, "creatyAtivo"), Is.SameAs(first),
                "A rota não deve avançar enquanto o BrainMaster não possui o canal de comando.");
            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("EmAndamento"));
            Assert.That(Field(strategist, "ultimaDecisao").ToString(), Is.EqualTo("rota estratégica aguardando autoridade de comando"));

            Assert.That(
                (bool)InvokeStatic(authorityType, "Claim", teamId, commandOwner, 300),
                Is.True,
                "O teste de retomada precisa devolver o canal de comando ao BrainMaster.");
            process.Invoke(strategist, new object[] { 3f });
            Assert.That(Field(strategist, "creatyAtivo"), Is.SameAs(next));
            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("EmAndamento"));
            Assert.That(((HashSet<int>)Field(strategist, "unidadesComOrdemConcluidaNaMissao")).Count, Is.EqualTo(0));
            Assert.That(Field(strategist, "missaoAtiva"), Is.SameAs(mission), "avançar a rota não deve encerrar a missão");
            Assert.That(Vector3.Distance(unit.transform.position, next.transform.position), Is.GreaterThan(30f));

            process.Invoke(strategist, new object[] { 4f });
            Assert.That(Field(strategist, "missaoAtiva"), Is.SameAs(mission), "a missão foi encerrada antes da chegada ao segundo Creaty");
            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("EmAndamento"), "estado após o segundo trecho");
        }
        finally
        {
            InvokeStatic(authorityType, "Release", teamId, commandOwner);
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
    public void CreatyAtivadoEIndexadoEDesativadoSaiDoIndiceDeCandidatos()
    {
        Type registryType = ResolverTipo("Hegemonia.AI.IA03.RegistroCreatysEstrategicos");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        Type creatyKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoCreaty");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        Type domainType = ResolverTipo("Hegemonia.AI.IA03.IA03DominioEstrategico");
        GameObject creatyObject = new GameObject("IA03 Creaty lifecycle registry test");
        creatyObject.SetActive(false);
        Component creaty = null;
        const int ownerTeamId = 9717;
        const int targetTeamId = 9723;

        try
        {
            creaty = creatyObject.AddComponent(creatyType);
            ConfigureRouteCreaty(creaty, "creaty-lifecycle-registry-test", ownerTeamId, targetTeamId);
            SetField(creaty, "tipo", Enum.Parse(creatyKindType, "PatrulhaNaval"));
            SetField(creaty, "nivelDeConflito", Enum.Parse(levelType, "ConflitoLimitado"));
            SetField(creaty, "dominio", Enum.Parse(domainType, "Naval"));

            Type candidateListType = typeof(List<>).MakeGenericType(creatyType);
            IList candidates = (IList)Activator.CreateInstance(candidateListType);
            MethodInfo fillCandidates = registryType.GetMethod("PreencherCandidatos");
            Assert.That(fillCandidates, Is.Not.Null);

            creatyObject.SetActive(true);
            // EditMode does not dispatch runtime lifecycle messages for this
            // component, so invoke the real callbacks to test their registry wiring.
            MethodInfo onEnable = creatyType.GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo onDisable = creatyType.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onEnable, Is.Not.Null);
            Assert.That(onDisable, Is.Not.Null);
            onEnable.Invoke(creaty, null);
            fillCandidates.Invoke(null, new object[]
            {
                candidates,
                ownerTeamId,
                targetTeamId,
                Enum.Parse(creatyKindType, "PatrulhaNaval"),
                Enum.Parse(levelType, "ConflitoLimitado"),
                Enum.Parse(domainType, "Naval")
            });

            Assert.That(candidates.Contains(creaty), Is.True,
                "O registro ativo deve inserir o Creaty no índice consultado pela IA03.");

            creatyObject.SetActive(false);
            onDisable.Invoke(creaty, null);
            candidates.Clear();
            fillCandidates.Invoke(null, new object[]
            {
                candidates,
                ownerTeamId,
                targetTeamId,
                Enum.Parse(creatyKindType, "PatrulhaNaval"),
                Enum.Parse(levelType, "ConflitoLimitado"),
                Enum.Parse(domainType, "Naval")
            });

            Assert.That(candidates.Contains(creaty), Is.False,
                "OnDisable deve remover o Creaty para que a IA03 não receba referência inativa.");
        }
        finally
        {
            RemoverCreatyParaTeste(creaty);
            UnityEngine.Object.DestroyImmediate(creatyObject);
        }
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
    public void RegraDeDominioAvaliaFavoravelDesfavoravelEmpateEInsuficiencia()
    {
        Type relatorioType = ResolverTipo("Hegemonia.AI.IA03.IA03RelatorioConflito");
        MethodInfo registrar = relatorioType.GetMethod("RegistrarResultadoCombate");
        MethodInfo avaliar = relatorioType.GetMethod("AtingiuDominioMinimo");

        bool AvaliarResultados(int vitorias, int derrotas, int minimo)
        {
            object relatorio = Activator.CreateInstance(relatorioType);
            for (int i = 0; i < vitorias; i++) registrar.Invoke(relatorio, new object[] { true });
            for (int i = 0; i < derrotas; i++) registrar.Invoke(relatorio, new object[] { false });
            return (bool)avaliar.Invoke(relatorio, new object[] { minimo, 2f / 3f });
        }

        Assert.That(AvaliarResultados(2, 1, 3), Is.True, "2/3 deve satisfazer o limite exato de dois terços.");
        Assert.That(AvaliarResultados(1, 2, 3), Is.False, "Um resultado claramente desfavorável não deve passar.");
        Assert.That(AvaliarResultados(1, 1, 2), Is.False, "Empate não atinge o domínio de dois terços.");
        Assert.That(AvaliarResultados(2, 0, 3), Is.False, "Dois resultados ainda são insuficientes para o mínimo configurado de três.");
    }

    [Test]
    public void AvaliacaoDeConflitoLimitadoEsperaOTempoMinimoConfigurado()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type governmentType = ResolverTipo("SistemaGovernoMundial");
        Type profileType = ResolverTipo("Hegemonia.AI.IA03.PerfilPaisSO");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        PropertyInfo governmentInstance = governmentType.GetProperty(
            "Instancia", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        object previousGovernment = governmentInstance.GetValue(null, null);
        GameObject strategistObject = new GameObject("IA03 limited conflict evaluation timing test");
        strategistObject.SetActive(false);
        GameObject governmentObject = new GameObject("IA03 timing test government");
        governmentObject.SetActive(false);
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        try
        {
            Component government = governmentObject.AddComponent(governmentType);
            governmentInstance.GetSetMethod(true).Invoke(null, new[] { government });
            Component strategist = strategistObject.AddComponent(strategistType);
            Component brain = strategistObject.GetComponent(brainType);
            SetField(strategist, "brain", brain);
            SetField(strategist, "perfilPais", profile);
            SetField(strategist, "paisAlvoTeamId", 3);
            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "ConflitoLimitado"));
            SetField(strategist, "inicioGuerraEm", 100f);
            SetField(profile, "avaliacaoConflitoLimitadoSegundos", 10f);

            MethodInfo evaluate = strategistType.GetMethod("AvaliarPrazosDeGuerra", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(evaluate, Is.Not.Null);
            evaluate.Invoke(strategist, new object[] { government, 109.99f });
            Assert.That(Field(strategist, "avaliouConflitoLimitado"), Is.False,
                "A avaliação não pode ocorrer antes do limite de 10 segundos configurado.");

            evaluate.Invoke(strategist, new object[] { government, 110.01f });
            Assert.That(Field(strategist, "avaliouConflitoLimitado"), Is.True,
                "A avaliação deve ocorrer ao atingir o tempo mínimo configurado.");
        }
        finally
        {
            governmentInstance.GetSetMethod(true).Invoke(null, new[] { previousGovernment });
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(governmentObject);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void EscaladaAutomaticaN1ExigeEconomiaDisponivelEContingenteMobilizavel()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type forceType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_ForceSnapshot");
        Type summaryType = ResolverTipo("Hegemonia.AI.IA03.GestorEconomiaIA03+Resumo");
        Type governmentType = ResolverTipo("SistemaGovernoMundial");
        Type countryType = ResolverTipo("DadosPaisGoverno");
        Type profileType = ResolverTipo("Hegemonia.AI.IA03.PerfilPaisSO");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        PropertyInfo governmentInstance = governmentType.GetProperty(
            "Instancia", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        object previousGovernment = governmentInstance.GetValue(null, null);
        GameObject strategistObject = new GameObject("IA03 N1 readiness escalation test");
        strategistObject.SetActive(false);
        GameObject governmentObject = new GameObject("IA03 readiness test government");
        governmentObject.SetActive(false);
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        try
        {
            Component government = governmentObject.AddComponent(governmentType);
            governmentInstance.GetSetMethod(true).Invoke(null, new[] { government });
            object country = Activator.CreateInstance(countryType);
            SetField(country, "teamId", 2);
            SetField(country, "populacaoCivil", 1);
            IList countries = (IList)governmentType.GetField("paises").GetValue(government);
            countries.Clear();
            countries.Add(country);
            Assert.That(governmentType.GetMethod("ObterPais").Invoke(government, new object[] { 2 }), Is.SameAs(country),
                "A fixture deve avaliar somente o país de teste, sem uma cópia padrão com efetivo diferente.");
            Component strategist = strategistObject.AddComponent(strategistType);
            Component brain = strategistObject.GetComponent(brainType);
            SetField(strategist, "brain", brain);
            SetField(strategist, "perfilPais", profile);
            SetField(strategist, "paisAlvoTeamId", 3);
            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "ConflitoLimitado"));
            SetField(strategist, "inicioGuerraEm", 0f);
            SetField(strategist, "avaliouConflitoLimitado", true);
            SetField(profile, "agressividade", 0.9f);
            SetField(profile, "avaliacaoConflitoLimitadoSegundos", 10f);
            SetField(profile, "preferenciaTerrestre", 0.6f);

            object summary = Activator.CreateInstance(summaryType);
            SetField(summary, "DadosDisponiveis", true);
            SetField(summary, "ReservaFinanceiraBaixa", true);
            SetField(strategist, "economiaAtual", summary);

            object force = Activator.CreateInstance(forceType);
            SetField(force, "TotalCombatUnits", 2);
            SetField(force, "FactoryCount", 1);
            SetField(force, "BarracksCount", 1);
            SetField(force, "TankUnits", 2);
            object context = Activator.CreateInstance(contextType);
            SetField(context, "ForceSnapshot", force);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });

            MethodInfo evaluate = strategistType.GetMethod(
                "AvaliarPrazosDeGuerra", BindingFlags.Instance | BindingFlags.NonPublic);
            evaluate.Invoke(strategist, new object[] { government, 320f });
            Assert.That(Field(strategist, "nivelDeConflito").ToString(), Is.EqualTo("ConflitoLimitado"));
            Assert.That(Field(strategist, "ultimaDecisao").ToString(), Does.Contain("reserva financeira"));

            SetField(summary, "ReservaFinanceiraBaixa", false);
            SetField(strategist, "economiaAtual", summary);

            SetField(force, "FactoryCount", 0);
            evaluate.Invoke(strategist, new object[] { government, 321f });
            Assert.That(Field(strategist, "nivelDeConflito").ToString(), Is.EqualTo("ConflitoLimitado"));
            Assert.That(Field(strategist, "ultimaDecisao").ToString(), Does.Contain("sem fábrica"));

            SetField(force, "FactoryCount", 1);
            SetField(country, "populacaoCivil", 0);
            evaluate.Invoke(strategist, new object[] { government, 322f });
            Assert.That(Field(strategist, "nivelDeConflito").ToString(), Is.EqualTo("ConflitoLimitado"));
            Assert.That(Field(strategist, "ultimaDecisao").ToString(), Does.Contain("sem manpower"));

            SetField(country, "populacaoCivil", 1);
            SetField(force, "TankUnits", 0);
            evaluate.Invoke(strategist, new object[] { government, 323f });
            Assert.That(Field(strategist, "nivelDeConflito").ToString(), Is.EqualTo("ConflitoLimitado"));
            Assert.That(Field(strategist, "ultimaDecisao").ToString(), Does.Contain("domínio terrestre prioritário"));

            SetField(force, "TankUnits", 1);
            SetField(force, "TotalCombatUnits", 1);
            evaluate.Invoke(strategist, new object[] { government, 324f });
            Assert.That(Field(strategist, "nivelDeConflito").ToString(), Is.EqualTo("ConflitoLimitado"));
            Assert.That(Field(strategist, "ultimaDecisao").ToString(), Does.Contain("contingente mínimo N1"));

            SetField(force, "TotalCombatUnits", 2);
            SetField(force, "TankUnits", 2);
            evaluate.Invoke(strategist, new object[] { government, 325f });
            Assert.That(Field(strategist, "nivelDeConflito").ToString(), Is.EqualTo("GuerraTotal"));

            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "ConflitoLimitado"));
            SetField(strategist, "inicioGuerraEm", 0f);
            SetField(force, "FactoryCount", 0);
            SetField(profile, "preferenciaTerrestre", 0.2f);
            SetField(profile, "preferenciaNaval", 0.1f);
            SetField(profile, "preferenciaAerea", 0.6f);
            SetField(force, "ReadyAircraft", 1);
            SetField(force, "MilitaryAirportCount", 1);
            evaluate.Invoke(strategist, new object[] { government, 326f });
            Assert.That(Field(strategist, "nivelDeConflito").ToString(), Is.EqualTo("GuerraTotal"),
                "Doutrina aérea com aeronave e aeroporto militar não deve exigir fábrica terrestre.");

            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "ConflitoLimitado"));
            SetField(strategist, "inicioGuerraEm", 0f);
            SetField(profile, "preferenciaTerrestre", 0.1f);
            SetField(profile, "preferenciaAerea", 0.2f);
            SetField(profile, "preferenciaNaval", 0.6f);
            SetField(profile, "minimoNaviosEscolta", 1);
            SetField(force, "NavalUnits", 1);
            SetField(force, "ShipyardCount", 1);
            evaluate.Invoke(strategist, new object[] { government, 327f });
            Assert.That(Field(strategist, "nivelDeConflito").ToString(), Is.EqualTo("GuerraTotal"),
                "Doutrina naval com escolta e base operacional não deve exigir fábrica terrestre.");

            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "ConflitoLimitado"));
            SetField(strategist, "inicioGuerraEm", 0f);
            SetField(profile, "preferenciaTerrestre", 0.4f);
            SetField(profile, "preferenciaAerea", 0.4f);
            SetField(profile, "preferenciaNaval", 0.2f);
            evaluate.Invoke(strategist, new object[] { government, 328f });
            Assert.That(Field(strategist, "nivelDeConflito").ToString(), Is.EqualTo("ConflitoLimitado"));
            Assert.That(Field(strategist, "ultimaDecisao").ToString(), Does.Contain("sem fábrica"),
                "Doutrina mista ainda deve preservar o gate atual de capacidade industrial.");
        }
        finally
        {
            governmentInstance.GetSetMethod(true).Invoke(null, new[] { previousGovernment });
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(governmentObject);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [TestCase("ConflitoLimitado")]
    [TestCase("GuerraTotal")]
    public void MissaoOfensivaN2EN1AguardaProntidaoEconomica(string nivelNome)
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type worldStateType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_WorldState");
        Type commandQueueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type summaryType = ResolverTipo("Hegemonia.AI.IA03.GestorEconomiaIA03+Resumo");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        Type authorityType = ResolverTipo("Hegemonia.AI.Sovereign.AIControlAuthority");
        Type governmentType = ResolverTipo("SistemaGovernoMundial");
        PropertyInfo governmentInstance = governmentType.GetProperty(
            "Instancia", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        object previousGovernment = governmentInstance.GetValue(null, null);
        const int teamId = 2;
        GameObject owner = new GameObject("IA03 readiness mission gate test");
        owner.SetActive(false);
        GameObject governmentObject = new GameObject("IA03 readiness diplomacy test government");
        governmentObject.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO"));
        string authorityOwnerKey = null;

        try
        {
            Component government = governmentObject.AddComponent(governmentType);
            governmentInstance.GetSetMethod(true).Invoke(null, new[] { government });
            object relation = governmentType.GetMethod("ObterRelacao", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(government, new object[] { teamId, 3 });
            SetField(relation, "guerraDeclarada", true);

            Component brain = owner.AddComponent(brainType);
            SetField(brain, "TeamId", teamId);
            authorityOwnerKey = "BrainMaster:" + teamId + ":" + brain.GetInstanceID();
            SetField(brain, "_authorityOwnerKey", authorityOwnerKey);
            InvokeStatic(authorityType, "Release", teamId, authorityOwnerKey);
            Assert.That(
                InvokeStatic(authorityType, "Claim", teamId, authorityOwnerKey, 300),
                Is.EqualTo(true),
                "O teste de prontidão precisa dar ao BrainMaster a autoridade de execução que testa.");

            Component strategist = owner.AddComponent(strategistType);
            object context = Activator.CreateInstance(contextType);
            object worldState = Activator.CreateInstance(worldStateType, new object[] { 2 });
            object commandQueue = Activator.CreateInstance(commandQueueType);
            SetField(context, "WorldState", worldState);
            SetField(context, "CommandQueue", commandQueue);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });
            SetField(strategist, "brain", brain);
            SetField(strategist, "paisAlvoTeamId", 3);
            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, nivelNome));
            SetField(mission, "tipoMissao", Enum.Parse(ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao"),
                nivelNome == "GuerraTotal" ? "GuerraTotal" : "AtaqueLimitado"));

            object summary = Activator.CreateInstance(summaryType);
            if (nivelNome == "ConflitoLimitado")
            {
                SetField(summary, "PodeEscalarParaN2", false);
                SetField(summary, "MotivoProntidao", "combustível operacional baixo");
            }
            else
            {
                SetField(summary, "DadosDisponiveis", false);
            }
            SetField(strategist, "economiaAtual", summary);

            MethodInfo tentarIniciar = strategistType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .FirstOrDefault(method => method.Name == "TentarIniciarMissao"
                    && method.GetParameters().Length == 3
                    && method.GetParameters()[0].ParameterType == ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO")
                    && method.GetParameters()[1].ParameterType == typeof(float)
                    && method.GetParameters()[2].IsOut);
            Assert.That(tentarIniciar, Is.Not.Null);
            object[] missionArguments = { mission, 100f, null };
            bool missionStarted = (bool)tentarIniciar.Invoke(strategist, missionArguments);

            string decisao = Convert.ToString(missionArguments[2]);
            Assert.That(missionStarted, Is.False);
            Assert.That(decisao, Does.Contain("missão ofensiva adiada"));
            Assert.That(decisao, Does.Contain(nivelNome == "ConflitoLimitado" ? "Nível 2" : "Nível 1"));
            Assert.That(Field(strategist, "missaoAtiva"), Is.Null);
            Assert.That((int)commandQueueType.GetProperty("PendingCount").GetValue(commandQueue), Is.EqualTo(0));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(authorityOwnerKey))
            {
                InvokeStatic(authorityType, "Release", teamId, authorityOwnerKey);
            }
            governmentInstance.GetSetMethod(true).Invoke(null, new[] { previousGovernment });
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(governmentObject);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void MissaoQueExigeDestruirAlvoSemAlvoObservadoNaoEnfileiraOrdem()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type worldStateType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_WorldState");
        Type queueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type missionKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao");
        Type missionOrderType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoOrdem");
        Type missionConditionType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        GameObject strategistObject = new GameObject("IA03 attack without observed target test");
        GameObject creatyObject = new GameObject("IA03 attack without observed target Creaty");
        strategistObject.SetActive(false);
        creatyObject.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);

        try
        {
            Component brain = strategistObject.AddComponent(brainType);
            Component strategist = strategistObject.AddComponent(strategistType);
            Component creaty = creatyObject.AddComponent(creatyType);
            SetField(brain, "TeamId", 1);

            object context = Activator.CreateInstance(contextType);
            object worldState = Activator.CreateInstance(worldStateType, new object[] { 1 });
            object queue = Activator.CreateInstance(queueType);
            SetField(context, "WorldState", worldState);
            SetField(context, "CommandQueue", queue);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });
            SetField(strategist, "brain", brain);
            SetField(strategist, "paisAlvoTeamId", 3);

            SetField(mission, "tipoMissao", Enum.Parse(missionKindType, "AtaqueLimitado"));
            SetField(mission, "tipoOrdem", Enum.Parse(missionOrderType, "Atacar"));
            SetField(mission, "condicaoDeSucesso", Enum.Parse(missionConditionType, "DestruirAlvo"));

            MethodInfo enqueue = strategistType.GetMethod(
                "EnfileirarOrdem",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(enqueue, Is.Not.Null);
            object[] arguments = { mission, creaty, new List<GameObject>(), null, null };
            Assert.That(enqueue.Invoke(strategist, arguments), Is.EqualTo(false));
            Assert.That(Convert.ToString(arguments[3]), Does.Contain("alvo"));
            Assert.That(queueType.GetProperty("PendingCount").GetValue(queue), Is.EqualTo(0),
                "Uma missão que exige destruir um alvo não pode deixar uma ordem sem alvo na fila.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(creatyObject);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void MissaoAtacarSemAlvoObservadoEnfileiraCoordenadaDoCreaty()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type worldStateType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_WorldState");
        Type queueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type missionKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao");
        Type missionOrderType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoOrdem");
        Type missionConditionType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        GameObject strategistObject = new GameObject("IA03 coordinate attack without visible target test");
        GameObject creatyObject = new GameObject("IA03 coordinate attack target Creaty");
        GameObject unit = new GameObject("IA03 coordinate attack unit");
        strategistObject.SetActive(false);
        creatyObject.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);

        try
        {
            Component brain = strategistObject.AddComponent(brainType);
            Component strategist = strategistObject.AddComponent(strategistType);
            Component creaty = creatyObject.AddComponent(creatyType);
            SetField(brain, "TeamId", 1);

            object context = Activator.CreateInstance(contextType);
            object worldState = Activator.CreateInstance(worldStateType, new object[] { 1 });
            object queue = Activator.CreateInstance(queueType);
            SetField(context, "WorldState", worldState);
            SetField(context, "CommandQueue", queue);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });
            SetField(strategist, "brain", brain);
            SetField(strategist, "paisAlvoTeamId", 3);
            SetField(creaty, "paisAlvoTeamId", 3);
            creatyObject.transform.position = new Vector3(30f, 0f, 40f);

            SetField(mission, "tipoMissao", Enum.Parse(missionKindType, "AtaqueLimitado"));
            SetField(mission, "tipoOrdem", Enum.Parse(missionOrderType, "Atacar"));
            SetField(mission, "condicaoDeSucesso", Enum.Parse(missionConditionType, "ChegarAoDestino"));
            SetField(mission, "condicaoDeFracasso", Enum.Parse(missionConditionType, "SobreviverAteOPrazo"));

            MethodInfo requiresObservedTarget = strategistType.GetMethod(
                "MissaoExigeDestruicaoDeAlvo",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(requiresObservedTarget, Is.Not.Null);
            Assert.That(requiresObservedTarget.Invoke(null, new object[] { mission }), Is.EqualTo(false));

            MethodInfo enqueue = strategistType.GetMethod(
                "EnfileirarOrdem",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(enqueue, Is.Not.Null);
            object[] enqueueArguments = { mission, creaty, new List<GameObject> { unit }, null, null };
            Assert.That(enqueue.Invoke(strategist, enqueueArguments), Is.EqualTo(true),
                Convert.ToString(enqueueArguments[3]));

            object[] dequeueArguments = { Time.time, null };
            MethodInfo tryDequeue = queueType.GetMethod("TryDequeue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(tryDequeue, Is.Not.Null);
            Assert.That((bool)tryDequeue.Invoke(queue, dequeueArguments), Is.EqualTo(true));
            object payload = Field(dequeueArguments[1], "Payload");
            Assert.That(Field(payload, "Target"), Is.Null);
            Assert.That(Field(payload, "TargetPosition"), Is.EqualTo(creatyObject.transform.position));
            Assert.That(Field(payload, "RequiresDeclaredWar"), Is.EqualTo(true));
            Assert.That(Field(payload, "TargetTeamId"), Is.EqualTo(3));
            Assert.That(Field(payload, "AllowCoordinateAttackAgainstKnownEnemy"), Is.EqualTo(true));
            Assert.That(Field(strategist, "alvoMissaoFoiDefinido"), Is.EqualTo(false),
                "Uma missão por coordenadas não pode ser tratada como destruição confirmada de um alvo específico.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unit);
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(creatyObject);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void MissaoComDestruicaoForcaOrdemDeAtaqueMesmoComTipoMover()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type worldStateType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_WorldState");
        Type queueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type observationType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_EnemyObservation");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type missionKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao");
        Type missionOrderType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoOrdem");
        Type missionConditionType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        Type commandType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandType");
        GameObject strategistObject = new GameObject("IA03 destroy target move normalization test");
        GameObject creatyObject = new GameObject("IA03 destroy target move normalization Creaty");
        GameObject targetObject = new GameObject("IA03 destroy target move normalization target");
        strategistObject.SetActive(false);
        creatyObject.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);

        try
        {
            Component brain = strategistObject.AddComponent(brainType);
            Component strategist = strategistObject.AddComponent(strategistType);
            Component creaty = creatyObject.AddComponent(creatyType);
            SetField(brain, "TeamId", 1);

            object context = Activator.CreateInstance(contextType);
            object worldState = Activator.CreateInstance(worldStateType, new object[] { 1 });
            object queue = Activator.CreateInstance(queueType);
            SetField(context, "WorldState", worldState);
            SetField(context, "CommandQueue", queue);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });
            SetField(strategist, "brain", brain);
            SetField(strategist, "paisAlvoTeamId", 3);

            IList visibleEnemies = (IList)worldStateType.GetField("VisibleEnemies").GetValue(worldState);
            object observation = Activator.CreateInstance(observationType);
            SetField(observation, "TeamId", 3);
            SetField(observation, "Transform", targetObject.transform);
            SetField(observation, "Position", targetObject.transform.position);
            SetField(observation, "ThreatScore", 100f);
            visibleEnemies.Add(observation);

            SetField(mission, "tipoMissao", Enum.Parse(missionKindType, "AtaqueLimitado"));
            SetField(mission, "tipoOrdem", Enum.Parse(missionOrderType, "Mover"));
            SetField(mission, "condicaoDeSucesso", Enum.Parse(missionConditionType, "DestruirAlvo"));

            MethodInfo enqueue = strategistType.GetMethod("EnfileirarOrdem", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(enqueue, Is.Not.Null);
            object[] arguments = { mission, creaty, new List<GameObject>(), null, null };
            Assert.That(enqueue.Invoke(strategist, arguments), Is.EqualTo(true));
            Assert.That(queueType.GetProperty("PendingCount").GetValue(queue), Is.EqualTo(1));

            object[] dequeueArguments = { Time.time, null };
            Assert.That(queueType.GetMethod("TryDequeue").Invoke(queue, dequeueArguments), Is.EqualTo(true));
            Assert.That(Field(dequeueArguments[1], "Type").ToString(), Is.EqualTo("Attack"));
            Assert.That(Field(dequeueArguments[1], "Payload").GetType().Name, Is.EqualTo("IA_AttackOrderData"));
            Assert.That(Enum.Parse(commandType, "Attack"), Is.EqualTo(Field(dequeueArguments[1], "Type")));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(creatyObject);
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void MissaoOfensivaIgnoraObservacaoDeOutraEquipeMesmoSemIdentidadeNoTransform()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type worldStateType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_WorldState");
        Type queueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type observationType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_EnemyObservation");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type missionKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao");
        Type missionOrderType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoOrdem");
        Type missionConditionType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        GameObject strategistObject = new GameObject("IA03 target team mismatch test");
        GameObject creatyObject = new GameObject("IA03 target team mismatch Creaty");
        strategistObject.SetActive(false);
        creatyObject.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);

        try
        {
            Component brain = strategistObject.AddComponent(brainType);
            Component strategist = strategistObject.AddComponent(strategistType);
            Component creaty = creatyObject.AddComponent(creatyType);
            SetField(brain, "TeamId", 1);

            object context = Activator.CreateInstance(contextType);
            object worldState = Activator.CreateInstance(worldStateType, new object[] { 1 });
            object queue = Activator.CreateInstance(queueType);
            SetField(context, "WorldState", worldState);
            SetField(context, "CommandQueue", queue);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });
            SetField(strategist, "brain", brain);
            SetField(strategist, "paisAlvoTeamId", 3);

            IList visibleEnemies = (IList)worldStateType.GetField("VisibleEnemies").GetValue(worldState);
            object observation = Activator.CreateInstance(observationType);
            SetField(observation, "TeamId", 4);
            SetField(observation, "Position", Vector3.zero);
            SetField(observation, "ThreatScore", 100f);
            SetField(observation, "Transform", null);
            visibleEnemies.Add(observation);

            SetField(mission, "tipoMissao", Enum.Parse(missionKindType, "AtaqueLimitado"));
            SetField(mission, "tipoOrdem", Enum.Parse(missionOrderType, "Atacar"));
            SetField(mission, "condicaoDeSucesso", Enum.Parse(missionConditionType, "DestruirAlvo"));

            MethodInfo enqueue = strategistType.GetMethod(
                "EnfileirarOrdem",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(enqueue, Is.Not.Null);
            object[] arguments = { mission, creaty, new List<GameObject>(), null, null };

            Assert.That(enqueue.Invoke(strategist, arguments), Is.EqualTo(false));
            Assert.That(Convert.ToString(arguments[3]), Does.Contain("alvo"));
            Assert.That(queueType.GetProperty("PendingCount").GetValue(queue), Is.EqualTo(0),
                "Uma observação de outra equipe não pode virar uma ordem Attack sem identidade válida.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(creatyObject);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void ResumoEconomicoIA03ReconheceDeficitDeEnergiaComoEstoqueEssencialBaixo()
    {
        Type economyType = ResolverTipo("Hegemonia.AI.IA03.GestorEconomiaIA03");
        Type summaryType = ResolverTipo("Hegemonia.AI.IA03.GestorEconomiaIA03+Resumo");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type governmentType = ResolverTipo("SistemaGovernoMundial");
        Type countryType = ResolverTipo("DadosPaisGoverno");
        Type profileType = ResolverTipo("Hegemonia.AI.IA03.PerfilPaisSO");
        GameObject governmentObject = new GameObject("IA03 energy deficit government");
        governmentObject.SetActive(false);
        GameObject brainObject = new GameObject("IA03 energy deficit brain");
        brainObject.SetActive(false);
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        try
        {
            Component government = governmentObject.AddComponent(governmentType);
            Component brain = brainObject.AddComponent(brainType);
            object country = Activator.CreateInstance(countryType);
            SetField(country, "teamId", 2);
            SetField(country, "saldo", 20000L);
            SetField(country, "comida", 500);
            SetField(country, "petroleo", 500);
            SetField(country, "energia", 500);
            SetField(country, "deficitEnergia", 0.25f);

            IList countries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(countryType));
            countries.Add(country);
            SetField(government, "paises", countries);

            object economy = Activator.CreateInstance(economyType);
            MethodInfo applyWeights = economyType.GetMethod("TentarAplicarPesos");
            object[] arguments = { government, brain, profile, 2, false, Activator.CreateInstance(summaryType) };
            Assert.That(applyWeights.Invoke(economy, arguments), Is.EqualTo(true));

            object summary = arguments[5];
            Assert.That(Field(summary, "DadosDisponiveis"), Is.EqualTo(true));
            Assert.That(Field(summary, "DeficitEnergia"), Is.EqualTo(0.25f));
            Assert.That(Field(summary, "EstoqueEssencialBaixo"), Is.EqualTo(true));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(governmentObject);
            UnityEngine.Object.DestroyImmediate(brainObject);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void ResumoEconomicoIA03BloqueiaProntidaoQuandoTodoOTesouroEstaReservado()
    {
        Type economyType = ResolverTipo("Hegemonia.AI.IA03.GestorEconomiaIA03");
        Type summaryType = ResolverTipo("Hegemonia.AI.IA03.GestorEconomiaIA03+Resumo");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type governmentType = ResolverTipo("SistemaGovernoMundial");
        Type countryType = ResolverTipo("DadosPaisGoverno");
        Type profileType = ResolverTipo("Hegemonia.AI.IA03.PerfilPaisSO");
        GameObject governmentObject = new GameObject("IA03 reserved treasury government");
        governmentObject.SetActive(false);
        GameObject brainObject = new GameObject("IA03 reserved treasury brain");
        brainObject.SetActive(false);
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        try
        {
            Component government = governmentObject.AddComponent(governmentType);
            Component brain = brainObject.AddComponent(brainType);
            object country = Activator.CreateInstance(countryType);
            SetField(country, "teamId", 2);
            SetField(country, "saldo", 20000L);
            SetField(country, "comida", 500);
            SetField(country, "petroleo", 500);
            SetField(country, "energia", 500);
            SetField(country, "alistaveis", 1000);
            SetField(country, "populacaoCivil", 10000);

            IList countries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(countryType));
            countries.Add(country);
            SetField(government, "paises", countries);
            governmentType.GetMethod("DefinirReservaFinanceiraEstrategica").Invoke(government, new object[] { 2, 20000L });

            object economy = Activator.CreateInstance(economyType);
            MethodInfo applyWeights = economyType.GetMethod("TentarAplicarPesos");
            object[] arguments = { government, brain, profile, 2, false, Activator.CreateInstance(summaryType) };
            Assert.That(applyWeights.Invoke(economy, arguments), Is.EqualTo(true));

            object summary = arguments[5];
            Assert.That(Field(summary, "Saldo"), Is.EqualTo(20000L));
            Assert.That(Field(summary, "SaldoDisponivel"), Is.EqualTo(0L));
            Assert.That(Field(summary, "ReservaFinanceiraBaixa"), Is.EqualTo(true));
            Assert.That(Field(summary, "PodeEscalarParaN2"), Is.EqualTo(false));
            Assert.That(Field(summary, "MotivoProntidao").ToString(), Does.Contain("reserva financeira"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(governmentObject);
            UnityEngine.Object.DestroyImmediate(brainObject);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void IA03SolicitaCreditoFederativoSomenteDeIntegranteDoMesmoBlocoEComCadencia()
    {
        Type economyType = ResolverTipo("Hegemonia.AI.IA03.GestorEconomiaIA03");
        Type summaryType = ResolverTipo("Hegemonia.AI.IA03.GestorEconomiaIA03+Resumo");
        Type governmentType = ResolverTipo("SistemaGovernoMundial");
        Type federationType = ResolverTipo("SistemaFederacoesGlobais");
        Type countryType = ResolverTipo("DadosPaisGoverno");
        PropertyInfo governmentInstance = governmentType.GetProperty("Instancia", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo federationInstance = federationType.GetProperty("Instancia", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setGovernmentInstance = governmentInstance.GetSetMethod(true);
        MethodInfo setFederationInstance = federationInstance.GetSetMethod(true);
        object previousGovernment = governmentInstance.GetValue(null, null);
        object previousFederation = federationInstance.GetValue(null, null);
        GameObject governmentObject = new GameObject("IA03 federation finance government");
        GameObject federationObject = new GameObject("IA03 federation finance service");
        governmentObject.SetActive(false);
        federationObject.SetActive(false);

        try
        {
            Component government = governmentObject.AddComponent(governmentType);
            Component federations = federationObject.AddComponent(federationType);
            IList countries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(countryType));
            object player = Activator.CreateInstance(countryType);
            object borrower = Activator.CreateInstance(countryType);
            object sameBlocLender = Activator.CreateInstance(countryType);
            object otherBlocLender = Activator.CreateInstance(countryType);

            SetField(government, "teamJogador", 1);
            SetField(player, "teamId", 1);
            SetField(player, "federacaoGlobal", "CooperacaoGlobal");
            SetField(player, "saldo", 1000000L);
            SetField(borrower, "teamId", 2);
            SetField(borrower, "federacaoGlobal", "CooperacaoGlobal");
            SetField(borrower, "saldo", 0L);
            SetField(borrower, "rendaPorSegundo", 1000f);
            SetField(borrower, "gastosPorSegundo", 0f);
            SetField(sameBlocLender, "teamId", 3);
            SetField(sameBlocLender, "federacaoGlobal", "CooperacaoGlobal");
            SetField(sameBlocLender, "saldo", 1000000L);
            SetField(otherBlocLender, "teamId", 4);
            SetField(otherBlocLender, "federacaoGlobal", "AliancaDefesa");
            SetField(otherBlocLender, "saldo", 1000000L);

            countries.Add(player);
            countries.Add(borrower);
            countries.Add(sameBlocLender);
            countries.Add(otherBlocLender);
            SetField(government, "paises", countries);
            setGovernmentInstance.Invoke(null, new[] { government });
            setFederationInstance.Invoke(null, new[] { federations });

            object economy = Activator.CreateInstance(economyType);
            object summary = Activator.CreateInstance(summaryType);
            SetField(summary, "DadosDisponiveis", true);
            SetField(summary, "ReservaFinanceiraBaixa", true);
            SetField(summary, "ReservaFinanceiraEstrategica", 5000L);
            SetField(summary, "SaldoDisponivel", 0L);

            MethodInfo requestFinancing = economyType.GetMethod("TentarSolicitarFinanciamento");
            Assert.That(requestFinancing, Is.Not.Null,
                "A IA03 deve reaproveitar o serviço de crédito federativo existente.");
            object[] arguments = { government, 2, 30f, summary, null };
            Assert.That(requestFinancing.Invoke(economy, arguments), Is.EqualTo(true), arguments[4] as string);

            Assert.That(Field(borrower, "saldo"), Is.EqualTo(5500L));
            Assert.That(Field(player, "saldo"), Is.EqualTo(1000000L),
                "A IA03 não pode retirar dinheiro do caixa do jogador sem uma proposta aceita.");
            Assert.That(Field(sameBlocLender, "saldo"), Is.EqualTo(994500L));
            Assert.That(Field(otherBlocLender, "saldo"), Is.EqualTo(1000000L),
                "O crédito não pode mover recursos entre federações diferentes.");
            Assert.That(((IList)Field(borrower, "emprestimos")).Count, Is.EqualTo(1));
            Assert.That(Field(arguments[3], "Saldo"), Is.EqualTo(5500L));

            arguments[2] = 90f;
            Assert.That(requestFinancing.Invoke(economy, arguments), Is.EqualTo(false),
                "A IA03 deve respeitar a cadência da nova tentativa de financiamento.");
            Assert.That(((IList)Field(borrower, "emprestimos")).Count, Is.EqualTo(1),
                "Uma nova tentativa não deve duplicar o empréstimo ativo.");
        }
        finally
        {
            setGovernmentInstance.Invoke(null, new[] { previousGovernment });
            setFederationInstance.Invoke(null, new[] { previousFederation });
            UnityEngine.Object.DestroyImmediate(governmentObject);
            UnityEngine.Object.DestroyImmediate(federationObject);
        }
    }

    [Test]
    public void ReservaEstrategicaDeManpowerBloqueiaMobilizacaoEProntidaoSemConsumirOPiso()
    {
        Type economyType = ResolverTipo("Hegemonia.AI.IA03.GestorEconomiaIA03");
        Type summaryType = ResolverTipo("Hegemonia.AI.IA03.GestorEconomiaIA03+Resumo");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type governmentType = ResolverTipo("SistemaGovernoMundial");
        Type countryType = ResolverTipo("DadosPaisGoverno");
        Type profileType = ResolverTipo("Hegemonia.AI.IA03.PerfilPaisSO");
        GameObject governmentObject = new GameObject("IA03 reserved manpower government");
        governmentObject.SetActive(false);
        GameObject brainObject = new GameObject("IA03 reserved manpower brain");
        brainObject.SetActive(false);
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        try
        {
            Component government = governmentObject.AddComponent(governmentType);
            Component brain = brainObject.AddComponent(brainType);
            object country = Activator.CreateInstance(countryType);
            SetField(country, "teamId", 2);
            SetField(country, "saldo", 50000L);
            SetField(country, "comida", 500);
            SetField(country, "petroleo", 500);
            SetField(country, "energia", 500);
            SetField(country, "populacao", 1000);
            SetField(country, "populacaoCivil", 200);
            SetField(country, "populacaoMilitarAtiva", 700);
            SetField(country, "reservistas", 0);
            SetField(country, "alistaveis", 100);
            SetField(profile, "reservaManpowerMinimaPercentualDaPopulacao", 0.1f);
            MethodInfo calculateReserve = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional")
                .GetMethod("CalcularReservaManpowerEstrategica", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(calculateReserve, Is.Not.Null);
            Assert.That(calculateReserve.Invoke(null, new object[] {
                country, Field(profile, "reservaManpowerMinimaPercentualDaPopulacao")
            }), Is.EqualTo(100L));

            IList countries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(countryType));
            countries.Add(country);
            SetField(government, "paises", countries);
            governmentType.GetMethod("DefinirReservaManpowerEstrategica")
                .Invoke(government, new object[] { 2, 100L });

            MethodInfo availableAboveReserve = governmentType.GetMethod("ObterManpowerDisponivelAlemDaReserva");
            MethodInfo canMobilize = governmentType.GetMethod("PodeMobilizarEfetivo");
            MethodInfo mobilize = governmentType.GetMethod("TentarMobilizarEfetivo");
            Assert.That(availableAboveReserve, Is.Not.Null);
            Assert.That(canMobilize, Is.Not.Null);
            Assert.That(mobilize, Is.Not.Null);
            Assert.That(availableAboveReserve.Invoke(government, new object[] { 2 }), Is.EqualTo(200L));
            Assert.That(canMobilize.Invoke(government, new object[] { 2, 201 }), Is.EqualTo(false));
            Assert.That(mobilize.Invoke(government, new object[] { 2, 200 }), Is.EqualTo(true));
            Assert.That(availableAboveReserve.Invoke(government, new object[] { 2 }), Is.EqualTo(0L));
            Assert.That(canMobilize.Invoke(government, new object[] { 2, 1 }), Is.EqualTo(false));
            Assert.That(Field(country, "populacaoCivil"), Is.EqualTo(100));
            Assert.That(Field(country, "populacaoMilitarAtiva"), Is.EqualTo(900));

            object economy = Activator.CreateInstance(economyType);
            object[] arguments = { government, brain, profile, 2, false, Activator.CreateInstance(summaryType) };
            Assert.That(economyType.GetMethod("TentarAplicarPesos").Invoke(economy, arguments), Is.EqualTo(true));
            object summary = arguments[5];
            Assert.That(Field(summary, "ManpowerDisponivel"), Is.EqualTo(100L));
            Assert.That(Field(summary, "ReservaManpowerEstrategica"), Is.EqualTo(100L));
            Assert.That(Field(summary, "ManpowerDisponivelAlemDaReserva"), Is.EqualTo(0L));
            Assert.That(Field(summary, "PodeEscalarParaN2"), Is.EqualTo(false));
            Assert.That(Field(summary, "ClassificacaoProntidao"), Is.EqualTo("CRÍTICA"));
            Assert.That(Field(summary, "MotivoProntidao").ToString(), Does.Contain("reserva estratégica"));

            governmentType.GetMethod("LiberarReservaManpowerEstrategica")
                .Invoke(government, new object[] { 2 });
            Assert.That(availableAboveReserve.Invoke(government, new object[] { 2 }), Is.EqualTo(100L));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(governmentObject);
            UnityEngine.Object.DestroyImmediate(brainObject);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [TestCase("Assets/Tests/IA03/PerfilPais_Valdoria.asset")]
    [TestCase("Assets/Tests/IA03/PerfilPais_Karsovia.asset")]
    public void PerfisIA03LegadosUsamDefaultDeReservaManpower(string caminhoPerfil)
    {
        Type profileType = ResolverTipo("Hegemonia.AI.IA03.PerfilPaisSO");
        ScriptableObject profile = AssetDatabase.LoadAssetAtPath(caminhoPerfil, profileType) as ScriptableObject;
        Assert.That(profile, Is.Not.Null, "Perfil não carregado: " + caminhoPerfil);

        PropertyInfo reserve = profileType.GetProperty("ReservaManpowerMinimaPercentualDaPopulacao");
        Assert.That(reserve, Is.Not.Null);
        Assert.That((float)reserve.GetValue(profile, null), Is.EqualTo(0.1f).Within(0.0001f));
    }

    [Test]
    public void IA01EIA02NaoReativamAeroportoSemEnergiaAoProduzirCaca()
    {
        Assert.That(TryProduceWithUnpoweredAirport(
            "Hegemonia.AI.IA01.IA01RuntimeContext",
            "Hegemonia.AI.IA01.IA01MilitaryDirector"), Is.True);
        Assert.That(TryProduceWithUnpoweredAirport(
            "Hegemonia.AI.IA02.IA02RuntimeContext",
            "Hegemonia.AI.IA02.IA02MilitaryDirector"), Is.True);
    }

    private static bool TryProduceWithUnpoweredAirport(string contextTypeName, string directorTypeName)
    {
        Type contextType = ResolverTipo(contextTypeName);
        Type directorType = ResolverTipo(directorTypeName);
        Type airportType = ResolverTipo("GerenciadorAeroporto");
        Type identityType = ResolverTipo("IdentidadeUnidade");
        object context = Activator.CreateInstance(contextType);
        object identitySnapshot = contextType.GetMethod("GetIdentitySnapshot").Invoke(context, null);
        SetField(identitySnapshot, "TeamId", 2);
        contextType.GetMethod("ApplyIdentity").Invoke(context, new[] { identitySnapshot });
        object director = Activator.CreateInstance(directorType, new object[] { null, context, null });

        GameObject airportObject = new GameObject(directorType.Name + " unpowered airport test");
        airportObject.SetActive(false);
        try
        {
            Component airport = airportObject.AddComponent(airportType);
            Component identity = airportObject.AddComponent(identityType);
            SetField(identity, "teamID", 2);
            airportObject.SetActive(true);
            SetField(airport, "semEnergia", true);

            MethodInfo produceFighter = directorType.GetMethod(
                "TryProduceFighterSemPagamento",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(produceFighter, Is.Not.Null);
            produceFighter.Invoke(director, new object[] { null, 0f, string.Empty });

            return (bool)Field(airport, "semEnergia");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(airportObject);
        }
    }

    [Test]
    public void RelatorioIgnoraRedeliveryDoMesmoEventoDeDestruicaoInclusiveOCusto()
    {
        Type reportType = ResolverTipo("Hegemonia.AI.IA03.IA03RelatorioConflito");
        Type eventType = ResolverTipo("CartaCombateRegistro+EventoCombate");
        object report = Activator.CreateInstance(reportType);
        object combatEvent = Activator.CreateInstance(eventType);
        SetField(combatEvent, "id", "combate-relatorio-redelivery-1");
        SetField(combatEvent, "tipo", "UNIDADE DESTRUÍDA");
        SetField(combatEvent, "equipeAtacante", 1);
        SetField(combatEvent, "equipeAlvo", 3);
        SetField(combatEvent, "alvoEhEstrutura", false);
        SetField(combatEvent, "custoReposicaoConhecido", true);
        SetField(combatEvent, "custoReposicaoEstimado", 1200L);

        MethodInfo registerEvent = reportType.GetMethod("RegistrarEventoCombate");
        MethodInfo registerCost = reportType.GetMethod("RegistrarPrejuizoEconomico");
        bool firstDelivery = (bool)registerEvent.Invoke(report, new[] { combatEvent, (object)1, 3 });
        if (firstDelivery)
        {
            registerCost.Invoke(report, new object[] { true, 1200f });
        }
        bool redelivery = (bool)registerEvent.Invoke(report, new[] { combatEvent, (object)1, 3 });
        if (redelivery)
        {
            registerCost.Invoke(report, new object[] { true, 1200f });
        }

        object snapshot = reportType.GetProperty("Acumulado").GetValue(report, null);
        Assert.That(firstDelivery, Is.True);
        Assert.That(redelivery, Is.False);
        Assert.That(Field(snapshot, "InimigosDestruidos"), Is.EqualTo(1));
        Assert.That(Field(snapshot, "PrejuizoEconomicoInimigo"), Is.EqualTo(1200f));
    }

    [Test]
    public void RelatorioDeCincoMinutosPreservaAcumuladoresETransportaDuracao()
    {
        Type reportType = ResolverTipo("Hegemonia.AI.IA03.IA03RelatorioConflito");
        object report = Activator.CreateInstance(reportType);
        Type eventType = ResolverTipo("CartaCombateRegistro+EventoCombate");
        object combatEvent = Activator.CreateInstance(eventType);
        SetField(combatEvent, "id", "combate-relatorio-intervalo-1");
        SetField(combatEvent, "tipo", "UNIDADE DESTRUÍDA");
        SetField(combatEvent, "equipeAtacante", 1);
        SetField(combatEvent, "equipeAlvo", 3);
        SetField(combatEvent, "alvoEhEstrutura", false);

        MethodInfo registerEvent = reportType.GetMethod("RegistrarEventoCombate");
        MethodInfo registerCost = reportType.GetMethod("RegistrarPrejuizoEconomico");
        MethodInfo registerBattle = reportType.GetMethod("RegistrarResultadoCombate");
        MethodInfo createReport = reportType.GetMethod("CriarRelatorio");
        Assert.That((bool)registerEvent.Invoke(report, new[] { combatEvent, (object)1, 3 }), Is.True);
        registerCost.Invoke(report, new object[] { true, 1200f });
        registerBattle.Invoke(report, new object[] { true });

        object first = createReport.Invoke(report, new object[] { 5, 7, 8, 300f, 300f, null });
        Assert.That(Field(first, "MomentoDoRelatorio"), Is.EqualTo(300f));
        Assert.That(Field(first, "DuracaoConflitoSegundos"), Is.EqualTo(300f));
        Assert.That(Field(first, "UnidadesPropriasDisponiveis"), Is.EqualTo(5));
        Assert.That(Field(first, "UnidadesInimigasConhecidas"), Is.EqualTo(7));
        Assert.That(Field(first, "CapacidadeMilitarRestante"), Is.EqualTo(0.625f).Within(0.001f));

        object second = createReport.Invoke(report, new object[] { 4, 6, 8, 600f, 600f, null });
        Assert.That(Field(second, "InimigosDestruidos"), Is.EqualTo(1), "Emitir novo relatório não deve zerar eventos acumulados.");
        Assert.That(Field(second, "BatalhasVencidas"), Is.EqualTo(1));
        Assert.That(Field(second, "PrejuizoEconomicoInimigo"), Is.EqualTo(1200f));
        Assert.That(Field(second, "MomentoDoRelatorio"), Is.EqualTo(600f));
        Assert.That(Field(second, "DuracaoConflitoSegundos"), Is.EqualTo(600f));

        reportType.GetMethod("Resetar").Invoke(report, null);
        object accumulated = reportType.GetProperty("Acumulado").GetValue(report, null);
        Assert.That(Field(accumulated, "InimigosDestruidos"), Is.EqualTo(0));
        Assert.That(Field(accumulated, "DuracaoConflitoSegundos"), Is.EqualTo(0f));
        Assert.That((bool)registerEvent.Invoke(report, new[] { combatEvent, (object)1, 3 }), Is.True,
            "O início de outro conflito deve limpar também a janela de deduplicação.");
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

    [TestCase(1, 3)]
    [TestCase(3, 1)]
    public void TorreAdquireInimigoSobRaizCompartilhadaSemMirarPropriaUnidadeOuAliado(
        int equipeAtiradora,
        int equipeInimiga)
    {
        GameObject scenario = new GameObject("IA03 grouped target acquisition test");
        Type governmentType = ResolverTipo("SistemaGovernoMundial");
        PropertyInfo governmentInstance = governmentType.GetProperty(
            "Instancia",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        object previousGovernment = governmentInstance.GetValue(null, null);
        GameObject governmentObject = new GameObject("IA03 grouped acquisition diplomacy");
        governmentObject.SetActive(false);
        Component government = governmentObject.AddComponent(governmentType);
        governmentInstance.GetSetMethod(true).Invoke(null, new[] { government });
        scenario.transform.position = new Vector3(1000000f, 0f, 1000000f);
        Component identidadeAtiradora = null;
        Component identidadeAliada = null;
        Component identidadeInimiga = null;
        MethodInfo unregister = null;
        try
        {
            identidadeAtiradora = CriarUnidadeComTorre(
                scenario.transform, "PaisAtacante", "UnidadeAtacante", equipeAtiradora, out Component torre);
            identidadeAliada = CriarUnidadeComTorre(
                scenario.transform, "PaisAliado", "UnidadeAliada", equipeAtiradora, out _);
            identidadeInimiga = CriarUnidadeComTorre(
                scenario.transform, "PaisDefensor", "UnidadeDefensora", equipeInimiga, out _);

            object relation = governmentType.GetMethod("ObterRelacao", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(government, new object[] { equipeAtiradora, equipeInimiga });
            SetField(relation, "guerraDeclarada", true);

            identidadeAtiradora.transform.position = scenario.transform.position;
            identidadeAliada.transform.position = scenario.transform.position + new Vector3(0f, 0f, 6f);
            identidadeInimiga.transform.position = scenario.transform.position + new Vector3(0f, 0f, 20f);
            identidadeAtiradora.gameObject.AddComponent<BoxCollider>();
            identidadeAliada.gameObject.AddComponent<BoxCollider>();
            identidadeInimiga.gameObject.AddComponent<BoxCollider>();

            Type registryType = ResolverTipo("RegistroEntidadesJogo");
            Type identityType = ResolverTipo("IdentidadeUnidade");
            MethodInfo register = registryType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(method => method.Name == "Register"
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == identityType);
            unregister = registryType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(method => method.Name == "Unregister"
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == identityType);
            register.Invoke(null, new object[] { identidadeAtiradora });
            register.Invoke(null, new object[] { identidadeAliada });
            register.Invoke(null, new object[] { identidadeInimiga });

            Type registeredUnitsType = typeof(List<>).MakeGenericType(identityType);
            object registeredUnits = Activator.CreateInstance(registeredUnitsType);
            registryType.GetMethod("FillUnidades", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { registeredUnits });
            bool enemyIsRegistered = (bool)registeredUnitsType.GetMethod("Contains")
                .Invoke(registeredUnits, new object[] { identidadeInimiga });
            Assert.That(enemyIsRegistered, Is.True, "O teste não registrou a unidade inimiga no cache real.");

            SetField(torre, "minhaIdentidade", identidadeAtiradora);
            SetField(torre, "meuTime", equipeAtiradora);
            SetField(torre, "alcance", 100f);
            SetField(torre, "souAntiAereo", false);
            Physics.SyncTransforms();

            MethodInfo procurarNoRegistro = torre.GetType().GetMethod(
                "ProcurarAlvoNoRegistroGlobal", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo procurarNoRadar = torre.GetType().GetMethod(
                "ProcurarAlvo", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(procurarNoRegistro, Is.Not.Null);
            Assert.That(procurarNoRadar, Is.Not.Null);
            MethodInfo podeAtacar = torre.GetType().GetMethod(
                "PodeAtacarAutomaticamente", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(podeAtacar, Is.Not.Null);
            Assert.That(podeAtacar.Invoke(torre, new object[] { identidadeInimiga }), Is.EqualTo(true));
            Assert.That(identidadeInimiga.gameObject.activeInHierarchy, Is.True);
            Assert.That(Vector3.Distance(torre.transform.position, identidadeInimiga.transform.position), Is.LessThan(100f));
            Assert.That((bool)ResolverTipo("ControleSubmarino")
                .GetMethod("PodeSerAlvoConvencional", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { identidadeInimiga.transform }), Is.True);

            Transform alvoGlobal = (Transform)procurarNoRegistro.Invoke(torre, null);
            Assert.That(alvoGlobal, Is.SameAs(identidadeInimiga.transform),
                "A busca pelo registro descartou o inimigo por compartilhar a raiz da cena.");

            procurarNoRadar.Invoke(torre, null);
            Assert.That(Field(torre, "alvoAtual"), Is.SameAs(identidadeInimiga.transform),
                "O radar local descartou o inimigo ou priorizou a própria unidade/um aliado.");
            Assert.That(identidadeAtiradora.transform.root, Is.SameAs(identidadeInimiga.transform.root));
        }
        finally
        {
            if (unregister != null)
            {
                if (identidadeAtiradora != null) unregister.Invoke(null, new[] { identidadeAtiradora });
                if (identidadeAliada != null) unregister.Invoke(null, new[] { identidadeAliada });
                if (identidadeInimiga != null) unregister.Invoke(null, new[] { identidadeInimiga });
            }
            governmentInstance.GetSetMethod(true).Invoke(null, new[] { previousGovernment });
            UnityEngine.Object.DestroyImmediate(governmentObject);
            UnityEngine.Object.DestroyImmediate(scenario);
        }
    }

    [Test]
    public void ProducaoAereaIAFalhaQuandoAeroportoBloqueiaSpawnEmAgua()
    {
        // Mantém o fixture fora do footprint do mapa global para que a
        // classificação explícita do marcador de água seja determinística.
        Vector3 spawn = new Vector3(350000f, 0f, 350000f);
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = "IA03 water spawn blocker";
        water.transform.position = spawn;
        water.transform.localScale = new Vector3(1000f, 10f, 1000f);
        water.AddComponent(ResolverTipo("MarcadorSuperficieMapa"));

        GameObject airportObject = new GameObject("IA03 airport spawn rejection test");
        // A base ativa percorre o mesmo filtro de elegibilidade usado pelo
        // backend; o teste só precisa isolar o bloqueio de água.
        airportObject.SetActive(true);
        airportObject.transform.position = spawn;
        GameObject prefab = new GameObject("IA03 fighter test prefab");
        ScriptableObject item = ScriptableObject.CreateInstance(ResolverTipo("DadosConstrucao"));
        Type governmentType = ResolverTipo("SistemaGovernoMundial");
        Type countryType = ResolverTipo("DadosPaisGoverno");
        PropertyInfo governmentInstance = governmentType.GetProperty(
            "Instancia", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        object previousGovernment = governmentInstance.GetValue(null, null);
        GameObject governmentObject = new GameObject("IA03 water spawn government");
        governmentObject.SetActive(false);
        Component airport = null;
        object backend = null;
        try
        {
            Component government = governmentObject.AddComponent(governmentType);
            governmentInstance.GetSetMethod(true).Invoke(null, new[] { government });
            object country = Activator.CreateInstance(countryType);
            SetField(country, "teamId", 987654);
            SetField(country, "populacao", 1000);
            SetField(country, "populacaoCivil", 1000);
            SetField(country, "alistaveis", 1000);
            SetField(country, "populacaoMilitarAtiva", 0);
            SetField(country, "reservistas", 0);
            IList countries = (IList)governmentType.GetField("paises").GetValue(government);
            countries.Clear();
            countries.Add(country);

            Component identity = airportObject.AddComponent(ResolverTipo("IdentidadeUnidade"));
            SetField(identity, "teamID", 987654);
            airport = airportObject.AddComponent(ResolverTipo("GerenciadorAeroporto"));
            SetField(airport, "semEnergia", false);
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

            LogAssert.Expect(LogType.Warning,
                new Regex("\\[Aeroporto\\] Spawn aéreo bloqueado em água: .* A IA tentará outra base elegível\\."));
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
            governmentInstance.GetSetMethod(true).Invoke(null, new[] { previousGovernment });
            UnityEngine.Object.DestroyImmediate(governmentObject);
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
            MethodInfo trySpawn = airport.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public)
                .First(method => method.Name == "TryComprarAviaoIAImediato"
                    && method.GetParameters().Length == 2);
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

            object[] arguments = { item, null, "ia03-unpowered-heliport", null };
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
    public void MetasMilitaresDoPerfilPreservamMetasAdaptativasMaisAltas()
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
            brainType.GetField("TargetFleet").SetValue(brain, 0);
            brainType.GetField("TargetAircraft").SetValue(brain, 0);

            MethodInfo applyTargets = strategistType.GetMethod("AplicarMetasMilitaresDoPerfil");
            Assert.That(applyTargets, Is.Not.Null);

            applyTargets.Invoke(strategist, null);
            Assert.That(brainType.GetField("TargetFleet").GetValue(brain), Is.EqualTo(2));
            Assert.That(brainType.GetField("TargetAircraft").GetValue(brain), Is.EqualTo(4));

            brainType.GetField("TargetFleet").SetValue(brain, 32);
            brainType.GetField("TargetAircraft").SetValue(brain, 32);
            applyTargets.Invoke(strategist, null);
            Assert.That(brainType.GetField("TargetFleet").GetValue(brain), Is.EqualTo(32));
            Assert.That(brainType.GetField("TargetAircraft").GetValue(brain), Is.EqualTo(32));
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

    [TestCase("GrupoPortaAvioes", "AvancoMilitar", "ZonaPortaAvioes", "exigePortaAvioes")]
    [TestCase("GrupoPortaAvioes", "ConflitoLimitado", "ZonaPortaAvioes", "exigePortaAvioes")]
    [TestCase("GrupoPortaAvioes", "GuerraTotal", "ZonaPortaAvioes", "exigePortaAvioes")]
    [TestCase("InvasaoAnfibia", "ConflitoLimitado", "EmbarqueTransporte", "exigeTransporteNaval")]
    [TestCase("InvasaoAnfibia", "GuerraTotal", "DesembarqueAnfibio", "exigeTransporteNaval")]
    [TestCase("VisitaPresidencial", "Tensao", "HelipontoDiplomatico", "exigePresidente")]
    [TestCase("VisitaPresidencial", "Tensao", "RecepcaoPresidencial", "exigePresidente")]
    public void MissaoEspecialAceitaNivelCreatyEPapelCompativeis(
        string nomeMissao,
        string nomeNivel,
        string nomeCreaty,
        string campoPapel)
    {
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type conflictType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        Type missionKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoCreaty");
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);

        try
        {
            SetField(mission, "tipoMissao", Enum.Parse(missionKindType, nomeMissao));
            SetField(mission, campoPapel, true);

            MethodInfo aceita = missionType.GetMethod("Aceita");
            Assert.That(aceita, Is.Not.Null);
            Assert.That(
                aceita.Invoke(mission, new[]
                {
                    Enum.Parse(conflictType, nomeNivel),
                    Enum.Parse(creatyType, nomeCreaty)
                }),
                Is.EqualTo(true));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [TestCase("GrupoPortaAvioes", "Tensao", "ZonaPortaAvioes", "exigePortaAvioes")]
    [TestCase("GrupoPortaAvioes", "GuerraTotal", "GuerraN1", "exigePortaAvioes")]
    [TestCase("InvasaoAnfibia", "AvancoMilitar", "DesembarqueAnfibio", "exigeTransporteNaval")]
    [TestCase("InvasaoAnfibia", "GuerraTotal", "GuerraN1", "exigeTransporteNaval")]
    [TestCase("VisitaPresidencial", "ConflitoLimitado", "RecepcaoPresidencial", "exigePresidente")]
    [TestCase("VisitaPresidencial", "Tensao", "PontoGenerico", "exigePresidente")]
    public void MissaoEspecialRejeitaNivelCreatyOuPapelIncompativel(
        string nomeMissao,
        string nomeNivel,
        string nomeCreaty,
        string campoPapel)
    {
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type conflictType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        Type missionKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoCreaty");
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);

        try
        {
            SetField(mission, "tipoMissao", Enum.Parse(missionKindType, nomeMissao));
            SetField(mission, campoPapel, true);

            MethodInfo aceita = missionType.GetMethod("Aceita");
            Assert.That(aceita, Is.Not.Null);
            Assert.That(
                aceita.Invoke(mission, new[]
                {
                    Enum.Parse(conflictType, nomeNivel),
                    Enum.Parse(creatyType, nomeCreaty)
                }),
                Is.EqualTo(false));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [TestCase("GrupoPortaAvioes", "ZonaPortaAvioes")]
    [TestCase("InvasaoAnfibia", "DesembarqueAnfibio")]
    [TestCase("VisitaPresidencial", "RecepcaoPresidencial")]
    public void MissaoEspecialRejeitaConfiguracaoSemPapelObrigatorio(
        string nomeMissao,
        string nomeCreaty)
    {
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type missionKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoCreaty");
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);

        try
        {
            SetField(mission, "tipoMissao", Enum.Parse(missionKindType, nomeMissao));

            MethodInfo aceita = missionType.GetMethod("Aceita");
            Assert.That(aceita, Is.Not.Null);
            Assert.That(
                aceita.Invoke(mission, new[]
                {
                    Enum.Parse(ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito"),
                        nomeMissao == "GrupoPortaAvioes" ? "AvancoMilitar"
                        : nomeMissao == "InvasaoAnfibia" ? "ConflitoLimitado"
                        : "Tensao"),
                    Enum.Parse(creatyType, nomeCreaty)
                }),
                Is.EqualTo(false));
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
    public void ReservasEstrategicasFicamProntasAntesDoPrimeiroTickDaIA03()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type governmentType = ResolverTipo("SistemaGovernoMundial");
        Type countryType = ResolverTipo("DadosPaisGoverno");
        Type profileType = ResolverTipo("Hegemonia.AI.IA03.PerfilPaisSO");
        GameObject strategistObject = new GameObject("IA03 pre-scheduler reserve bootstrap test");
        GameObject governmentObject = new GameObject("IA03 pre-scheduler reserve government");
        strategistObject.SetActive(false);
        governmentObject.SetActive(false);
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setInstance = instanceProperty.GetSetMethod(true);
        object previousGovernment = instanceProperty.GetValue(null);
        const int teamId = 9821;

        try
        {
            Component government = governmentObject.AddComponent(governmentType);
            Component brain = strategistObject.AddComponent(brainType);
            Component strategist = strategistObject.AddComponent(strategistType);
            SetField(brain, "TeamId", teamId);
            SetField(strategist, "brain", brain);
            SetField(strategist, "perfilPais", profile);
            SetField(profile, "reservaFinanceiraDesejada", 12345f);
            SetField(profile, "reservaManpowerMinimaPercentualDaPopulacao", 0.2f);

            object country = Activator.CreateInstance(countryType);
            SetField(country, "teamId", teamId);
            SetField(country, "populacao", 1000);
            SetField(country, "populacaoCivil", 800);
            SetField(country, "populacaoMilitarAtiva", 200);
            SetField(country, "reservistas", 0);
            SetField(country, "alistaveis", 0);
            IList countries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(countryType));
            countries.Add(country);
            SetField(government, "paises", countries);
            setInstance.Invoke(null, new[] { government });

            MethodInfo guaranteeReserves = strategistType.GetMethod("GarantirReservasEstrategicas");
            Assert.That(guaranteeReserves, Is.Not.Null);
            Assert.That(guaranteeReserves.Invoke(strategist, null), Is.EqualTo(true));
            Assert.That(governmentType.GetMethod("ObterReservaFinanceiraEstrategica")
                .Invoke(government, new object[] { teamId }), Is.EqualTo(12345L));
            Assert.That(governmentType.GetMethod("ObterReservaManpowerEstrategica")
                .Invoke(government, new object[] { teamId }), Is.EqualTo(200L));

            Assert.That(guaranteeReserves.Invoke(strategist, null), Is.EqualTo(true),
                "A preparação chamada antes do scheduler deve ser idempotente por instância do governo.");
        }
        finally
        {
            setInstance.Invoke(null, new[] { previousGovernment });
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(governmentObject);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void ReservaManpowerEstrategicaAcompanhaPopulacaoSemAlterarReservaFinanceira()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type governmentType = ResolverTipo("SistemaGovernoMundial");
        Type countryType = ResolverTipo("DadosPaisGoverno");
        Type profileType = ResolverTipo("Hegemonia.AI.IA03.PerfilPaisSO");
        GameObject owner = new GameObject("IA03 dynamic manpower reserve test");
        GameObject governmentObject = new GameObject("IA03 dynamic manpower government");
        owner.SetActive(false);
        governmentObject.SetActive(false);
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setInstance = instanceProperty.GetSetMethod(true);
        object previousGovernment = instanceProperty.GetValue(null);
        const int teamId = 9822;

        try
        {
            Component government = governmentObject.AddComponent(governmentType);
            Component brain = owner.AddComponent(brainType);
            Component strategist = owner.AddComponent(strategistType);
            SetField(brain, "TeamId", teamId);
            SetField(strategist, "brain", brain);
            SetField(strategist, "perfilPais", profile);
            SetField(profile, "reservaFinanceiraDesejada", 12345f);
            SetField(profile, "reservaManpowerMinimaPercentualDaPopulacao", 0.2f);

            object country = Activator.CreateInstance(countryType);
            SetField(country, "teamId", teamId);
            SetField(country, "populacao", 1000);
            SetField(country, "populacaoCivil", 800);
            SetField(country, "populacaoMilitarAtiva", 200);
            IList countries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(countryType));
            countries.Add(country);
            SetField(government, "paises", countries);
            setInstance.Invoke(null, new[] { government });

            MethodInfo guarantee = strategistType.GetMethod("GarantirReservasEstrategicas");
            Assert.That(guarantee, Is.Not.Null);
            Assert.That(guarantee.Invoke(strategist, null), Is.EqualTo(true));
            Assert.That(governmentType.GetMethod("ObterReservaManpowerEstrategica")
                .Invoke(government, new object[] { teamId }), Is.EqualTo(200L));
            Assert.That(governmentType.GetMethod("ObterReservaFinanceiraEstrategica")
                .Invoke(government, new object[] { teamId }), Is.EqualTo(12345L));

            SetField(country, "populacaoCivil", 799);
            Assert.That(guarantee.Invoke(strategist, null), Is.EqualTo(true));
            Assert.That(governmentType.GetMethod("ObterReservaManpowerEstrategica")
                .Invoke(government, new object[] { teamId }), Is.EqualTo(200L),
                "Alterações demográficas que não cruzam o próximo inteiro não devem alterar a reserva arredondada.");

            SetField(country, "populacao", 1500);
            SetField(country, "populacaoCivil", 1300);
            Assert.That(guarantee.Invoke(strategist, null), Is.EqualTo(true));
            Assert.That(governmentType.GetMethod("ObterReservaManpowerEstrategica")
                .Invoke(government, new object[] { teamId }), Is.EqualTo(300L),
                "A garantia recorrente deve atualizar a reserva conforme a população nacional.");
            Assert.That(governmentType.GetMethod("ObterReservaFinanceiraEstrategica")
                .Invoke(government, new object[] { teamId }), Is.EqualTo(12345L),
                "A reconciliação demográfica não deve alterar a reserva monetária.");

            Assert.That(governmentType.GetMethod("TentarMobilizarEfetivo")
                .Invoke(government, new object[] { teamId, 100 }), Is.EqualTo(true),
                "A mobilização deve usar o piso manpower vigente antes da atualização seguinte.");
            Assert.That(governmentType.GetMethod("ObterReservaManpowerEstrategica")
                .Invoke(government, new object[] { teamId }), Is.EqualTo(300L),
                "Mobilizar efetivo não pode consumir a reserva protegida.");

            SetField(country, "populacao", 500);
            SetField(country, "populacaoCivil", 200);
            SetField(country, "populacaoMilitarAtiva", 200);
            SetField(country, "reservistas", 0);
            SetField(country, "alistaveis", 100);
            Assert.That(guarantee.Invoke(strategist, null), Is.EqualTo(true));
            Assert.That(governmentType.GetMethod("ObterReservaManpowerEstrategica")
                .Invoke(government, new object[] { teamId }), Is.EqualTo(100L),
                "A reserva deve também acompanhar a redução da população total.");
        }
        finally
        {
            setInstance.Invoke(null, new[] { previousGovernment });
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(governmentObject);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void DesativarIA03LiberaReservasNacionaisEReposicaoPendente()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type governmentType = ResolverTipo("SistemaGovernoMundial");
        Type registryType = ResolverTipo("Hegemonia.AI.Shared.IAAutoProductionRegistry");
        Type commandQueueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type pendingType = strategistType.GetNestedType("ReposicaoPendente", BindingFlags.NonPublic);
        Assert.That(pendingType, Is.Not.Null);

        GameObject strategistObject = new GameObject("IA03 inactive resource cleanup test");
        GameObject governmentObject = new GameObject("IA03 inactive resource cleanup government");
        strategistObject.SetActive(false);
        governmentObject.SetActive(false);

        Component strategist = null;
        Component government = null;
        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setInstance = instanceProperty.GetSetMethod(true);
        object previousGovernment = instanceProperty.GetValue(null);
        string productionOrderId = string.Empty;
        const int teamId = 9817;
        const string unitType = "ia03-disabled-cleanup-test-unit";
        try
        {
            strategist = strategistObject.AddComponent(strategistType);
            Component brain = strategistObject.AddComponent(brainType);
            SetField(brain, "TeamId", teamId);
            SetField(strategist, "brain", brain);

            object commandQueue = Activator.CreateInstance(commandQueueType);
            SetField(brain, "_commandQueue", commandQueue);
            object context = Activator.CreateInstance(ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context"));
            SetField(context, "CommandQueue", commandQueue);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });

            government = governmentObject.AddComponent(governmentType);
            setInstance.Invoke(null, new[] { government });
            governmentType.GetMethod("DefinirReservaFinanceiraEstrategica")
                .Invoke(government, new object[] { teamId, 5000L });
            governmentType.GetMethod("DefinirReservaManpowerEstrategica")
                .Invoke(government, new object[] { teamId, 250L });

            object[] reserveArguments = { teamId, unitType, "ia03-disabled-cleanup", 1, 0, null, 10f, 180f };
            Assert.That(registryType.GetMethod("TryReserveReplacement").Invoke(null, reserveArguments), Is.EqualTo(true));
            productionOrderId = (string)reserveArguments[5];
            object commandRequest = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandFactory")
                .GetMethod("Create")
                .Invoke(null, new object[]
                {
                    Enum.Parse(ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandType"), "Produce"),
                    "IA03EstrategaNacional",
                    "estrategico",
                    "teste de cancelamento de reposição",
                    500,
                    "production",
                    unitType,
                    0.35f,
                    null,
                    productionOrderId
                });
            string enqueueReason = string.Empty;
            Assert.That(commandQueueType.GetMethod("Enqueue").Invoke(commandQueue,
                new object[] { commandRequest, 10f, enqueueReason }), Is.EqualTo(true));

            object pending = Activator.CreateInstance(pendingType, true);
            SetField(pending, "itemKey", unitType);
            SetField(pending, "quantidade", 1);
            SetField(pending, "productionOrderId", productionOrderId);
            ((IList)Field(strategist, "reposicoesPendentes")).Add(pending);

            SetField(strategist, "ativo", false);
            // These reservations are intentionally seeded as owned by this
            // IA03 instance; cleanup must not release reservations belonging
            // to another producer.
            SetField(strategist, "governoReservasEstrategicas", government);
            SetField(strategist, "governoReservasEstrategicasTeamId", teamId);
            strategistType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(strategist, new object[] { 2f, 0.02f });

            Assert.That(governmentType.GetMethod("ObterReservaFinanceiraEstrategica")
                .Invoke(government, new object[] { teamId }), Is.EqualTo(0L));
            Assert.That(governmentType.GetMethod("ObterReservaManpowerEstrategica")
                .Invoke(government, new object[] { teamId }), Is.EqualTo(0L));
            Assert.That(((IList)Field(strategist, "reposicoesPendentes")).Count, Is.EqualTo(0));

            object[] stateArguments = { productionOrderId, null };
            Assert.That(registryType.GetMethod("TryGetOrderState").Invoke(null, stateArguments), Is.EqualTo(true));
            Assert.That(stateArguments[1].ToString(), Is.EqualTo("Released"));
            Assert.That(commandQueueType.GetMethod("GetStatus").Invoke(commandQueue,
                new object[] { productionOrderId }).ToString(), Is.EqualTo("Cancelled"));

            object[] secondReserveArguments =
                { teamId, unitType, "ia03-disabled-cleanup-owned", 1, 0, null, 10f, 180f };
            Assert.That(registryType.GetMethod("TryReserveReplacement").Invoke(null, secondReserveArguments), Is.EqualTo(true));
            string producerOwnedOrderId = (string)secondReserveArguments[5];
            Assert.That(registryType.GetMethod("ConfirmQueued").Invoke(null,
                new object[] { producerOwnedOrderId, 77, 10f }), Is.EqualTo(true));
            Assert.That(registryType.GetMethod("IsOwnedByProducer").Invoke(null,
                new object[] { producerOwnedOrderId }), Is.EqualTo(true));
            object producerOwnedRequest = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandFactory")
                .GetMethod("Create")
                .Invoke(null, new object[]
                {
                    Enum.Parse(ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandType"), "Produce"),
                    "IA03EstrategaNacional",
                    "estrategico",
                    "teste de fila interna do produtor",
                    500,
                    "production",
                    unitType,
                    0.35f,
                    null,
                    producerOwnedOrderId
                });
            Assert.That(commandQueueType.GetMethod("Enqueue").Invoke(commandQueue,
                new object[] { producerOwnedRequest, 10f, string.Empty }), Is.EqualTo(true));
            object queuedBeforeDisable = commandQueueType.GetMethod("GetStatus").Invoke(commandQueue,
                new object[] { producerOwnedOrderId });
            Assert.That(queuedBeforeDisable.ToString(), Is.EqualTo("Queued"));

            object producerPending = Activator.CreateInstance(pendingType, true);
            SetField(producerPending, "itemKey", unitType);
            SetField(producerPending, "quantidade", 0);
            SetField(producerPending, "productionOrderId", producerOwnedOrderId);
            ((IList)Field(strategist, "reposicoesPendentes")).Add(producerPending);
            strategistType.GetMethod("CancelarReposicoesPendentes", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(strategist, null);

            object[] producerStateArguments = { producerOwnedOrderId, null };
            Assert.That(registryType.GetMethod("TryGetOrderState").Invoke(null, producerStateArguments), Is.EqualTo(true));
            Assert.That(producerStateArguments[1].ToString(), Is.EqualTo("Queued"));
            Assert.That(commandQueueType.GetMethod("GetStatus").Invoke(commandQueue,
                new object[] { producerOwnedOrderId }).ToString(), Is.EqualTo("Queued"));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(productionOrderId))
            {
                registryType.GetMethod("Release").Invoke(null, new object[] { productionOrderId, 11f });
            }
            setInstance.Invoke(null, new[] { previousGovernment });
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(governmentObject);
        }
    }

    [Test]
    public void ReposicoesAtivasDoRegistroSaoReanexadasSemCriarQuantidadeDuplicada()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type schedulerType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_PerformanceScheduler");
        Type registryType = ResolverTipo("Hegemonia.AI.Shared.IAAutoProductionRegistry");
        const int teamId = 9821;
        const string itemKey = "save-recovery-carrier";
        const string purpose = "ia03-replacement:" + itemKey;
        GameObject owner = new GameObject("IA03 replacement save recovery test");
        owner.SetActive(false);

        try
        {
            Component brain = owner.AddComponent(brainType);
            Component strategist = owner.AddComponent(strategistType);
            SetField(brain, "TeamId", teamId);
            SetField(strategist, "brain", brain);

            strategistType.GetMethod("NotificarRestauracaoProducaoAutomatica", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            Assert.That(Field(strategist, "restauracaoProducaoPendente"), Is.EqualTo(true));

            object context = Activator.CreateInstance(contextType);
            object scheduler = Activator.CreateInstance(schedulerType);
            SetField(context, "Scheduler", scheduler);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });
            schedulerType.GetMethod("Register").Invoke(scheduler, new object[] { strategist, 0f, 30f });

            // A primeira tentativa pode ocorrer antes de o save restaurar o
            // registro de produção; ela não deve fixar o estado como concluído.
            strategistType.GetMethod("NotificarRestauracaoProducaoAutomatica", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            Assert.That(((IList)Field(strategist, "reposicoesPendentes")).Count, Is.EqualTo(0));
            IList snapshots = (IList)schedulerType.GetMethod("GetSnapshot").Invoke(scheduler, null);
            Assert.That(Field(snapshots[0], "NextTick"), Is.EqualTo(30f));

            object[] reserveArguments = { teamId, "naval", purpose, 1, 0, null, 1f, 180f };
            Assert.That(registryType.GetMethod("TryReserveReplacement").Invoke(null, reserveArguments), Is.EqualTo(true));
            string orderId = (string)reserveArguments[5];
            Assert.That((bool)registryType.GetMethod("ConfirmQueued").Invoke(null, new object[] { orderId, 0, 2f }), Is.True);

            strategistType.GetMethod("NotificarRestauracaoProducaoAutomatica", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            snapshots = (IList)schedulerType.GetMethod("GetSnapshot").Invoke(scheduler, null);
            Assert.That((float)Field(snapshots[0], "NextTick"), Is.LessThan(30f));
            // Segunda chamada comprova que a reconstrução é idempotente.
            strategistType.GetMethod("NotificarRestauracaoProducaoAutomatica", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);

            IList pending = (IList)Field(strategist, "reposicoesPendentes");
            Assert.That(pending.Count, Is.EqualTo(1));
            Assert.That(Field(pending[0], "itemKey"), Is.EqualTo(itemKey));
            Assert.That(Field(pending[0], "productionOrderId"), Is.EqualTo(orderId));
            Assert.That(Field(pending[0], "quantidade"), Is.EqualTo(0));
        }
        finally
        {
            registryType.GetMethod("Clear").Invoke(null, null);
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void FalhaDeManpowerNoSpawnNavalMantemOrdemAtivaAteEntrega()
    {
        Type registryType = ResolverTipo("Hegemonia.AI.Shared.IAAutoProductionRegistry");
        Type stateType = ResolverTipo("Hegemonia.AI.Shared.IAProductionOrderState");
        Type shipyardType = ResolverTipo("Estaleiro");
        Type slotType = shipyardType.GetNestedType("SlotConstrucao", BindingFlags.Public | BindingFlags.NonPublic);
        Type governmentType = ResolverTipo("SistemaGovernoMundial");
        Type identityType = ResolverTipo("IdentidadeUnidade");
        Assert.That(slotType, Is.Not.Null);

        const int teamId = 1;
        GameObject governmentObject = new GameObject("Naval delivery manpower regression government");
        GameObject shipyardObject = new GameObject("Naval delivery manpower regression shipyard");
        GameObject prefab = new GameObject("Naval delivery manpower regression prefab");
        shipyardObject.SetActive(false);
        prefab.SetActive(false);
        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setInstance = instanceProperty.GetSetMethod(true);
        object previousGovernment = instanceProperty.GetValue(null);
        string orderId = string.Empty;

        try
        {
            Component government = governmentObject.AddComponent(governmentType);
            Type countryType = ResolverTipo("DadosPaisGoverno");
            IList countries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(countryType));
            object country = Activator.CreateInstance(countryType);
            SetField(country, "teamId", teamId);
            countries.Add(country);
            SetField(government, "paises", countries);
            setInstance.Invoke(null, new[] { government });
            SetField(country, "populacao", 1);
            SetField(country, "populacaoCivil", 1);
            SetField(country, "populacaoMaxima", 1);
            SetField(country, "alistaveis", 0);

            Component prefabIdentity = prefab.AddComponent(identityType);
            SetField(prefabIdentity, "tipoUnidade", Enum.Parse(ResolverTipo("TipoUnidade"), "Naval"));
            SetField(prefabIdentity, "militaresConsumidos", 1000);

            object[] reserveArguments = { teamId, "naval", "ia03-replacement:manpower-test", 1, 0, null, 1f, 180f };
            Assert.That(registryType.GetMethod("TryReserveReplacement").Invoke(null, reserveArguments), Is.EqualTo(true));
            orderId = (string)reserveArguments[5];
            Assert.That((bool)registryType.GetMethod("ConfirmConstructionStarted")
                .Invoke(null, new object[] { orderId, shipyardObject.GetInstanceID(), 2f }), Is.True);

            Component shipyard = shipyardObject.AddComponent(shipyardType);
            object slot = Activator.CreateInstance(slotType);
            SetField(slot, "estaOcupado", true);
            SetField(slot, "prefabAtual", prefab);
            SetField(slot, "progresso", 100f);
            SetField(slot, "productionOrderId", orderId);
            Array slots = Array.CreateInstance(slotType, 1);
            slots.SetValue(slot, 0);
            SetField(shipyard, "slots", slots);

            shipyardType.GetMethod("FinalizarConstrucao", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(shipyard, new[] { slot });

            object[] stateArguments = { orderId, null };
            Assert.That((bool)registryType.GetMethod("TryGetOrderState").Invoke(null, stateArguments), Is.True);
            Assert.That(stateArguments[1], Is.EqualTo(Enum.Parse(stateType, "Constructing")));
            Assert.That(Field(slot, "estaOcupado"), Is.EqualTo(true));
            Assert.That(Field(slot, "progresso"), Is.GreaterThanOrEqualTo(100f));

            SetField(country, "populacao", 5000);
            SetField(country, "populacaoCivil", 5000);
            SetField(country, "populacaoMaxima", 5000);
            shipyardType.GetMethod("FinalizarConstrucao", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(shipyard, new[] { slot });

            Assert.That((bool)registryType.GetMethod("TryGetOrderState").Invoke(null, stateArguments), Is.True);
            Assert.That(stateArguments[1], Is.EqualTo(Enum.Parse(stateType, "Delivered")));
            Assert.That(Field(slot, "estaOcupado"), Is.EqualTo(false));
        }
        finally
        {
            registryType.GetMethod("Clear").Invoke(null, null);
            if (!string.IsNullOrWhiteSpace(orderId))
            {
                registryType.GetMethod("Release").Invoke(null, new object[] { orderId, 3f });
            }
            setInstance.Invoke(null, new[] { previousGovernment });
            UnityEngine.Object.DestroyImmediate(shipyardObject);
            UnityEngine.Object.DestroyImmediate(prefab);
            UnityEngine.Object.DestroyImmediate(governmentObject);
        }
    }

    [Test]
    public void TrocaDeGovernoEEquipeLiberaReservaNaInstanciaEChaveOriginais()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type governmentType = ResolverTipo("SistemaGovernoMundial");
        Type countryType = ResolverTipo("DadosPaisGoverno");
        Type profileType = ResolverTipo("Hegemonia.AI.IA03.PerfilPaisSO");
        GameObject owner = new GameObject("IA03 reserve owner change cleanup test");
        GameObject oldGovernmentObject = new GameObject("IA03 old reserve government");
        GameObject newGovernmentObject = new GameObject("IA03 replacement reserve government");
        owner.SetActive(false);
        oldGovernmentObject.SetActive(false);
        newGovernmentObject.SetActive(false);
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        PropertyInfo instanceProperty = governmentType.GetProperty("Instancia", BindingFlags.Public | BindingFlags.Static);
        MethodInfo setInstance = instanceProperty.GetSetMethod(true);
        object previousGovernment = instanceProperty.GetValue(null);
        const int originalTeamId = 9831;
        const int replacementTeamId = 9832;
        Component oldGovernment = null;
        Component newGovernment = null;
        Component strategist = null;

        try
        {
            oldGovernment = oldGovernmentObject.AddComponent(governmentType);
            newGovernment = newGovernmentObject.AddComponent(governmentType);
            Component brain = owner.AddComponent(brainType);
            strategist = owner.AddComponent(strategistType);
            SetField(brain, "TeamId", originalTeamId);
            SetField(strategist, "brain", brain);
            SetField(strategist, "perfilPais", profile);
            SetField(profile, "reservaFinanceiraDesejada", 4000f);
            SetField(profile, "reservaManpowerMinimaPercentualDaPopulacao", 0.2f);

            object oldCountry = Activator.CreateInstance(countryType);
            SetField(oldCountry, "teamId", originalTeamId);
            SetField(oldCountry, "populacao", 1000);
            SetField(oldCountry, "populacaoCivil", 800);
            IList oldCountries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(countryType));
            oldCountries.Add(oldCountry);
            SetField(oldGovernment, "paises", oldCountries);

            object newCountry = Activator.CreateInstance(countryType);
            SetField(newCountry, "teamId", originalTeamId);
            SetField(newCountry, "populacao", 2000);
            SetField(newCountry, "populacaoCivil", 1600);
            IList newCountries = (IList)governmentType.GetField("paises").GetValue(newGovernment);
            newCountries.Clear();
            newCountries.Add(newCountry);
            SetField(newGovernment, "paises", newCountries);

            setInstance.Invoke(null, new[] { oldGovernment });
            MethodInfo guarantee = strategistType.GetMethod("GarantirReservasEstrategicas");
            Assert.That(guarantee.Invoke(strategist, null), Is.EqualTo(true));
            Assert.That(governmentType.GetMethod("ObterReservaFinanceiraEstrategica")
                .Invoke(oldGovernment, new object[] { originalTeamId }), Is.EqualTo(4000L));
            Assert.That(governmentType.GetMethod("ObterReservaManpowerEstrategica")
                .Invoke(oldGovernment, new object[] { originalTeamId }), Is.EqualTo(200L));

            setInstance.Invoke(null, new[] { newGovernment });
            Assert.That(guarantee.Invoke(strategist, null), Is.EqualTo(true));

            Assert.That(governmentType.GetMethod("ObterReservaFinanceiraEstrategica")
                .Invoke(oldGovernment, new object[] { originalTeamId }), Is.EqualTo(0L),
                "A troca do singleton deve liberar a reserva na instância antiga.");
            Assert.That(governmentType.GetMethod("ObterReservaManpowerEstrategica")
                .Invoke(oldGovernment, new object[] { originalTeamId }), Is.EqualTo(0L),
                "A troca do singleton deve liberar o manpower na instância antiga.");
            Assert.That(governmentType.GetMethod("ObterReservaFinanceiraEstrategica")
                .Invoke(newGovernment, new object[] { originalTeamId }), Is.EqualTo(4000L),
                "Ao assumir a nova instância, a IA03 deve reconciliar sua reserva financeira configurada.");
            Assert.That(governmentType.GetMethod("ObterReservaManpowerEstrategica")
                .Invoke(newGovernment, new object[] { originalTeamId }), Is.EqualTo(400L));

            object replacementCountry = Activator.CreateInstance(countryType);
            SetField(replacementCountry, "teamId", replacementTeamId);
            SetField(replacementCountry, "populacao", 500);
            SetField(replacementCountry, "populacaoCivil", 400);
            newCountries.Add(replacementCountry);
            SetField(brain, "TeamId", replacementTeamId);
            Assert.That(guarantee.Invoke(strategist, null), Is.EqualTo(true));
            Assert.That(governmentType.GetMethod("ObterReservaFinanceiraEstrategica")
                .Invoke(newGovernment, new object[] { originalTeamId }), Is.EqualTo(0L),
                "Alterar TeamId deve liberar a chave antiga antes de registrar a nova.");
            Assert.That(governmentType.GetMethod("ObterReservaFinanceiraEstrategica")
                .Invoke(newGovernment, new object[] { replacementTeamId }), Is.EqualTo(4000L));
            Assert.That(governmentType.GetMethod("ObterReservaManpowerEstrategica")
                .Invoke(newGovernment, new object[] { replacementTeamId }), Is.EqualTo(100L));

            SetField(brain, "TeamId", originalTeamId);
            strategistType.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(strategist, null);
            Assert.That(governmentType.GetMethod("ObterReservaFinanceiraEstrategica")
                .Invoke(newGovernment, new object[] { replacementTeamId }), Is.EqualTo(0L),
                "OnDisable deve limpar a chave possuída, não o TeamId já alterado.");
        }
        finally
        {
            strategistType.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(strategist, null);
            setInstance.Invoke(null, new[] { previousGovernment });
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(oldGovernmentObject);
            UnityEngine.Object.DestroyImmediate(newGovernmentObject);
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

            Finalizar("AtaqueLimitado", "Sucesso", "CapturarTerritorio");
            object report = strategistType.GetProperty("RelatorioAtual").GetValue(strategist);
            Assert.That(Field(report, "BatalhasVencidas"), Is.EqualTo(0), "Conquistar uma região não é, por si só, uma batalha.");
            Assert.That(Field(report, "BatalhasPerdidas"), Is.EqualTo(0));

            Finalizar("AtaqueLimitado", "Sucesso");
            Assert.That(Field(report, "BatalhasVencidas"), Is.EqualTo(0),
                "Uma missão ofensiva sem alvo explícito não pode ser contabilizada como vitória.");

            Finalizar("AtaqueLimitado", "Sucesso", temAlvo: true);
            Assert.That(Field(report, "BatalhasVencidas"), Is.EqualTo(1));
            Assert.That(Field(report, "BatalhasPerdidas"), Is.EqualTo(0));

            Finalizar("GuerraTotal", "Fracasso", grupoPerdeuUnidade: true, temAlvo: true);
            Assert.That(Field(report, "BatalhasVencidas"), Is.EqualTo(1));
            Assert.That(Field(report, "BatalhasPerdidas"), Is.EqualTo(1));

            Finalizar("AtaqueLimitado", "Expirada");
            Finalizar("GuerraTotal", "Cancelada");
            Finalizar("Patrulha", "Sucesso");
            Finalizar("GuerraTotal", "Fracasso", "CapturarTerritorio", grupoPerdeuUnidade: true);
            Assert.That(Field(report, "BatalhasVencidas"), Is.EqualTo(1), "Timeout, cancelamento e patrulha são inconclusivos para batalhas.");
            Assert.That(Field(report, "BatalhasPerdidas"), Is.EqualTo(1));

            void Finalizar(
                string tipo,
                string resultado,
                string condicao = "DestruirAlvo",
                bool grupoPerdeuUnidade = false,
                bool temAlvo = false)
            {
                ScriptableObject activeMission = ScriptableObject.CreateInstance(missionType);
                missions.Add(activeMission);
                SetField(activeMission, "tipoMissao", Enum.Parse(missionKindType, tipo));
                SetField(activeMission, "condicaoDeSucesso", Enum.Parse(conditionType, condicao));
                SetField(strategist, "missaoAtiva", activeMission);
                SetField(strategist, "grupoPerdeuUnidade", grupoPerdeuUnidade);
                SetField(strategist, "alvoMissaoFoiDefinido", temAlvo);
                SetField(strategist, "alvoMissaoDestruido", temAlvo && resultado == "Sucesso");
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
    public void GrupoIA03NaoReservaUnidadeTerrestreSemCombustivelOperacional()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type worldStateType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_WorldState");
        Type identityType = ResolverTipo("IdentidadeUnidade");
        Type fuelType = ResolverTipo("CombustivelUnidade");
        Type profileType = ResolverTipo("Hegemonia.AI.IA03.PerfilPaisSO");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        Type domainType = ResolverTipo("Hegemonia.AI.IA03.IA03DominioEstrategico");
        Type unitType = ResolverTipo("TipoUnidade");
        GameObject strategistObject = new GameObject("IA03 fuel readiness group test");
        strategistObject.SetActive(false);
        GameObject emptyUnit = CriarVeiculoIA03ComCombustivel(identityType, fuelType, unitType, 0f);
        GameObject readyUnit = CriarVeiculoIA03ComCombustivel(identityType, fuelType, unitType, 75f);
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        try
        {
            Component strategist = strategistObject.AddComponent(strategistType);
            Component brain = strategistObject.GetComponent(brainType);
            SetField(brain, "TeamId", 1);
            object worldState = Activator.CreateInstance(worldStateType, new object[] { 1 });
            ((IList)worldStateType.GetField("OwnUnits").GetValue(worldState)).Add(emptyUnit);
            ((IList)worldStateType.GetField("OwnUnits").GetValue(worldState)).Add(readyUnit);
            ((IList)worldStateType.GetField("OwnCombatUnits").GetValue(worldState)).Add(emptyUnit);
            ((IList)worldStateType.GetField("OwnCombatUnits").GetValue(worldState)).Add(readyUnit);
            object context = Activator.CreateInstance(contextType);
            SetField(context, "WorldState", worldState);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });
            SetField(strategist, "brain", brain);
            SetField(strategist, "perfilPais", profile);
            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "ConflitoLimitado"));

            MethodInfo buildGroup = strategistType.GetMethod("ConstruirGrupoUnidades", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildGroup, Is.Not.Null);
            bool built = (bool)buildGroup.Invoke(strategist, new object[]
            {
                null,
                Enum.Parse(domainType, "Terrestre"),
                1,
                1,
                null
            });

            List<GameObject> selected = (List<GameObject>)strategistType
                .GetField("unidadesCandidatas", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(strategist);
            Assert.That(built, Is.True);
            Assert.That(selected, Has.Count.EqualTo(1));
            Assert.That(selected[0], Is.SameAs(readyUnit));
            Assert.That(selected.Contains(emptyUnit), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(emptyUnit);
            UnityEngine.Object.DestroyImmediate(readyUnit);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    private static GameObject CriarVeiculoIA03ComCombustivel(Type identityType, Type fuelType, Type unitType, float fuel)
    {
        var unit = new GameObject("IA03 test vehicle " + fuel);
        unit.SetActive(false);
        Component identity = unit.AddComponent(identityType);
        SetField(identity, "teamID", 1);
        SetField(identity, "tipoUnidade", Enum.Parse(unitType, "Veiculo"));
        Component fuelState = unit.AddComponent(fuelType);
        SetField(fuelState, "usaCombustivel", true);
        SetField(fuelState, "classe", Enum.Parse(ResolverTipo("ClasseCombustivelUnidade"), "Terrestre"));
        SetField(fuelState, "capacidade", 100f);
        SetField(fuelState, "combustivelAtual", fuel);
        SetField(fuelState, "preservarCapacidadeConfigurada", true);
        SetField(fuelState, "preencherAoIniciar", false);
        unit.SetActive(true);
        return unit;
    }

    [Test]
    public void FalhaDeDisponibilidadeSemPerdaMilitarNaoContaBatalhaPerdida()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type missionKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao");
        Type conditionType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        GameObject strategistObject = new GameObject("IA03 unavailable mission does not count as battle test");
        strategistObject.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);
        try
        {
            Component strategist = strategistObject.AddComponent(strategistType);
            Component brain = strategistObject.GetComponent(brainType);
            SetField(strategist, "brain", brain);
            SetField(strategist, "nivelDeConflito", Enum.Parse(
                ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito"), "ConflitoLimitado"));
            SetField(strategist, "missaoAtiva", mission);
            SetField(mission, "tipoMissao", Enum.Parse(missionKindType, "AtaqueLimitado"));
            SetField(mission, "condicaoDeSucesso", Enum.Parse(conditionType, "DestruirAlvo"));

            MethodInfo process = strategistType.GetMethod("ProcessarMissaoAtiva", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(process, Is.Not.Null);
            process.Invoke(strategist, new object[] { 10f });

            object report = strategistType.GetProperty("RelatorioAtual").GetValue(strategist);
            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("Fracasso"));
            Assert.That(Field(report, "BatalhasPerdidas"), Is.EqualTo(0));
            Assert.That(Field(strategist, "missaoAtiva"), Is.Null);
            Assert.That(Field(strategist, "creatyAtivo"), Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void AlvoDestruidoConcluiMissaoMesmoQuandoUltimoAtacanteFoiPerdido()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type missionKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao");
        Type conditionType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        GameObject strategistObject = new GameObject("IA03 destroyed-target terminal result test");
        GameObject creatyObject = new GameObject("IA03 destroyed-target Creaty");
        strategistObject.SetActive(false);
        creatyObject.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);
        GameObject unit = new GameObject("IA03 last attacker lost");
        unit.SetActive(false);

        try
        {
            Component strategist = strategistObject.AddComponent(strategistType);
            Component brain = strategistObject.GetComponent(brainType);
            Component creaty = creatyObject.AddComponent(creatyType);
            SetField(strategist, "brain", brain);
            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "ConflitoLimitado"));
            SetField(strategist, "missaoAtiva", mission);
            SetField(strategist, "creatyAtivo", creaty);
            SetField(strategist, "unidadesOriginaisNaMissao", 1);
            SetField(strategist, "alvoMissaoFoiDefinido", true);
            SetField(strategist, "alvoMissaoDestruido", true);
            SetField(mission, "tipoMissao", Enum.Parse(missionKindType, "AtaqueLimitado"));
            SetField(mission, "condicaoDeSucesso", Enum.Parse(conditionType, "DestruirAlvo"));
            SetField(mission, "condicaoDeFracasso", Enum.Parse(conditionType, "SobreviverAteOPrazo"));
            ((List<GameObject>)Field(strategist, "unidadesAtivasNaMissao")).Add(unit);

            MethodInfo process = strategistType.GetMethod("ProcessarMissaoAtiva", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(process, Is.Not.Null);
            process.Invoke(strategist, new object[] { 10f });

            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("Sucesso"));
            Assert.That(Field(strategist, "missaoAtiva"), Is.Null);
            object report = strategistType.GetProperty("RelatorioAtual").GetValue(strategist);
            Assert.That(Field(report, "BatalhasVencidas"), Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unit);
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
            SetField(strategist, "nivelDeConflito", Enum.Parse(
                ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito"), "Tensao"));

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
            const float missionStartTime = 0f;
            const float missionDeadline = 5f;
            SetField(strategist, "missaoAtiva", mission);
            SetField(strategist, "creatyAtivo", creaty);
            SetField(strategist, "unidadesReservadas", 1);
            SetField(strategist, "unidadesOriginaisNaMissao", 1);
            SetField(strategist, "inicioMissaoEm", missionStartTime);
            SetField(strategist, "idOrdemAtivaDaMissao", orderId);
            ((List<GameObject>)Field(strategist, "unidadesAtivasNaMissao")).Add(unit);
            SetField(creaty, "unidadesReservadas", 1);

            MethodInfo process = strategistType.GetMethod("ProcessarMissaoAtiva", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(process, Is.Not.Null);

            const float previousTick = missionDeadline - 0.05f;
            process.Invoke(strategist, new object[] { previousTick });
            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("EmAndamento"));
            Assert.That(Field(strategist, "missaoAtiva"), Is.SameAs(mission));
            Assert.That(Field(strategist, "unidadesReservadas"), Is.EqualTo(1));
            Assert.That(queueType.GetProperty("PendingCount").GetValue(queue), Is.EqualTo(1));
            Debug.Log("[IA03][TIMEOUT_PROBE][BEFORE_DEADLINE] Time.time=" + Time.time.ToString("F2")
                + " start=" + missionStartTime.ToString("F2")
                + " deadline=" + missionDeadline.ToString("F2")
                + " tick=" + previousTick.ToString("F2")
                + " remaining=" + (missionDeadline - previousTick).ToString("F2")
                + " result=" + Field(strategist, "estadoDaMissao")
                + " orderStatus=" + queueType.GetMethod("GetStatus").Invoke(queue, new object[] { orderId }));

            const float nextEvaluation = missionDeadline + 0.05f;
            Debug.Log("[IA03][TIMEOUT_PROBE][DEADLINE_EVALUATION] Time.time=" + Time.time.ToString("F2")
                + " deadline=" + missionDeadline.ToString("F2")
                + " previousTick=" + previousTick.ToString("F2")
                + " tick=" + nextEvaluation.ToString("F2")
                + " elapsed=" + (nextEvaluation - missionStartTime).ToString("F2")
                + " remaining=" + (missionDeadline - nextEvaluation).ToString("F2"));
            process.Invoke(strategist, new object[] { nextEvaluation });

            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("Expirada"));
            Assert.That(Field(strategist, "missaoAtiva"), Is.Null);
            Assert.That(Field(strategist, "creatyAtivo"), Is.Null);
            Assert.That(Field(strategist, "unidadesReservadas"), Is.EqualTo(0));
            Assert.That(((List<GameObject>)Field(strategist, "unidadesAtivasNaMissao")).Count, Is.EqualTo(0));
            Assert.That(creatyType.GetProperty("UnidadesReservadas").GetValue(creaty), Is.EqualTo(0));
            Assert.That(queueType.GetProperty("PendingCount").GetValue(queue), Is.EqualTo(0));
            Assert.That(queueType.GetMethod("GetStatus").Invoke(queue, new object[] { orderId }).ToString(), Is.EqualTo("Cancelled"));
            Debug.Log("[IA03][TIMEOUT_PROBE][TERMINAL] tick=" + nextEvaluation.ToString("F2")
                + " result=" + Field(strategist, "estadoDaMissao")
                + " missionActive=" + (Field(strategist, "missaoAtiva") != null)
                + " orderStatus=" + queueType.GetMethod("GetStatus").Invoke(queue, new object[] { orderId })
                + " group=" + ((List<GameObject>)Field(strategist, "unidadesAtivasNaMissao")).Count
                + " unitsReserved=" + Field(strategist, "unidadesReservadas")
                + " creatyReserved=" + creatyType.GetProperty("UnidadesReservadas").GetValue(creaty));
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
    public void FalhaTerminalDaOrdemEncerraMissaoSemEsperarTimeout()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type queueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type requestType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandRequest");
        Type commandType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandType");
        Type missionConditionType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        GameObject strategistObject = new GameObject("IA03 failed command mission test");
        GameObject creatyObject = new GameObject("IA03 failed command Creaty test");
        strategistObject.SetActive(false);
        creatyObject.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);
        GameObject unit = new GameObject("IA03 failed command unit");

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
            SetField(strategist, "nivelDeConflito", Enum.Parse(
                ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito"), "Tensao"));

            SetField(mission, "tempoMaximoSegundos", 600f);
            SetField(mission, "condicaoDeSucesso", Enum.Parse(missionConditionType, "ConfirmacaoExterna"));
            SetField(mission, "condicaoDeFracasso", Enum.Parse(missionConditionType, "SobreviverAteOPrazo"));

            const string orderId = "ia03-terminal-failed-order";
            object request = Activator.CreateInstance(requestType);
            SetField(request, "Id", orderId);
            SetField(request, "Origin", "IA03EstrategaNacional");
            SetField(request, "Domain", "tactical");
            SetField(request, "Reason", "falha terminal de teste");
            SetField(request, "Family", "tactical");
            SetField(request, "Type", Enum.Parse(commandType, "Move"));
            SetField(request, "DedupKey", orderId);
            object[] enqueueArguments = { request, 1f, null };
            Assert.That(queueType.GetMethod("Enqueue").Invoke(queue, enqueueArguments), Is.EqualTo(true));

            object[] dequeueArguments = { 1f, null };
            Assert.That(queueType.GetMethod("TryDequeue").Invoke(queue, dequeueArguments), Is.EqualTo(true));
            queueType.GetMethod("Complete").Invoke(
                queue,
                new object[] { dequeueArguments[1], false, 1f, "executor recusou" });

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
            process.Invoke(strategist, new object[] { 10f });

            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("Fracasso"));
            Assert.That(Field(strategist, "missaoAtiva"), Is.Null);
            Assert.That(Field(strategist, "unidadesReservadas"), Is.EqualTo(0));
            Assert.That(creatyType.GetProperty("UnidadesReservadas").GetValue(creaty), Is.EqualTo(0));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unit);
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(creatyObject);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void MissaoSemCombustivelInterrompeELiberaReservaParaReavaliarDepoisDoReabastecimento()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type missionKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao");
        Type missionOrderType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoOrdem");
        Type missionConditionType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        Type domainType = ResolverTipo("Hegemonia.AI.IA03.IA03DominioEstrategico");
        Type profileType = ResolverTipo("Hegemonia.AI.IA03.PerfilPaisSO");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type contextType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_Context");
        Type queueType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandQueue");
        Type requestType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandRequest");
        Type commandType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandType");
        Type identityType = ResolverTipo("IdentidadeUnidade");
        Type fuelType = ResolverTipo("CombustivelUnidade");
        Type unitKindType = ResolverTipo("TipoUnidade");
        Type worldStateType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_WorldState");
        Type economyType = ResolverTipo("Hegemonia.AI.IA03.GestorEconomiaIA03");
        Type summaryType = ResolverTipo("Hegemonia.AI.IA03.GestorEconomiaIA03+Resumo");

        GameObject strategistObject = new GameObject("IA03 exhausted-fuel mission test");
        GameObject creatyObject = new GameObject("IA03 exhausted-fuel Creaty test");
        strategistObject.SetActive(false);
        creatyObject.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        GameObject unit = CriarVeiculoIA03ComCombustivel(identityType, fuelType, unitKindType, 0f);
        object worldState = Activator.CreateInstance(worldStateType, new object[] { 1 });
        ((IList)worldStateType.GetField("OwnUnits").GetValue(worldState)).Add(unit);
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
            SetField(strategist, "perfilPais", profile);
            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "ConflitoLimitado"));

            SetField(mission, "idMissao", "ia03-espera-combustivel");
            SetField(mission, "nomeMissao", "Aguardar reabastecimento");
            SetField(mission, "tipoMissao", Enum.Parse(missionKindType, "Patrulha"));
            SetField(mission, "dominio", Enum.Parse(domainType, "Terrestre"));
            SetField(mission, "tipoOrdem", Enum.Parse(missionOrderType, "Mover"));
            SetField(mission, "nivelMenosGrave", Enum.Parse(levelType, "Tensao"));
            SetField(mission, "nivelMaisGrave", Enum.Parse(levelType, "GuerraTotal"));
            SetField(mission, "tempoMaximoSegundos", 300f);
            SetField(mission, "condicaoDeSucesso", Enum.Parse(missionConditionType, "ConfirmacaoExterna"));
            SetField(mission, "condicaoDeFracasso", Enum.Parse(missionConditionType, "SobreviverAteOPrazo"));
            ((IList)Field(strategist, "missoesEstrategicas")).Add(mission);

            const string orderId = "ia03-fuel-exhausted-order";
            object request = Activator.CreateInstance(requestType);
            SetField(request, "Id", orderId);
            SetField(request, "Origin", "IA03EstrategaNacional");
            SetField(request, "Domain", "tactical");
            SetField(request, "Reason", "missão suspensa aguardando combustível");
            SetField(request, "Family", "tactical");
            SetField(request, "Type", Enum.Parse(commandType, "Move"));
            SetField(request, "DedupKey", orderId);
            object[] enqueueArguments = { request, 1f, null };
            Assert.That(queueType.GetMethod("Enqueue").Invoke(queue, enqueueArguments), Is.EqualTo(true));

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
            process.Invoke(strategist, new object[] { 10f });

            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("Cancelada"),
                "A IA03 deve interromper a missão quando todas as unidades ativas estão sem combustível, sem esperar os 300 segundos do prazo.");
            Assert.That(Field(strategist, "missaoAtiva"), Is.Null);
            Assert.That(Field(strategist, "creatyAtivo"), Is.Null);
            Assert.That(Field(strategist, "unidadesReservadas"), Is.EqualTo(0));
            Assert.That(creatyType.GetProperty("UnidadesReservadas").GetValue(creaty), Is.EqualTo(0));
            Assert.That(queueType.GetProperty("PendingCount").GetValue(queue), Is.EqualTo(0));
            Assert.That(queueType.GetMethod("GetStatus").Invoke(queue, new object[] { orderId }).ToString(), Is.EqualTo("Cancelled"));
            Assert.That(Field(strategist, "ultimaDecisao").ToString(), Does.Contain("combustível"));

            IList queuedAfterFuelCancellation = (IList)Field(strategist, "filaMissoes");
            Assert.That(queuedAfterFuelCancellation, Has.Count.EqualTo(1),
                "Uma missão externa suspensa por falta de combustível deve voltar à fila para reavaliação.");
            Assert.That(queuedAfterFuelCancellation[0], Is.SameAs(mission));

            MethodInfo captureReadiness = economyType.GetMethod(
                "CapturarUnidadesOperacionais",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(captureReadiness, Is.Not.Null);
            object[] readinessArguments = { worldState, Activator.CreateInstance(summaryType) };
            captureReadiness.Invoke(null, readinessArguments);
            Assert.That(Field(readinessArguments[1], "UnidadesTerrestresOperacionais"), Is.EqualTo(0),
                "A prontidão IA03 não deve contar como operacional um veículo parado por falta de combustível.");

            SetField(unit.GetComponent(fuelType), "combustivelAtual", 25f);
            readinessArguments[1] = Activator.CreateInstance(summaryType);
            captureReadiness.Invoke(null, readinessArguments);
            Assert.That(Field(readinessArguments[1], "UnidadesTerrestresOperacionais"), Is.EqualTo(1),
                "Após o reabastecimento, a unidade deve voltar a contar como operacional.");

            MethodInfo refreshMissions = strategistType.GetMethod("AtualizarFilaMissoes", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refreshMissions, Is.Not.Null);
            refreshMissions.Invoke(strategist, null);
            IList queueMissions = (IList)Field(strategist, "filaMissoes");
            Assert.That(queueMissions, Has.Count.EqualTo(1), "A falha logística deve deixar a missão disponível para reavaliação após o reabastecimento.");
            Assert.That(queueMissions[0], Is.SameAs(mission));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(creatyObject);
            UnityEngine.Object.DestroyImmediate(unit);
            UnityEngine.Object.DestroyImmediate(mission);
            UnityEngine.Object.DestroyImmediate(profile);
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

    [Test]
    public void BrainMasterAceitaMovimentoQuandoUnidadeJaEstaNoDestino()
    {
        Type backendType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        object backend = Activator.CreateInstance(backendType, new object[] { 1 });
        GameObject unit = new GameObject("IA03 already-at-destination unit");
        try
        {
            Type requestType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandRequest");
            object request = Activator.CreateInstance(requestType);
            SetField(request, "Type", Enum.Parse(ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandType"), "Move"));
            Type moveDataType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_MoveOrderData");
            object moveData = Activator.CreateInstance(moveDataType);
            SetField(moveData, "Units", new List<GameObject> { unit });
            SetField(moveData, "Destination", Vector3.zero);
            SetField(request, "Payload", moveData);

            object commandService = backendType.GetProperty("CommandService").GetValue(backend, null);
            object[] executeArguments = { request, null, null };
            bool accepted = (bool)commandService.GetType().GetMethod("Execute").Invoke(commandService, executeArguments);
            string message = executeArguments[2] as string;

            Assert.That(accepted, Is.True);
            Assert.That(message, Does.Contain("movidas=1"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unit);
        }
    }

    [Test]
    public void BrainMasterAceitaAtaqueAereoQuandoUnidadeJaEstaNoPonto()
    {
        Type backendType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        object backend = Activator.CreateInstance(backendType, new object[] { 1 });
        GameObject unit = new GameObject("IA03 aircraft already-at-attack-position");
        GameObject target = new GameObject("IA03 attack target");
        GameObject projectile = new GameObject("IA03 aircraft projectile");
        try
        {
            unit.AddComponent(ResolverTipo("ControleAviao"));
            Component weapon = unit.AddComponent(ResolverTipo("SistemaDeTiro"));
            SetField(weapon, "capacidadePente", 1);
            SetField(weapon, "prefabProjetil", projectile);
            unit.SetActive(false);

            Type requestType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandRequest");
            object request = Activator.CreateInstance(requestType);
            SetField(request, "Type", Enum.Parse(ResolverTipo("Hegemonia.AI.BrainMaster.IA_CommandType"), "Attack"));
            SetField(request, "Origin", "IA03EstrategaNacional");
            SetField(request, "Id", "ia03:attack:aircraft");
            Type attackDataType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_AttackOrderData");
            object attackData = Activator.CreateInstance(attackDataType);
            SetField(attackData, "Units", new List<GameObject> { unit });
            SetField(attackData, "Target", target.transform);
            SetField(attackData, "TargetPosition", Vector3.zero);
            SetField(request, "Payload", attackData);

            object commandService = backendType.GetProperty("CommandService").GetValue(backend, null);
            object[] executeArguments = { request, null, null };
            bool accepted = (bool)commandService.GetType().GetMethod("Execute").Invoke(commandService, executeArguments);
            string message = executeArguments[2] as string;

            Assert.That(accepted, Is.True);
            Assert.That(message, Does.Contain("atacantes=1"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unit);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(projectile);
        }
    }

    [Test]
    public void BrainMasterArmaSubmarinoComTorpedoParaAlvoNavalMesmoForaDoAlcanceAtual()
    {
        Type backendType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        Type submarineType = ResolverTipo("ControleSubmarino");
        Type targetType = ResolverTipo("ControleNavioRealista");
        Type torpedoType = ResolverTipo("Torpedo");
        object backend = Activator.CreateInstance(backendType, new object[] { 1 });
        GameObject unit = new GameObject("IA03 torpedo-only attack submarine");
        GameObject tube = new GameObject("IA03 submarine torpedo tube");
        GameObject target = new GameObject("IA03 surface attack target");
        GameObject projectile = new GameObject("IA03 submarine torpedo projectile");
        try
        {
            tube.transform.SetParent(unit.transform, false);
            target.AddComponent(targetType);
            projectile.AddComponent(torpedoType);

            Component submarine = unit.AddComponent(submarineType);
            SetField(submarine, "misseisDisponiveis", 0);
            SetField(submarine, "prefabTorpedo", projectile);
            SetField(submarine, "torpedosDisponiveis", 1);
            SetField(submarine, "tubosTorpedo", new[] { tube.transform });
            SetField(submarine, "tubosTorpedoUsados", new[] { false });
            SetField(submarine, "alcanceTorpedos", 10f);
            SetField(submarine, "proximoLancamentoTorpedo", Time.time + 60f);

            object commandService = backendType.GetProperty("CommandService").GetValue(backend, null);
            MethodInfo armUnit = commandService.GetType().GetMethod(
                "ArmUnitForAttack",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(armUnit, Is.Not.Null);

            bool armed = (bool)armUnit.Invoke(commandService, new object[]
            {
                unit,
                target.transform,
                target.transform.position
            });

            Assert.That(armed, Is.True,
                "A ordem BrainMaster deve aceitar o submarino torpedo-only e mantê-lo em automático até entrar no alcance.");
            Assert.That(Field(submarine, "modoAtual").ToString(), Is.EqualTo("Automatico"));
            Assert.That(Field(submarine, "torpedosDisponiveis"), Is.EqualTo(1),
                "A ordem fora do alcance não pode consumir munição prematuramente.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(projectile);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(tube);
            UnityEngine.Object.DestroyImmediate(unit);
        }
    }

    [Test]
    public void BrainMasterArmaTorretaModularSemControleUnidade()
    {
        Type backendType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        Type modularTurretType = ResolverTipo("ControleTorretaModular");
        Assert.That(backendType, Is.Not.Null);
        Assert.That(modularTurretType, Is.Not.Null);

        object backend = Activator.CreateInstance(backendType, new object[] { 1 });
        GameObject unit = new GameObject("IA03 modular turret unit without controller");
        unit.SetActive(false);
        GameObject turretObject = new GameObject("IA03 modular turret child");
        turretObject.transform.SetParent(unit.transform, false);
        GameObject target = new GameObject("IA03 modular turret attack target");
        target.transform.position = unit.transform.position + Vector3.forward * 10f;

        try
        {
            Component turret = turretObject.AddComponent(modularTurretType);
            SetField(turret, "modoPassivo", true);

            object commandService = backendType.GetProperty("CommandService").GetValue(backend, null);
            MethodInfo armUnit = commandService.GetType().GetMethod(
                "ArmUnitForAttack",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(armUnit, Is.Not.Null);

            bool armed = (bool)armUnit.Invoke(commandService, new object[]
            {
                unit,
                target.transform,
                target.transform.position
            });

            Assert.That(armed, Is.True,
                "A ordem explícita do BrainMaster deve ativar a torreta modular mesmo sem ControleUnidade.");
            Assert.That(Field(turret, "modoPassivo"), Is.EqualTo(false));
            Assert.That(Field(turret, "alvoPrioritario"), Is.SameAs(target.transform),
                "O alvo explícito deve permanecer prioritário até entrar no alcance do radar.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(unit);
        }
    }

    [Test]
    public void BrainMasterAtivaTorretaAntiaereaSemControleParaAlvoAereo()
    {
        Type backendType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        Type antiAirTurretType = ResolverTipo("TorretaAntiaerea");
        Assert.That(backendType, Is.Not.Null);
        Assert.That(antiAirTurretType, Is.Not.Null);

        object backend = Activator.CreateInstance(backendType, new object[] { 1 });
        GameObject unit = new GameObject("IA03 anti-air unit without controller");
        unit.SetActive(false);
        GameObject turretObject = new GameObject("IA03 anti-air turret child");
        turretObject.transform.SetParent(unit.transform, false);
        GameObject target = new GameObject("IA03 airborne attack target");
        target.transform.position = new Vector3(0f, 30f, 0f);

        try
        {
            Component turret = turretObject.AddComponent(antiAirTurretType);
            SetField(turret, "modoPassivo", true);

            object commandService = backendType.GetProperty("CommandService").GetValue(backend, null);
            MethodInfo armUnit = commandService.GetType().GetMethod(
                "ArmUnitForAttack",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(armUnit, Is.Not.Null);

            bool armed = (bool)armUnit.Invoke(commandService, new object[]
            {
                unit,
                target.transform,
                target.transform.position
            });

            Assert.That(armed, Is.True,
                "Uma ordem contra aeronave deve ativar a defesa AA mesmo sem ControleUnidade.");
            Assert.That(Field(turret, "modoPassivo"), Is.EqualTo(false));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(unit);
        }
    }

    [Test]
    public void BrainMasterMantemTorretaAntiaereaPassivaParaAlvoTerrestre()
    {
        Type backendType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        Type antiAirTurretType = ResolverTipo("TorretaAntiaerea");
        Assert.That(backendType, Is.Not.Null);
        Assert.That(antiAirTurretType, Is.Not.Null);

        object backend = Activator.CreateInstance(backendType, new object[] { 1 });
        GameObject unit = new GameObject("IA03 anti-air unit without controller");
        unit.SetActive(false);
        GameObject turretObject = new GameObject("IA03 anti-air turret child");
        turretObject.transform.SetParent(unit.transform, false);
        GameObject target = new GameObject("IA03 ground attack target");

        try
        {
            Component turret = turretObject.AddComponent(antiAirTurretType);
            SetField(turret, "modoPassivo", true);

            object commandService = backendType.GetProperty("CommandService").GetValue(backend, null);
            MethodInfo armUnit = commandService.GetType().GetMethod(
                "ArmUnitForAttack",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(armUnit, Is.Not.Null);

            bool armed = (bool)armUnit.Invoke(commandService, new object[]
            {
                unit,
                target.transform,
                target.transform.position
            });

            Assert.That(armed, Is.False,
                "Uma torreta AA isolada não constitui armamento para uma ordem contra alvo terrestre.");
            Assert.That(Field(turret, "modoPassivo"), Is.EqualTo(true),
                "Uma ordem terrestre não deve ativar a defesa antiaérea.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(unit);
        }
    }

    [Test]
    public void MissaoDefaultCreatyIgnoraPrimeiroTipoInvalidoQuandoHaTipoPosteriorValido()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoCreaty");
        GameObject strategistObject = new GameObject("IA03 default Creaty selection test");
        strategistObject.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);

        try
        {
            Component strategist = strategistObject.AddComponent(strategistType);
            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "ConflitoLimitado"));
            Type creatyListType = typeof(List<>).MakeGenericType(creatyType);
            IList creatysPermitidos = (IList)Activator.CreateInstance(creatyListType);
            creatysPermitidos.Add(Enum.Parse(creatyType, "GuerraN1"));
            creatysPermitidos.Add(Enum.Parse(creatyType, "PontoGenerico"));
            SetField(
                mission,
                "creatysPermitidos",
                creatysPermitidos);

            MethodInfo escolher = strategistType.GetMethod(
                "MissaoDefaultCreaty",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(escolher, Is.Not.Null);
            Assert.That(
                escolher.Invoke(strategist, new object[] { mission }).ToString(),
                Is.EqualTo("PontoGenerico"),
                "A fila deve procurar um tipo configurado válido, não apenas o primeiro item da lista.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(strategistObject);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void MissaoEstrategicaNaoIniciaSemAutoridadeDoBrainMaster()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        GameObject owner = new GameObject("IA03 execution authority gate test");
        owner.SetActive(false);

        try
        {
            Component strategist = owner.AddComponent(strategistType);
            Component brain = owner.AddComponent(brainType);
            SetField(strategist, "brain", brain);

            MethodInfo canStart = strategistType.GetMethod(
                "PodeIniciarMissaoEstrategica",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(canStart, Is.Not.Null);
            Assert.That(canStart.Invoke(strategist, null), Is.EqualTo(false));
            Assert.That(
                Field(strategist, "ultimaDecisao").ToString(),
                Is.EqualTo("missão adiada: BrainMaster sem autoridade de execução"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void MissaoOfensivaNaoAtravessaAvancoMilitarSemAutorizacaoDeFogo()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        Type orderType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoOrdem");
        Type conditionType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        GameObject owner = new GameObject("IA03 offensive diplomatic gate test");
        owner.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);

        try
        {
            Component strategist = owner.AddComponent(strategistType);
            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "AvancoMilitar"));
            SetField(mission, "tipoOrdem", Enum.Parse(orderType, "Mover"));
            SetField(mission, "condicaoDeSucesso", Enum.Parse(conditionType, "DestruirAlvo"));

            MethodInfo gate = strategistType.GetMethod(
                "PodeEmitirMissaoOfensiva",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(gate, Is.Not.Null);
            object[] arguments = { mission, null };

            Assert.That(gate.Invoke(strategist, arguments), Is.EqualTo(false));
            Assert.That(Convert.ToString(arguments[1]), Does.Contain("nível diplomático"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [TestCase("InvasaoAnfibia", "AvancoMilitar", "nível diplomático")]
    [TestCase("InvasaoAnfibia", "ConflitoLimitado", "guerra formal")]
    [TestCase("InvasaoAnfibia", "GuerraTotal", "guerra formal")]
    [TestCase("AtaqueLimitado", "ConflitoLimitado", "guerra formal")]
    [TestCase("GuerraTotal", "GuerraTotal", "guerra formal")]
    public void TipoDeMissaoOfensivaExigeAutorizacaoFormalMesmoComOrdemDeMover(
        string nomeMissao,
        string nomeNivel,
        string trechoMotivoEsperado)
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type missionKindType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao");
        Type conflictType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        GameObject owner = new GameObject("IA03 mission-type war authorization test");
        owner.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);

        try
        {
            Component strategist = owner.AddComponent(strategistType);
            SetField(strategist, "nivelDeConflito", Enum.Parse(conflictType, nomeNivel));
            SetField(mission, "tipoMissao", Enum.Parse(missionKindType, nomeMissao));
            SetField(mission, "tipoOrdem", Enum.Parse(ResolverTipo("Hegemonia.AI.IA03.IA03TipoOrdem"), "Mover"));
            SetField(mission, "condicaoDeSucesso", Enum.Parse(ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao"), "ChegarAoDestino"));
            SetField(mission, "condicaoDeFracasso", Enum.Parse(ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao"), "SobreviverAteOPrazo"));

            MethodInfo gate = strategistType.GetMethod(
                "PodeEmitirMissaoOfensiva",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(gate, Is.Not.Null);
            object[] arguments = { mission, null };

            Assert.That(gate.Invoke(strategist, arguments), Is.EqualTo(false));
            Assert.That(Convert.ToString(arguments[1]), Does.Contain(trechoMotivoEsperado));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void MissaoAtivaEInterrompidaQuandoCessarFogoVoltaAPaz()
    {
        Type strategistType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type levelType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        GameObject owner = new GameObject("IA03 ceasefire active mission test");
        owner.SetActive(false);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);

        try
        {
            Component brain = owner.AddComponent(brainType);
            Component strategist = owner.AddComponent(strategistType);
            SetField(strategist, "brain", brain);
            SetField(strategist, "missaoAtiva", mission);
            SetField(strategist, "nivelDeConflito", Enum.Parse(levelType, "Paz"));

            MethodInfo process = strategistType.GetMethod(
                "ProcessarMissaoAtiva",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(process, Is.Not.Null);
            process.Invoke(strategist, new object[] { 20f });

            Assert.That(Field(strategist, "missaoAtiva"), Is.Null);
            Assert.That(Field(strategist, "estadoDaMissao").ToString(), Is.EqualTo("Cancelada"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [TestCase("exigePortaAvioes")]
    [TestCase("exigeTransporteNaval")]
    [TestCase("exigePresidente")]
    public void MissaoComRequisitoDeGrupoNaoERejeitadaPeloGateDeConfiguracao(string campoRequisito)
    {
        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type conflictType = ResolverTipo("Hegemonia.AI.IA03.IA03NivelConflito");
        Type creatyType = ResolverTipo("Hegemonia.AI.IA03.IA03TipoCreaty");
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);

        try
        {
            SetField(mission, campoRequisito, true);
            SetField(mission, "nivelMenosGrave", Enum.Parse(conflictType, "Tensao"));
            SetField(mission, "nivelMaisGrave", Enum.Parse(conflictType, "GuerraTotal"));

            MethodInfo aceita = missionType.GetMethod("Aceita");
            Assert.That(aceita, Is.Not.Null);
            Assert.That(
                aceita.Invoke(mission, new object[]
                {
                    Enum.Parse(conflictType, "ConflitoLimitado"),
                    Enum.Parse(creatyType, "PontoGenerico")
                }),
                Is.EqualTo(true),
                "O requisito de papel deve ser validado na montagem do grupo, não rejeitado pelo gate da missão.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    [Test]
    public void RelatorioDeConflitoIA03SobreviveAoRoundTripDoSave()
    {
        Type relatorioType = ResolverTipo("Hegemonia.AI.IA03.IA03RelatorioConflito");
        Type saveDataType = ResolverTipo("Hegemonia.AI.IA03.IA03RelatorioSaveData");
        object original = Activator.CreateInstance(relatorioType);

        relatorioType.GetMethod("RegistrarResultadoCombate").Invoke(original, new object[] { true });
        relatorioType.GetMethod("RegistrarResultadoCombate").Invoke(original, new object[] { false });
        relatorioType.GetMethod("RegistrarPrejuizoEconomico").Invoke(original, new object[] { true, 2750f });
        relatorioType.GetMethod("RegistrarObjetivoCapturado").Invoke(original, new object[] { true });

        object saveData = relatorioType.GetMethod("CapturarEstadoSalvavel").Invoke(original, null);
        string json = JsonUtility.ToJson(saveData);
        object carregado = JsonUtility.FromJson(json, saveDataType);
        object restaurado = Activator.CreateInstance(relatorioType);
        relatorioType.GetMethod("RestaurarEstadoSalvavel").Invoke(restaurado, new[] { carregado });

        object snapshot = relatorioType.GetProperty("Acumulado").GetValue(restaurado);
        Assert.That(Field(snapshot, "BatalhasVencidas"), Is.EqualTo(1));
        Assert.That(Field(snapshot, "BatalhasPerdidas"), Is.EqualTo(1));
        Assert.That(Field(snapshot, "PrejuizoEconomicoInimigo"), Is.EqualTo(2750f));
        Assert.That(Field(snapshot, "ObjetivosCapturados"), Is.EqualTo(1));

        relatorioType.GetMethod("RestaurarEstadoSalvavel").Invoke(restaurado, new object[] { null });
        Assert.That(Field(snapshot, "BatalhasVencidas"), Is.EqualTo(1),
            "Um save legado sem o snapshot IA03 não deve apagar estado já existente.");
    }

    private static Type ResolverTipo(string nome)
    {
        Type tipo = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(nome, false))
            .FirstOrDefault(candidato => candidato != null);
        Assert.That(tipo, Is.Not.Null, "Tipo não encontrado: " + nome);
        return tipo;
    }

    private static object InvokeStatic(Type tipo, string nome, params object[] argumentos)
    {
        Type[] tiposArgumentos = argumentos.Select(argumento => argumento.GetType()).ToArray();
        MethodInfo metodo = tipo.GetMethod(
            nome,
            BindingFlags.Public | BindingFlags.Static,
            null,
            tiposArgumentos,
            null);
        Assert.That(metodo, Is.Not.Null, "Método estático não encontrado: " + tipo.FullName + "." + nome);
        return metodo.Invoke(null, argumentos);
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
