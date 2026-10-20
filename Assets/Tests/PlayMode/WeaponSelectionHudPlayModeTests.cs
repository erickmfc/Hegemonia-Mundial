using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class WeaponSelectionHudPlayModeTests
{
    [UnityTest]
    public IEnumerator NavalLauncherHudSelectionChangesActualMissileAndTorpedoChoice()
    {
        GameObject launcherObject = new GameObject("HudWeaponLauncherTest");
        GameObject trident = new GameObject("TridentPrefabTest");
        GameObject cruise = new GameObject("CruisePrefabTest");
        GameObject torpedo = new GameObject("TorpedoPrefabTest");
        try
        {
            Component launcher = AddRuntimeComponent(launcherObject, "LancadorNaval");
            SetMember(launcher, "prefabMissel", trident);
            SetMember(launcher, "prefabMisselCruzeiro", cruise);
            SetMember(launcher, "prefabTorpedo", torpedo);
            AddRuntimeComponent(torpedo, "Torpedo");
            torpedo.SetActive(false);

            Type launcherType = launcher.GetType();
            object cruiseType = EnumValue(launcherType, "TipoMissilNaval", "Kh23Cruzeiro");
            Assert.IsTrue((bool)Invoke(launcher, "DefinirTipoMissilHud", cruiseType));
            Assert.AreSame(cruise, GetMember(launcher, "PrefabMissilSelecionado"));
            Assert.AreEqual(EnumValue(launcherType, "ArmamentoHud", "MissilNaval"),
                GetMember(launcher, "ArmamentoSelecionadoHud"));

            Assert.IsTrue((bool)Invoke(launcher, "DefinirArmamentoHud",
                EnumValue(launcherType, "ArmamentoHud", "Torpedo")));
            Assert.AreEqual(EnumValue(launcherType, "ArmamentoHud", "Torpedo"),
                GetMember(launcher, "ArmamentoSelecionadoHud"));
            Assert.IsTrue((bool)Invoke(launcher, "DefinirArmamentoHud",
                EnumValue(launcherType, "ArmamentoHud", "Automatico")));
            Assert.AreEqual(EnumValue(launcherType, "ArmamentoHud", "Automatico"),
                GetMember(launcher, "ArmamentoSelecionadoHud"));
        }
        finally
        {
            UnityEngine.Object.Destroy(launcherObject);
            UnityEngine.Object.Destroy(trident);
            UnityEngine.Object.Destroy(cruise);
            UnityEngine.Object.Destroy(torpedo);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator ExplicitMissileChoiceDoesNotFallBackToAvailableTorpedoes()
    {
        GameObject launcherObject = new GameObject("HudWeaponNoFallbackTest");
        GameObject missile = new GameObject("MissilePrefabTest");
        GameObject torpedo = new GameObject("TorpedoPrefabTest");
        GameObject target = new GameObject("NavalTargetTest");
        try
        {
            Component launcher = AddRuntimeComponent(launcherObject, "LancadorNaval");
            SetMember(launcher, "modoAtual", EnumValue(launcher.GetType(), "ModoOperacao", "Manual"));
            SetMember(launcher, "prefabMissel", missile);
            SetMember(launcher, "prefabTorpedo", torpedo);
            AddRuntimeComponent(torpedo, "Torpedo");
            torpedo.SetActive(false);
            SetMember(launcher, "municaoTotal", 0);
            SetMember(launcher, "torpedosTotal", 4);
            SetMember(launcher, "tempoRecargaSalva", 0f);
            Invoke(launcher, "DefinirArmamentoHud", EnumValue(launcher.GetType(), "ArmamentoHud", "MissilNaval"));
            SetMember(AddRuntimeComponent(target, "IdentidadeUnidade"), "tipoUnidade",
                EnumValue(FindRuntimeType("TipoUnidade"), null, "Naval"));

            yield return null;

            object[] args = { target.transform.position, target.transform, false, null };
            bool pode = (bool)Invoke(launcher, "PodeLancarCoordenado", args);
            string motivo = args[3] as string;
            Assert.IsFalse(pode, "A escolha explícita de míssil não deve lançar torpedo como alternativa.");
            StringAssert.Contains("missil naval selecionado", motivo);
            Assert.AreEqual(4, GetMember(launcher, "torpedosTotal"),
                "A validação não pode consumir torpedos como fallback.");
        }
        finally
        {
            UnityEngine.Object.Destroy(launcherObject);
            UnityEngine.Object.Destroy(missile);
            UnityEngine.Object.Destroy(torpedo);
            UnityEngine.Object.Destroy(target);
        }
    }

    [UnityTest]
    public IEnumerator SubmarineTorpedoChoiceRejectsUnavailableTorpedoInsteadOfUsingMissile()
    {
        GameObject submarineObject = new GameObject("HudWeaponSubmarineTest");
        GameObject missile = new GameObject("SubmarineMissilePrefabTest");
        GameObject torpedoPrefab = new GameObject("SubmarineTorpedoPrefabTest");
        GameObject tube = new GameObject("SubmarineTorpedoTubeTest");
        GameObject target = new GameObject("SubmarineNavalTargetTest");
        try
        {
            Component submarine = AddRuntimeComponent(submarineObject, "ControleSubmarino");
            SetMember(submarine, "prefabMisselSubmarino", missile);
            SetMember(submarine, "prefabTorpedo", torpedoPrefab);
            AddRuntimeComponent(torpedoPrefab, "Torpedo");
            torpedoPrefab.SetActive(false);
            SetMember(submarine, "tubosTorpedo", new[] { tube.transform });
            SetMember(AddRuntimeComponent(target, "IdentidadeUnidade"), "tipoUnidade",
                EnumValue(FindRuntimeType("TipoUnidade"), null, "Naval"));

            yield return null;

            Type submarineType = submarine.GetType();
            SetMember(submarine, "modoAtual", EnumValue(submarineType, "ModoOperacao", "Manual"));
            SetMember(submarine, "misseisDisponiveis", 4);
            SetMember(submarine, "torpedosDisponiveis", 0);
            Assert.IsTrue((bool)Invoke(submarine, "DefinirArmamentoHud",
                EnumValue(submarineType, "ArmamentoHud", "Torpedo")));
            object[] args = { target.transform.position, target.transform, false, null };
            bool pode = (bool)Invoke(submarine, "PodeLancarCoordenado", args);
            string motivo = args[3] as string;

            Assert.IsFalse(pode, "O submarino não deve usar seu míssil quando o jogador selecionou torpedo.");
            StringAssert.Contains("torpedo", motivo);
            Assert.AreEqual(4, GetMember(submarine, "misseisDisponiveis"));
        }
        finally
        {
            UnityEngine.Object.Destroy(submarineObject);
            UnityEngine.Object.Destroy(missile);
            UnityEngine.Object.Destroy(torpedoPrefab);
            UnityEngine.Object.Destroy(tube);
            UnityEngine.Object.Destroy(target);
        }
    }

    [UnityTest]
    public IEnumerator ShipTurretCanSelectItsInstalledMissileAndCannon()
    {
        GameObject turretObject = new GameObject("HudWeaponShipTurretTest");
        GameObject cannonAmmo = new GameObject("ShipGunAmmoPrefabTest");
        GameObject missile = new GameObject("ShipAirDefenseMissilePrefabTest");
        try
        {
            Component turret = AddRuntimeComponent(turretObject, "ControleTorreta");
            SetMember(turret, "municaoPrefab", cannonAmmo);
            SetMember(turret, "misselPrefab", missile);
            Type turretType = turret.GetType();

            Assert.IsTrue((bool)Invoke(turret, "DefinirArmamentoHud",
                EnumValue(turretType, "ArmamentoHud", "Missil")));
            Assert.AreEqual(EnumValue(turretType, "ArmamentoHud", "Missil"),
                GetMember(turret, "ArmamentoSelecionadoHud"));
            Assert.IsTrue((bool)Invoke(turret, "DefinirArmamentoHud",
                EnumValue(turretType, "ArmamentoHud", "Canhao")));
            Assert.AreEqual(EnumValue(turretType, "ArmamentoHud", "Canhao"),
                GetMember(turret, "ArmamentoSelecionadoHud"));
            Assert.IsTrue((bool)Invoke(turret, "DefinirArmamentoHud",
                EnumValue(turretType, "ArmamentoHud", "Automatico")));
            Assert.AreEqual(EnumValue(turretType, "ArmamentoHud", "Automatico"),
                GetMember(turret, "ArmamentoSelecionadoHud"));
        }
        finally
        {
            UnityEngine.Object.Destroy(turretObject);
            UnityEngine.Object.Destroy(cannonAmmo);
            UnityEngine.Object.Destroy(missile);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator ModularTurretUsesOnlyTheSelectedInstalledWeapon()
    {
        GameObject turretObject = new GameObject("HudWeaponModularTurretTest");
        GameObject gunPrefab = new GameObject("GunAmmoPrefabTest");
        GameObject missilePrefab = new GameObject("ModularMissilePrefabTest");
        GameObject muzzle = new GameObject("WeaponMuzzleTest");
        try
        {
            Component turret = AddRuntimeComponent(turretObject, "ControleTorretaModular");
            Type moduleType = FindRuntimeType("ModuloArma");
            object gun = Activator.CreateInstance(moduleType);
            object missile = Activator.CreateInstance(moduleType);
            ConfigureModule(gun, "Canhão de teste", gunPrefab, muzzle.transform, 10, 10, 1f);
            ConfigureModule(missile, "Míssil de teste", missilePrefab, muzzle.transform, 2, 2, 100f);

            IList installed = GetMember(turret, "armas") as IList;
            Assert.That(installed, Is.Not.Null);
            installed.Add(gun);
            installed.Add(missile);
            SetMember(turret, "prioridade", EnumValue(turret.GetType(), "PrioridadeArma", "MaisDano"));

            Assert.IsTrue((bool)Invoke(turret, "DefinirArmaHud", 0));
            MethodInfo selecionar = turret.GetType().GetMethod("SelecionarArma", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(selecionar, Is.Not.Null);
            Assert.AreSame(gun, selecionar.Invoke(turret, null),
                "A prioridade automática não deve substituir a escolha explícita.");

            SetMember(gun, "municaoAtual", 0);
            Assert.IsNull(selecionar.Invoke(turret, null), "Sem munição, não deve disparar outra arma por fallback.");
            Assert.IsTrue((bool)Invoke(turret, "DefinirArmaHud", -1));
            Assert.AreSame(missile, selecionar.Invoke(turret, null),
                "Ao voltar para Automático, a prioridade configurada volta a valer.");
        }
        finally
        {
            UnityEngine.Object.Destroy(turretObject);
            UnityEngine.Object.Destroy(gunPrefab);
            UnityEngine.Object.Destroy(missilePrefab);
            UnityEngine.Object.Destroy(muzzle);
        }
        yield return null;
    }

    private static void ConfigureModule(object module, string name, GameObject prefab,
        Transform muzzle, int magazine, int ammunition, float damage)
    {
        SetMember(module, "nomeArma", name);
        SetMember(module, "municaoPrefab", prefab);
        SetMember(module, "pontosDisparo", new[] { muzzle });
        SetMember(module, "tamanhoCartucho", magazine);
        SetMember(module, "municaoAtual", ammunition);
        SetMember(module, "danoBase", damage);
    }

    private static Component AddRuntimeComponent(GameObject target, string typeName)
    {
        Type type = FindRuntimeType(typeName);
        Assert.That(type, Is.Not.Null, "Runtime type missing: " + typeName);
        return target.AddComponent(type);
    }

    private static Type FindRuntimeType(string typeName)
    {
        return Type.GetType(typeName + ", Assembly-CSharp", false);
    }

    private static object EnumValue(Type ownerType, string enumName, string value)
    {
        Assert.That(ownerType, Is.Not.Null);
        Type enumType = ownerType.IsEnum ? ownerType : ownerType.GetNestedType(enumName,
            BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(enumType, Is.Not.Null, "Enum missing: " + enumName + " on " + ownerType.Name);
        return Enum.Parse(enumType, value);
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        Assert.That(target, Is.Not.Null);
        MethodInfo method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Method missing: " + methodName);
        return method.Invoke(target, args);
    }

    private static object GetMember(object target, string memberName)
    {
        Type type = target.GetType();
        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null) return field.GetValue(target);
        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null, "Field/property missing: " + memberName);
        return property.GetValue(target, null);
    }

    private static void SetMember(object target, string memberName, object value)
    {
        Type type = target.GetType();
        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null, "Writable field/property missing: " + memberName);
        Assert.That(property.CanWrite, Is.True, "Property is read-only: " + memberName);
        property.SetValue(target, value, null);
    }
}
