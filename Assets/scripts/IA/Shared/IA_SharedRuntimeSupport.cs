using System;
using System.Collections.Generic;
using System.Reflection;
using Hegemonia.AI.BrainMaster;
using UnityEngine;
using Hegemonia.AI.Master;

namespace Hegemonia.AI.Shared
{
    public enum IA_RuntimeStackKind
    {
        Unknown = 0,
        BrainMaster = 1,
        Sovereign = 2,
        Nova = 3,
        Legacy = 4,
        Observer = 5,
        Support = 6
    }

    public static class IA_SharedRuntimeSupport
    {
        public const int BrainMasterAuthorityPriority = 300;
        public const int SovereignAuthorityPriority = 220;
        public const int NovaAuthorityPriority = 180;
        public const int LegacyAuthorityPriority = 100;
        public const int ObserverAuthorityPriority = 40;

        public static readonly Type[] LegacyStackTypes =
        {
            typeof(global::CerebroIA),
        };

        public static bool IsBrainMasterMode
        {
            get { return IA_ModeSwitch.CurrentMode == IA_ModeSwitch.AIStackMode.BrainMaster; }
        }

        public static bool IsNovaIAMode
        {
            get { return IA_ModeSwitch.CurrentMode == IA_ModeSwitch.AIStackMode.NovaIA; }
        }

        public static bool IsLegacyMode
        {
            get { return IA_ModeSwitch.CurrentMode == IA_ModeSwitch.AIStackMode.Legacy; }
        }

        public static bool IsStackAllowedInCurrentMode(string typeName)
        {
            return IsStackAllowedInMode(typeName, IA_ModeSwitch.CurrentMode);
        }

        public static bool IsStackAllowedInMode(string typeName, IA_ModeSwitch.AIStackMode mode)
        {
            switch (ResolveStackKindValue(typeName))
            {
                case IA_RuntimeStackKind.BrainMaster:
                case IA_RuntimeStackKind.Observer:
                    return mode == IA_ModeSwitch.AIStackMode.BrainMaster;
                case IA_RuntimeStackKind.Sovereign:
                    return mode == IA_ModeSwitch.AIStackMode.BrainMaster;
                case IA_RuntimeStackKind.Nova:
                    return mode == IA_ModeSwitch.AIStackMode.NovaIA;
                case IA_RuntimeStackKind.Legacy:
                    return mode == IA_ModeSwitch.AIStackMode.Legacy;
                case IA_RuntimeStackKind.Support:
                case IA_RuntimeStackKind.Unknown:
                default:
                    return true;
            }
        }

        public static bool IsLegacyAiType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            return typeName.Contains("CerebroIA");
        }

        public static int ResolveAuthorityPriority(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return 0;
            }

            if (typeName.Contains("Hegemonia.AI.BrainMaster.IA_BrainMaster"))
            {
                return BrainMasterAuthorityPriority;
            }

            if (typeName.Contains("Hegemonia.AI.Sovereign.AISovereignController"))
            {
                return SovereignAuthorityPriority;
            }

            if (typeName.Contains("Hegemonia.AI.Master.IA_MasterController"))
            {
                return NovaAuthorityPriority;
            }

            if (typeName.Contains("IA_DeusaBrain"))
            {
                return ObserverAuthorityPriority;
            }

            if (IsLegacyAiType(typeName))
            {
                return LegacyAuthorityPriority;
            }

