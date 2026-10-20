using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CombatEndToEndPlayModeTests
{
    private sealed class DiplomacyFixtureSnapshot
    {
        public Component government;
        public object previousGovernment;
        public PropertyInfo instanceProperty;
        public GameObject createdGovernmentRoot;
        public System.Collections.IList relations;
        public object relation;
        public bool relationExisted;
        public object postureA;
        public object postureB;
        public object ceasefire;
    }

    [UnityTest]
    public IEnumerator EnemyTurretIdentifiesTargetFiresProjectileAndAppliesDamage()
    {
        GameObject alvo = new GameObject("AlvoJogadorCombate");
        GameObject atirador = new GameObject("TorreInimigaCombate");
        GameObject cano = new GameObject("CanoCombate");
        GameObject projetilPrefab = new GameObject("ProjetilCombatePrefab");
        GameObject donoDoProjetil = null;
        DiplomacyFixtureSnapshot diplomacy = null;

        try
        {
            alvo.SetActive(false);
            BoxCollider colisao = alvo.AddComponent<BoxCollider>();
            colisao.size = new Vector3(4f, 4f, 4f);
            // O teste usa reflexão porque este assembly não referencia o
            // Assembly-CSharp predefinido diretamente.
            Component danos = alvo.AddComponent(ResolveType("SistemaDeDanos"));
            Component identidadeAlvo = alvo.AddComponent(ResolveType("IdentidadeUnidade"));
            SetField(identidadeAlvo, "teamID", 1);
            alvo.transform.position = new Vector3(0f, 0f, 50f);

            atirador.SetActive(false);
            Component identidadeAtirador = atirador.AddComponent(ResolveType("IdentidadeUnidade"));
            SetField(identidadeAtirador, "teamID", 2);
            Component torreta = atirador.AddComponent(ResolveType("ControleTorreta"));
            SetField(torreta, "etiquetaAlvo", "Inimigo");
            SetField(torreta, "alcance", 60f);
            SetField(torreta, "tempoEntreTiros", 0.05f);
            SetField(torreta, "dispararMesmoDesalinhado", true);
            SetField(torreta, "pecaQueGira", atirador.transform);

            cano.transform.SetParent(atirador.transform, false);
            cano.transform.localPosition = Vector3.zero;
            cano.transform.localRotation = Quaternion.identity;
            SetField(torreta, "locaisDoTiro", new[] { cano.transform });

            projetilPrefab.SetActive(false);
            Component projetil = projetilPrefab.AddComponent(ResolveType("Projetil"));
            SetField(projetil, "velocidade", 100f);
            SetField(projetil, "dano", 25);
            SetField(projetil, "tempoDeVida", 3f);
            SetField(torreta, "municaoPrefab", projetilPrefab);

            diplomacy = PrepararPosturaHostil(2, 1);
            alvo.SetActive(true);
            yield return null;
            atirador.SetActive(true);
            yield return null;

            float limite = Time.realtimeSinceStartup + 3f;
            FieldInfo alvoAtual = ResolveType("ControleTorreta").GetField("alvoAtual", BindingFlags.Instance | BindingFlags.NonPublic);
            Type tipoProjetil = ResolveType("Projetil");
            FieldInfo ativosNoMapa = tipoProjetil.GetField("ativosNoMapa", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo obterDono = tipoProjetil.GetMethod("GetDono", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(alvoAtual, Is.Not.Null);
            Assert.That(ativosNoMapa, Is.Not.Null);
            Assert.That(obterDono, Is.Not.Null);
            while (Time.realtimeSinceStartup < limite && (float)ReadField(danos, "vidaAtual") >= (float)ReadField(danos, "vidaMaxima"))
            {
                foreach (object projetilAtivo in (System.Collections.IEnumerable)ativosNoMapa.GetValue(null))
                {
                    if (projetilAtivo == null) continue;
                    GameObject dono = obterDono.Invoke(projetilAtivo, null) as GameObject;
                    if (dono == atirador)
                    {
                        donoDoProjetil = atirador;
                    }
                }
                yield return null;
            }

            Assert.That(alvoAtual.GetValue(torreta), Is.EqualTo(alvo.transform),
                "A torreta inimiga não identificou o alvo hostil dentro do alcance.");
            Assert.Less((float)ReadField(danos, "vidaAtual"), (float)ReadField(danos, "vidaMaxima"),
                "A torreta identificou o alvo, mas não criou um projétil que aplicasse dano.");
            Assert.That(donoDoProjetil, Is.EqualTo(atirador),
                "O projétil criado pela torreta não preservou o identificador da unidade que disparou.");
        }
        finally
        {
            RestaurarPostura(diplomacy);
            DestruirProjeteisDoAtacante(atirador);
            UnityEngine.Object.Destroy(alvo);
            UnityEngine.Object.Destroy(atirador);
            UnityEngine.Object.Destroy(projetilPrefab);
            UnityEngine.Object.Destroy(cano);
        }

        yield return null;
        PropertyInfo instancia = ResolveType("SistemaGovernoMundial").GetProperty(
            "Instancia", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        object instanciaRestaurada = instancia.GetValue(null);
        Assert.That(instanciaRestaurada, Is.SameAs(diplomacy != null ? diplomacy.previousGovernment : null),
            "A fixture deve restaurar o singleton diplomático que existia antes do teste.");
    }

    [UnityTest]
    public IEnumerator TurretDoesNotIdentifyAUnitFromItsOwnTeamAsEnemy()
    {
        GameObject aliado = new GameObject("AliadoNaoPodeSerAlvo");
        GameObject atirador = new GameObject("TorreAliadaCombate");
        try
        {
            aliado.SetActive(false);
            aliado.AddComponent<BoxCollider>().size = new Vector3(4f, 4f, 4f);
            Component identidadeAliada = aliado.AddComponent(ResolveType("IdentidadeUnidade"));
            SetField(identidadeAliada, "teamID", 2);
            aliado.transform.position = new Vector3(0f, 0f, 20f);

            atirador.SetActive(false);
            Component identidadeAtirador = atirador.AddComponent(ResolveType("IdentidadeUnidade"));
            SetField(identidadeAtirador, "teamID", 2);
            Component torreta = atirador.AddComponent(ResolveType("ControleTorreta"));
            SetField(torreta, "alcance", 60f);
            SetField(torreta, "pecaQueGira", atirador.transform);

            aliado.SetActive(true);
            atirador.SetActive(true);
            yield return new WaitForSeconds(0.8f);

            FieldInfo alvoAtual = ResolveType("ControleTorreta").GetField("alvoAtual", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(alvoAtual, Is.Not.Null);
            Assert.That(alvoAtual.GetValue(torreta), Is.Null,
                "A torreta considerou uma unidade do mesmo time como alvo inimigo.");
        }
        finally
        {
            UnityEngine.Object.Destroy(aliado);
            UnityEngine.Object.Destroy(atirador);
        }
    }

    private static Type ResolveType(string name)
    {
        return System.AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .First(type => type != null);
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static object ReadField(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    }

    private static DiplomacyFixtureSnapshot PrepararPosturaHostil(int teamAtacante, int teamAlvo)
    {
        Type governoType = ResolveType("SistemaGovernoMundial");
        PropertyInfo instancia = governoType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo setInstancia = instancia.GetSetMethod(true);
        object governoAnterior = instancia.GetValue(null);
        Component governo = governoAnterior as Component;
        GameObject raizGovernoFixture = null;
        if (governo == null)
        {
            // A fixture precisa apenas da lista de relações consultada pela torreta.
            // Evita GarantirInstancia(), que cria um singleton persistente e serviços
            // nacionais que não pertencem a este teste.
            raizGovernoFixture = new GameObject("CombatEndToEnd_DiplomacyFixture");
            raizGovernoFixture.SetActive(false);
            governo = raizGovernoFixture.AddComponent(governoType);
            setInstancia.Invoke(null, new[] { (object)governo });
        }
        DiplomacyFixtureSnapshot snapshot = new DiplomacyFixtureSnapshot
        {
            government = governo,
            previousGovernment = governoAnterior,
            instanceProperty = instancia,
            createdGovernmentRoot = raizGovernoFixture
        };

        try
        {
            Assert.That(governo, Is.Not.Null, "A fixture de combate automático precisa do sistema diplomático do jogo.");
            System.Collections.IList relacoes = ReadField(governo, "relacoes") as System.Collections.IList;
            snapshot.relations = relacoes;
            Assert.That(relacoes, Is.Not.Null);

            object relacao = null;
            for (int i = 0; i < relacoes.Count; i++)
            {
                object candidata = relacoes[i];
                if (candidata == null) continue;
                int teamA = Convert.ToInt32(ReadField(candidata, "teamA"));
                int teamB = Convert.ToInt32(ReadField(candidata, "teamB"));
                if ((teamA == teamAtacante && teamB == teamAlvo)
                    || (teamA == teamAlvo && teamB == teamAtacante))
                {
                    relacao = candidata;
                    break;
                }
            }

            snapshot.relationExisted = relacao != null;
            if (relacao == null)
            {
                MethodInfo obterRelacao = governoType.GetMethod(
                    "ObterRelacao", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                relacao = obterRelacao.Invoke(governo, new object[] { teamAtacante, teamAlvo });
            }

            snapshot.relation = relacao;
            snapshot.postureA = ReadField(relacao, "posturaAParaB");
            snapshot.postureB = ReadField(relacao, "posturaBParaA");
            snapshot.ceasefire = ReadField(relacao, "cessarFogoAtivo");

            Type posturaType = ResolveType("PosturaRelacaoPais");
            object inimigo = Enum.Parse(posturaType, "Inimigo");
            SetField(relacao, "posturaAParaB", inimigo);
            SetField(relacao, "posturaBParaA", inimigo);
            SetField(relacao, "cessarFogoAtivo", false);
            return snapshot;
        }
        catch
        {
            RestaurarPostura(snapshot);
            throw;
        }
    }

    private static void RestaurarPostura(DiplomacyFixtureSnapshot snapshot)
    {
        if (snapshot == null) return;
        if (snapshot.relation != null && snapshot.relationExisted)
        {
            SetField(snapshot.relation, "posturaAParaB", snapshot.postureA);
            SetField(snapshot.relation, "posturaBParaA", snapshot.postureB);
            SetField(snapshot.relation, "cessarFogoAtivo", snapshot.ceasefire);
        }
        else if (snapshot.relation != null)
        {
            snapshot.relations?.Remove(snapshot.relation);
        }

        if (snapshot.instanceProperty != null)
        {
            snapshot.instanceProperty.GetSetMethod(true)?.Invoke(null, new[] { snapshot.previousGovernment });
        }

        if (snapshot.createdGovernmentRoot != null)
        {
            UnityEngine.Object.Destroy(snapshot.createdGovernmentRoot);
        }
    }

    private static void DestruirProjeteisDoAtacante(GameObject atacante)
    {
        if (atacante == null) return;

        Type tipoProjetil = ResolveType("Projetil");
        FieldInfo ativosNoMapa = tipoProjetil.GetField("ativosNoMapa", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo obterDono = tipoProjetil.GetMethod("GetDono", BindingFlags.Instance | BindingFlags.Public);
        System.Collections.IEnumerable ativos = ativosNoMapa != null
            ? ativosNoMapa.GetValue(null) as System.Collections.IEnumerable
            : null;
        if (ativos == null || obterDono == null) return;

        System.Collections.Generic.List<GameObject> projeteisDoTeste = new System.Collections.Generic.List<GameObject>();
        foreach (object ativo in ativos)
        {
            Component projetil = ativo as Component;
            if (projetil == null || (obterDono.Invoke(projetil, null) as GameObject) != atacante) continue;
            projeteisDoTeste.Add(projetil.gameObject);
        }

        for (int i = 0; i < projeteisDoTeste.Count; i++)
        {
            if (projeteisDoTeste[i] != null)
            {
                UnityEngine.Object.Destroy(projeteisDoTeste[i]);
            }
        }
    }
}
