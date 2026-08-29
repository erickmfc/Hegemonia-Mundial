using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class QuartelModalPlayModeTests
{
    [UnityTest]
    public IEnumerator FecharQuartelLiberaEntradaDoMundo()
    {
        Type gerenciadorType = ResolveType("GerenciadorQuartel");
        Type menuType = ResolveType("QuartelMenuUIController");
        Type interactionType = ResolveType("InteractionModeService");
        GameObject objeto = new GameObject("QuartelModalPlayMode");

        try
        {
            Component gerenciador = objeto.AddComponent(gerenciadorType);
            yield return null;

            Invoke(gerenciadorType, gerenciador, "AlternarInterface");
            yield return null;

            PropertyInfo interfaceAberta = gerenciadorType.GetProperty(
                "InterfaceAberta",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(interfaceAberta, Is.Not.Null);
            Assert.That((bool)interfaceAberta.GetValue(null, null), Is.True,
                "O Quartel não registrou o modal aberto.");
            Assert.That(objeto.GetComponent(menuType), Is.Not.Null);

            Invoke(gerenciadorType, gerenciador, "FecharInterfacePorUI");
            yield return null;
            yield return null;

            Assert.That((bool)interfaceAberta.GetValue(null, null), Is.False,
                "O Quartel continuou marcado como aberto após o fechamento.");

            MethodInfo snapshotMethod = interactionType.GetMethod(
                "CurrentSnapshot",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(snapshotMethod, Is.Not.Null);
            object snapshot = snapshotMethod.Invoke(null, null);
            FieldInfo owner = snapshot.GetType().GetField(
                "Owner",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(owner, Is.Not.Null);
            Assert.That(owner.GetValue(snapshot).ToString(), Is.EqualTo("None"),
                "O bloqueio do Quartel ficou preso depois de fechar a Carta.");
        }
        finally
        {
            UnityEngine.Object.Destroy(objeto);
        }
    }

    private static Type ResolveType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Tipo não encontrado: " + name);
        return type;
    }

    private static object Invoke(Type type, object target, string methodName)
    {
        MethodInfo method = type.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Método não encontrado: " + methodName);
        return method.Invoke(target, null);
    }
}
