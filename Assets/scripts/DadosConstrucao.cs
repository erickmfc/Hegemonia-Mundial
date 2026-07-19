using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.IA01;
using Hegemonia.Menus.Comandos;
using UnityEngine;

// 1. O ESQUELETO DA FICHA TÉCNICA (ScriptableObject)
// Isso cria uma nova opção no menu do Unity: botão direito > Create > Hegemonia > Ficha de Construcao
[CreateAssetMenu(fileName = "NovaConstrucao", menuName = "Hegemonia/Ficha de Construcao")]
public class DadosConstrucao : ScriptableObject
{
    public enum CategoriaItem
    {
        Exercito,       // Tropas terrestres (antigo Militar)
        Marinha,        // Navios e unidades navais
        Aeronautica,    // Aviões e helicópteros
        Tecnologia,     // Pesquisas e upgrades (novo nome para Militar)
        Infraestrutura,
        Energia,
        Urbana
    }

    public enum EscalaPoder
    {
        NaoClassificado = 0,
        S = 1,
        A = 2,
        B = 3,
        C = 4,
        D = 5
    }

    [Header("Informações Básicas")]
    public string nomeItem = "Nome da Unidade";
    [TextArea] public string descricao = "Descrição curta...";
    public Sprite icone; // A foto que vai no botão

    [Header("Identidade IA")]
    [Tooltip("ID estável usado pela IA. Se vazio, o nome da ficha é usado.")]
    public string itemId = string.Empty;
    [Tooltip("Aliases extras usados pela IA, separados por virgula, ; ou quebra de linha.")]
    [TextArea(1, 4)] public string aliases = string.Empty;
    [Tooltip("Capacidades explícitas. Se Auto, a IA infere pelo prefab e pela categoria.")]
    public IA_ConstructionCapability capacidades = IA_ConstructionCapability.Auto;

    [Header("Técnico")]
    [Tooltip("Arraste aqui o objeto AZUL da pasta (Prefab), NÃO arraste da cena!")]
    public GameObject prefabDaUnidade; // O objeto 3D que vai ser construído
    public int preco = 100;

    [Header("Classificação")]
    public CategoriaItem categoria;
    [Tooltip("Escala de poder NARA usada para balanceamento e leitura tática.")]
    public EscalaPoder escalaPoder = EscalaPoder.NaoClassificado;
    [Tooltip("Papel estrategico explicito usado pela IA.")]
    public IA01StrategicRole strategicRole = IA01StrategicRole.None;

    [Header("Balanceamento em Dados")]
    public DadosBalanceamentoUnidade balanceamento;

    [Header("Comportamentos e Menu")]
    [Tooltip("Scripts de ação que aparecerão no menu quando esta unidade for selecionada")]
    public List<ComandoMenu> scriptsDeComando;

