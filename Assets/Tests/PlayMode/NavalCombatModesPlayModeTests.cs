using System.Collections;
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class NavalCombatModesPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void E3ContactAgeIsSharedAndExpiredTracksCannotEnterAutomaticFire()
    {
        Type e3 = ResolveType("BoeingE3Reconhecimento");
        Type contactType = e3.GetNestedType("ContatoReconhecimento", BindingFlags.Public);
        object contact = Activator.CreateInstance(contactType);
        Set(contact, "ultimaAtualizacao", 70f);
        Set(contact, "validadeAte", 100f);

        string valid = (string)e3.GetMethod("ObterEstadoContato", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { contact, 80f });
        string stale = (string)e3.GetMethod("ObterEstadoContato", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { contact, 90f });
        string lost = (string)e3.GetMethod("ObterEstadoContato", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { contact, 101f });

        Type quartel = ResolveType("GerenciadorQuartel");
        var automaticoValido = quartel.GetMethod("ContatoE3ValidoParaAutomatico", BindingFlags.Public | BindingFlags.Static);
        Assert.AreEqual("VALIDO", valid);
        Assert.AreEqual("DESATUALIZADO", stale);
        Assert.AreEqual("PERDIDO", lost);
        Assert.IsTrue((bool)automaticoValido.Invoke(null, new object[] { valid, 100f, 80f }));
        Assert.IsFalse((bool)automaticoValido.Invoke(null, new object[] { stale, 100f, 90f }));
        Assert.IsFalse((bool)automaticoValido.Invoke(null, new object[] { lost, 100f, 101f }));
    }

    [UnityTest]
    public IEnumerator SharedModesKeepMainGunsManualAndAntiairActive()
    {
        GameObject ship = new GameObject("NavioComandoCombatePlayMode");
        try
        {
            Component identity = Add(ship, "IdentidadeUnidade");
            Set(identity, "teamID", 1);
            GameObject mainGunObject = new GameObject("Canhao_Principal");
            mainGunObject.transform.SetParent(ship.transform, false);
            Component mainGun = Add(mainGunObject, "ControleTorreta");
            Set(mainGun, "etiquetaAlvo", "Naval");

            GameObject antiAirObject = new GameObject("Torreta_Antiaerea");
            antiAirObject.transform.SetParent(ship.transform, false);
            Component antiAirGun = Add(antiAirObject, "ControleTorreta");
            Component dedicatedAntiAir = Add(antiAirObject, "TorretaAntiaerea");

            Component launcher = Add(ship, "LancadorNaval");
            Component submarine = Add(ship, "ControleSubmarino");
            Component control = Add(ship, "ControleUnidade");
            Set(control, "selecionado", true);
            yield return null;
            yield return null;

            Assert.IsTrue((bool)ReadProperty(antiAirGun, "EhAntiAereo"));

            CiclarModo(control); // Automatico -> Passivo
            Assert.AreEqual("Passivo", Read(launcher, "modoAtual").ToString());
            Assert.AreEqual("Passivo", Read(submarine, "modoAtual").ToString());
            Assert.IsFalse((bool)ReadProperty(control, "ModoCombateAtivo"));
            Assert.IsTrue((bool)Read(mainGun, "modoPassivo"));
            Assert.IsTrue((bool)Read(antiAirGun, "modoPassivo"));
            Assert.IsTrue((bool)Read(dedicatedAntiAir, "modoPassivo"));

            CiclarModo(control); // Passivo -> Manual
            Assert.AreEqual("Manual", Read(launcher, "modoAtual").ToString());
            Assert.AreEqual("Manual", Read(submarine, "modoAtual").ToString());
            Assert.IsTrue((bool)ReadProperty(control, "ModoCombateAtivo"));
            Assert.IsFalse((bool)Read(mainGun, "modoPassivo"));
            Assert.IsTrue((bool)Read(mainGun, "modoManualNave"));
            Assert.IsFalse((bool)Read(antiAirGun, "modoPassivo"));
            Assert.IsFalse((bool)Read(dedicatedAntiAir, "modoPassivo"));

            CiclarModo(control); // Manual -> Automatico
            Assert.AreEqual("Automatico", Read(launcher, "modoAtual").ToString());
            Assert.AreEqual("Automatico", Read(submarine, "modoAtual").ToString());
            Assert.IsFalse((bool)Read(mainGun, "modoManualNave"));
            Assert.IsFalse((bool)Read(dedicatedAntiAir, "modoPassivo"));
        }
        finally
        {
            UnityEngine.Object.Destroy(ship);
        }
    }

    [UnityTest]
    public IEnumerator SurfaceShipWithoutLauncherStillGetsManualGunAndActiveAntiair()
    {
        GameObject ship = new GameObject("NavioSemLancadorPlayMode");
        try
        {
            Component identity = Add(ship, "IdentidadeUnidade");
            Set(identity, "teamID", 1);
            GameObject mainGunObject = new GameObject("Canhao_Principal");
            mainGunObject.transform.SetParent(ship.transform, false);
            Component mainGun = Add(mainGunObject, "ControleTorreta");
            Set(mainGun, "etiquetaAlvo", "Naval");
            GameObject antiAirObject = new GameObject("Torreta_Antiaerea");
            antiAirObject.transform.SetParent(ship.transform, false);
            Component antiAir = Add(antiAirObject, "TorretaAntiaerea");
            Component control = Add(ship, "ControleUnidade");
            Set(control, "selecionado", true);
            yield return null;

            CiclarModo(control); // Automatico -> Passivo
            CiclarModo(control); // Passivo -> Manual
            Assert.IsFalse((bool)Read(mainGun, "modoPassivo"));
            Assert.IsTrue((bool)Read(mainGun, "modoManualNave"));
            Assert.IsFalse((bool)Read(antiAir, "modoPassivo"));
        }
        finally
        {
            UnityEngine.Object.Destroy(ship);
        }
    }

    private static void CiclarModo(Component controle)
    {
        Type menu = ResolveType("MenuCombateNaval");
        menu.GetMethod("CiclarModoCombate", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { controle });
    }

    private static Component Add(GameObject target, string typeName)
    {
        return target.AddComponent(ResolveType(typeName));
    }

    private static Type ResolveType(string name)
    {
        return System.AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .First(type => type != null);
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, Members).SetValue(target, value);
    }

    private static object Read(object target, string name)
    {
        return target.GetType().GetField(name, Members).GetValue(target);
    }

    private static object ReadProperty(object target, string name)
    {
        return target.GetType().GetProperty(name, Members).GetValue(target, null);
    }
}
