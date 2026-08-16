using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using Math = System.Math;
using UnityEngine.AI;
using System.Linq;
using Hegemonia.AI.BrainMaster;

public class MenuConstrucao : MonoBehaviour
{
    // Recompilacao forcada para garantir que o fluxo ICBM atualizado seja carregado no Play Mode.
    public static MenuConstrucao Instancia { get; private set; }

    private static long PrecoEfetivo(DadosConstrucao item)
    {
        return item != null ? item.ObterPrecoEfetivo() : 0L;
    }

    [System.Serializable]
    public class ConfigVisualCategoria
    {
        public DadosConstrucao.CategoriaItem categoria;
        public Sprite icone;
        public string glifoFallback = "?";
    }

    // --- CONFIGURAÇÕES VISUAIS (DESIGN MODERNO) ---
    [Header("Design Moderno - Cores & Estilo")]
    public KeyCode teclaAtalho = KeyCode.C;
    
    // Fundo Principal (Glassmorphism Escuro)
    public Color corFundoJanela = new Color(0.12f, 0.12f, 0.15f, 0.95f);
    
    // Cores de Destaque (Neon / Sci-Fi)
    public Color corDestaque = new Color(0.0f, 0.8f, 1.0f, 1.0f); // Ciano Neon
    public Color corTextoPrimario = new Color(0.95f, 0.95f, 0.95f, 1f);
    public Color corTextoSecundario = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("Cores das Categorias (Sutis)")]
    public Color corCardBase = new Color(0.2f, 0.2f, 0.25f, 0.8f);
    public Color corBordaAtiva = new Color(1f, 1f, 1f, 0.3f);
    public Color corFundoSecundario = new Color(0.07f, 0.1f, 0.13f, 0.94f);
    public Color corBordaCartao = new Color(0.5f, 0.76f, 0.9f, 0.28f);
    public Color corDivisoria = new Color(0.6f, 0.85f, 1f, 0.12f);

    [Header("Abas de Categoria")]
    public List<ConfigVisualCategoria> aparenciasCategorias = new List<ConfigVisualCategoria>();

    [Header("Dependencias")]
    public Construtor construtorCena;

    [Header("Sistema")]
    public List<DadosConstrucao> catalogo = new List<DadosConstrucao>();

    public static List<DadosConstrucao> catalogoGlobal;
    public static bool EstaAberto;

    private GameObject painelPrincipal;
    private Transform containerBotoes;
    private Transform containerAbas;
    private GridLayoutGroup gradeBotoes;
    private RectTransform viewportBotoes;
    private ScrollRect scrollRectBotoes;
    private CanvasGroup canvasGroupPainel;
    private InputField campoBusca;
    private Image imagemDetalheIcone;
    private Text textoDetalheIconeFallback;
    private Text textoDetalheNome;
    private Text textoDetalheCategoria;
    private Text textoDetalhePreco;
    private Text textoDetalheTipo;
    private Text textoDetalheVelocidade;
    private Text textoDetalheVida;
    private Text textoDetalhePoderFogo;
    private Text textoDetalheDescricao;
    private DadosConstrucao itemDetalheAtual;
    private bool ignorarEventoBusca;
    
    private GerenteDeJogo gerente;
    private bool menuAberto = false;
    private DadosConstrucao.CategoriaItem categoriaAtual = DadosConstrucao.CategoriaItem.Exercito;
    private string filtroBuscaAtual = string.Empty;
    private Dictionary<string, int> quantidadesPorItem = new Dictionary<string, int>();
    private readonly List<GerenciadorAeroporto> bufferAeroportos = new List<GerenciadorAeroporto>(16);
    private readonly Dictionary<DadosConstrucao.CategoriaItem, ConfigVisualCategoria> lookupVisualCategorias = new Dictionary<DadosConstrucao.CategoriaItem, ConfigVisualCategoria>();
    private readonly Dictionary<int, Sprite> cacheIconesResolvidos = new Dictionary<int, Sprite>();
    private Sprite[] iconesRuntimeCatalogo;
    private Sprite iconePlaceholderRuntime;
    private bool resetarScrollParaTopoPendente;
    private bool atalhosSuspensos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void GarantirInstanciaRuntime()
    {
        // O menu inicial nao possui a interface de campanha e, antes, este
        // bootstrap era marcado como DontDestroyOnLoad. Ao entrar em cena19),
        // ele sobrevivia sem catalogo e o MenuConstrucao serializado da cena
        // era destruido pelo singleton; o resultado era uma grade vazia.
        // Procura tambem objetos inativos e deixa cada cena ser dona da sua
        // propria interface.
        MenuConstrucao[] existentes = Resources.FindObjectsOfTypeAll<MenuConstrucao>();
        for (int i = 0; i < existentes.Length; i++)
        {
            MenuConstrucao existente = existentes[i];
            if (existente != null && existente.gameObject.scene.IsValid())
            {
                return;
            }
        }

        if (Object.FindFirstObjectByType<MenuConstrucao>() != null)
        {
            return;
        }

        GameObject root = new GameObject("MenuConstrucao_Auto");
        root.AddComponent<MenuConstrucao>();
        Debug.Log("[MenuConstrucao] Instancia ausente na cena. Criado bootstrap automatico.");
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
    }

    void Start()
    {
        gerente = Object.FindFirstObjectByType<GerenteDeJogo>();
        GarantirCatalogoValido();

        GarantirConfigsVisuaisCategoria();
        GerarInterfaceCompleta();

        if (painelPrincipal != null)
        {
            painelPrincipal.SetActive(false);
            if(canvasGroupPainel != null) 
            {
                canvasGroupPainel.alpha = 0;
                canvasGroupPainel.blocksRaycasts = false;
                canvasGroupPainel.interactable = false;
            }
        }

        // A fundacao vem antes de qualquer unidade militar. O primeiro card
        // exibido pelo menu de construcao deve ser a Prefeitura.
        FiltrarPorCategoria(DadosConstrucao.CategoriaItem.Urbana);
    }

    void OnDestroy()
    {
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    // (Removido Scanner Global de Prefabs que destruía os arquivos Assets do HD causando Missing Scripts)

#if false
    void CarregarTodasAsFichasLegado()
    {
        if (catalogo == null) catalogo = new List<DadosConstrucao>();
        List<DadosConstrucao> fichasConfiguradasNaCena = catalogo
            .Where(ficha => ficha != null)
            .Distinct()
            .ToList();

        DadosConstrucao[] fichasResources = Resources.LoadAll<DadosConstrucao>(string.Empty);
        for (int i = 0; i < fichasResources.Length; i++)
        {
            DadosConstrucao ficha = fichasResources[i];
            if (ficha != null && !fichasConfiguradasNaCena.Contains(ficha))
            {
                fichasConfiguradasNaCena.Add(ficha);
            }
        }

#if UNITY_EDITOR
        string[] fichasEssenciais =
        {
            "Assets/Prefabs/Aeroporto/Aeroporto militar.asset",
            "Assets/Prefabs/Aeroporto/Aeroporto comercial/Aeroporto comercial.asset",
            "Assets/Prefabs/Construtor de Veiculos/Construtor de Veiculos.asset",
            "Assets/Prefabs/Construtor de Veiculos/Tenda/Construcao_Tenda.asset",
            "Assets/Prefabs/Estaleiro Marinho/Estaleiro_Naval.asset",
            "Assets/Prefabs/Imobiliario/Fabrica/Industria.asset",
            "Assets/Prefabs/Imobiliario/casa/Casa.asset",
            "Assets/Prefabs/Imobiliario/Pred Medio/Predio Medio.asset",
            "Assets/Prefabs/Imobiliario/Pred Vilage/NovaConstrucao.asset",
            "Assets/Prefabs/Imobiliario/Perd Hard/Pred Hard.asset",
            "Assets/Prefabs/Energia/Usina de carvao.asset",
            // Ficha do silo/lançador ICBM: fica fora de Resources, portanto
            // precisa entrar explicitamente no catálogo essencial do menu.
            "Assets/Prefabs/Missiles/ICBM/Foguete.asset"
        };
        for (int i = 0; i < fichasEssenciais.Length; i++)
        {
            DadosConstrucao ficha = UnityEditor.AssetDatabase.LoadAssetAtPath<DadosConstrucao>(fichasEssenciais[i]);
            if (ficha != null && !fichasConfiguradasNaCena.Contains(ficha))
            {
                fichasConfiguradasNaCena.Add(ficha);
            }
        }
#endif

        catalogo.Clear();
        

        foreach (var ficha in fichasConfiguradasNaCena)
        {
            if (ficha == null) continue;
            string nomeValido = ficha.GetDisplayName();
            if (string.IsNullOrEmpty(nomeValido)) continue;

            GameObject prefab = null;
            bool temPrefab = ficha.TryGetPrefabBasico(out prefab);

            if (temPrefab && prefab != null)
            {
                string nm = nomeValido.ToLower();
                bool isDestrocos = false;
                try
                {
                    isDestrocos = nm.Contains("destroc") || nm.Contains("destroç") || nm.Contains("chama") ||
                                  prefab.GetComponent<DestrocosEmChamas>() != null;
                }
                catch (MissingReferenceException)
                {
                    // Prefab tem missing scripts em filhos — ignora filtro de destroços, mas mantem o card no menu
                }
                catch (System.Exception)
                {
                }

                if (isDestrocos)
                {
                    continue;
                }
            }

            catalogo.Add(ficha);
            if (!quantidadesPorItem.ContainsKey(ficha.NomeItem))
            quantidadesPorItem.Add(ficha.NomeItem, 1);
        }
        catalogo = catalogo.OrderBy(f => (int)f.categoria).ThenBy(f => f.GetDisplayName()).ToList();
        catalogoGlobal = new List<DadosConstrucao>(catalogo);
        CatalogoProdutoCompartilhado.RegistrarConstrucoes(catalogoGlobal);
    }

#if UNITY_EDITOR
    void AdicionarPrefabsImobiliariosSemFicha(List<DadosConstrucao> fichasEncontradas)
    {
        if (fichasEncontradas == null)
        {
            return;
        }

        const string pastaImobiliario = "Assets/Prefabs/Imobiliario";
        string[] guidsPrefabs = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { pastaImobiliario });
        foreach (string guidPrefab in guidsPrefabs)
        {
            string pathPrefab = UnityEditor.AssetDatabase.GUIDToAssetPath(guidPrefab);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(pathPrefab);
            if (prefab == null)
            {
                continue;
            }

            bool jaTemFicha = fichasEncontradas.Any(ficha =>
            {
                if (ficha == null)
                {
                    return false;
                }

                GameObject prefabFicha;
                return ficha.TryGetPrefabBasico(out prefabFicha) && prefabFicha == prefab;
            });
            if (jaTemFicha)
            {
                continue;
            }

            DadosConstrucao fichaTemporaria = ScriptableObject.CreateInstance<DadosConstrucao>();
            fichaTemporaria.name = prefab.name;
            fichaTemporaria.NomeItem = NomeAmigavelDoPrefab(prefab.name);
            fichaTemporaria.descricao = "Imovel urbano.";
            fichaTemporaria.PrefabDaUnidade = prefab;
            fichaTemporaria.preco = 100;
            fichaTemporaria.categoria = DadosConstrucao.CategoriaItem.Urbana;
            fichasEncontradas.Add(fichaTemporaria);
        }
    }

    string NomeAmigavelDoPrefab(string nomePrefab)
    {
        if (string.IsNullOrWhiteSpace(nomePrefab))
        {
            return "Imovel";
        }

        string nome = nomePrefab.Replace("_", " ").Replace("-", " ");
        while (nome.Contains("  "))
        {
            nome = nome.Replace("  ", " ");
        }

        return nome.Trim();
    }
#endif

#endif

    void GarantirCatalogoValido()
    {
        if (catalogo == null)
        {
            catalogo = new List<DadosConstrucao>();
        }

        GarantirUsinaCarvaoNoCatalogo();
        GarantirComerciosNoCatalogo();
        GarantirPrefeituraNoCatalogo();

        List<DadosConstrucao> catalogoDaCena = new List<DadosConstrucao>();
        foreach (DadosConstrucao item in catalogo)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.NomeItem))
            {
                continue;
            }

            GameObject prefab;
            if (!item.TryGetPrefabBasico(out prefab))
            {
                continue;
            }

            string nome = item.GetDisplayName();
            bool isDestrocos = nome.IndexOf("destroc", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               nome.IndexOf("chama", System.StringComparison.OrdinalIgnoreCase) >= 0;
            try
            {
                isDestrocos = isDestrocos || prefab.GetComponent<DestrocosEmChamas>() != null;
            }
            catch (MissingReferenceException)
            {
                continue;
            }

            if (isDestrocos)
            {
                continue;
            }

            catalogoDaCena.Add(item);
        if (!quantidadesPorItem.ContainsKey(item.NomeItem))
            {
                quantidadesPorItem.Add(item.NomeItem, 1);
            }
        }

        catalogo = catalogoDaCena
            .Distinct()
            .OrderBy(item => (int)item.categoria)
            .ThenBy(item => item.GetDisplayName())
            .ToList();
        catalogoGlobal = new List<DadosConstrucao>(catalogo);
        CatalogoProdutoCompartilhado.RegistrarConstrucoes(catalogoGlobal);
    }

    private void GarantirUsinaCarvaoNoCatalogo()
    {
        DadosConstrucao usinaCarvao = Resources.Load<DadosConstrucao>("Usina Carvao");
        if (usinaCarvao == null || catalogo.Contains(usinaCarvao))
        {
            return;
        }

        catalogo.Add(usinaCarvao);
        if (!quantidadesPorItem.ContainsKey(usinaCarvao.NomeItem))
        {
            quantidadesPorItem.Add(usinaCarvao.NomeItem, 1);
        }
    }

    /// <summary>
    /// Mantem os dois edificios comerciais basicos disponiveis mesmo quando
    /// a cena foi criada antes do catalogo de comercio. As fichas ficam em
    /// Resources para funcionar tanto no Editor quanto no build final.
    /// </summary>
    private void GarantirComerciosNoCatalogo()
    {
        string[] caminhos =
        {
            "Construcoes/Predio_Comercial",
            "Construcoes/Centro_Comercial"
        };

        for (int i = 0; i < caminhos.Length; i++)
        {
            DadosConstrucao ficha = Resources.Load<DadosConstrucao>(caminhos[i]);
            if (ficha == null || catalogo.Contains(ficha))
            {
                continue;
            }

            bool duplicada = false;
            GameObject prefabFicha;
            bool temPrefabFicha = ficha.TryGetPrefabBasico(out prefabFicha);
            for (int j = 0; j < catalogo.Count; j++)
            {
                DadosConstrucao existente = catalogo[j];
                if (existente == null)
                {
                    continue;
                }

                if (string.Equals(existente.GetStableId(), ficha.GetStableId(), System.StringComparison.OrdinalIgnoreCase))
                {
                    duplicada = true;
                    break;
                }

                GameObject prefabExistente;
                if (temPrefabFicha && existente.TryGetPrefabBasico(out prefabExistente) && prefabExistente == prefabFicha)
                {
                    duplicada = true;
                    break;
                }
            }

            if (!duplicada)
            {
                catalogo.Add(ficha);
                if (!quantidadesPorItem.ContainsKey(ficha.NomeItem))
                {
                    quantidadesPorItem.Add(ficha.NomeItem, 1);
                }
            }
        }
    }

    // A ficha original da Prefeitura fica fora de Resources. Mantemos uma
    // copia de runtime em Resources para que ela exista no catalogo tambem em
    // builds, cenas novas e partidas que nao serializaram a referencia.
    private void GarantirPrefeituraNoCatalogo()
    {
        DadosConstrucao prefeitura = Resources.Load<DadosConstrucao>("Construcoes/Prefeitura");
        if (prefeitura == null)
        {
            return;
        }

        string idPrefeitura = prefeitura.GetStableId();
        bool jaExiste = catalogo.Any(item => item != null
            && (item == prefeitura
                || string.Equals(item.GetStableId(), idPrefeitura, System.StringComparison.OrdinalIgnoreCase)));
        if (jaExiste)
        {
            return;
        }

        catalogo.Add(prefeitura);
        if (!quantidadesPorItem.ContainsKey(prefeitura.NomeItem))
        {
            quantidadesPorItem.Add(prefeitura.NomeItem, 1);
        }
    }

#if false
    void GarantirCatalogoValidoLegado()
    {
        if (catalogo == null)
        {
            catalogo = new List<DadosConstrucao>();
        }

        if (catalogo.Count == 0)
        {
            DadosConstrucao[] recursos = Resources.FindObjectsOfTypeAll<DadosConstrucao>();
            if (recursos != null && recursos.Length > 0)
            {
                for (int i = 0; i < recursos.Length; i++)
                {
                    DadosConstrucao recurso = recursos[i];
                    if (recurso != null && !catalogo.Contains(recurso))
                    {
                        catalogo.Add(recurso);
                    }
                }

                if (catalogo.Count > 0)
                {
                    Debug.Log("[MenuConstrucao] Catálogo preenchido via Resources com " + catalogo.Count + " fichas.");
                }
            }

#if UNITY_EDITOR
            if (catalogo.Count == 0)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:DadosConstrucao");
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                    DadosConstrucao recurso = UnityEditor.AssetDatabase.LoadAssetAtPath<DadosConstrucao>(path);
                    if (recurso != null && !catalogo.Contains(recurso))
                    {
                        catalogo.Add(recurso);
                    }
                }

                if (catalogo.Count > 0)
                {
                    Debug.Log("[MenuConstrucao] Catálogo preenchido via AssetDatabase com " + catalogo.Count + " fichas.");
                }
            }