    public bool TryGetPrefab(out GameObject prefab)
    {
        prefab = null;

        try
        {
            prefab = prefabDaUnidade;
            if (prefab == null)
            {
                return false;
            }

            // Forca a validacao do handle nativo para bloquear "fake nulls" do Unity
            // que so explodem no primeiro GetComponent/GetComponentInChildren.
            string _ = prefab.name;

            // Detecta prefabs com Missing Scripts: GetComponents retorna slots nulos
            // para cada componente cujo tipo foi deletado/renomeado.
            Component[] comps = prefab.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null)
                {
                    // Prefab corrompido — descarta silenciosamente.
                    prefab = null;
                    return false;
                }
            }
        }
        catch (MissingReferenceException)
        {
            prefab = null;
        }
        catch (System.NullReferenceException)
        {
            prefab = null;
        }

        return prefab != null;
    }

    public bool TryGetPrefabBasico(out GameObject prefab)
    {
        prefab = null;

        try
        {
            prefab = prefabDaUnidade;
            if (prefab == null)
            {
                return false;
            }

            string _ = prefab.name;
        }
        catch (MissingReferenceException)
        {
            prefab = null;
        }
        catch (System.NullReferenceException)
        {
            prefab = null;
        }

        return prefab != null;
    }

    public string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(nomeItem))
        {
            return nomeItem.Trim();
        }

        if (!string.IsNullOrWhiteSpace(itemId))
        {
            return itemId.Trim();
        }

        GameObject prefab;
        if (TryGetPrefabBasico(out prefab) && !string.IsNullOrWhiteSpace(prefab.name))
        {
            return prefab.name.Trim();
        }

        return name;
    }

    public string GetStableId()
    {
        string source = !string.IsNullOrWhiteSpace(itemId) ? itemId : GetDisplayName();
        return IA_Text.Normalize(source);
    }

    public IA_ConstructionCapability GetResolvedCapabilities()
    {
        if (capacidades != IA_ConstructionCapability.Auto)
        {
            return capacidades;
        }

        return InferCapabilities();
    }

    public bool HasCapability(IA_ConstructionCapability capability)
    {
        if (capability == IA_ConstructionCapability.Auto)
        {
            return false;
        }

        return (GetResolvedCapabilities() & capability) == capability;
    }

    public IEnumerable<string> GetExplicitAliases()
    {
        if (string.IsNullOrWhiteSpace(aliases))
        {
            yield break;
        }

        string[] tokens = aliases.Split(new[] { ',', ';', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            yield return token.Trim();
        }
    }

    private IA_ConstructionCapability InferCapabilities()
    {
        IA_ConstructionCapability result = IA_ConstructionCapability.Auto;
        GameObject prefab;
        bool hasPrefab = TryGetPrefabBasico(out prefab);
        string joined = IA_Text.Normalize(GetDisplayName() + " " + name + " " + (hasPrefab ? prefab.name : string.Empty));

        bool hasCommercialAirport = false;
        bool hasHeliport = false;
        bool hasCarrier = false;
        bool hasMilitaryAirport = false;
        bool hasStructureComponent = false;
        bool hasUnitComponent = false;

        if (hasPrefab)
        {
            try
            {
                hasCommercialAirport = prefab.GetComponent<GerenciadorAeroportoComercial>() != null;
                hasHeliport = prefab.GetComponent<Heliporto>() != null;
                hasCarrier = prefab.GetComponent<GerenciadorPortaAvioes>() != null;
                hasMilitaryAirport = prefab.GetComponent<GerenciadorAeroporto>() != null
                    && !hasCommercialAirport
                    && !hasHeliport
                    && !hasCarrier;

                hasStructureComponent = !hasCarrier
                    && (prefab.GetComponent<GerenciadorAeroporto>() != null
                        || hasCommercialAirport
                        || hasHeliport
                        || prefab.GetComponent<Estaleiro>() != null
                        || prefab.GetComponent<PierMarinha>() != null
                        || prefab.GetComponent<Fabrica>() != null
                        || prefab.GetComponent<Imovel>() != null
                        || prefab.GetComponent<PlataformaOffshore>() != null
                        || prefab.GetComponent<MarcadorTerritorio>() != null);

                hasUnitComponent = prefab.GetComponent<ControleAviao>() != null
                    || prefab.GetComponent<ControleAviaoCaca>() != null
                    || prefab.GetComponent<ControleAviaoComercial>() != null
                    || prefab.GetComponent<Helicoptero>() != null
                    || prefab.GetComponent<ControleNavioRealista>() != null
                    || prefab.GetComponent<ControleSubmarino>() != null
                    || prefab.GetComponent<NavioPetroleiro>() != null
                    || prefab.GetComponent<NavioTransporteTropas>() != null
                    || prefab.GetComponent<ControleUnidade>() != null;
            }
            catch (MissingReferenceException)
            {
                // Prefab com missing scripts nos filhos — usa apenas dados textuais.
            }
            catch (System.Exception)
            {
                // Protege contra qualquer outro erro de acesso ao objeto nativo Unity.
            }
        }

        if (hasStructureComponent
            || categoria == CategoriaItem.Infraestrutura
            || categoria == CategoriaItem.Energia
            || categoria == CategoriaItem.Urbana
            || categoria == CategoriaItem.Tecnologia)
        {
            result |= IA_ConstructionCapability.Structure;
        }
        else if (hasUnitComponent || categoria == CategoriaItem.Marinha || categoria == CategoriaItem.Aeronautica || categoria == CategoriaItem.Exercito)
        {
            result |= IA_ConstructionCapability.Unit;
        }

        switch (categoria)
        {
            case CategoriaItem.Marinha:
                result |= IA_ConstructionCapability.Naval;
                break;
            case CategoriaItem.Aeronautica:
                result |= IA_ConstructionCapability.Air;
                break;
            case CategoriaItem.Exercito:
            case CategoriaItem.Tecnologia:
                result |= IA_ConstructionCapability.Land;
                break;
            case CategoriaItem.Infraestrutura:
            case CategoriaItem.Energia:
            case CategoriaItem.Urbana:
                result |= IA_ConstructionCapability.Structure;
                break;
        }

        if (joined.Contains("prefeitura") || joined.Contains("governo") || joined.Contains("capital")
            || joined.Contains("city hall") || joined.Contains("town hall")
            || joined.Contains("quartel general") || joined.Contains("hq") || joined.Contains("headquarters"))
        {
            result |= IA_ConstructionCapability.Core | IA_ConstructionCapability.Structure;
        }

        if (joined.Contains("quartel") || joined.Contains("tenda") || joined.Contains("barraca") || joined.Contains("barracks"))
        {
            result |= IA_ConstructionCapability.Military | IA_ConstructionCapability.Barracks | IA_ConstructionCapability.Structure;
        }

        if (joined.Contains("fabrica") || joined.Contains("construtor") || joined.Contains("factory"))
        {
            result |= IA_ConstructionCapability.Military | IA_ConstructionCapability.Factory | IA_ConstructionCapability.Structure;
        }

        if (joined.Contains("armazem") || joined.Contains("warehouse") || joined.Contains("galpao"))
        {
            result |= IA_ConstructionCapability.Economy | IA_ConstructionCapability.Warehouse | IA_ConstructionCapability.Structure;
        }

        if (joined.Contains("usina") || joined.Contains("energia") || joined.Contains("solar") || joined.Contains("nuclear") || joined.Contains("power plant") || joined.Contains("power"))
        {
            result |= IA_ConstructionCapability.Economy | IA_ConstructionCapability.Power | IA_ConstructionCapability.Structure;
        }

        if (joined.Contains("radar"))
        {
            result |= IA_ConstructionCapability.Defense | IA_ConstructionCapability.Radar | IA_ConstructionCapability.Structure;
        }

        if (joined.Contains("ciws") || joined.Contains("phalanx") || joined.Contains("torreta") || joined.Contains("sentinela") || joined.Contains("antia"))
        {
            result |= IA_ConstructionCapability.Defense | IA_ConstructionCapability.Structure;
        }

        if (joined.Contains("aeroporto comercial") || joined.Contains("commercial airport") || hasCommercialAirport)
        {
            result |= IA_ConstructionCapability.Air | IA_ConstructionCapability.Airport | IA_ConstructionCapability.CommercialAirport | IA_ConstructionCapability.Commercial | IA_ConstructionCapability.Structure;
        }
        else if (hasMilitaryAirport || joined.Contains("aeroporto") || joined.Contains("airport") || joined.Contains("pista"))
        {
            result |= IA_ConstructionCapability.Air | IA_ConstructionCapability.Airport | IA_ConstructionCapability.MilitaryAirport | IA_ConstructionCapability.Military | IA_ConstructionCapability.Structure;
        }

        if (hasHeliport || joined.Contains("heliporto") || joined.Contains("heliport"))
        {
            result |= IA_ConstructionCapability.Air | IA_ConstructionCapability.Heliport | IA_ConstructionCapability.Structure;
        }

        if (joined.Contains("estaleiro") || joined.Contains("shipyard"))
        {
            result |= IA_ConstructionCapability.Naval | IA_ConstructionCapability.Shipyard | IA_ConstructionCapability.Structure;
        }

        if (joined.Contains("pier"))
        {
            result |= IA_ConstructionCapability.Naval | IA_ConstructionCapability.Pier | IA_ConstructionCapability.Structure;
        }

        if (joined.Contains("plataforma") || joined.Contains("offshore platform"))
        {
            result |= IA_ConstructionCapability.Naval | IA_ConstructionCapability.Platform | IA_ConstructionCapability.Oil | IA_ConstructionCapability.Structure;
        }

        if (joined.Contains("petroleiro") || joined.Contains("petrolifero") || joined.Contains("tanker"))
        {
            result |= IA_ConstructionCapability.Naval | IA_ConstructionCapability.Transport | IA_ConstructionCapability.Oil | IA_ConstructionCapability.OilTanker;
        }

        if (joined.Contains("transporte"))
        {
            result |= IA_ConstructionCapability.Transport;
        }

        if (joined.Contains("comercial"))
        {
            result |= IA_ConstructionCapability.Commercial;
        }

        if (joined.Contains("civil") || joined.Contains("resid") || joined.Contains("moradia") || joined.Contains("casa") || joined.Contains("imovel") || joined.Contains("village"))
        {
            result |= IA_ConstructionCapability.Civil;
        }

        // Bloco protegido: prefabDaUnidade pode ter "fake null" ou missing scripts
        // que só explodem no primeiro GetComponent, não no null-check do C#.
        GameObject validatedPrefab;
        if (TryGetPrefabBasico(out validatedPrefab))
        {
            try
            {
                if (validatedPrefab.GetComponent<ControleAviaoComercial>() != null)
                {
                    result |= IA_ConstructionCapability.Air | IA_ConstructionCapability.Unit | IA_ConstructionCapability.Aircraft | IA_ConstructionCapability.CommercialAircraft | IA_ConstructionCapability.Commercial;
                }

                if (validatedPrefab.GetComponent<ControleAviao>() != null || validatedPrefab.GetComponent<ControleAviaoCaca>() != null)
                {
                    result |= IA_ConstructionCapability.Air | IA_ConstructionCapability.Unit | IA_ConstructionCapability.Aircraft | IA_ConstructionCapability.FighterAircraft | IA_ConstructionCapability.Military;
                }

                if (validatedPrefab.GetComponent<Helicoptero>() != null)
                {
                    result |= IA_ConstructionCapability.Air | IA_ConstructionCapability.Unit | IA_ConstructionCapability.Aircraft | IA_ConstructionCapability.Helicopter | IA_ConstructionCapability.Military;
                }

                if (validatedPrefab.GetComponent<ControleNavioRealista>() != null)
                {
                    result |= IA_ConstructionCapability.Naval | IA_ConstructionCapability.Unit;
                }

                if (validatedPrefab.GetComponent<ControleSubmarino>() != null)
                {
                    result |= IA_ConstructionCapability.Naval | IA_ConstructionCapability.Unit;
                }

                if (validatedPrefab.GetComponent<NavioTransporteTropas>() != null)
                {
                    result |= IA_ConstructionCapability.Naval | IA_ConstructionCapability.Unit | IA_ConstructionCapability.Transport | IA_ConstructionCapability.NavalTransport;
                }

                if (validatedPrefab.GetComponent<NavioPetroleiro>() != null)
                {
                    result |= IA_ConstructionCapability.Naval | IA_ConstructionCapability.Unit | IA_ConstructionCapability.Transport | IA_ConstructionCapability.NavalTransport | IA_ConstructionCapability.OilTanker | IA_ConstructionCapability.Oil;
                }
            }
            catch (MissingReferenceException)
            {
                // Prefab com missing scripts nos filhos — ignora bloco de GetComponents.
            }
            catch (System.Exception)
            {
                // Protege contra qualquer outro erro de acesso ao objeto nativo Unity.
            }
        }

        return result;
    }
}
