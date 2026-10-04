using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SistemaDeDanosRegistryTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            if (objects[i] != null)
            {
                Object.DestroyImmediate(objects[i]);
            }
        }

        objects.Clear();
    }

    [Test]
    public void MissingAggressorUsesNearestHostileFromRegisteredEntities()
    {
        Vector3 center = new Vector3(100000f, 0f, 100000f);
        GameObject victimObject = CreateIdentity("DamageVictim", 3, TipoUnidade.Veiculo, center);
        CreateIdentity("DamageFriendly", 3, TipoUnidade.Veiculo, center + Vector3.right);
        CreateIdentity("DamageStructure", 2, TipoUnidade.Estrutura, center + Vector3.right * 2f);
        GameObject hostileObject = CreateIdentity("DamageHostile", 2, TipoUnidade.Infantaria, center + Vector3.right * 3f);
        SistemaDeDanos damage = victimObject.AddComponent<SistemaDeDanos>();

        MethodInfo inferirAgressor = typeof(SistemaDeDanos).GetMethod(
            "InferirAgressorProximo",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(inferirAgressor, Is.Not.Null);
        GameObject aggressor = (GameObject)inferirAgressor.Invoke(damage, null);
        Assert.That(aggressor, Is.SameAs(hostileObject));
    }

    private GameObject CreateIdentity(string objectName, int teamId, TipoUnidade type, Vector3 position)
    {
        GameObject instance = new GameObject(objectName);
        instance.transform.position = position;
        objects.Add(instance);

        IdentidadeUnidade identity = instance.AddComponent<IdentidadeUnidade>();
        identity.teamID = teamId;
        identity.tipoUnidade = type;
        return instance;
    }
}