#endif
        }

#if UNITY_EDITOR
            GarantirFichaC700NoCatalogo();
            GarantirFichaICBMNoCatalogo();
#endif

        List<DadosConstrucao> catalogoValido = new List<DadosConstrucao>();
        foreach (var item in catalogo)
        {
            if (item == null)
            {
                continue;
            }

            GameObject prefab;
        if (!item.TryGetPrefabBasico(out prefab) || string.IsNullOrWhiteSpace(item.NomeItem))
            {
                continue;
            }

        string nm = item.NomeItem.ToLower();
            bool isDestrocos = false;
            try
            {
                isDestrocos = nm.Contains("destroc") || nm.Contains("destroç") || nm.Contains("chama") ||
                              prefab.GetComponent<DestrocosEmChamas>() != null;
            }
            catch (MissingReferenceException)
            {
                continue;
            }
            catch (System.Exception)
            {
                continue;
            }

            if (isDestrocos)
            {
                continue;
            }

            catalogoValido.Add(item);
        }

        catalogo = catalogoValido
            .Distinct()
            .OrderBy(item => (int)item.categoria)
            .ThenBy(item => item.GetDisplayName())
            .ToList();
        catalogoGlobal = new List<DadosConstrucao>(catalogo);
        CatalogoProdutoCompartilhado.RegistrarConstrucoes(catalogoGlobal);
    }

#endif

    /// <summary>
    /// Bootstrap publico para consumidores autonomos, como IA01, que podem
    /// iniciar antes do Start desta UI. Nao abre o menu nem altera a selecao.
    /// </summary>
    public void GarantirCatalogoParaIA()
    {
        if (catalogo == null || catalogo.Count == 0 || catalogoGlobal == null || catalogoGlobal.Count == 0)
        {
            GarantirCatalogoValido();
        }
    }

#if false
#if UNITY_EDITOR
    void GarantirFichaC700NoCatalogo()
    {
        const string caminhoC700 = "Assets/Prefabs/Aeroporto/C700/c700.asset";
        DadosConstrucao c700 = UnityEditor.AssetDatabase.LoadAssetAtPath<DadosConstrucao>(caminhoC700);
        if (c700 == null)
        {
            return;
        }

        if (!catalogo.Contains(c700))
        {
            catalogo.Add(c700);
        }
    }

    void GarantirFichaICBMNoCatalogo()
    {
        const string caminhoIcbm = "Assets/Prefabs/Missiles/ICBM/Foguete.asset";
        DadosConstrucao icbm = UnityEditor.AssetDatabase.LoadAssetAtPath<DadosConstrucao>(caminhoIcbm);
        if (icbm != null && !catalogo.Contains(icbm))
        {
            catalogo.Add(icbm);
        if (!quantidadesPorItem.ContainsKey(icbm.NomeItem))
            {
            quantidadesPorItem.Add(icbm.NomeItem, 1);
            }
            Debug.Log("[MenuConstrucao] Ficha ICBM adicionada ao catalogo runtime.");
        }
    }
#endif