            return 0;
        }

        public static string ResolveStackKind(string typeName)
        {
            return ResolveStackKindValue(typeName).ToString().ToLowerInvariant();
        }

        public static IA_RuntimeStackKind ResolveStackKindValue(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return IA_RuntimeStackKind.Unknown;
            }

            if (typeName.Contains("Hegemonia.AI.BrainMaster.IA_BrainMaster"))
            {
                return IA_RuntimeStackKind.BrainMaster;
            }

            if (typeName.Contains("Hegemonia.AI.Sovereign.AISovereignController"))
            {
                return IA_RuntimeStackKind.Sovereign;
            }

            if (typeName.Contains("Hegemonia.AI.Master.IA_MasterController"))
            {
                return IA_RuntimeStackKind.Nova;
            }

            if (IsLegacyAiType(typeName))
            {
                return IA_RuntimeStackKind.Legacy;
            }

            if (typeName.Contains("IA_DeusaBrain"))
            {
                return IA_RuntimeStackKind.Observer;
            }

            return IA_RuntimeStackKind.Support;
        }

        public static bool IsCommandAuthorityType(string typeName)
        {
            return ResolveAuthorityPriority(typeName) >= NovaAuthorityPriority;
        }

        public static string CanonicalizeActionKey(string key)
        {
            string normalized = IA_Text.Normalize(key);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            if (normalized.Contains("prefeitura") || normalized.Contains("governo") || normalized.Contains("capital") || normalized.Contains("city hall") || normalized.Contains("town hall"))
            {
                return "prefeitura";
            }

            if (normalized.Contains("quartel general") || normalized.Contains("quartel_general") || normalized == "hq" || normalized.Contains("headquarters"))
            {
                return "quartel general";
            }

            if (normalized.Contains("quartel") || normalized.Contains("tenda") || normalized.Contains("barraca") || normalized.Contains("infantaria") || normalized.Contains("barracks"))
            {
                return "tenda militar";
            }

            if (normalized.Contains("construtor de veiculos") || normalized.Contains("fabrica") || normalized.Contains("factory"))
            {
                return "construtor de veiculos";
            }

            if (normalized.Contains("armazem") || normalized.Contains("warehouse") || normalized.Contains("galpao"))
            {
                return "armazem";
            }

            if (normalized.Contains("radar"))
            {
                return "torre de radar";
            }

            if (normalized.Contains("torreta") || normalized.Contains("sentinela"))
            {
                return "torreta";
            }

            if (normalized.Contains("ciws") || normalized.Contains("phalanx") || normalized.Contains("antia") || normalized.Contains("ares"))
            {
                return "ciws";
            }

            if (normalized.Contains("muro") || normalized.Contains("wall"))
            {
                return "muro de concreto";
            }

            if (normalized.Contains("estaleiro") || normalized.Contains("shipyard"))
            {
                return "estaleiro naval";
            }

            if (normalized.Contains("aeroporto militar") || normalized.Contains("base aerea militar") || normalized.Contains("military airport"))
            {
                return "aeroporto militar";
            }

            if (normalized.Contains("aeroporto comercial") || normalized.Contains("aeroporto_comercial") || normalized.Contains("aeroportocomercial") || normalized.Contains("commercial airport"))
            {
                return "aeroporto comercial";
            }

            if (normalized.Contains("aeroporto") || normalized.Contains("airport") || normalized.Contains("base aerea") || normalized.Contains("pista"))
            {
                return "aeroporto";
            }

            if (normalized.Contains("heliporto") || normalized.Contains("heliport"))
            {
                return "heliporto";
            }

            if (normalized.Contains("plataforma"))
            {
                return "plataforma";
            }

            if (normalized.Contains("petroleiro") || normalized.Contains("petrolifero") || normalized.Contains("oil tanker") || normalized.Contains("tanker"))
            {
                return "petroleiro";
            }

            if (normalized.Contains("lancador") || normalized.Contains("missil") || normalized.Contains("silo"))
            {
                return "lancador de misseis";
            }

            if (normalized.Contains("imovel") || normalized.Contains("casa")
                || normalized.Contains("moradia") || normalized.Contains("residencia")
                || normalized.Contains("habitacao") || normalized.Contains("house")
                || normalized.Contains("apartamento"))
            {
                return "imovel";
            }

            if (normalized.Contains("village") || normalized.Contains("aldeia")
                || normalized.Contains("predio") || normalized.Contains("edificio")
                || normalized.Contains("vila"))
            {
                return "village";
            }

            return normalized;
        }

        public static string BuildCommandDedupKey(string family, string seed, object payload = null)
        {
            string normalizedFamily = IA_Text.Normalize(family);
            if (string.IsNullOrEmpty(normalizedFamily))
            {
                normalizedFamily = "acao";
            }

            string resolvedSeed = ResolveCommandDedupSeed(payload, seed);
            if (string.IsNullOrEmpty(resolvedSeed))
            {
                return normalizedFamily;
            }

            return normalizedFamily + ":" + resolvedSeed;
        }

        private static string ResolveCommandDedupSeed(object payload, string seed)
        {
            string candidate = string.Empty;

            IA_BuildOrderData build = payload as IA_BuildOrderData;
            if (build != null)
            {
                candidate = build.ItemKey;
            }

            IA_ProduceOrderData produce = payload as IA_ProduceOrderData;
            if (produce != null && string.IsNullOrEmpty(candidate))
            {
                candidate = produce.ItemKey;
            }

            IA_AbilityOrderData ability = payload as IA_AbilityOrderData;
            if (ability != null && string.IsNullOrEmpty(candidate))
            {
                candidate = ability.AbilityKey;
            }

            if (string.IsNullOrEmpty(candidate))
            {
                candidate = seed;
            }

            int lastSeparator = candidate.LastIndexOf(':');
            if (lastSeparator >= 0 && lastSeparator < candidate.Length - 1)
            {
                candidate = candidate.Substring(lastSeparator + 1);
            }

            candidate = CanonicalizeActionKey(candidate);
            if (string.IsNullOrEmpty(candidate))
            {
                return string.Empty;
            }

            CatalogoProdutoUnificadoItem catalogItem;
            if (CatalogoProdutoCompartilhado.TentarObter(candidate, out catalogItem) && catalogItem != null && !string.IsNullOrEmpty(catalogItem.id))
            {
                return catalogItem.id;
            }

            return candidate;
        }

        public static bool BelongsToTeam(Component behaviour, int teamId)
        {
            if (behaviour == null)
            {
                return false;
            }

            global::IdentidadeUnidade unidade = behaviour.GetComponent<global::IdentidadeUnidade>();
            if (unidade == null)
            {
                unidade = behaviour.GetComponentInParent<global::IdentidadeUnidade>();
            }

            if (unidade != null)
            {
                return unidade.teamID == teamId;
            }

            global::IdentidadeIA identidadeIA = behaviour.GetComponent<global::IdentidadeIA>();
            if (identidadeIA == null)
            {
                identidadeIA = behaviour.GetComponentInParent<global::IdentidadeIA>();
            }

            if (identidadeIA != null)
            {
                return identidadeIA.teamID == teamId;
            }

            FieldInfo teamField = behaviour.GetType().GetField("teamID", BindingFlags.Public | BindingFlags.Instance)
                ?? behaviour.GetType().GetField("TeamID", BindingFlags.Public | BindingFlags.Instance)
                ?? behaviour.GetType().GetField("TeamId", BindingFlags.Public | BindingFlags.Instance)
                ?? behaviour.GetType().GetField("teamID_Inimigo", BindingFlags.Public | BindingFlags.Instance)
                ?? behaviour.GetType().GetField("teamIDInimigo", BindingFlags.Public | BindingFlags.Instance);
            if (teamField != null && teamField.FieldType == typeof(int))
            {
                return (int)teamField.GetValue(behaviour) == teamId;
            }

            PropertyInfo teamProperty = behaviour.GetType().GetProperty("TeamId", BindingFlags.Public | BindingFlags.Instance)
                ?? behaviour.GetType().GetProperty("TeamID", BindingFlags.Public | BindingFlags.Instance);
            if (teamProperty != null && teamProperty.PropertyType == typeof(int) && teamProperty.CanRead)
            {
                return (int)teamProperty.GetValue(behaviour, null) == teamId;
            }

            return false;
        }

        public static bool BelongsToTeam(GameObject gameObject, int teamId)
        {
            if (gameObject == null)
            {
                return false;
            }

            global::IdentidadeUnidade unidade = gameObject.GetComponent<global::IdentidadeUnidade>();
            if (unidade == null)
            {
                unidade = gameObject.GetComponentInParent<global::IdentidadeUnidade>();
            }

            if (unidade != null)
            {
                return unidade.teamID == teamId;
            }

            global::IdentidadeIA identidadeIA = gameObject.GetComponent<global::IdentidadeIA>();
            if (identidadeIA == null)
            {
                identidadeIA = gameObject.GetComponentInParent<global::IdentidadeIA>();
            }

            return identidadeIA != null && identidadeIA.teamID == teamId;
        }

        public static bool TryResolveObserverMode(Type enumType, out object shadow)
        {
            shadow = null;
            if (enumType == null || !enumType.IsEnum)
            {
                return false;
            }

            string[] names = Enum.GetNames(enumType);
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], "ShadowReadOnly", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(names[i], "Observer", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(names[i], "ObserveOnly", StringComparison.OrdinalIgnoreCase))
                {
                    shadow = Enum.Parse(enumType, names[i]);
                    return true;
                }
            }

            return false;
        }
    }

    public static class IA_UnitySearch
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        public static T[] FindAll<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        }

        public static T FindFirst<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindFirstObjectByType<T>();
        }
