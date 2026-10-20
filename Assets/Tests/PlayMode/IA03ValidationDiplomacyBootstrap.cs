using System;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-32000)]
public sealed class IA03ValidationDiplomacyBootstrap : MonoBehaviour
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string ValidationScenePath = "Assets/Tests/PlayMode/IA03_WarValidation.unity";
    private const int ValdoriaTeamId = 1;
    private const int KarsoviaTeamId = 3;
    private bool firstCrossCountryDamageLogged;
    private EventInfo damageEvent;
    private Delegate damageHandler;

    private void Awake()
    {
        Debug.Log("[IA03][Validação][Bootstrap] Awake scene=" + gameObject.scene.path
            + " activeScene=" + SceneManager.GetActiveScene().path
            + " playing=" + Application.isPlaying
            + " editor=" + Application.isEditor, this);
        if (!Application.isPlaying
            || !Application.isEditor
            || !string.Equals(gameObject.scene.path, ValidationScenePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Type governoType = ResolveType("SistemaGovernoMundial");
        if (governoType == null)
        {
            Debug.LogError("[IA03][Validação] Tipo SistemaGovernoMundial indisponível.", this);
            return;
        }

        MethodInfo garantirInstancia = governoType.GetMethod("GarantirInstancia", StaticMembers);
        garantirInstancia?.Invoke(null, null);
        PropertyInfo instanciaProperty = governoType.GetProperty("Instancia", StaticMembers);
        object governo = instanciaProperty != null ? instanciaProperty.GetValue(null) : null;
        if (governo == null)
        {
            Debug.LogError("[IA03][Validação] Sistema de governo indisponível para preparar o cenário em Paz.", this);
            return;
        }

        MethodInfo obterPais = governoType.GetMethod("ObterPais", InstanceMembers);
        MethodInfo obterRelacao = governoType.GetMethod("ObterRelacao", InstanceMembers);
        if (obterPais == null || obterRelacao == null)
        {
            Debug.LogError("[IA03][Validação] API diplomática incompleta no SistemaGovernoMundial.", this);
            return;
        }

        object valdoria = obterPais.Invoke(governo, new object[] { ValdoriaTeamId });
        object karsovia = obterPais.Invoke(governo, new object[] { KarsoviaTeamId });
        object relacao = obterRelacao.Invoke(governo, new object[] { ValdoriaTeamId, KarsoviaTeamId });

        if (valdoria != null)
        {
            SetMember(valdoria, "emGuerra", false);
            SetMember(valdoria, "rivalTeamId", -1);
        }

        if (karsovia != null)
        {
            SetMember(karsovia, "emGuerra", false);
            SetMember(karsovia, "rivalTeamId", -1);
        }

        if (relacao != null)
        {
            SetMember(relacao, "valor", 0);
            SetMember(relacao, "guerraDeclarada", false);
            SetMember(relacao, "sancaoAtiva", false);
            SetMember(relacao, "cessarFogoAtivo", false);
            SetEnumMember(relacao, "posturaAParaB", "Neutro");
            SetEnumMember(relacao, "posturaBParaA", "Neutro");
        }

        governoType.GetMethod("NotificarGovernoAtualizado", InstanceMembers)?.Invoke(governo, null);

        SubscribeToFirstCrossCountryDamage();
        Debug.Log("[IA03][Validação][Bootstrap] OnDanoGlobalAssinado=" + (damageHandler != null), this);
        Debug.Log("[IA03][Validação] Valdoria e Karsovia iniciam em Paz.", this);
    }

    private void OnDestroy()
    {
        if (damageEvent == null || damageHandler == null) return;
        damageEvent.RemoveEventHandler(null, damageHandler);
        damageEvent = null;
        damageHandler = null;
    }

    private void SubscribeToFirstCrossCountryDamage()
    {
        Type damageType = ResolveType("SistemaDeDanos");
        damageEvent = damageType != null ? damageType.GetEvent("OnDanoGlobal", StaticMembers) : null;
        if (damageEvent == null || damageEvent.EventHandlerType == null) return;

        MethodInfo callback = GetType().GetMethod(nameof(LogFirstCrossCountryDamage), InstanceMembers);
        ParameterInfo[] signature = damageEvent.EventHandlerType.GetMethod("Invoke").GetParameters();
        ParameterExpression[] parameters = new ParameterExpression[signature.Length];
        Expression[] arguments = new Expression[signature.Length];
        for (int i = 0; i < signature.Length; i++)
        {
            parameters[i] = Expression.Parameter(signature[i].ParameterType, signature[i].Name);
            arguments[i] = Expression.Convert(parameters[i], typeof(object));
        }

        MethodCallExpression body = Expression.Call(Expression.Constant(this), callback, arguments);
        damageHandler = Expression.Lambda(damageEvent.EventHandlerType, body, parameters).Compile();
        damageEvent.AddEventHandler(null, damageHandler);
    }

    private void LogFirstCrossCountryDamage(object victimValue, object attackerValue, object damageValue)
    {
        if (firstCrossCountryDamageLogged) return;

        Component victim = victimValue as Component;
        GameObject attacker = attackerValue as GameObject;
        if (victim == null || attacker == null) return;

        Type identityType = ResolveType("IdentidadeUnidade");
        Component attackerIdentity = identityType != null ? attacker.GetComponentInParent(identityType) : null;
        Component victimIdentity = identityType != null ? victim.GetComponentInParent(identityType) : null;
        int attackerTeam = ReadTeamId(attackerIdentity);
        int victimTeam = ReadTeamId(victimIdentity);
        if (!((attackerTeam == ValdoriaTeamId && victimTeam == KarsoviaTeamId)
            || (attackerTeam == KarsoviaTeamId && victimTeam == ValdoriaTeamId)))
        {
            return;
        }

        firstCrossCountryDamageLogged = true;
        Type governmentType = ResolveType("SistemaGovernoMundial");
        object government = governmentType != null
            ? governmentType.GetProperty("Instancia", StaticMembers)?.GetValue(null)
            : null;
        object relation = government != null
            ? governmentType.GetMethod("ObterRelacao", InstanceMembers)?.Invoke(government, new object[] { victimTeam, attackerTeam })
            : null;
        bool guerraDepoisDoDano = Convert.ToBoolean(GetMemberValue(relation, "guerraDeclarada") ?? false);
        float damage = Convert.ToSingle(damageValue);
        Debug.Log("[IA03][Validação][PrimeiroDano] início=Paz"
            + " agressor=" + attacker.name + " equipe=" + attackerTeam
            + " vítima=" + victim.gameObject.name + " equipeVítima=" + victimTeam
            + " dano=" + damage.ToString("0.##")
            + " guerraDepois=" + guerraDepoisDoDano
            + " frame=" + Time.frameCount + " t=" + Time.time.ToString("0.##"), this);
    }

    private static int ReadTeamId(Component identity)
    {
        return identity != null ? Convert.ToInt32(GetMemberValue(identity, "teamID") ?? -1) : -1;
    }

    private static object GetMemberValue(object target, string name)
    {
        if (target == null) return null;
        FieldInfo field = target.GetType().GetField(name, InstanceMembers);
        if (field != null) return field.GetValue(target);
        PropertyInfo property = target.GetType().GetProperty(name, InstanceMembers);
        return property != null && property.CanRead ? property.GetValue(target) : null;
    }

    private static Type ResolveType(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(fullName, false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static void SetMember(object target, string name, object value)
    {
        if (target == null)
        {
            return;
        }

        FieldInfo field = target.GetType().GetField(name, InstanceMembers);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        PropertyInfo property = target.GetType().GetProperty(name, InstanceMembers);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value);
        }
    }

    private static void SetEnumMember(object target, string name, string value)
    {
        if (target == null)
        {
            return;
        }

        FieldInfo field = target.GetType().GetField(name, InstanceMembers);
        if (field != null && field.FieldType.IsEnum)
        {
            field.SetValue(target, Enum.Parse(field.FieldType, value));
        }
    }
}