#endif

    List<DadosConstrucao> ObterItensDaCategoria(DadosConstrucao.CategoriaItem categoriaDesejada, bool aplicarBusca)
    {
        GarantirCatalogoValido();

        IEnumerable<DadosConstrucao> query = catalogo.Where(item => item != null && item.categoria == categoriaDesejada);
        if (aplicarBusca)
        {
            query = query.Where(ItemPassaFiltroBusca);
        }

        return query
            .OrderBy(PrioridadeDoCard)
            .ThenBy(item => item.GetDisplayName())
            .ToList();
    }

    private static int PrioridadeDoCard(DadosConstrucao item)
    {
        if (item == null)
        {
            return 2;
        }

        string id = item.GetStableId();
        if (string.Equals(id, "capital.prefeitura", System.StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        string nome = (item.NomeItem + " " + item.GetDisplayName()).ToLowerInvariant();
        return nome.Contains("prefeitura") ? 0 : 1;
    }

    void DefinirTextoBuscaSemEvento(string novoTexto)
    {
        filtroBuscaAtual = novoTexto ?? string.Empty;

        if (campoBusca == null)
        {
            return;
        }

        ignorarEventoBusca = true;
        campoBusca.text = filtroBuscaAtual;
        ignorarEventoBusca = false;
    }

    bool BuscaPodeSerRecuperadaAutomaticamente()
    {
        if (string.IsNullOrWhiteSpace(filtroBuscaAtual))
        {
            return false;
        }

        if (campoBusca == null)
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(campoBusca.text)
            || !string.Equals(campoBusca.text, filtroBuscaAtual, System.StringComparison.Ordinal);
    }

    void LimparBuscaPresa()
    {
        if (string.IsNullOrEmpty(filtroBuscaAtual) && (campoBusca == null || string.IsNullOrEmpty(campoBusca.text)))
        {
            return;
        }

        DefinirTextoBuscaSemEvento(string.Empty);

        if (campoBusca != null)
        {
            campoBusca.DeactivateInputField();

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == campoBusca.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }

    void Update()
    {
        // O Governo novo fecha o Canvas de construÃ§Ã£o sem passar pelo HUD
        // legado. Se o foco for liberado, recupera o atalho C mesmo que uma
        // cena antiga tenha deixado o sinal de suspensÃ£o preso.
        if (atalhosSuspensos && !MenuGoverno.EstaAberto)
        {
            atalhosSuspensos = false;
        }

        if (atalhosSuspensos)
        {
            return;
        }

        LiberarSelecaoOculta();

        if (teclaAtalho != KeyCode.None && Input.GetKeyDown(teclaAtalho))
        {
            if (MenuComandoController.Instancia != null && MenuComandoController.Instancia.MenuAberto) return;
            if (AtalhoBloqueadoPorCampoTexto())
            {
                return;
            }
            Debug.Log("[MenuConstrucao] Tecla de atalho pressionada (C). Alternando menu...");
            AlternarMenu(!menuAberto);
        }
    }

    bool AtalhoBloqueadoPorCampoTexto()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
        {
            return false;
        }

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.activeInHierarchy)
        {
            return false;
        }

        bool ehCampoTexto = selected.GetComponent<InputField>() != null
            || selected.GetComponent<TMPro.TMP_InputField>() != null;
        if (!ehCampoTexto)
        {
            return false;
        }

        if (!HierarquiaCanvasVisivel(selected.transform))
        {
            EventSystem.current.SetSelectedGameObject(null);
            return false;
        }

        bool campoDoMenuConstrucao = painelPrincipal != null
            && selected.transform.IsChildOf(painelPrincipal.transform);

        // Permite fechar o menu mesmo com a busca focada, mas evita abrir enquanto o
        // jogador estiver digitando em campos realmente visíveis de outros painéis.
        return !menuAberto || !campoDoMenuConstrucao;
    }

    void LiberarSelecaoOculta()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (!EhCampoTexto(selected))
        {
            return;
        }

        if (selected == null || !selected.activeInHierarchy || !HierarquiaCanvasVisivel(selected.transform))
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    bool EhCampoTexto(GameObject alvo)
    {
        return alvo != null
            && (alvo.GetComponent<InputField>() != null
                || alvo.GetComponent<TMPro.TMP_InputField>() != null);
    }

    bool HierarquiaCanvasVisivel(Transform alvo)
    {
        if (alvo == null || !alvo.gameObject.activeInHierarchy)
        {
            return false;
        }

        Canvas canvas = alvo.GetComponentInParent<Canvas>(true);
        if (canvas != null && !canvas.isActiveAndEnabled)
        {
            return false;
        }

        CanvasGroup[] grupos = alvo.GetComponentsInParent<CanvasGroup>(true);
        for (int i = 0; i < grupos.Length; i++)
        {
            CanvasGroup grupo = grupos[i];
            if (grupo == null || grupo.ignoreParentGroups)
            {
                continue;
            }

            if (grupo.alpha <= 0.01f || !grupo.interactable || !grupo.blocksRaycasts)
            {
                return false;
            }
        }

        return true;
    }

    public void AlternarMenu()
    {
        if (painelPrincipal == null) return;
        AlternarMenu(!menuAberto);
    }

    public void DefinirVisibilidadeHud(bool visivel)
    {
        atalhosSuspensos = !visivel;

        if (!visivel)
        {
            if (menuAberto)
            {
                AlternarMenu(false);
            }
            else if (painelPrincipal != null)
            {
                painelPrincipal.SetActive(false);
                if (canvasGroupPainel != null)
                {
                    canvasGroupPainel.alpha = 0f;
                    canvasGroupPainel.blocksRaycasts = false;
                    canvasGroupPainel.interactable = false;
                }
            }

            LimparBuscaPresa();
            LiberarSelecaoOculta();
            return;
        }

        if (painelPrincipal != null && menuAberto)
        {
            painelPrincipal.SetActive(true);
        }

        if (canvasGroupPainel != null)
        {
            canvasGroupPainel.blocksRaycasts = menuAberto;
            canvasGroupPainel.interactable = menuAberto;
        }
    }

    public void AlternarMenu(bool abrir)
    {
        if (painelPrincipal == null || canvasGroupPainel == null)
        {
            Debug.LogWarning("[MenuConstrucao] Painel ausente. Reconstruindo interface em runtime.");
            GerarInterfaceCompleta();
        }
        if (painelPrincipal == null) 
        {
            Debug.LogWarning("[MenuConstrucao] painelPrincipal é nulo! Não foi possível abrir o menu.");
            return;
        }
        
        StopAllCoroutines(); 
        
        if (abrir)
        {
            try
            {
                MenuPier menuPier = Object.FindFirstObjectByType<MenuPier>();
                if (menuPier != null) menuPier.FecharMenu();

                MenuMisseis menuMiss = Object.FindFirstObjectByType<MenuMisseis>();
                if (menuMiss != null && MenuMisseis.EstaAberto) menuMiss.CancelarLancamento();

                MenuGoverno menuGov = Object.FindFirstObjectByType<MenuGoverno>();
                if (menuGov != null && MenuGoverno.EstaAberto) menuGov.AlternarMenu(false);

                MenuGovernoNovoController menuGovNovo = Object.FindFirstObjectByType<MenuGovernoNovoController>();
                if (menuGovNovo != null && MenuGoverno.EstaAberto) menuGovNovo.Abrir(false);

                RegistroEntidadesJogo.FillAeroportos(bufferAeroportos);
                foreach (GerenciadorAeroporto aeroporto in bufferAeroportos)
                {
                    if (aeroporto != null)
                    {
                        aeroporto.CancelarInteracaoPorConstrucao();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MenuConstrucao] Erro ao fechar outros menus: {ex.Message}");
            }

            GarantirCatalogoValido();
            List<DadosConstrucao> itensBaseCategoria = ObterItensDaCategoria(categoriaAtual, false);
            List<DadosConstrucao> itensFiltradosCategoria = ObterItensDaCategoria(categoriaAtual, true);
            if (itensFiltradosCategoria.Count == 0 && itensBaseCategoria.Count > 0 && BuscaPodeSerRecuperadaAutomaticamente())
            {
                LimparBuscaPresa();
            }

            FiltrarPorCategoria(categoriaAtual);
        }
        
        if (canvasGroupPainel != null)
        {
            canvasGroupPainel.blocksRaycasts = abrir;
            canvasGroupPainel.interactable = abrir;
        }

        StartCoroutine(AnimarMenu(abrir));
        EsconderHUD(abrir);
    }

    private void EsconderHUD(bool esconder)
    {
        // MiniMapa
        MiniMapa mm = UnityEngine.Object.FindFirstObjectByType<MiniMapa>();
        if (mm != null)
        {
            Transform canvasMM = mm.transform.root.Find("Canvas_MiniMapa");
            if (canvasMM != null) canvasMM.gameObject.SetActive(!esconder);
            mm.gameObject.SetActive(!esconder);
        }

        // Menu de Comportamento (Estado: Passivo/Ativo)
        MenuComportamento mcomp = AcharComponenteMesmoInativo<MenuComportamento>();
        if (mcomp != null)
        {
            DefinirVisibilidadeComponenteOuObjeto(mcomp, !esconder);
        }

        // Menu de Governo (se estiver aberto, fecha)
        if (esconder)
        {
            MenuGoverno mg = AcharComponenteMesmoInativo<MenuGoverno>();
            if (mg != null && MenuGoverno.EstaAberto) mg.AlternarMenu(false);
        }
    }

    private static T AcharComponenteMesmoInativo<T>() where T : MonoBehaviour
    {
#if UNITY_2023_1_OR_NEWER
        T ativo = Object.FindFirstObjectByType<T>();
        if (ativo != null) return ativo;

        T[] encontrados = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return encontrados.FirstOrDefault();
#else
        return Object.FindObjectOfType<T>(true);
#endif
    }

    private void DefinirVisibilidadeComponenteOuObjeto(MonoBehaviour componente, bool visivel)
    {
        if (componente == null) return;

        MenuComportamento menuComportamento = componente as MenuComportamento;
        if (menuComportamento != null)
        {
            menuComportamento.DefinirVisibilidadeHud(visivel);
            return;
        }

        if (PodeDesativarObjetoDeHud(componente.gameObject))
        {
            componente.gameObject.SetActive(visivel);
        }
        else
        {
            componente.enabled = visivel;
        }
    }

    private bool PodeDesativarObjetoDeHud(GameObject alvo)
    {
        if (alvo == null || alvo == gameObject) return false;
        if (alvo.GetComponent<GerenteDeJogo>() != null) return false;
        if (alvo.GetComponent<Construtor>() != null) return false;
        if (alvo.GetComponent<MenuGoverno>() != null) return false;
        return true;
    }

    IEnumerator AnimarMenu(bool abrir)
    {
        menuAberto = abrir;
        EstaAberto = abrir;

        if (abrir)
        {
            painelPrincipal.SetActive(true);
            if(containerBotoes != null) LayoutRebuilder.ForceRebuildLayoutImmediate(containerBotoes.GetComponent<RectTransform>());
        }

        float alphaInicial = canvasGroupPainel.alpha;
        float alphaFinal = abrir ? 1f : 0f;
        float duracao = 0.2f;
        float tempo = 0;

        while (tempo < duracao)
        {
            tempo += Time.unscaledDeltaTime;
            canvasGroupPainel.alpha = Mathf.Lerp(alphaInicial, alphaFinal, tempo / duracao);
            yield return null;
        }

        canvasGroupPainel.alpha = alphaFinal;

        if (!abrir)
        {
            if (campoBusca != null)
            {
                campoBusca.DeactivateInputField();
            }

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                GameObject selecionado = EventSystem.current.currentSelectedGameObject;
                if (selecionado != null && painelPrincipal != null && selecionado.transform.IsChildOf(painelPrincipal.transform))
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }

            painelPrincipal.SetActive(false);
        }
    }

    void GerarInterfaceCompleta()
    {
        GameObject canvasObj = GameObject.Find("Canvas_Interface");
        if (canvasObj == null)
        {
            Canvas canvasExistente = Object.FindFirstObjectByType<Canvas>();
            if (canvasExistente != null) canvasObj = canvasExistente.gameObject;
            else
            {
                canvasObj = new GameObject("Canvas_Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }

        Transform painelAntigo = canvasObj.transform.Find("Painel_Construcao_Moderno");
        if (painelAntigo != null) DestroyImmediate(painelAntigo.gameObject);

        painelPrincipal = CriarRetangulo("Painel_Construcao_Moderno", canvasObj.transform);
        Image imgFundo = painelPrincipal.AddComponent<Image>();
        imgFundo.color = corFundoJanela;

        canvasGroupPainel = painelPrincipal.AddComponent<CanvasGroup>();
        canvasGroupPainel.blocksRaycasts = true;
        canvasGroupPainel.interactable = true;

        RectTransform rtPanel = painelPrincipal.GetComponent<RectTransform>();
        rtPanel.anchorMin = new Vector2(0.18f, 0.20f);
        rtPanel.anchorMax = new Vector2(0.98f, 0.90f);
        rtPanel.offsetMin = Vector2.zero;
        rtPanel.offsetMax = Vector2.zero;

        Outline outline = painelPrincipal.AddComponent<Outline>();
        outline.effectColor = new Color(0.55f, 0.82f, 0.94f, 0.22f);
        outline.effectDistance = new Vector2(1f, -1f);

        Shadow shadow = painelPrincipal.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0.65f, 0.85f, 0.14f);
        shadow.effectDistance = new Vector2(0f, -4f);

        VerticalLayoutGroup layoutPrincipal = painelPrincipal.AddComponent<VerticalLayoutGroup>();
        layoutPrincipal.padding = new RectOffset(18, 18, 10, 14);
        layoutPrincipal.spacing = 10;
        layoutPrincipal.childControlHeight = true;
        layoutPrincipal.childControlWidth = true;
        layoutPrincipal.childForceExpandHeight = false;
        layoutPrincipal.childForceExpandWidth = true;

        if (Object.FindFirstObjectByType<ModoDemolicao>() == null)
        {
            GameObject go = new GameObject("ModoDemolicao_Manager");
            go.AddComponent<ModoDemolicao>();
        }

        CriarPlacaTitulo(painelPrincipal.transform);
        CriarLinhaNavegacao(painelPrincipal.transform);
        CriarCorpoPrincipal(painelPrincipal.transform);
        painelPrincipal.SetActive(false);
        canvasGroupPainel.alpha = 0f;
        canvasGroupPainel.blocksRaycasts = false;
        canvasGroupPainel.interactable = false;
    }

    void CriarPlacaTitulo(Transform pai)
    {
        GameObject topoObj = CriarRetangulo("FaixaTitulo", pai);
        LayoutElement leTopo = topoObj.AddComponent<LayoutElement>();
        leTopo.minHeight = 28;
        leTopo.preferredHeight = 28;
        leTopo.flexibleHeight = 0f;

        HorizontalLayoutGroup layoutTopo = topoObj.AddComponent<HorizontalLayoutGroup>();
        layoutTopo.childAlignment = TextAnchor.MiddleCenter;
        layoutTopo.childControlWidth = true;
        layoutTopo.childControlHeight = true;
        layoutTopo.childForceExpandWidth = true;
        layoutTopo.childForceExpandHeight = false;
        layoutTopo.spacing = 10;

        CriarLinhaDecorativa(topoObj.transform);

        // Fechamento explícito evita que o clique atravesse o painel e acione
        // o menu Governo que fica atrás. O botão usa o mesmo CanvasGroup do
        // menu, portanto recebe o clique em uma única tentativa.
        GameObject fecharObj = CriarRetangulo("BotaoFechar", topoObj.transform);
        LayoutElement leFechar = fecharObj.AddComponent<LayoutElement>();
        leFechar.minWidth = 38f;
        leFechar.preferredWidth = 38f;
        leFechar.minHeight = 24f;
        leFechar.preferredHeight = 24f;
        Image imgFechar = fecharObj.AddComponent<Image>();
        imgFechar.color = new Color(0.45f, 0.08f, 0.10f, 0.92f);
        imgFechar.raycastTarget = true;
        Button botaoFechar = fecharObj.AddComponent<Button>();
        botaoFechar.targetGraphic = imgFechar;
        botaoFechar.onClick.AddListener(() => AlternarMenu(false));
        GameObject textoFechar = CriarRetangulo("Texto", fecharObj.transform);
        Text txFechar = textoFechar.AddComponent<Text>();
        txFechar.text = "X";
        txFechar.font = ObterFontePadrao();
        txFechar.fontSize = 16;
        txFechar.fontStyle = FontStyle.Bold;
        txFechar.alignment = TextAnchor.MiddleCenter;
        txFechar.color = Color.white;
        txFechar.raycastTarget = false;
        EsticarRectTransform(textoFechar.GetComponent<RectTransform>());

        GameObject badgeTitulo = CriarRetangulo("BadgeTitulo", topoObj.transform);
        LayoutElement leBadge = badgeTitulo.AddComponent<LayoutElement>();
        leBadge.minWidth = 300;
        leBadge.preferredWidth = 360;
        leBadge.minHeight = 24;
        leBadge.preferredHeight = 24;
        leBadge.flexibleHeight = 0f;

        Image imgBadge = badgeTitulo.AddComponent<Image>();
        imgBadge.color = new Color(0.09f, 0.13f, 0.18f, 0.98f);

        Outline badgeOutline = badgeTitulo.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(0.56f, 0.84f, 0.96f, 0.28f);
        badgeOutline.effectDistance = new Vector2(1f, -1f);

        Shadow badgeShadow = badgeTitulo.AddComponent<Shadow>();
        badgeShadow.effectColor = new Color(0f, 0.8f, 1f, 0.2f);
        badgeShadow.effectDistance = new Vector2(0f, -2f);

        GameObject tituloObj = CriarRetangulo("TextoTitulo", badgeTitulo.transform);
        Text titulo = tituloObj.AddComponent<Text>();
        titulo.text = "HEGEMONIA GLOBAL";
        titulo.font = ObterFontePadrao();
        titulo.fontSize = 19;
        titulo.fontStyle = FontStyle.Bold;
        titulo.alignment = TextAnchor.MiddleCenter;
        titulo.color = corTextoPrimario;
        titulo.raycastTarget = false;
        EsticarRectTransform(tituloObj.GetComponent<RectTransform>());

        CriarLinhaDecorativa(topoObj.transform);
    }

    void CriarLinhaDecorativa(Transform pai)
    {
        GameObject linha = CriarRetangulo("Linha", pai);
        LayoutElement leLinha = linha.AddComponent<LayoutElement>();
        leLinha.flexibleWidth = 1f;
        leLinha.minHeight = 1f;
        leLinha.preferredHeight = 1f;

        Image imgLinha = linha.AddComponent<Image>();
        imgLinha.color = corDivisoria;
        imgLinha.raycastTarget = false;
    }

    void CriarLinhaNavegacao(Transform pai)
    {
        GameObject navObj = CriarRetangulo("LinhaNavegacao", pai);
        LayoutElement leNav = navObj.AddComponent<LayoutElement>();
        leNav.minHeight = 30;
        leNav.preferredHeight = 30;
        leNav.flexibleHeight = 0f;

        HorizontalLayoutGroup layoutNav = navObj.AddComponent<HorizontalLayoutGroup>();
        layoutNav.spacing = 6;
        layoutNav.childAlignment = TextAnchor.MiddleCenter;
        layoutNav.childControlWidth = true;
        layoutNav.childControlHeight = true;
        layoutNav.childForceExpandWidth = false;
        layoutNav.childForceExpandHeight = true;

        GameObject abasHost = CriarRetangulo("AbasHost", navObj.transform);
        LayoutElement leAbasHost = abasHost.AddComponent<LayoutElement>();
        leAbasHost.flexibleWidth = 1f;

        HorizontalLayoutGroup layoutAbas = abasHost.AddComponent<HorizontalLayoutGroup>();
        layoutAbas.childControlWidth = true;
        layoutAbas.childControlHeight = true;
        layoutAbas.childForceExpandWidth = false;
        layoutAbas.childForceExpandHeight = true;
        layoutAbas.childAlignment = TextAnchor.MiddleLeft;
        layoutAbas.spacing = 5;
        containerAbas = abasHost.transform;

        foreach (DadosConstrucao.CategoriaItem cat in System.Enum.GetValues(typeof(DadosConstrucao.CategoriaItem)))
        {
            CriarBotaoAbaModerno(cat, containerAbas);
        }

        CriarBuscaVisual(navObj.transform);
    }

    void CriarBuscaVisual(Transform pai)
    {
        GameObject buscaObj = CriarRetangulo("BuscaVisual", pai);
        LayoutElement leBusca = buscaObj.AddComponent<LayoutElement>();
        leBusca.minWidth = 120;
        leBusca.preferredWidth = 140;
        leBusca.minHeight = 22;
        leBusca.preferredHeight = 22;
        leBusca.flexibleHeight = 0f;

        Image imgBusca = buscaObj.AddComponent<Image>();
        imgBusca.color = new Color(0.08f, 0.12f, 0.16f, 0.96f);
        imgBusca.raycastTarget = true;

        Outline outline = buscaObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.08f);
        outline.effectDistance = new Vector2(1f, -1f);

        HorizontalLayoutGroup layoutBusca = buscaObj.AddComponent<HorizontalLayoutGroup>();
        layoutBusca.padding = new RectOffset(5, 5, 0, 0);
        layoutBusca.spacing = 4;
        layoutBusca.childAlignment = TextAnchor.MiddleLeft;
        layoutBusca.childControlWidth = true;
        layoutBusca.childControlHeight = true;
        layoutBusca.childForceExpandWidth = false;
        layoutBusca.childForceExpandHeight = true;

        GameObject iconeObj = CriarRetangulo("IconeBusca", buscaObj.transform);
        LayoutElement leIcone = iconeObj.AddComponent<LayoutElement>();
        leIcone.minWidth = 10;
        leIcone.preferredWidth = 10;

        Text iconeBusca = iconeObj.AddComponent<Text>();
        iconeBusca.text = "Q";
        iconeBusca.font = ObterFontePadrao();
        iconeBusca.fontSize = 8;
        iconeBusca.fontStyle = FontStyle.Bold;
        iconeBusca.alignment = TextAnchor.MiddleCenter;
        iconeBusca.color = corTextoSecundario;
        iconeBusca.raycastTarget = false;

        GameObject inputAreaObj = CriarRetangulo("InputAreaBusca", buscaObj.transform);
        LayoutElement leInputArea = inputAreaObj.AddComponent<LayoutElement>();
        leInputArea.flexibleWidth = 1f;

        Image imgInput = inputAreaObj.AddComponent<Image>();
        imgInput.color = Color.clear;
        imgInput.raycastTarget = true;

        InputField inputBusca = inputAreaObj.AddComponent<InputField>();
        inputBusca.lineType = InputField.LineType.SingleLine;
        inputBusca.text = filtroBuscaAtual;
        inputBusca.targetGraphic = imgInput;

        GameObject textoObj = CriarRetangulo("TextoBusca", inputAreaObj.transform);
        Text textoBusca = textoObj.AddComponent<Text>();
        textoBusca.font = ObterFontePadrao();
        textoBusca.fontSize = 8;
        textoBusca.alignment = TextAnchor.MiddleLeft;
        textoBusca.color = corTextoPrimario;
        textoBusca.raycastTarget = false;
        EsticarRectTransform(textoObj.GetComponent<RectTransform>());

        GameObject placeholderObj = CriarRetangulo("PlaceholderBusca", inputAreaObj.transform);
        Text placeholder = placeholderObj.AddComponent<Text>();
        placeholder.text = "Pesquisar Unidade...";
        placeholder.font = ObterFontePadrao();
        placeholder.fontSize = 8;
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.color = new Color(corTextoSecundario.r, corTextoSecundario.g, corTextoSecundario.b, 0.75f);
        placeholder.raycastTarget = false;
        EsticarRectTransform(placeholderObj.GetComponent<RectTransform>());

        inputBusca.textComponent = textoBusca;
        inputBusca.placeholder = placeholder;
        inputBusca.onValueChanged.AddListener(AtualizarFiltroBusca);
        campoBusca = inputBusca;

        EventTrigger triggerBusca = buscaObj.AddComponent<EventTrigger>();
        triggerBusca.triggers = new List<EventTrigger.Entry>();
        EventTrigger.Entry cliqueBusca = new EventTrigger.Entry();
        cliqueBusca.eventID = EventTriggerType.PointerDown;
        cliqueBusca.callback.AddListener((_) =>
        {
            if (campoBusca != null)
            {
                campoBusca.Select();
                campoBusca.ActivateInputField();
            }
        });
        triggerBusca.triggers.Add(cliqueBusca);
    }

    void CriarCorpoPrincipal(Transform pai)
    {
        GameObject corpoObj = CriarRetangulo("CorpoMenu", pai);
        LayoutElement leCorpo = corpoObj.AddComponent<LayoutElement>();
        leCorpo.flexibleHeight = 1f;

        HorizontalLayoutGroup layoutCorpo = corpoObj.AddComponent<HorizontalLayoutGroup>();
        layoutCorpo.spacing = 12;
        layoutCorpo.childControlWidth = true;
        layoutCorpo.childControlHeight = true;
        layoutCorpo.childForceExpandWidth = false;
        layoutCorpo.childForceExpandHeight = true;

        GameObject colunaPrincipal = CriarRetangulo("ColunaPrincipal", corpoObj.transform);
        LayoutElement leColunaPrincipal = colunaPrincipal.AddComponent<LayoutElement>();
        leColunaPrincipal.minWidth = 0;
        leColunaPrincipal.preferredWidth = 0;
        leColunaPrincipal.flexibleWidth = 1f;

        Image imgColuna = colunaPrincipal.AddComponent<Image>();
        imgColuna.color = new Color(0.04f, 0.06f, 0.08f, 0.48f);

        Outline outlineColuna = colunaPrincipal.AddComponent<Outline>();
        outlineColuna.effectColor = corDivisoria;
        outlineColuna.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup layoutColuna = colunaPrincipal.AddComponent<VerticalLayoutGroup>();
        layoutColuna.padding = new RectOffset(10, 10, 10, 10); // Margem restaurada (padding agora na Grade!)
        layoutColuna.spacing = 8;
        layoutColuna.childControlWidth = true;
        layoutColuna.childControlHeight = true;
        layoutColuna.childForceExpandWidth = true;
        layoutColuna.childForceExpandHeight = false;

        CriarAreaScroll(colunaPrincipal.transform);
        CriarPainelLateralDecorativo(corpoObj.transform);
    }

    void CriarAreaScroll(Transform pai)
    {
        GameObject bodyObj = CriarRetangulo("Body_Scroll", pai);
        LayoutElement leBody = bodyObj.AddComponent<LayoutElement>();
        leBody.flexibleHeight = 1f;

        Image imgBody = bodyObj.AddComponent<Image>();
        imgBody.color = new Color(0f, 0f, 0f, 0.18f);
        imgBody.raycastTarget = true;

        ScrollRect sr = bodyObj.AddComponent<ScrollRect>();
        sr.scrollSensitivity = 15f;
        sr.decelerationRate = 0.135f;
        sr.elasticity = 0.1f;
        sr.inertia = true;
        sr.horizontal = false;
        sr.vertical = true;
        scrollRectBotoes = sr;

        GameObject viewport = CriarRetangulo("Viewport", bodyObj.transform);
        Image imgView = viewport.AddComponent<Image>();
        imgView.color = Color.clear;
        imgView.raycastTarget = false;
        viewport.AddComponent<RectMask2D>();

        RectTransform rtView = viewport.GetComponent<RectTransform>();
        EsticarRectTransform(rtView);
        rtView.offsetMin = new Vector2(8f, 8f);
        rtView.offsetMax = new Vector2(-8f, -8f);
        viewportBotoes = rtView;

        GameObject content = CriarRetangulo("Content_Grid", viewport.transform);
        containerBotoes = content.transform;

        RectTransform rtContent = content.GetComponent<RectTransform>();
        rtContent.anchorMin = new Vector2(0f, 1f);
        rtContent.anchorMax = new Vector2(1f, 1f);
        rtContent.pivot = new Vector2(0f, 1f); // PIVOT EM 0 IMPEDE QUE A GRADE "VAZE" PARA A ESQUERDA!

        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(138f, 186f);
        grid.spacing = new Vector2(16f, 16f);
        grid.padding = new RectOffset(30, 10, 10, 20); // 5% movido para a esquerda (de 60 foi pra 30)
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.Flexible; // Permite preencher todo o espaço disponível sem limite
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        gradeBotoes = grid;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = rtContent;
        sr.viewport = rtView;
    }

    void CriarPainelLateralDecorativo(Transform pai)
    {
        GameObject lateralSlotObj = CriarRetangulo("PainelLateralSlot", pai);
        LayoutElement leSlot = lateralSlotObj.AddComponent<LayoutElement>();
        leSlot.minWidth = 190; // Ainda MAIS largo para acomodar fontes maiores e imagem gigante
        leSlot.preferredWidth = 200;
        leSlot.flexibleWidth = 0f;

        GameObject lateralObj = CriarRetangulo("PainelLateral", lateralSlotObj.transform);
        EsticarRectTransform(lateralObj.GetComponent<RectTransform>());

        CanvasGroup group = lateralObj.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        Image imgLateral = lateralObj.AddComponent<Image>();
        imgLateral.color = new Color(0.05f, 0.09f, 0.12f, 0.92f);
        imgLateral.raycastTarget = false;

        Outline outline = lateralObj.AddComponent<Outline>();
        outline.effectColor = corDivisoria;
        outline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup layoutLateral = lateralObj.AddComponent<VerticalLayoutGroup>();
        layoutLateral.padding = new RectOffset(6, 6, 6, 6);
        layoutLateral.spacing = 5;
        layoutLateral.childControlWidth = true;
        layoutLateral.childControlHeight = true;
        layoutLateral.childForceExpandWidth = true;
        layoutLateral.childForceExpandHeight = false;

        CriarCabecalhoLateral("Ficha do Item", lateralObj.transform);

        GameObject previewObj = CriarCartaoLateral("PreviewItem", lateralObj.transform);
        LayoutElement lePreview = previewObj.AddComponent<LayoutElement>();
        lePreview.minHeight = 150; // Quase o dobro de altura pro preview
        lePreview.preferredHeight = 150;

        GameObject iconFrame = CriarRetangulo("IconeFrame", previewObj.transform);
        LayoutElement leIcone = iconFrame.AddComponent<LayoutElement>();
        leIcone.minHeight = 100; // Imagem gigante agora, mais alta que as dos botões (84)
        leIcone.preferredHeight = 100;

        Image imgFrame = iconFrame.AddComponent<Image>();
        imgFrame.color = new Color(1f, 1f, 1f, 0.04f);
        imgFrame.raycastTarget = false;

        Outline outlineFrame = iconFrame.AddComponent<Outline>();
        outlineFrame.effectColor = new Color(1f, 1f, 1f, 0.06f);
        outlineFrame.effectDistance = new Vector2(1f, -1f);

        GameObject iconSpriteObj = CriarRetangulo("IconeSprite", iconFrame.transform);
        imagemDetalheIcone = iconSpriteObj.AddComponent<Image>();
        imagemDetalheIcone.preserveAspect = true;
        imagemDetalheIcone.raycastTarget = false;
        EsticarRectTransform(iconSpriteObj.GetComponent<RectTransform>());
        iconSpriteObj.GetComponent<RectTransform>().offsetMin = new Vector2(5f, 5f);
        iconSpriteObj.GetComponent<RectTransform>().offsetMax = new Vector2(-5f, -5f);

        GameObject iconFallbackObj = CriarRetangulo("IconeFallback", iconFrame.transform);
        textoDetalheIconeFallback = iconFallbackObj.AddComponent<Text>();
        textoDetalheIconeFallback.font = ObterFontePadrao();
        textoDetalheIconeFallback.fontSize = 32; // Glifo cresce também
        textoDetalheIconeFallback.fontStyle = FontStyle.Bold;
        textoDetalheIconeFallback.alignment = TextAnchor.MiddleCenter;
        textoDetalheIconeFallback.color = corTextoSecundario;
        textoDetalheIconeFallback.raycastTarget = false;
        EsticarRectTransform(iconFallbackObj.GetComponent<RectTransform>());

        GameObject nomeObj = CriarRetangulo("NomeDetalhe", previewObj.transform);
        LayoutElement leNome = nomeObj.AddComponent<LayoutElement>();
        leNome.minHeight = 24; // Mais espaço pro texto
        leNome.preferredHeight = 24;

        textoDetalheNome = nomeObj.AddComponent<Text>();
        textoDetalheNome.font = ObterFontePadrao();
        textoDetalheNome.fontSize = 15; // Nome grandão
        textoDetalheNome.fontStyle = FontStyle.Bold;
        textoDetalheNome.alignment = TextAnchor.MiddleCenter;
        textoDetalheNome.color = corTextoPrimario;
        textoDetalheNome.resizeTextForBestFit = true;
        textoDetalheNome.resizeTextMinSize = 9;
        textoDetalheNome.resizeTextMaxSize = 15;
        textoDetalheNome.raycastTarget = false;

        GameObject categoriaObj = CriarRetangulo("CategoriaDetalhe", previewObj.transform);
        LayoutElement leCategoria = categoriaObj.AddComponent<LayoutElement>();
        leCategoria.minHeight = 16;
        leCategoria.preferredHeight = 16;

        textoDetalheCategoria = categoriaObj.AddComponent<Text>();
        textoDetalheCategoria.font = ObterFontePadrao();
        textoDetalheCategoria.fontSize = 11; // fonte categoria legivel
        textoDetalheCategoria.alignment = TextAnchor.MiddleCenter;
        textoDetalheCategoria.color = corTextoSecundario;
        textoDetalheCategoria.raycastTarget = false;

        CriarSeparadorLateral(lateralObj.transform);

        GameObject infoObj = CriarCartaoLateral("InfoItem", lateralObj.transform);
        textoDetalhePreco = CriarLinhaInfoDetalhe("Custo", infoObj.transform);
        textoDetalheTipo = CriarLinhaInfoDetalhe("Tipo", infoObj.transform);
        textoDetalheVelocidade = CriarLinhaInfoDetalhe("Velocidade", infoObj.transform);
        textoDetalheVida = CriarLinhaInfoDetalhe("Blindagem", infoObj.transform);
        textoDetalhePoderFogo = CriarLinhaInfoDetalhe("Poder Ofensivo", infoObj.transform);

        GameObject descricaoBox = CriarCartaoLateral("DescricaoItem", lateralObj.transform);
        LayoutElement leDescricaoBox = descricaoBox.AddComponent<LayoutElement>();
        leDescricaoBox.minHeight = 110;
        leDescricaoBox.preferredHeight = 110;

        CriarCabecalhoLateral("Serve Para", descricaoBox.transform);

        GameObject descricaoObj = CriarRetangulo("TextoDescricao", descricaoBox.transform);
        LayoutElement leDescricao = descricaoObj.AddComponent<LayoutElement>();
        leDescricao.minHeight = 52;
        leDescricao.preferredHeight = 52;

        textoDetalheDescricao = descricaoObj.AddComponent<Text>();
        textoDetalheDescricao.font = ObterFontePadrao();
        textoDetalheDescricao.fontSize = 11; // fonte da descrição
        textoDetalheDescricao.alignment = TextAnchor.UpperLeft;
        textoDetalheDescricao.horizontalOverflow = HorizontalWrapMode.Wrap;
        textoDetalheDescricao.verticalOverflow = VerticalWrapMode.Truncate;
        textoDetalheDescricao.color = corTextoPrimario;
        textoDetalheDescricao.raycastTarget = false;

        AtualizarPainelDetalhes(null);
    }

    void CriarCabecalhoLateral(string titulo, Transform pai)
    {
        GameObject tituloObj = CriarRetangulo("Titulo_" + titulo, pai);
        LayoutElement leTitulo = tituloObj.AddComponent<LayoutElement>();
        leTitulo.minHeight = 14;
        leTitulo.preferredHeight = 14;

        Text texto = tituloObj.AddComponent<Text>();
        texto.text = titulo;
        texto.font = ObterFontePadrao();
        texto.fontSize = 11; // fonte cabecalho
        texto.fontStyle = FontStyle.Bold;
        texto.alignment = TextAnchor.MiddleLeft;
        texto.color = corTextoSecundario;
        texto.raycastTarget = false;
    }

    void CriarListaDecorativa(string rotulo, bool ativa, Transform pai)
    {
        GameObject itemObj = CriarRetangulo("Item_" + rotulo, pai);
        LayoutElement leItem = itemObj.AddComponent<LayoutElement>();
        leItem.minHeight = 22;
        leItem.preferredHeight = 22;

        HorizontalLayoutGroup layoutItem = itemObj.AddComponent<HorizontalLayoutGroup>();
        layoutItem.spacing = 8;
        layoutItem.childAlignment = TextAnchor.MiddleLeft;
        layoutItem.childControlWidth = false;
        layoutItem.childControlHeight = true;
        layoutItem.childForceExpandWidth = false;
        layoutItem.childForceExpandHeight = true;

        GameObject bulletObj = CriarRetangulo("Bullet", itemObj.transform);
        LayoutElement leBullet = bulletObj.AddComponent<LayoutElement>();
        leBullet.minWidth = 16;
        leBullet.preferredWidth = 16;

        Text bullet = bulletObj.AddComponent<Text>();
        bullet.text = ativa ? "[x]" : "[ ]";
        bullet.font = ObterFontePadrao();
        bullet.fontSize = 10;
        bullet.alignment = TextAnchor.MiddleCenter;
        bullet.color = ativa ? corDestaque : corTextoSecundario;
        bullet.raycastTarget = false;

        GameObject textoObj = CriarRetangulo("Texto", itemObj.transform);
        LayoutElement leTexto = textoObj.AddComponent<LayoutElement>();
        leTexto.flexibleWidth = 1f;

        Text texto = textoObj.AddComponent<Text>();
        texto.text = rotulo;
        texto.font = ObterFontePadrao();
        texto.fontSize = 11;
        texto.alignment = TextAnchor.MiddleLeft;
        texto.color = ativa ? corTextoPrimario : corTextoSecundario;
        texto.raycastTarget = false;
    }

    void CriarChipDecorativo(string rotulo, bool ativo, Transform pai)
    {
        GameObject chipObj = CriarRetangulo("Chip_" + rotulo, pai);
        LayoutElement leChip = chipObj.AddComponent<LayoutElement>();
        leChip.minWidth = 34;
        leChip.preferredWidth = 34;
        leChip.minHeight = 24;
        leChip.preferredHeight = 24;

        Image imgChip = chipObj.AddComponent<Image>();
        imgChip.color = ativo
            ? new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.24f)
            : new Color(1f, 1f, 1f, 0.06f);
        imgChip.raycastTarget = false;

        Outline outline = chipObj.AddComponent<Outline>();
        outline.effectColor = ativo ? new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.35f) : corDivisoria;
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject textoObj = CriarRetangulo("Texto", chipObj.transform);
        Text texto = textoObj.AddComponent<Text>();
        texto.text = rotulo;
        texto.font = ObterFontePadrao();
        texto.fontSize = 10;
        texto.fontStyle = FontStyle.Bold;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.color = ativo ? corTextoPrimario : corTextoSecundario;
        texto.raycastTarget = false;
        EsticarRectTransform(textoObj.GetComponent<RectTransform>());
    }

    void CriarSeparadorLateral(Transform pai)
    {
        GameObject separador = CriarRetangulo("Separador", pai);
        LayoutElement leSeparador = separador.AddComponent<LayoutElement>();
        leSeparador.minHeight = 1;
        leSeparador.preferredHeight = 1;

        Image imgSeparador = separador.AddComponent<Image>();
        imgSeparador.color = corDivisoria;
        imgSeparador.raycastTarget = false;
    }

    GameObject CriarCartaoLateral(string nome, Transform pai)
    {
        GameObject cardObj = CriarRetangulo(nome, pai);
        Image imgCard = cardObj.AddComponent<Image>();
        imgCard.color = new Color(1f, 1f, 1f, 0.04f);
        imgCard.raycastTarget = false;

        Outline outline = cardObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        outline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup layout = cardObj.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(5, 5, 5, 5);
        layout.spacing = 3;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return cardObj;
    }

    Text CriarLinhaInfoDetalhe(string rotulo, Transform pai)
    {
        GameObject linhaObj = CriarRetangulo("Linha_" + rotulo, pai);
        LayoutElement leLinha = linhaObj.AddComponent<LayoutElement>();
        leLinha.minHeight = 20; // Altura da linha de status
        leLinha.preferredHeight = 20;

        HorizontalLayoutGroup layoutLinha = linhaObj.AddComponent<HorizontalLayoutGroup>();
        layoutLinha.spacing = 2;
        layoutLinha.childAlignment = TextAnchor.MiddleLeft;
        layoutLinha.childControlWidth = true;
        layoutLinha.childControlHeight = true;
        layoutLinha.childForceExpandWidth = false;
        layoutLinha.childForceExpandHeight = true;

        GameObject rotuloObj = CriarRetangulo("Rotulo", linhaObj.transform);
        LayoutElement leRotulo = rotuloObj.AddComponent<LayoutElement>();
        leRotulo.minWidth = 72; // Muito mais largo para caber "Velocidade", "Blindagem" sem quebrar linha
        leRotulo.preferredWidth = 72;

        Text textoRotulo = rotuloObj.AddComponent<Text>();
        textoRotulo.text = rotulo;
        textoRotulo.font = ObterFontePadrao();
        textoRotulo.fontSize = 11; // Etiqueta tipo "Tipo", "Custo"
        textoRotulo.fontStyle = FontStyle.Bold;
        textoRotulo.alignment = TextAnchor.MiddleLeft;
        textoRotulo.color = corTextoSecundario;
        textoRotulo.raycastTarget = false;

        GameObject valorObj = CriarRetangulo("Valor", linhaObj.transform);
        LayoutElement leValor = valorObj.AddComponent<LayoutElement>();
        leValor.flexibleWidth = 1f;

        Text textoValor = valorObj.AddComponent<Text>();
        textoValor.font = ObterFontePadrao();
        textoValor.fontSize = 12; // Valor real em si
        textoValor.alignment = TextAnchor.MiddleRight;
        textoValor.color = corTextoPrimario;
        textoValor.resizeTextForBestFit = true;
        textoValor.resizeTextMinSize = 9;
        textoValor.resizeTextMaxSize = 12;
        textoValor.raycastTarget = false;

        return textoValor;
    }

    void AtualizarFiltroBusca(string novoTexto)
    {
        if (ignorarEventoBusca)
        {
            return;
        }

        filtroBuscaAtual = novoTexto ?? string.Empty;

        if (campoBusca != null && campoBusca.text != filtroBuscaAtual)
        {
            DefinirTextoBuscaSemEvento(filtroBuscaAtual);
        }

        FiltrarPorCategoria(categoriaAtual);
    }

    bool ItemPassaFiltroBusca(DadosConstrucao item)
    {
        if (item == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(filtroBuscaAtual))
        {
            return true;
        }

        string filtro = filtroBuscaAtual.Trim();
        if (filtro.Length == 0)
        {
            return true;
        }

        string displayName = item.GetDisplayName();

        return (!string.IsNullOrEmpty(item.NomeItem) && item.NomeItem.IndexOf(filtro, System.StringComparison.OrdinalIgnoreCase) >= 0)
            || (!string.IsNullOrEmpty(displayName) && displayName.IndexOf(filtro, System.StringComparison.OrdinalIgnoreCase) >= 0)
            || (!string.IsNullOrEmpty(item.descricao) && item.descricao.IndexOf(filtro, System.StringComparison.OrdinalIgnoreCase) >= 0)
            || ObterRotuloCategoria(item.categoria).IndexOf(filtro, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void AjustarGradeAoEspacoDisponivel()
    {
        if (gradeBotoes == null || viewportBotoes == null)
        {
            return;
        }

        float larguraViewport = viewportBotoes.rect.width;
        if (larguraViewport <= 0f)
        {
            return;
        }

        float larguraUtil = larguraViewport - gradeBotoes.padding.left - gradeBotoes.padding.right;
        float bloco = gradeBotoes.cellSize.x + gradeBotoes.spacing.x;
        int colunas = Mathf.FloorToInt((larguraUtil + gradeBotoes.spacing.x) / bloco);
        colunas = Mathf.Max(colunas, 4); // Remove o teto de 7, permite quantas colunas couberem na tela!

        if (gradeBotoes.constraint != GridLayoutGroup.Constraint.FixedColumnCount)
        {
            gradeBotoes.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        }

        if (gradeBotoes.constraintCount != colunas)
        {
            gradeBotoes.constraintCount = colunas;
        }
    }

    void AtualizarPainelDetalhes(DadosConstrucao item)
    {
        if (textoDetalheNome == null)
        {
            return;
        }

        if (item != null && item == itemDetalheAtual)
        {
            return;
        }

        itemDetalheAtual = item;

        if (item == null)
        {
            if (imagemDetalheIcone != null)
            {
                imagemDetalheIcone.sprite = null;
                imagemDetalheIcone.color = Color.clear;
            }

            if (textoDetalheIconeFallback != null)
            {
                textoDetalheIconeFallback.text = ObterGlifoPadrao(categoriaAtual);
            }

            textoDetalheNome.text = "Selecione um item";
            textoDetalheCategoria.text = ObterRotuloCategoria(categoriaAtual);
            textoDetalhePreco.text = "--";
            textoDetalheTipo.text = "--";
            textoDetalheVelocidade.text = "--";
            if (textoDetalheVida != null) textoDetalheVida.text = "--";
            if (textoDetalhePoderFogo != null) textoDetalhePoderFogo.text = "--";
            textoDetalheDescricao.text = "Passe o mouse sobre um card para ver velocidade, funcao e detalhes da unidade.";
            return;
        }

        if (imagemDetalheIcone != null)
        {
            Sprite iconeItem = ObterIconeItem(item);
            imagemDetalheIcone.sprite = iconeItem;
            imagemDetalheIcone.color = iconeItem != null ? Color.white : Color.clear;
        }

        if (textoDetalheIconeFallback != null)
        {
            ConfigVisualCategoria config = ObterConfigVisualCategoria(item.categoria);
            textoDetalheIconeFallback.text = ObterIconeItem(item) == null
                ? (config != null ? config.glifoFallback : ObterGlifoPadrao(item.categoria))
                : string.Empty;
        }

        textoDetalheNome.text = item.GetDisplayName();
        textoDetalheCategoria.text = ObterRotuloCategoria(item.categoria);
        textoDetalhePreco.text = ValoresDefinitivosHegemonia.FormatarDinheiro(PrecoEfetivo(item));
        textoDetalheTipo.text = ObterTipoItem(item);
        textoDetalheVelocidade.text = ObterTextoVelocidadeItem(item);
        if (textoDetalheVida != null) textoDetalheVida.text = ObterTextoVidaItem(item);
        if (textoDetalhePoderFogo != null) textoDetalhePoderFogo.text = ObterTextoPoderFogoItem(item);
        textoDetalheDescricao.text = ObterDescricaoDetalheItem(item);
    }

    void AdicionarEventoHoverDetalhes(GameObject alvo, DadosConstrucao item)
    {
        if (alvo == null || item == null)
        {
            return;
        }

        EventTrigger trigger = alvo.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = alvo.AddComponent<EventTrigger>();
        }

        if (trigger.triggers == null)
        {
            trigger.triggers = new List<EventTrigger.Entry>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerEnter;
        entry.callback.AddListener((_) => AtualizarPainelDetalhes(item));
        trigger.triggers.Add(entry);
    }

    void CriarBotaoAbaModerno(DadosConstrucao.CategoriaItem categoria, Transform pai)
    {
        GameObject btnObj = CriarRetangulo("Aba_" + categoria, pai);
        LayoutElement leBotao = btnObj.AddComponent<LayoutElement>();
        leBotao.minWidth = 60;
        leBotao.preferredWidth = 60;
        leBotao.minHeight = 28; // Trazendo um pouco do tamanho de volta (+10% perceptível)
        leBotao.preferredHeight = 28;
        leBotao.flexibleHeight = 0f;

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.05f);

        Button btn = btnObj.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.98f);
        colors.pressedColor = new Color(0.85f, 0.95f, 1f, 0.95f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.05f;
        btn.colors = colors;

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = corDivisoria;
        outline.effectDistance = new Vector2(1f, -1f);

        Shadow glow = btnObj.AddComponent<Shadow>();
        glow.effectColor = new Color(0f, 0.75f, 1f, 0.08f);
        glow.effectDistance = new Vector2(0f, -2f);

        GameObject accentObj = CriarRetangulo("Accent", btnObj.transform);
        Image accent = accentObj.AddComponent<Image>();
        accent.color = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0f);
        accent.raycastTarget = false;
        RectTransform rtAccent = accentObj.GetComponent<RectTransform>();
        rtAccent.anchorMin = new Vector2(0f, 0f);
        rtAccent.anchorMax = new Vector2(1f, 0f);
        rtAccent.pivot = new Vector2(0.5f, 0f);
        rtAccent.sizeDelta = new Vector2(0f, 2f);

        GameObject conteudoObj = CriarRetangulo("Conteudo", btnObj.transform);
        RectTransform rtConteudo = conteudoObj.GetComponent<RectTransform>();
        EsticarRectTransform(rtConteudo);
        rtConteudo.offsetMin = new Vector2(0f, 2f);

        VerticalLayoutGroup layoutConteudo = conteudoObj.AddComponent<VerticalLayoutGroup>();
        layoutConteudo.padding = new RectOffset(2, 2, 3, 2);
        layoutConteudo.spacing = 1;
        layoutConteudo.childAlignment = TextAnchor.MiddleCenter;
        layoutConteudo.childControlWidth = true;
        layoutConteudo.childControlHeight = true;
        layoutConteudo.childForceExpandWidth = true;
        layoutConteudo.childForceExpandHeight = false;

        GameObject iconeFrame = CriarRetangulo("Icone", conteudoObj.transform);
        LayoutElement leIcone = iconeFrame.AddComponent<LayoutElement>();
        leIcone.minHeight = 12; // Compensando
        leIcone.preferredHeight = 12;

        ConfigVisualCategoria config = ObterConfigVisualCategoria(categoria);
        if (config != null && config.icone != null)
        {
            GameObject spriteObj = CriarRetangulo("IconeSprite", iconeFrame.transform);
            Image imgIcone = spriteObj.AddComponent<Image>();
            imgIcone.sprite = config.icone;
            imgIcone.preserveAspect = true;
            imgIcone.color = new Color(0.88f, 0.94f, 1f, 0.95f);
            imgIcone.raycastTarget = false;
            EsticarRectTransform(spriteObj.GetComponent<RectTransform>());
        }
        else
        {
            GameObject glyphObj = CriarRetangulo("IconeGlyph", iconeFrame.transform);
            Text glyph = glyphObj.AddComponent<Text>();
            glyph.text = config != null ? config.glifoFallback : ObterGlifoPadrao(categoria);
            glyph.font = ObterFontePadrao();
            glyph.alignment = TextAnchor.MiddleCenter;
            glyph.fontSize = 9;
            glyph.fontStyle = FontStyle.Bold;
            glyph.color = corTextoSecundario;
            glyph.raycastTarget = false;
            EsticarRectTransform(glyphObj.GetComponent<RectTransform>());
        }

        GameObject txtObj = CriarRetangulo("Texto", conteudoObj.transform);
        LayoutElement leTexto = txtObj.AddComponent<LayoutElement>();
        leTexto.minHeight = 10;
        leTexto.preferredHeight = 10;

        Text txt = txtObj.AddComponent<Text>();
        txt.text = ObterRotuloCategoria(categoria);
        txt.font = ObterFontePadrao();
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 6;
        txt.fontStyle = FontStyle.Bold;
        txt.color = corTextoSecundario;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 5;
        txt.resizeTextMaxSize = 6;
        txt.raycastTarget = false;

        btn.onClick.AddListener(() => 
        {
            if(ModoDemolicao.Instancia) ModoDemolicao.Instancia.AlternarModo(false); 

            if (!menuAberto) AlternarMenu(true);
            FiltrarPorCategoria(categoria);
        });
    }

    void AtualizarVisualAbas(DadosConstrucao.CategoriaItem catAtiva)
    {
        if (containerAbas == null) return;
        foreach (Transform aba in containerAbas)
        {
            Image img = aba.GetComponent<Image>();
            Outline outline = aba.GetComponent<Outline>();
            Shadow shadow = aba.GetComponent<Shadow>();
            Text txt = aba.Find("Conteudo/Texto") != null ? aba.Find("Conteudo/Texto").GetComponent<Text>() : null;
            Image accent = aba.Find("Accent") != null ? aba.Find("Accent").GetComponent<Image>() : null;
            Image spriteIcone = aba.Find("Conteudo/Icone/IconeSprite") != null ? aba.Find("Conteudo/Icone/IconeSprite").GetComponent<Image>() : null;
            Text glyphIcone = aba.Find("Conteudo/Icone/IconeGlyph") != null ? aba.Find("Conteudo/Icone/IconeGlyph").GetComponent<Text>() : null;

            bool ehAtiva = aba.name == "Aba_" + catAtiva;

            if (ehAtiva)
            {
                img.color = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.18f);
                if (outline != null) outline.effectColor = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.45f);
                if (shadow != null) shadow.effectColor = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.16f);
                if (accent != null) accent.color = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.95f);
                if (txt != null)
                {
                    txt.color = corTextoPrimario;
                    txt.fontSize = 6;
                }
                if (spriteIcone != null) spriteIcone.color = Color.white;
                if (glyphIcone != null) glyphIcone.color = corDestaque;
            }
            else
            {
                img.color = new Color(1f, 1f, 1f, 0.05f);
                if (outline != null) outline.effectColor = corDivisoria;
                if (shadow != null) shadow.effectColor = new Color(0f, 0.75f, 1f, 0.08f);
                if (accent != null) accent.color = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0f);
                if (txt != null)
                {
                    txt.color = corTextoSecundario;
                    txt.fontSize = 6;
                }
                if (spriteIcone != null) spriteIcone.color = new Color(0.78f, 0.85f, 0.9f, 0.78f);
                if (glyphIcone != null) glyphIcone.color = corTextoSecundario;
            }
        }
    }

    public static bool AbrirCategoria(DadosConstrucao.CategoriaItem categoriaDesejada)
    {
        MenuConstrucao menu = Object.FindFirstObjectByType<MenuConstrucao>();
        if (menu == null) return false;

        menu.AlternarMenu(true);
        menu.FiltrarPorCategoria(categoriaDesejada);
        return true;
    }

    public void FiltrarPorCategoria(DadosConstrucao.CategoriaItem categoriaDesejada)
    {
        categoriaAtual = categoriaDesejada;
        AtualizarVisualAbas(categoriaDesejada);
        GarantirCatalogoValido();

        if (containerBotoes == null) return;

        foreach (Transform child in containerBotoes) Destroy(child.gameObject);

        List<DadosConstrucao> itensDaCategoria = ObterItensDaCategoria(categoriaDesejada, false);
        List<DadosConstrucao> itensFiltrados = string.IsNullOrWhiteSpace(filtroBuscaAtual)
            ? itensDaCategoria
            : ObterItensDaCategoria(categoriaDesejada, true);

        DadosConstrucao primeiroItemCategoria = null;
        foreach (DadosConstrucao item in itensFiltrados)
        {
            if (item != null)
            {
                if (primeiroItemCategoria == null)
                {
                    primeiroItemCategoria = item;
                }
                CriarCardItemModerno(item);
            }
        }

        resetarScrollParaTopoPendente = true;
        AtualizarPainelDetalhes(primeiroItemCategoria);
        
        StartCoroutine(AtualizarLayouts());
    }

    IEnumerator AtualizarLayouts()
    {
        yield return new WaitForEndOfFrame();
        AjustarGradeAoEspacoDisponivel();
        if(containerBotoes != null) LayoutRebuilder.ForceRebuildLayoutImmediate(containerBotoes.GetComponent<RectTransform>());
        if (scrollRectBotoes != null && resetarScrollParaTopoPendente)
        {
            Canvas.ForceUpdateCanvases();
            scrollRectBotoes.verticalNormalizedPosition = 1f;
            resetarScrollParaTopoPendente = false;
        }
    }

    void CriarCardItemModerno(DadosConstrucao item)
    {
        GameObject cardObj = CriarRetangulo("Card_" + item.NomeItem, containerBotoes);
        Image imgBg = cardObj.AddComponent<Image>();
        imgBg.color = corCardBase;
        imgBg.raycastTarget = true;

        Outline outline = cardObj.AddComponent<Outline>();
        outline.effectColor = corBordaCartao;
        outline.effectDistance = new Vector2(1f, -1f);

        Shadow shadow = cardObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0.65f, 0.85f, 0.08f);
        shadow.effectDistance = new Vector2(0f, -2f);

        Button btnCard = cardObj.AddComponent<Button>();
        btnCard.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = btnCard.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.98f, 1f, 1f);
        colors.pressedColor = new Color(0.82f, 0.9f, 1f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.05f;
        btnCard.colors = colors;
        // O card continua sendo uma compra rapida. A quantidade escolhida nos
        // controles e usada por ConstruirItem; os botoes filhos (+, - e
        // COMPRAR/CONSTRUIR) continuam tendo seus proprios listeners e nao
        // perdem o valor ao abrir os detalhes.
        btnCard.onClick.AddListener(() =>
        {
            AtualizarPainelDetalhes(item);
            ConstruirItem(item, imgBg);
        });
        AdicionarEventoHoverDetalhes(cardObj, item);

        VerticalLayoutGroup layoutCard = cardObj.AddComponent<VerticalLayoutGroup>();
        layoutCard.padding = new RectOffset(7, 7, 7, 7);
        layoutCard.spacing = 4;
        layoutCard.childControlHeight = true;
        layoutCard.childControlWidth = true;
        layoutCard.childForceExpandWidth = true;
        layoutCard.childForceExpandHeight = false;

        GameObject brilhoTopo = CriarRetangulo("BrilhoTopo", cardObj.transform);
        LayoutElement leBrilho = brilhoTopo.AddComponent<LayoutElement>();
        leBrilho.minHeight = 2;
        leBrilho.preferredHeight = 2;
        Image imgBrilho = brilhoTopo.AddComponent<Image>();
        imgBrilho.color = new Color(corDestaque.r, corDestaque.g, corDestaque.b, 0.65f);
        imgBrilho.raycastTarget = false;

        GameObject iconArea = CriarRetangulo("AreaIcone", cardObj.transform);
        LayoutElement leIcon = iconArea.AddComponent<LayoutElement>();
        leIcon.minHeight = 84;
        leIcon.preferredHeight = 84;
        leIcon.flexibleHeight = 0;

        Image imgFrame = iconArea.AddComponent<Image>();
        imgFrame.color = new Color(1f, 1f, 1f, 0.04f);
        imgFrame.raycastTarget = false;

        Outline frameOutline = iconArea.AddComponent<Outline>();
        frameOutline.effectColor = new Color(1f, 1f, 1f, 0.08f);
        frameOutline.effectDistance = new Vector2(1f, -1f);

        GameObject iconHolder = CriarRetangulo("IconeHolder", iconArea.transform);
        RectTransform rtIconHolder = iconHolder.GetComponent<RectTransform>();
        EsticarRectTransform(rtIconHolder);
        rtIconHolder.offsetMin = new Vector2(8f, 8f);
        rtIconHolder.offsetMax = new Vector2(-8f, -8f);

        Image imgIcon = iconHolder.AddComponent<Image>();
        imgIcon.raycastTarget = false;

        Sprite iconeItem = ObterIconeItem(item);
        if (iconeItem != null)
        {
            imgIcon.sprite = iconeItem;
            imgIcon.preserveAspect = true;
            imgIcon.color = Color.white;
        }
        else
        {
            imgIcon.color = new Color(1f, 1f, 1f, 0.1f);
            GameObject textPlace = CriarRetangulo("TxtPlace", iconHolder.transform);
            Text tPlace = textPlace.AddComponent<Text>();
            tPlace.text = "NO IMAGE";
            tPlace.font = ObterFontePadrao();
            tPlace.alignment = TextAnchor.MiddleCenter;
            tPlace.fontSize = 11;
            tPlace.color = new Color(1f, 1f, 1f, 0.24f);
            tPlace.resizeTextForBestFit = true;
            tPlace.resizeTextMaxSize = 11;
            tPlace.raycastTarget = false;
            EsticarRectTransform(textPlace.GetComponent<RectTransform>());
        }

        GameObject nomeObj = CriarRetangulo("NomeItem", cardObj.transform);
        LayoutElement leNome = nomeObj.AddComponent<LayoutElement>();
        leNome.minHeight = 30;
        leNome.preferredHeight = 30;

        Text tNome = nomeObj.AddComponent<Text>();
        tNome.text = item.GetDisplayName();
        tNome.font = ObterFontePadrao();
        tNome.fontSize = 14;
        tNome.alignment = TextAnchor.MiddleCenter;
        tNome.color = corTextoPrimario;
        tNome.fontStyle = FontStyle.Bold;
        tNome.resizeTextForBestFit = true;
        tNome.resizeTextMinSize = 10;
        tNome.resizeTextMaxSize = 14;
        tNome.raycastTarget = false;

        GameObject precoObj = CriarRetangulo("Preco", cardObj.transform);
        LayoutElement lePreco = precoObj.AddComponent<LayoutElement>();
        lePreco.minHeight = 18;
        lePreco.preferredHeight = 18;

        Text tPreco = precoObj.AddComponent<Text>();
        tPreco.text = $"<color=#44FF88>{ValoresDefinitivosHegemonia.FormatarDinheiro(PrecoEfetivo(item))}</color>";
        tPreco.font = ObterFontePadrao();
        tPreco.fontSize = 13;
        tPreco.alignment = TextAnchor.MiddleCenter;
        tPreco.supportRichText = true;
        tPreco.raycastTarget = false;

        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(cardObj.transform, false);
        spacer.AddComponent<RectTransform>();
        LayoutElement leSpacer = spacer.AddComponent<LayoutElement>();
        leSpacer.flexibleHeight = 1;

        GameObject controlsObj = CriarRetangulo("Controles", cardObj.transform);
        LayoutElement leControls = controlsObj.AddComponent<LayoutElement>();
        leControls.minHeight = 34;
        leControls.preferredHeight = 34;

        Image imgControls = controlsObj.AddComponent<Image>();
        imgControls.color = new Color(0.08f, 0.12f, 0.16f, 0.95f);
        imgControls.raycastTarget = false;

        Outline outlineControls = controlsObj.AddComponent<Outline>();
        outlineControls.effectColor = new Color(1f, 1f, 1f, 0.06f);
        outlineControls.effectDistance = new Vector2(1f, -1f);

        HorizontalLayoutGroup layoutControls = controlsObj.AddComponent<HorizontalLayoutGroup>();
        layoutControls.padding = new RectOffset(4, 4, 4, 4);
        layoutControls.spacing = 4;
        layoutControls.childControlWidth = true;
        layoutControls.childForceExpandWidth = true;
        layoutControls.childControlHeight = true;
        layoutControls.childForceExpandHeight = true;

        GameObject qtdBox = CriarRetangulo("BoxQtd", controlsObj.transform);
        LayoutElement leQtdBox = qtdBox.AddComponent<LayoutElement>();
        leQtdBox.minWidth = 58;
        leQtdBox.preferredWidth = 58;

        Image imgQtdBox = qtdBox.AddComponent<Image>();
        imgQtdBox.color = new Color(1f, 1f, 1f, 0.05f);
        imgQtdBox.raycastTarget = false;

        Outline outlineQtd = qtdBox.AddComponent<Outline>();
        outlineQtd.effectColor = new Color(1f, 1f, 1f, 0.05f);
        outlineQtd.effectDistance = new Vector2(1f, -1f);

        HorizontalLayoutGroup layoutQtd = qtdBox.AddComponent<HorizontalLayoutGroup>();
        layoutQtd.spacing = 1;
        layoutQtd.childAlignment = TextAnchor.MiddleCenter;
        layoutQtd.childControlWidth = true;
        layoutQtd.childControlHeight = true;
        layoutQtd.childForceExpandWidth = false;
        layoutQtd.childForceExpandHeight = true;

        if (!quantidadesPorItem.ContainsKey(item.NomeItem)) quantidadesPorItem[item.NomeItem] = 1;

        GameObject btnMenos = CriarBotaoSimples("-", qtdBox.transform, new Color(1,0.3f,0.3f));

        GameObject txtQtdObj = CriarRetangulo("TxtQtd", qtdBox.transform);
        Text tQtd = txtQtdObj.AddComponent<Text>();
        tQtd.text = quantidadesPorItem[item.NomeItem].ToString();
        tQtd.font = ObterFontePadrao();
        tQtd.alignment = TextAnchor.MiddleCenter;
        tQtd.color = Color.white;
        tQtd.fontSize = 13;
        tQtd.raycastTarget = false;
        LayoutElement leTxtQ = txtQtdObj.AddComponent<LayoutElement>();
        leTxtQ.minWidth = 18;
        leTxtQ.preferredWidth = 18;
        leTxtQ.flexibleWidth = 1;

        GameObject btnMais = CriarBotaoSimples("+", qtdBox.transform, new Color(0.3f,1f,0.3f));

        GameObject btnComprarObj = CriarRetangulo("BtnComprar", controlsObj.transform);
        Image imgComprar = btnComprarObj.AddComponent<Image>();
        imgComprar.color = new Color(0.12f, 0.46f, 0.24f, 0.95f);

        Outline outlineComprar = btnComprarObj.AddComponent<Outline>();
        outlineComprar.effectColor = new Color(0.45f, 1f, 0.65f, 0.22f);
        outlineComprar.effectDistance = new Vector2(1f, -1f);

        Button btnComp = btnComprarObj.AddComponent<Button>();
        LayoutElement leComp = btnComprarObj.AddComponent<LayoutElement>();
        leComp.flexibleWidth = 1.4f;

        GameObject txtCompObj = CriarRetangulo("TxtComp", btnComprarObj.transform);
        Text tComp = txtCompObj.AddComponent<Text>();
        tComp.text = ClassificarFluxo(item) == TipoFluxoConstrucao.Estrutura ? "CONSTRUIR" : "COMPRAR";
        tComp.font = ObterFontePadrao();
        tComp.alignment = TextAnchor.MiddleCenter;
        tComp.color = Color.white;
        tComp.fontSize = 9;
        tComp.fontStyle = FontStyle.Bold;
        tComp.resizeTextForBestFit = true;
        tComp.resizeTextMaxSize = 10;
        tComp.resizeTextMinSize = 6;
        tComp.raycastTarget = false;
        EsticarRectTransform(txtCompObj.GetComponent<RectTransform>());

        Text refTextoQtd = tQtd;
        btnMenos.GetComponent<Button>().onClick.AddListener(() => AlterarQuantidade(item.NomeItem, -1, refTextoQtd));
        btnMenos.GetComponent<Button>().onClick.AddListener(() => AtualizarPainelDetalhes(item));
        btnMais.GetComponent<Button>().onClick.AddListener(() => AlterarQuantidade(item.NomeItem, 1, refTextoQtd));
        btnMais.GetComponent<Button>().onClick.AddListener(() => AtualizarPainelDetalhes(item));
        btnComp.onClick.AddListener(() => AtualizarPainelDetalhes(item));
        btnComp.onClick.AddListener(() => ConstruirItem(item, imgBg));

        AdicionarEventoHoverDetalhes(btnMenos, item);
        AdicionarEventoHoverDetalhes(btnMais, item);
        AdicionarEventoHoverDetalhes(btnComprarObj, item);
    }

    GameObject CriarBotaoSimples(string texto, Transform pai, Color corTexto)
    {
        GameObject btnObj = CriarRetangulo("Btn" + texto, pai);
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.08f, 0.12f, 0.16f, 0.95f);

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        outline.effectDistance = new Vector2(1f, -1f);

        Button btn = btnObj.AddComponent<Button>();

        GameObject txtObj = CriarRetangulo("Txt", btnObj.transform);
        Text t = txtObj.AddComponent<Text>();
        t.text = texto;
        t.font = ObterFontePadrao();
        t.color = corTexto;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontStyle = FontStyle.Bold;
        t.fontSize = 13;
        t.raycastTarget = false;
        EsticarRectTransform(txtObj.GetComponent<RectTransform>());

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.minWidth = 17;
        le.preferredWidth = 17;

        return btnObj;
    }

    void AlterarQuantidade(string idItem, int delta, Text textoUI)
    {
        if (quantidadesPorItem.ContainsKey(idItem))
        {
            int novaQtd = quantidadesPorItem[idItem] + delta;
            if (novaQtd < 1) novaQtd = 1;
            if (novaQtd > 50) novaQtd = 50;

            quantidadesPorItem[idItem] = novaQtd;
            textoUI.text = novaQtd.ToString();
        }
    }

    GameObject CriarRetangulo(string nome, Transform pai)
    {
        GameObject obj = new GameObject(nome);
        obj.transform.SetParent(pai, false);
        obj.AddComponent<RectTransform>();
        return obj;
    }

    void GarantirConfigsVisuaisCategoria()
    {
        if (aparenciasCategorias == null)
        {
            aparenciasCategorias = new List<ConfigVisualCategoria>();
        }

        lookupVisualCategorias.Clear();

        foreach (DadosConstrucao.CategoriaItem categoria in System.Enum.GetValues(typeof(DadosConstrucao.CategoriaItem)))
        {
            ConfigVisualCategoria config = aparenciasCategorias.FirstOrDefault(c => c != null && c.categoria == categoria);
            if (config == null)
            {
                config = new ConfigVisualCategoria
                {
                    categoria = categoria,
                    glifoFallback = ObterGlifoPadrao(categoria)
                };
                aparenciasCategorias.Add(config);
            }
            else if (string.IsNullOrWhiteSpace(config.glifoFallback))
            {
                config.glifoFallback = ObterGlifoPadrao(categoria);
            }

            lookupVisualCategorias[categoria] = config;
        }
    }

    ConfigVisualCategoria ObterConfigVisualCategoria(DadosConstrucao.CategoriaItem categoria)
    {
        if (lookupVisualCategorias.Count == 0)
        {
            GarantirConfigsVisuaisCategoria();
        }

        ConfigVisualCategoria config;
        if (lookupVisualCategorias.TryGetValue(categoria, out config))
        {
            return config;
        }

        return null;
    }

    string ObterRotuloCategoria(DadosConstrucao.CategoriaItem categoria)
    {
        switch (categoria)
        {
            case DadosConstrucao.CategoriaItem.Exercito:
                return "EXERCITO";
            case DadosConstrucao.CategoriaItem.Marinha:
                return "MARINHA";
            case DadosConstrucao.CategoriaItem.Aeronautica:
                return "AERONAUTICA";
            case DadosConstrucao.CategoriaItem.Tecnologia:
                return "TECNOLOGIA";
            case DadosConstrucao.CategoriaItem.Infraestrutura:
                return "INFRAESTRUTURA";
            case DadosConstrucao.CategoriaItem.Energia:
                return "ENERGIA";
            case DadosConstrucao.CategoriaItem.Urbana:
                return "URBANA";
            default:
                return categoria.ToString().ToUpperInvariant();
        }
    }

    string ObterGlifoPadrao(DadosConstrucao.CategoriaItem categoria)
    {
        switch (categoria)
        {
            case DadosConstrucao.CategoriaItem.Exercito:
                return "E";
            case DadosConstrucao.CategoriaItem.Marinha:
                return "N";
            case DadosConstrucao.CategoriaItem.Aeronautica:
                return "A";
            case DadosConstrucao.CategoriaItem.Tecnologia:
                return "T";
            case DadosConstrucao.CategoriaItem.Infraestrutura:
                return "I";
            case DadosConstrucao.CategoriaItem.Energia:
                return "P";
            case DadosConstrucao.CategoriaItem.Urbana:
                return "U";
            default:
                return "?";
        }
    }

    Font ObterFontePadrao()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    Sprite ObterIconeItem(DadosConstrucao item)
    {
        if (item == null)
        {
            return null;
        }

        if (item.icone != null)
        {
            string nomeIcone = item.icone.name != null ? item.icone.name.ToLowerInvariant() : string.Empty;
            if (!nomeIcone.Contains("bkwv7tbkwv7tbkwv"))
            {
                return item.icone;
            }
        }

        int chave = item.GetInstanceID();
        Sprite iconeResolvido;
        if (cacheIconesResolvidos.TryGetValue(chave, out iconeResolvido))
        {
            return iconeResolvido;
        }

#if UNITY_EDITOR
        iconeResolvido = ProcurarSpriteEditor(item);
        if (iconeResolvido == null && item.PrefabDaUnidade != null)
        {
            Texture2D preview = UnityEditor.AssetPreview.GetAssetPreview(item.PrefabDaUnidade)
                ?? UnityEditor.AssetPreview.GetMiniThumbnail(item.PrefabDaUnidade);
            if (preview != null)
            {
                iconeResolvido = Sprite.Create(preview, new Rect(0, 0, preview.width, preview.height), new Vector2(0.5f, 0.5f));
            }
        }
#endif

        if (iconeResolvido == null)
        {
            iconeResolvido = ProcurarSpriteRuntime(item);
        }

        if (iconeResolvido == null)
        {
            ConfigVisualCategoria config = ObterConfigVisualCategoria(item.categoria);
            if (config != null && config.icone != null)
            {
                iconeResolvido = config.icone;
            }
        }

        if (iconeResolvido == null)
        {
            iconeResolvido = ObterPlaceholderRuntimeIcone();
        }

        cacheIconesResolvidos[chave] = iconeResolvido;

        return iconeResolvido;
    }

    Sprite ProcurarSpriteRuntime(DadosConstrucao item)
    {
        if (item == null) return null;

        if (iconesRuntimeCatalogo == null || iconesRuntimeCatalogo.Length == 0)
        {
            iconesRuntimeCatalogo = Resources.LoadAll<Sprite>("IconesGerados");
        }

        string nomeItem = NormalizarTextoBuscaIcone(item.NomeItem);
        string nomePrefab = item.PrefabDaUnidade != null
            ? NormalizarTextoBuscaIcone(item.PrefabDaUnidade.name)
            : string.Empty;

        for (int i = 0; i < iconesRuntimeCatalogo.Length; i++)
        {
            Sprite sprite = iconesRuntimeCatalogo[i];
            if (sprite == null) continue;

            string nomeAsset = NormalizarTextoBuscaIcone(sprite.name);
            bool pareceMesmoItem = (!string.IsNullOrEmpty(nomeItem)
                && (nomeAsset.Contains(nomeItem) || nomeItem.Contains(nomeAsset)))
                || (!string.IsNullOrEmpty(nomePrefab)
                && (nomeAsset.Contains(nomePrefab) || nomePrefab.Contains(nomeAsset)));

            if (pareceMesmoItem) return sprite;
        }

        return null;
    }

    Sprite ObterPlaceholderRuntimeIcone()
    {
        if (iconePlaceholderRuntime != null)
        {
            return iconePlaceholderRuntime;
        }

        const int tamanho = 24;
        Texture2D textura = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, false);
        textura.filterMode = FilterMode.Bilinear;
        textura.wrapMode = TextureWrapMode.Clamp;

        Color32 fundo = new Color32(36, 48, 61, 255);
        Color32 borda = new Color32(124, 164, 196, 255);
        Color32 brilho = new Color32(200, 230, 255, 255);

        for (int y = 0; y < tamanho; y++)
        {
            for (int x = 0; x < tamanho; x++)
            {
                bool ehBorda = x <= 1 || y <= 1 || x >= tamanho - 2 || y >= tamanho - 2;
                bool ehDiagonal = Mathf.Abs(x - y) <= 1 || Mathf.Abs((tamanho - 1 - x) - y) <= 1;
                textura.SetPixel(x, y, ehBorda ? borda : (ehDiagonal ? brilho : fundo));
            }
        }

        textura.Apply(false, false);
        textura.hideFlags = HideFlags.HideAndDontSave;
        iconePlaceholderRuntime = Sprite.Create(textura, new Rect(0f, 0f, tamanho, tamanho), new Vector2(0.5f, 0.5f), tamanho);
        return iconePlaceholderRuntime;
    }