#else
        public static T[] FindAll<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsOfType<T>();
        }

        public static T FindFirst<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectOfType<T>();
        }
#endif
    }

    [Serializable]
    public sealed class ContatoNavalIA
    {
        public Transform alvo;
        public Vector3 posicao;
        public float ultimaDeteccao = -999f;
        public int forcaEstimativa;
        public bool altoValor;

        public bool EstaAtivo(float janelaSegundos = 12f)
        {
            return Time.time - ultimaDeteccao <= janelaSegundos && posicao != Vector3.zero;
        }

        public void Limpar()
        {
            alvo = null;
            posicao = Vector3.zero;
            ultimaDeteccao = -999f;
            forcaEstimativa = 0;
            altoValor = false;
        }
    }

    [Serializable]
    public sealed class GrupoNavalIA
    {
        public readonly List<GameObject> carriers = new List<GameObject>();
        public readonly List<GameObject> transportes = new List<GameObject>();
        public readonly List<GameObject> escoltasMissil = new List<GameObject>();
        public readonly List<GameObject> patrulhas = new List<GameObject>();
        public readonly List<GameObject> submarinos = new List<GameObject>();
        public readonly List<GameObject> logisticos = new List<GameObject>();

        public void Limpar()
        {
            carriers.Clear();
            transportes.Clear();
            escoltasMissil.Clear();
            patrulhas.Clear();
            submarinos.Clear();
            logisticos.Clear();
        }

        public int TotalTransportes()
        {
            return transportes.Count;
        }

        public int TotalEscoltas()
        {
            return escoltasMissil.Count + patrulhas.Count + submarinos.Count;
        }
    }

    [Serializable]
    public sealed class IA_ControllerState<TSeverity>
    {
        public int TeamId;
        public TSeverity StableSeverity;
        public int EscalateVotes;
        public int RelaxVotes;
    }
}
