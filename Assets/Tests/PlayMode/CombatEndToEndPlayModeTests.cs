using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CombatEndToEndPlayModeTests
{
    [UnityTest]
    public IEnumerator EnemyTurretIdentifiesTargetFiresProjectileAndAppliesDamage()
    {
        GameObject alvo = new GameObject("AlvoJogadorCombate");
        GameObject atirador = new GameObject("TorreInimigaCombate");
        GameObject cano = new GameObject("CanoCombate");
        GameObject projetilPrefab = new GameObject("ProjetilCombatePrefab");
        GameObject donoDoProjetil = null;

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
            UnityEngine.Object.Destroy(alvo);
            UnityEngine.Object.Destroy(atirador);
            UnityEngine.Object.Destroy(projetilPrefab);
            UnityEngine.Object.Destroy(cano);
        }
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
}