#if UNITY_EDITOR
    Sprite ProcurarSpriteEditor(DadosConstrucao item)
    {
        if (item == null)
        {
            return null;
        }

        string nomeItem = NormalizarTextoBuscaIcone(item.NomeItem);
        string nomePrefab = item.PrefabDaUnidade != null ? NormalizarTextoBuscaIcone(item.PrefabDaUnidade.name) : string.Empty;
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { "Assets" });

        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            string nomeAsset = NormalizarTextoBuscaIcone(System.IO.Path.GetFileNameWithoutExtension(path));
            if (string.IsNullOrEmpty(nomeAsset))
            {
                continue;
            }

            bool pareceMesmoItem = (!string.IsNullOrEmpty(nomeItem) && (nomeAsset.Contains(nomeItem) || nomeItem.Contains(nomeAsset)))
                || (!string.IsNullOrEmpty(nomePrefab) && (nomeAsset.Contains(nomePrefab) || nomePrefab.Contains(nomeAsset)));

            if (!pareceMesmoItem)
            {
                continue;
            }

            Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    string NormalizarTextoBuscaIcone(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        string normalizado = texto.ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty);
        return normalizado;
    }
#endif

    void EsticarRectTransform(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    string ObterTipoItem(DadosConstrucao item)
    {
        if (item == null)
        {
            return "--";
        }

        if (item.balanceamento != null && !string.IsNullOrWhiteSpace(item.balanceamento.rotuloTipo))
        {
            return item.balanceamento.rotuloTipo.Trim();
        }

        if (item.categoria == DadosConstrucao.CategoriaItem.Tecnologia)
        {
            return "Pesquisa";
        }

        if (item.categoria == DadosConstrucao.CategoriaItem.Infraestrutura)
        {
            return "Infraestrutura";
        }

        if (item.categoria == DadosConstrucao.CategoriaItem.Energia)
        {
            return "Energia";
        }

        if (item.categoria == DadosConstrucao.CategoriaItem.Urbana)
        {
            return "Estrutura urbana";
        }

        if (EhUnidadeAerea(item))
        {
            return "Unidade aerea";
        }

        if (EhUnidadeNaval(item))
        {
            return "Unidade naval";
        }

        if (EhEstruturaParaDetalhes(item))
        {
            return "Estrutura fixa";
        }

        return "Unidade terrestre";
    }

    string ObterTextoVelocidadeItem(DadosConstrucao item)
    {
        GameObject prefab;
        if (item == null || !item.TryGetPrefabBasico(out prefab))
        {
            return "--";
        }

        if (item.balanceamento != null && !string.IsNullOrWhiteSpace(item.balanceamento.velocidadeExibida))
        {
            return item.balanceamento.velocidadeExibida.Trim();
        }

        float velocidade = -1f;

        Helicoptero helicoptero = prefab.GetComponent<Helicoptero>() ?? prefab.GetComponentInChildren<Helicoptero>(true);
        if (helicoptero != null)
        {
            // O controlador usa m/s; os cards exibem km/h como os demais meios.
            velocidade = helicoptero.velocidadeNavegacao;
        }

        ControleAviao controleAviao = prefab.GetComponent<ControleAviao>() ?? prefab.GetComponentInChildren<ControleAviao>(true);
        if (velocidade <= 0f && controleAviao != null)
        {
            velocidade = controleAviao.velocidadeMaximaVoo;
        }

        if (velocidade <= 0f)
        {
            C700TransporteAereo transporteAereo = prefab.GetComponent<C700TransporteAereo>() ?? prefab.GetComponentInChildren<C700TransporteAereo>(true);
            if (transporteAereo != null)
            {
                velocidade = transporteAereo.velocidadeCruzeiro;
            }
        }

        if (velocidade <= 0f)
        {
            CacaVooRealista caca = prefab.GetComponent<CacaVooRealista>() ?? prefab.GetComponentInChildren<CacaVooRealista>(true);
            if (caca != null)
            {
                velocidade = caca.velocidadeMaxima;
            }
        }

        if (velocidade <= 0f)
        {
            ControleNavioRealista navio = prefab.GetComponent<ControleNavioRealista>() ?? prefab.GetComponentInChildren<ControleNavioRealista>(true);
            if (navio != null)
            {
                velocidade = navio.velocidadeMaxima;
            }
        }

        if (velocidade <= 0f)
        {
            HovercraftTransporte hovercraft = prefab.GetComponent<HovercraftTransporte>() ?? prefab.GetComponentInChildren<HovercraftTransporte>(true);
            if (hovercraft != null)
            {
                velocidade = hovercraft.velocidade;
            }
        }

        if (velocidade <= 0f)
        {
            NavMeshAgent agente = prefab.GetComponent<NavMeshAgent>() ?? prefab.GetComponentInChildren<NavMeshAgent>(true);
            if (agente != null)
            {
                velocidade = agente.speed;
            }
        }

        if (velocidade > 0f)
        {
            return (velocidade * 3.6f).ToString("0") + " km/h";
        }

        return EhEstruturaParaDetalhes(item) ? "Fixa" : "N/A";
    }

    string ObterTextoVidaItem(DadosConstrucao item)
    {
        GameObject prefab;
        if (item == null || !item.TryGetPrefabBasico(out prefab)) return "--";
        if (item.balanceamento != null && !string.IsNullOrWhiteSpace(item.balanceamento.blindagemExibida))
        {
            return item.balanceamento.blindagemExibida.Trim();
        }
        SistemaDeDanos sys = prefab.GetComponent<SistemaDeDanos>();
        if (sys == null) sys = prefab.GetComponentInChildren<SistemaDeDanos>(true);

        if (sys != null)
        {
            return sys.vidaMaxima.ToString();
        }
        return EhEstruturaParaDetalhes(item) ? "Forte" : "Leve";
    }

    string ObterTextoPoderFogoItem(DadosConstrucao item)
    {
        GameObject prefab;
        if (item == null || !item.TryGetPrefabBasico(out prefab)) return "--";
        if (item.balanceamento != null && !string.IsNullOrWhiteSpace(item.balanceamento.poderOfensivoExibido))
        {
            return item.balanceamento.poderOfensivoExibido.Trim();
        }
        var comps = prefab.GetComponentsInChildren<Component>(true);
        foreach (var c in comps)
        {
            if (c == null) continue; // Adicionado null check para evitar erro com Missing Scripts
            string n = c.GetType().Name.ToLower();
            if (n.Contains("torreta") || n.Contains("arma") || n.Contains("lancador") || n.Contains("caca"))
                return "Armado";
        }
        return EhEstruturaParaDetalhes(item) ? "Fixo" : "Desarmado";
    }

    string ObterDescricaoDetalheItem(DadosConstrucao item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (item.balanceamento != null && !string.IsNullOrWhiteSpace(item.balanceamento.descricaoTatica))
        {
            string descricaoBalanceamento = item.balanceamento.descricaoTatica.Trim();
            if (item.balanceamento.limiteEmCampo > 0)
            {
                descricaoBalanceamento += $"\nLimite recomendado em campo: {item.balanceamento.limiteEmCampo}.";
            }
            return descricaoBalanceamento;
        }

        if (!string.IsNullOrWhiteSpace(item.descricao))
        {
            return item.descricao.Trim();
        }

        return ObterDescricaoFallback(item.categoria);
    }

    bool EhEstruturaParaDetalhes(DadosConstrucao item)
    {
        if (item == null)
        {
            return false;
        }

        if (item.categoria == DadosConstrucao.CategoriaItem.Infraestrutura
            || item.categoria == DadosConstrucao.CategoriaItem.Energia
            || item.categoria == DadosConstrucao.CategoriaItem.Urbana
            || item.categoria == DadosConstrucao.CategoriaItem.Tecnologia)
        {
            return true;
        }

        if (item.PrefabDaUnidade == null)
        {
            return false;
        }

        GameObject prefab = item.PrefabDaUnidade;
        bool possuiIdentidadeNaval = prefab.GetComponent<IdentidadeNaval>() != null
                                   || prefab.GetComponentInChildren<IdentidadeNaval>(true) != null;
        bool ehPortaAvioes = prefab.GetComponent<GerenciadorPortaAvioes>() != null
                           || (prefab.GetComponent<GerenciadorAeroporto>() != null && possuiIdentidadeNaval);

        if (ehPortaAvioes)
        {
            return false;
        }

        if (prefab.CompareTag("Imovel")
            || prefab.GetComponent<Fabrica>() != null
            || prefab.GetComponent<Estaleiro>() != null
            || prefab.GetComponent<Heliporto>() != null
            || prefab.GetComponent<GerenciadorAeroporto>() != null
            || prefab.GetComponent<PierMarinha>() != null)
        {
            return true;
        }

        string nomeComposto = (prefab.name + "_" + item.NomeItem).ToLowerInvariant();
        return nomeComposto.Contains("hangar")
            || nomeComposto.Contains("fabrica")
            || nomeComposto.Contains("quartel")
            || nomeComposto.Contains("silo")
            || nomeComposto.Contains("torre")
            || nomeComposto.Contains("bunker")
            || nomeComposto.Contains("estaleiro")
            || nomeComposto.Contains("pier")
            || nomeComposto.Contains("plataforma");
    }

    bool EhUnidadeNaval(DadosConstrucao item)
    {
        if (item == null || item.PrefabDaUnidade == null)
        {
            return item != null && item.categoria == DadosConstrucao.CategoriaItem.Marinha;
        }

        GameObject prefab = item.PrefabDaUnidade;
        return item.categoria == DadosConstrucao.CategoriaItem.Marinha
            || prefab.GetComponent<IdentidadeNaval>() != null
            || prefab.GetComponentInChildren<IdentidadeNaval>(true) != null
            || prefab.GetComponent<ControleNavioRealista>() != null
            || prefab.GetComponentInChildren<ControleNavioRealista>(true) != null
            || prefab.GetComponent<ControleSubmarino>() != null
            || prefab.GetComponentInChildren<ControleSubmarino>(true) != null
            || prefab.GetComponent<ControladorNavioVigilante>() != null
            || prefab.GetComponentInChildren<ControladorNavioVigilante>(true) != null
            || prefab.GetComponent<HovercraftTransporte>() != null
            || prefab.GetComponentInChildren<HovercraftTransporte>(true) != null
            || prefab.GetComponent<NavioTransporteTropas>() != null
            || prefab.GetComponentInChildren<NavioTransporteTropas>(true) != null
            || prefab.GetComponent<NavioPetroleiro>() != null
            || prefab.GetComponentInChildren<NavioPetroleiro>(true) != null
            || prefab.GetComponent<NavioLiberty>() != null
            || prefab.GetComponentInChildren<NavioLiberty>(true) != null
            || prefab.GetComponent<TransporteAnfibio>() != null
            || prefab.GetComponentInChildren<TransporteAnfibio>(true) != null;
    }

    bool EhUnidadeAerea(DadosConstrucao item)
    {
        if (item == null || item.PrefabDaUnidade == null)
        {
            return item != null && item.categoria == DadosConstrucao.CategoriaItem.Aeronautica;
        }

        GameObject prefab = item.PrefabDaUnidade;
        return item.categoria == DadosConstrucao.CategoriaItem.Aeronautica
            || prefab.GetComponent<ControleAviao>() != null
            || prefab.GetComponentInChildren<ControleAviao>(true) != null
            || prefab.GetComponent<C700TransporteAereo>() != null
            || prefab.GetComponentInChildren<C700TransporteAereo>(true) != null
            || prefab.GetComponent<CacaVooRealista>() != null
            || prefab.GetComponentInChildren<CacaVooRealista>(true) != null
            || prefab.GetComponent<Helicoptero>() != null
            || prefab.GetComponentInChildren<Helicoptero>(true) != null;
    }

    string ObterDescricaoFallback(DadosConstrucao.CategoriaItem categoria)
    {
        switch (categoria)
        {
            case DadosConstrucao.CategoriaItem.Exercito:
                return "Unidade voltada para combate terrestre, pressao ofensiva e defesa de territorio.";
            case DadosConstrucao.CategoriaItem.Marinha:
                return "Unidade para dominar rotas costeiras, travessias e confrontos navais.";
            case DadosConstrucao.CategoriaItem.Aeronautica:
                return "Unidade de alcance rapido para patrulha, ataque e resposta aerea.";
            case DadosConstrucao.CategoriaItem.Tecnologia:
                return "Melhoria estrategica para desbloquear vantagem tecnica e fortalecer seu dominio.";
            case DadosConstrucao.CategoriaItem.Infraestrutura:
                return "Estrutura de suporte para expandir producao, logistica e capacidade operacional.";
            case DadosConstrucao.CategoriaItem.Energia:
                return "Estrutura essencial para sustentar operacao, consumo e crescimento da base.";
            case DadosConstrucao.CategoriaItem.Urbana:
                return "Estrutura civil e administrativa para apoiar o desenvolvimento do territorio.";
            default:
                return "Informacoes tecnicas indisponiveis para este item.";
        }
    }

    enum TipoFluxoConstrucao
    {
        Estrutura,
        UnidadeTerrestre,
        UnidadeAerea,
        UnidadeNaval
    }

    TipoFluxoConstrucao ClassificarFluxo(DadosConstrucao item)
    {
        if (EhUnidadeAerea(item) && !EhEstruturaAereaPosicionavel(item))
        {
            return TipoFluxoConstrucao.UnidadeAerea;
        }

        if (EhUnidadeNaval(item) && !EhEstruturaNavalPosicionavel(item))
        {
            return TipoFluxoConstrucao.UnidadeNaval;
        }

        if (EhEstruturaPosicionavel(item))
        {
            return TipoFluxoConstrucao.Estrutura;
        }

        return TipoFluxoConstrucao.UnidadeTerrestre;
    }

    bool EhEstruturaAereaPosicionavel(DadosConstrucao item)
    {
        if (item == null || item.PrefabDaUnidade == null)
        {
            return false;
        }

        GameObject prefab = item.PrefabDaUnidade;
        string nome = (prefab.name + "_" + item.NomeItem).ToLowerInvariant();

        return prefab.GetComponent<GerenciadorAeroporto>() != null
            || prefab.GetComponentInChildren<GerenciadorAeroporto>(true) != null
            || prefab.GetComponent<Heliporto>() != null
            || prefab.GetComponentInChildren<Heliporto>(true) != null
            || nome.Contains("aeroporto")
            || nome.Contains("heliporto")
            || nome.Contains("hangar")
            || nome.Contains("pista")
            || nome.Contains("airfield");
    }

    bool EhEstruturaNavalPosicionavel(DadosConstrucao item)
    {
        if (item == null || item.PrefabDaUnidade == null)
        {
            return false;
        }

        GameObject prefab = item.PrefabDaUnidade;
        string nome = (prefab.name + "_" + item.NomeItem).ToLowerInvariant();

        return prefab.GetComponent<Estaleiro>() != null
            || prefab.GetComponentInChildren<Estaleiro>(true) != null
            || prefab.GetComponent<PierMarinha>() != null
            || prefab.GetComponentInChildren<PierMarinha>(true) != null
            || nome.Contains("estaleiro")
            || nome.Contains("pier")
            || nome.Contains("plataforma")
            || nome.Contains("doca")
            || nome.Contains("cais")
            || nome.Contains("porto");
    }

    bool EhEstruturaPosicionavel(DadosConstrucao item)
    {
        if (item == null || item.PrefabDaUnidade == null)
        {
            return false;
        }

        GameObject prefab = item.PrefabDaUnidade;
        string nome = (prefab.name + "_" + item.NomeItem).ToLowerInvariant();

        if (item.categoria == DadosConstrucao.CategoriaItem.Urbana
            || item.categoria == DadosConstrucao.CategoriaItem.Infraestrutura
            || item.categoria == DadosConstrucao.CategoriaItem.Energia
            || item.categoria == DadosConstrucao.CategoriaItem.Tecnologia)
        {
            return true;
        }

        if (prefab.CompareTag("Imovel")
            || prefab.GetComponent<Imovel>() != null
            || prefab.GetComponentInChildren<Imovel>(true) != null
            || prefab.GetComponent<Fabrica>() != null
            || prefab.GetComponent<Estaleiro>() != null
            || prefab.GetComponent<Heliporto>() != null
            || prefab.GetComponent<GerenciadorAeroporto>() != null
            || prefab.GetComponent<PierMarinha>() != null
            || prefab.GetComponent<ComplexoGovernamental>() != null
            || prefab.GetComponent<MarcadorTerritorio>() != null)
        {
            return true;
        }

        string[] termosEstrutura = {
            "imovel", "imóvel", "predio", "prédio", "edificio", "edifício", "apartamento",
            "fabrica", "fábrica", "fazenda", "farm", "prefeitura", "fronteira", "bandeira",
            "hangar", "aeroporto", "heliporto", "pista", "quartel", "tenda", "silo",
            "torre", "torreta", "muro", "wall", "bunker", "defesa", "antiaerea", "antiaérea",
            "missil", "míssil", "canhao", "canhão", "metralhadora", "plataforma",
            "estaleiro", "pier", "centro", "base", "guarita", "trincheira", "forte",
            "barricada", "fortaleza", "acampamento", "posto", "radar", "artilharia",
            "bateria", "gerador", "mina", "coletor", "construtor", "refinaria", "building"
        };

        return termosEstrutura.Any(termo => nome.Contains(termo));
    }

    int ObterQuantidadeParaCompra(DadosConstrucao item, bool forcarUm)
    {
        if (item == null || string.IsNullOrEmpty(item.NomeItem) || forcarUm)
        {
            return 1;
        }

        return quantidadesPorItem.ContainsKey(item.NomeItem) ? Mathf.Max(1, quantidadesPorItem[item.NomeItem]) : 1;
    }

    bool TemDinheiroPara(long custo)
    {
        if (custo <= 0)
        {
            return true;
        }

        if (GerenciadorRecursos.Instancia != null)
        {
            return GerenciadorRecursos.Instancia.dinheiro >= custo;
        }

        if (gerente == null) gerente = Object.FindFirstObjectByType<GerenteDeJogo>();
        return gerente != null && gerente.dinheiroAtual >= custo;
    }

    void ConstruirItem(DadosConstrucao item, Image cardImage = null)
    {
        if (item == null)
        {
            EmitirAvisoJogador(LocalizationManager.T("build.invalid_item", "Item de construcao invalido."));
            DiagnosticoDesempenhoJogo.RegistrarEvento("CompraFalha", "Item nulo");
            return;
        }

        if (gerente == null) gerente = Object.FindFirstObjectByType<GerenteDeJogo>();
        if (gerente == null)
        {
            EmitirAvisoJogador(LocalizationManager.T("build.no_manager", "Gerente de jogo nao encontrado. Nao foi possivel iniciar a construcao."));
            DiagnosticoDesempenhoJogo.RegistrarEvento("CompraFalha", item.NomeItem + ": gerente ausente");
            return;
        }

        if (item.PrefabDaUnidade == null)
        {
            Debug.LogError($"[MenuConstrucao] Prefab '{item.NomeItem}' faltando!");
            EmitirAvisoJogador(string.Format(LocalizationManager.T("build.missing_prefab", "Prefab faltando para {0}."), item.NomeItem));
            DiagnosticoDesempenhoJogo.RegistrarEvento("CompraFalha", item.NomeItem + ": prefab faltando");
            return;
        }

        TipoFluxoConstrucao fluxo = ClassificarFluxo(item);
        int quantidade = ObterQuantidadeParaCompra(item, fluxo == TipoFluxoConstrucao.Estrutura);
        long preco = PrecoEfetivo(item);
        long custoTotal = Math.Max(0L, preco) * Math.Max(0, quantidade);

        string motivoGovernanca;
        if (!GovernadorGameplayRTS.PermitirProducao(item, quantidade, out motivoGovernanca))
        {
            if (cardImage != null) StartCoroutine(FlashCardErro(cardImage));
            EmitirAvisoJogador(motivoGovernanca);
            DiagnosticoDesempenhoJogo.RegistrarEvento("CompraFalha", item.NomeItem + ": " + motivoGovernanca);
            return;
        }

        if (fluxo != TipoFluxoConstrucao.Estrutura && !TemDinheiroPara(custoTotal))
        {
            if (cardImage != null) StartCoroutine(FlashCardErro(cardImage));
            EmitirAvisoJogador(string.Format(LocalizationManager.T("build.no_money", "Fundos insuficientes para comprar {0}."), item.NomeItem));
            DiagnosticoDesempenhoJogo.RegistrarEvento("CompraFalha", item.NomeItem + ": fundos insuficientes");
            return;
        }

        DesligarModoDemolicaoSeAtivo();

        // O silo ICBM é uma estrutura estratégica mesmo que o prefab tenha
        // sido importado sem os componentes usuais de imóvel. Forçamos o
        // fluxo de posicionamento pela ficha, evitando que ele caia no fluxo
        // de compra de unidade terrestre ou na validação territorial comum.
        bool ehIcbm = string.Equals(item.ItemId, "foguete_icbm", System.StringComparison.OrdinalIgnoreCase)
            || item.GetDisplayName().IndexOf("ICBM", System.StringComparison.OrdinalIgnoreCase) >= 0;
        if (ehIcbm)
        {
            Debug.LogWarning($"[MenuConstrucao][ICBM] fluxo de estrutura confirmado; prefab={item.PrefabDaUnidade.name}");
            IniciarConstrucaoFantasma(item, cardImage);
            return;
        }

        switch (fluxo)
        {
            case TipoFluxoConstrucao.Estrutura:
                IniciarConstrucaoFantasma(item, cardImage);
                break;
            case TipoFluxoConstrucao.UnidadeAerea:
                ProduzirUnidadeAerea(item, quantidade, cardImage);
                break;
            case TipoFluxoConstrucao.UnidadeNaval:
                ProduzirUnidadeNaval(item, quantidade, cardImage);
                break;
            default:
                ProduzirUnidadeTerrestre(item, quantidade, cardImage);
                break;
        }
    }

    Construtor ObterOuCriarConstrutor()
    {
        if (construtorCena != null)
        {
            if (!construtorCena.gameObject.activeSelf)
            {
                construtorCena.gameObject.SetActive(true);
            }

            construtorCena.enabled = true;
            return construtorCena;
        }

        if (Construtor.Instancia != null && Construtor.Instancia.enabled && Construtor.Instancia.gameObject.activeInHierarchy)
        {
            construtorCena = Construtor.Instancia;
            return Construtor.Instancia;
        }

        Construtor construtor = Object.FindFirstObjectByType<Construtor>();
        if (construtor != null)
        {
            construtorCena = construtor;
            return construtor;
        }

        Construtor[] construtoresInativos = Object.FindObjectsByType<Construtor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Construtor existente in construtoresInativos)
        {
            if (existente == null) continue;

            if (!existente.gameObject.activeSelf)
            {
                existente.gameObject.SetActive(true);
            }

            existente.enabled = true;
            construtorCena = existente;
            return existente;
        }

        GameObject obj = new GameObject("Construtor_Manager");
        construtor = obj.AddComponent<Construtor>();
        construtorCena = construtor;
        Debug.LogWarning("[MenuConstrucao] Nenhum Construtor estava ativo na cena. Criando Construtor_Manager automaticamente.");
        return construtor;
    }

    void IniciarConstrucaoFantasma(DadosConstrucao item, Image cardImage)
    {
        long preco = PrecoEfetivo(item);
        Debug.Log($"[MenuConstrucao][DEBUG] iniciar item={item?.name} nome={item?.GetDisplayName()} id={item?.ItemId} prefab={(item != null && item.PrefabDaUnidade != null ? item.PrefabDaUnidade.name : "NULL")}");
        Construtor construtor = ObterOuCriarConstrutor();
        if (construtor == null)
        {
            if (cardImage != null) StartCoroutine(FlashCardErro(cardImage));
            EmitirAvisoJogador(LocalizationManager.T("build.no_constructor", "Construtor nao encontrado na cena. Impossivel posicionar a estrutura."));
            DiagnosticoDesempenhoJogo.RegistrarEvento("CompraFalha", item.NomeItem + ": construtor ausente");
            return;
        }

        if (construtor.modoConstrucao && construtor.prefabSelecionado == item.PrefabDaUnidade)
        {
            AlternarMenu(false);
            return;
        }

        if (construtor.modoConstrucao)
        {
            construtor.CancelarConstrucao(false);
        }

        if (cardImage != null) StartCoroutine(FlashCard(cardImage));
        bool ehIcbm = item != null &&
            (string.Equals(item.ItemId, "foguete_icbm", System.StringComparison.OrdinalIgnoreCase) ||
             item.GetDisplayName().IndexOf("ICBM", System.StringComparison.OrdinalIgnoreCase) >= 0);
        construtor.SelecionarParaConstruir(item.PrefabDaUnidade, Math.Max(0L, preco), item.categoria, ehIcbm);
        AlternarMenu(false);
    }

    void ProduzirUnidadeTerrestre(DadosConstrucao item, int quantidade, Image cardImage)
    {
        gerente.ComprarUnidade(item.PrefabDaUnidade, PrecoEfetivo(item), quantidade);
        if (cardImage != null) StartCoroutine(FlashCard(cardImage));
        AlternarMenu(false);
    }

    bool HeliportoSemEnergia(Heliporto h)
    {
        if (h == null) return false;
        Imovel im = h.GetComponent<Imovel>();
        if (im == null) im = h.GetComponentInParent<Imovel>();
        if (im != null) return im.semEnergia;
        return false;
    }

    void ProduzirUnidadeAerea(DadosConstrucao item, int quantidade, Image cardImage)
    {
        bool isHelicopter = false;
        if (item != null && item.PrefabDaUnidade != null)
        {
            isHelicopter = item.PrefabDaUnidade.GetComponent<Helicoptero>() != null || 
                           item.PrefabDaUnidade.GetComponentInChildren<Helicoptero>(true) != null;
        }

        RegistroEntidadesJogo.FillAeroportos(bufferAeroportos);

        List<GerenciadorAeroporto> meusAeroportos = bufferAeroportos
            .Where(a =>
            {
                if (a == null) return false;
                if (a is GerenciadorPortaAvioes) return false; // Impede que compras do menu geral caiam no porta-aviões
                if (a is GerenciadorAeroportoComercial) return false; // Impede aviões militares no aeroporto comercial
                return EhEstruturaDoJogador(a);
            })
            .ToList();

        List<Heliporto> meusHeliportos = new List<Heliporto>();
        if (isHelicopter)
        {
            List<Heliporto> tempHeli = new List<Heliporto>();
            RegistroEntidadesJogo.FillHeliportos(tempHeli);
            meusHeliportos = tempHeli
                .Where(h =>
                {
                    if (h == null) return false;
                    return EhEstruturaDoJogador(h);
                })
                .ToList();
        }

        if (meusAeroportos.Count == 0 && meusHeliportos.Count == 0)
        {
            if (cardImage != null) StartCoroutine(FlashCardErro(cardImage));
            string erroMsg = isHelicopter 
                ? LocalizationManager.T("build.need_airport_or_heliport", "Bloqueado: voce precisa construir um AEROPORTO ou HELIPORTO primeiro para comprar aeronaves.")
                : LocalizationManager.T("build.need_airport", "Bloqueado: voce precisa construir um AEROPORTO primeiro para comprar aeronaves.");
            EmitirAvisoJogador(erroMsg);
            DiagnosticoDesempenhoJogo.RegistrarEvento("CompraFalha", item.NomeItem + ": sem aeroporto ou heliporto");
            return;
        }

        Heliporto targetHeliporto = null;
        GerenciadorAeroporto targetAeroporto = null;

        if (isHelicopter)
        {
            // Prefer heliports with space and energy
            targetHeliporto = meusHeliportos
                .Where(h => h.TemEspacoParaPousar() && !HeliportoSemEnergia(h))
                .FirstOrDefault();

            if (targetHeliporto == null)
            {
                targetHeliporto = meusHeliportos
                    .Where(h => h.TemEspacoParaPousar())
                    .FirstOrDefault();
            }

            if (targetHeliporto == null)
            {
                targetAeroporto = meusAeroportos
                    .Where(a => !a.semEnergia)
                    .OrderByDescending(a =>
                    {
                        int score = 0;
                        if (a.GetType() != typeof(GerenciadorPortaAvioes)) score += 100;
                        if (a.ObterPrimeiraVagaLivre() != null) score += 50;
                        return score;
                    })
                    .FirstOrDefault();

                if (targetAeroporto == null)
                {
                    targetAeroporto = meusAeroportos
                        .OrderByDescending(a =>
                        {
                            int score = 0;
                            if (a.GetType() != typeof(GerenciadorPortaAvioes)) score += 100;
                            if (a.ObterPrimeiraVagaLivre() != null) score += 50;
                            return score;
                        })
                        .FirstOrDefault();
                }
            }
        }
        else
        {
            targetAeroporto = meusAeroportos
                .Where(a => !a.semEnergia)
                .OrderByDescending(a =>
                {
                    int score = 0;
                    if (a.GetType() != typeof(GerenciadorPortaAvioes)) score += 100;
                    if (a.ObterPrimeiraVagaLivre() != null) score += 50;
                    return score;
                })
                .FirstOrDefault();

            if (targetAeroporto == null)
            {
                targetAeroporto = meusAeroportos
                    .OrderByDescending(a =>
                    {
                        int score = 0;
                        if (a.GetType() != typeof(GerenciadorPortaAvioes)) score += 100;
                        if (a.ObterPrimeiraVagaLivre() != null) score += 50;
                        return score;
                    })
                    .FirstOrDefault();
            }
        }

        bool targetIsPowerless = false;
        if (targetHeliporto != null)
        {
            targetIsPowerless = HeliportoSemEnergia(targetHeliporto);
        }
        else if (targetAeroporto != null)
        {
            targetIsPowerless = targetAeroporto.semEnergia;
        }

        if (targetIsPowerless)
        {
            if (cardImage != null) StartCoroutine(FlashCardErro(cardImage));
            EmitirAvisoJogador(LocalizationManager.T("build.destination_no_power", "Aviso: o aeroporto de destino esta sem energia, mas a compra foi liberada."));
            DiagnosticoDesempenhoJogo.RegistrarEvento("CompraFalha", item.NomeItem + ": sem energia (ignorado pelo patch)");
            // return; // <-- REMOVIDO para permitir a compra mesmo se o script de energia falhar
        }

        if (targetHeliporto == null && targetAeroporto == null)
        {
            if (cardImage != null) StartCoroutine(FlashCardErro(cardImage));
            EmitirAvisoJogador(LocalizationManager.T("build.no_airport", "Erro: nenhum aeroporto ou heliporto valido encontrado para entregar esta aeronave."));
            DiagnosticoDesempenhoJogo.RegistrarEvento("CompraFalha", item.NomeItem + ": aeroporto/heliporto invalido");
            return;
        }

        int comprados = 0;
        for (int i = 0; i < quantidade; i++)
        {
            if (!gerente.TentarGastarDinheiro(PrecoEfetivo(item)))
            {
                break;
            }

            if (targetHeliporto != null)
            {
                Vector3 spawnPos = targetHeliporto.ObterPontoDePousoMundial();
                GameObject heliObj = Instantiate(item.PrefabDaUnidade, spawnPos, targetHeliporto.transform.rotation);
                
                IdentidadeUnidade id = heliObj.GetComponent<IdentidadeUnidade>();
                if (id == null) id = heliObj.AddComponent<IdentidadeUnidade>();
                id.teamID = 1;
                id.nomeDoPais = "Hegemonia";

                ControleUnidade controle = heliObj.GetComponent<ControleUnidade>();
                if (controle == null) controle = heliObj.AddComponent<ControleUnidade>();

                Helicoptero heli = heliObj.GetComponent<Helicoptero>();
                if (heli == null) heli = heliObj.GetComponentInChildren<Helicoptero>(true);
                if (heli != null)
                {
                    targetHeliporto.HelicopteroPousou(heli);
                }
                comprados++;
            }
            else if (targetAeroporto != null)
            {
                targetAeroporto.ComprarAviao(item.PrefabDaUnidade);
                comprados++;
            }
        }

        if (comprados <= 0)
        {
            if (cardImage != null) StartCoroutine(FlashCardErro(cardImage));
            EmitirAvisoJogador(string.Format(LocalizationManager.T("build.no_money", "Fundos insuficientes para comprar {0}."), item.NomeItem));
            DiagnosticoDesempenhoJogo.RegistrarEvento("CompraFalha", item.NomeItem + ": fundos insuficientes");
            return;
        }

        if (cardImage != null) StartCoroutine(FlashCard(cardImage));
        AlternarMenu(false);
    }

    void ProduzirUnidadeNaval(DadosConstrucao item, int quantidade, Image cardImage)
    {
        bool ehNavioGrande = EhNavioGrande(item.PrefabDaUnidade);
        List<Estaleiro> estaleiros = Object.FindObjectsByType<Estaleiro>(FindObjectsSortMode.None)
            .Where(e => e != null && EhEstruturaDoJogador(e) && EstruturaNavalOperacional(e))
            .ToList();

        // Pier e estrutura de atracagem/logistica. A fila de producao naval do
        // jogador deve nascer exclusivamente no Estaleiro.
        if (estaleiros.Count == 0)
        {
            if (cardImage != null) StartCoroutine(FlashCardErro(cardImage));
            DiagnosticarEstruturasNavaisJogador();
            EmitirAvisoJogador(ehNavioGrande
                ? LocalizationManager.T("build.need_shipyard_big", "Bloqueado: construa um ESTALEIRO costeiro valido para produzir esse navio grande.")
                : LocalizationManager.T("build.need_shipyard", "Bloqueado: construa um ESTALEIRO costeiro valido para produzir navios."));
            DiagnosticoDesempenhoJogo.RegistrarEvento("CompraFalha", item.NomeItem + ": sem estrutura naval");
            return;
        }

        int enfileirados = 0;
        for (int i = 0; i < quantidade; i++)
        {
            if (!gerente.TentarGastarDinheiro(PrecoEfetivo(item)))
            {
                break;
            }

            bool sucesso = false;
            foreach (Estaleiro estaleiro in estaleiros)
            {
                if (estaleiro == null || !estaleiro.TemVaga || !EstruturaNavalOperacional(estaleiro))
                {
                    continue;
                }

                if (estaleiro.ConstruirUnidade(item.PrefabDaUnidade))
                {
                    sucesso = true;
                    enfileirados++;
                    break;
                }
            }

            if (!sucesso)
            {
                ReembolsarDinheiro(PrecoEfetivo(item));
                EmitirAvisoJogador(string.Format(LocalizationManager.T("build.naval_fail", "Falha ao produzir '{0}' em estruturas navais validas."), item.NomeItem));
                DiagnosticoDesempenhoJogo.RegistrarEvento("CompraFalha", item.NomeItem + ": estrutura naval recusou");
                break;
            }
        }

        if (enfileirados <= 0)
        {
            if (cardImage != null) StartCoroutine(FlashCardErro(cardImage));
            EmitirAvisoJogador(string.Format(LocalizationManager.T("build.naval_none", "Nao foi possivel produzir {0}."), item.NomeItem));
            DiagnosticoDesempenhoJogo.RegistrarEvento("CompraFalha", item.NomeItem + ": nenhum enfileirado");
            return;
        }

        if (cardImage != null) StartCoroutine(FlashCard(cardImage));
        if (enfileirados < quantidade)
        {
            Debug.LogWarning($"[MenuConstrucao] Producao naval parcial: {enfileirados}/{quantidade}.");
        }

        AlternarMenu(false);
    }

    void DesligarModoDemolicaoSeAtivo()
    {
        if (ModoDemolicao.TemModoAtivo)
        {
            ModoDemolicao.Instancia.AlternarModo(false);
        }
    }

    void EmitirAvisoJogador(string mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
        {
            return;
        }

        Debug.LogWarning(mensagem);
        HUDAjudaRTS.MostrarMensagemTemporaria(mensagem, 3.6f);
    }

    bool EhEstruturaDoJogador(Component estrutura)
    {
        if (estrutura == null)
        {
            return false;
        }

        Estaleiro estaleiro = estrutura as Estaleiro;
        if (estaleiro != null)
        {
            return estaleiro.OwnerTeamId == 1;
        }

        PierMarinha pier = estrutura as PierMarinha;
        if (pier != null)
        {
            return pier.OwnerTeamId == 1;
        }

        IdentidadeUnidade id = estrutura.GetComponent<IdentidadeUnidade>();
        if (id == null)
        {
            id = estrutura.GetComponentInParent<IdentidadeUnidade>();
        }

        if (id != null)
        {
            return id.teamID == 1;
        }

        IA_ManualPlacementTag manualTag = estrutura.GetComponent<IA_ManualPlacementTag>();
        if (manualTag == null)
        {
            manualTag = estrutura.GetComponentInParent<IA_ManualPlacementTag>();
        }

        if (manualTag != null)
        {
            return true;
        }

        return !EhEstruturaMarcadaComoIA(estrutura);
    }

    /// <summary>
    /// Mantém no log a causa exata quando uma compra naval é bloqueada. Isso
    /// diferencia identidade antiga do prefab, dono explícito e pose costeira
    /// inválida, sem depender de proximidade no mapa.
    /// </summary>
    void DiagnosticarEstruturasNavaisJogador()
    {
        Estaleiro[] todos = Object.FindObjectsByType<Estaleiro>(FindObjectsSortMode.None);
        if (todos == null || todos.Length == 0)
        {
            Debug.LogWarning("[MenuConstrucao][Naval] Nenhum componente Estaleiro ativo foi encontrado na cena.");
            return;
        }

        for (int i = 0; i < todos.Length; i++)
        {
            Estaleiro estaleiro = todos[i];
            if (estaleiro == null) continue;

            IdentidadeUnidade identidade = estaleiro.GetComponent<IdentidadeUnidade>();
            if (identidade == null) identidade = estaleiro.GetComponentInParent<IdentidadeUnidade>();
            IA_ManualPlacementTag manual = estaleiro.GetComponent<IA_ManualPlacementTag>();
            if (manual == null) manual = estaleiro.GetComponentInParent<IA_ManualPlacementTag>();

            string validacao;
            bool poseValida = NavalPlacementResolver.IsCurrentStructurePoseValid(estaleiro.gameObject, out validacao);
            Debug.LogWarning(string.Format(
                "[MenuConstrucao][Naval] Estaleiro='{0}' owner={1} identidade={2} tag='{3}' jogador={4} poseValida={5} motivo='{6}'.",
                estaleiro.name,
                estaleiro.OwnerTeamId,
                identidade != null ? identidade.teamID.ToString() : "ausente",
                manual != null ? manual.SourceLabel : "ausente",
                EhEstruturaDoJogador(estaleiro),
                poseValida,
                validacao));
        }
    }

    bool EhEstruturaMarcadaComoIA(Component estrutura)
    {
        if (estrutura == null)
        {
            return false;
        }

        return estrutura.GetComponentInParent<IdentidadeIA>() != null
            || estrutura.GetComponentInParent<IA_IdentityRegistryHook>() != null
            || estrutura.GetComponentInParent<IA_BrainMaster>() != null;
    }

    bool EstruturaNavalOperacional(Component estrutura)
    {
        if (estrutura == null || estrutura.gameObject == null)
        {
            return false;
        }

        if (estrutura.GetComponent<IA_ManualPlacementTag>() != null
            || estrutura.GetComponentInParent<IA_ManualPlacementTag>() != null)
        {
            return true;
        }

        if (estrutura is Estaleiro estaleiro)
        {
            string validacao;
            if (NavalPlacementResolver.IsCurrentStructurePoseValid(estaleiro.gameObject, out validacao))
            {
                return true;
            }

            return estaleiro.EstaNaConstrucaoValida(NavalPlacementResolver.ResolveSeaLevel());
        }

        if (estrutura is PierMarinha pier)
        {
            string validacao;
            return NavalPlacementResolver.IsCurrentStructurePoseValid(pier.gameObject, out validacao);
        }

        return true;
    }

    bool EhNavioGrande(GameObject prefabDoNavio)
    {
        if (prefabDoNavio == null)
        {
            return false;
        }

        IdentidadeNaval identidadeNaval = prefabDoNavio.GetComponent<IdentidadeNaval>();
        if (identidadeNaval == null)
        {
            identidadeNaval = prefabDoNavio.GetComponentInChildren<IdentidadeNaval>();
        }

        if (identidadeNaval != null && identidadeNaval.categoriaNavio == IdentidadeNaval.CategoriaNavio.TransporteGrande)
        {
            return true;
        }

        return prefabDoNavio.GetComponent<NavioPetroleiro>() != null
            || prefabDoNavio.GetComponent<NavioCargaMercado>() != null
            || prefabDoNavio.name.IndexOf("navio de carga", System.StringComparison.OrdinalIgnoreCase) >= 0
            || prefabDoNavio.GetComponent<TransporteAnfibio>() != null
            || prefabDoNavio.GetComponent<NavioLiberty>() != null;
    }

    void ReembolsarDinheiro(long valor)
    {
        if (valor <= 0)
        {
            return;
        }

        if (GerenciadorRecursos.Instancia != null)
        {
            GerenciadorRecursos.Instancia.AdicionarRecursos(addDinheiro: valor);
        }
    }

    IEnumerator FlashCard(Image img)
    {
        if (img == null) yield break;
        Color corOriginal = corCardBase;
        Color corSucesso = new Color(0.2f, 0.8f, 0.2f, 0.9f); 

        float tempo = 0;
        while(tempo < 0.15f)
        {
            tempo += Time.deltaTime;
            if(img != null) img.color = Color.Lerp(corOriginal, corSucesso, tempo / 0.15f);
            yield return null;
        }

        tempo = 0;
        while(tempo < 0.4f)
        {
            tempo += Time.deltaTime;
            if(img != null) img.color = Color.Lerp(corSucesso, corOriginal, tempo / 0.4f);
            yield return null;
        }
        if(img != null) img.color = corOriginal;
    }

    IEnumerator FlashCardErro(Image img)
    {
        if (img == null) yield break;
        Color corOriginal = corCardBase;
        Color corErro = new Color(0.8f, 0.2f, 0.2f, 0.9f); 

        float tempo = 0;
        while(tempo < 0.1f)
        {
            tempo += Time.unscaledDeltaTime;
            if(img != null) img.color = Color.Lerp(corOriginal, corErro, tempo / 0.1f);
            yield return null;
        }
        tempo = 0;
        while(tempo < 0.3f)
        {
            tempo += Time.unscaledDeltaTime;
            if(img != null) img.color = Color.Lerp(corErro, corOriginal, tempo / 0.3f);
            yield return null;
        }
        if(img != null) img.color = corOriginal;
    }
}
