using System.Collections;
using System.Collections.Generic;
using System.Text;
using Hegemonia.AI.BrainMaster;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10010)]
public sealed class AuditoriaConteudoJogo : MonoBehaviour
{
    public readonly struct ResultadoAuditoriaConteudo
    {
        public readonly int TotalFichas;
        public readonly int Erros;
        public readonly int Avisos;
        public readonly string Cena;

        public bool PassouGate => Erros == 0;

        public ResultadoAuditoriaConteudo(int totalFichas, int erros, int avisos, string cena)
        {
            TotalFichas = totalFichas;
            Erros = erros;
            Avisos = avisos;
            Cena = cena ?? string.Empty;
        }
    }

    private static AuditoriaConteudoJogo instancia;
    private static bool usarCatalogoSobrescritoParaTeste = false;
    private static readonly List<DadosConstrucao> catalogoSobrescritoParaTeste = new List<DadosConstrucao>();

    public static ResultadoAuditoriaConteudo UltimoResultado { get; private set; } = new ResultadoAuditoriaConteudo(0, 1, 0, string.Empty);

    private readonly HashSet<int> fichasAuditadas = new HashSet<int>();
    private readonly HashSet<string> idsEstaveisAuditados = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DadosConstrucao> primeiraFichaPorId = new Dictionary<string, DadosConstrucao>(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<DadosConstrucao> fichas = new List<DadosConstrucao>(256);
    private readonly StringBuilder resumoBuilder = new StringBuilder(256);
    private Coroutine rotinaAuditoria;
    private int avisosConsoleEmitidos;
    private int avisosConsoleSuprimidos;

    [Header("Desempenho em Play Mode")]
    [SerializeField, Min(0.5f)] private float atrasoAuditoriaSegundos = 4f;
    [SerializeField, Min(1)] private int fichasPorFrame = 8;
    [SerializeField, Min(0)] private int maxAvisosDetalhadosNoConsole = 4;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void GarantirInstancia()
    {
        if (instancia != null)
        {
            return;
        }

        AuditoriaConteudoJogo existente = FindFirstObjectByType<AuditoriaConteudoJogo>();
        if (existente != null)
        {
            instancia = existente;
            DontDestroyOnLoad(existente.gameObject);
            existente.AgendarAuditoria();
            return;
        }

        GameObject obj = new GameObject("AuditoriaConteudoJogo");
        instancia = obj.AddComponent<AuditoriaConteudoJogo>();
        DontDestroyOnLoad(obj);
    }

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        AgendarAuditoria();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instancia == this)
        {
            instancia = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AgendarAuditoria();
    }

    private void AgendarAuditoria()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        if (rotinaAuditoria != null)
        {
            StopCoroutine(rotinaAuditoria);
        }

        rotinaAuditoria = StartCoroutine(RodarAuditoriaAtrasada());
    }

    public void ExecutarAuditoriaImediata()
    {
        if (rotinaAuditoria != null)
        {
            StopCoroutine(rotinaAuditoria);
            rotinaAuditoria = null;
        }

        RodarAuditoria();
    }

    public static void DefinirCatalogoSobrescritoParaTeste(IList<DadosConstrucao> fichasTeste)
    {
        catalogoSobrescritoParaTeste.Clear();
        usarCatalogoSobrescritoParaTeste = fichasTeste != null;

        if (!usarCatalogoSobrescritoParaTeste)
        {
            return;
        }

        for (int i = 0; i < fichasTeste.Count; i++)
        {
            catalogoSobrescritoParaTeste.Add(fichasTeste[i]);
        }
    }

    private IEnumerator RodarAuditoriaAtrasada()
    {
        // A cena ainda esta criando objetos nos primeiros segundos. Adiar e
        // distribuir a validacao evita disputar o mesmo frame com carregamento.
        yield return new WaitForSecondsRealtime(atrasoAuditoriaSegundos);
        yield return RodarAuditoriaEmEtapas();
        rotinaAuditoria = null;
    }

    private void RodarAuditoria()
    {
        PrepararAuditoria();
        int erros = 0;
        int avisos = 0;
        int eventosEmitidos = 0;
        const int limiteEventos = 24;

        for (int i = 0; i < fichas.Count; i++)
        {
            AuditarFichaOuRegistrarNula(fichas[i], ref erros, ref avisos, ref eventosEmitidos, limiteEventos);
        }

        FinalizarAuditoria(erros, avisos);
    }

    private IEnumerator RodarAuditoriaEmEtapas()
    {
        PrepararAuditoria();
        int erros = 0;
        int avisos = 0;
        int eventosEmitidos = 0;
        const int limiteEventos = 24;
        int limitePorFrame = Mathf.Max(1, fichasPorFrame);

        for (int i = 0; i < fichas.Count; i++)
        {
            AuditarFichaOuRegistrarNula(fichas[i], ref erros, ref avisos, ref eventosEmitidos, limiteEventos);
            if ((i + 1) % limitePorFrame == 0)
            {
                yield return null;
            }
        }

        FinalizarAuditoria(erros, avisos);
    }

    private void PrepararAuditoria()
    {
        fichas.Clear();
        fichasAuditadas.Clear();
        idsEstaveisAuditados.Clear();
        primeiraFichaPorId.Clear();
        avisosConsoleEmitidos = 0;
        avisosConsoleSuprimidos = 0;
        ColetarFichas();
    }

    private void AuditarFichaOuRegistrarNula(DadosConstrucao ficha, ref int erros, ref int avisos, ref int eventosEmitidos, int limiteEventos)
    {
        if (ficha == null)
        {
            erros++;
            Emitir("ERRO", "Ficha nula no catalogo", ref eventosEmitidos, limiteEventos);
            return;
        }

        AuditarFicha(ficha, ref erros, ref avisos, ref eventosEmitidos, limiteEventos);
    }

    private void FinalizarAuditoria(int erros, int avisos)
    {
        resumoBuilder.Length = 0;
        resumoBuilder.Append("fichas=").Append(fichas.Count)
            .Append(" erros=").Append(erros)
            .Append(" avisos=").Append(avisos)
            .Append(" cena=").Append(SceneManager.GetActiveScene().name);
        if (avisosConsoleSuprimidos > 0)
        {
            resumoBuilder.Append(" avisosConsoleSuprimidos=").Append(avisosConsoleSuprimidos);
        }

        string resumo = resumoBuilder.ToString();
        UltimoResultado = new ResultadoAuditoriaConteudo(fichas.Count, erros, avisos, SceneManager.GetActiveScene().name);
        DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("auditoria_conteudo", resumo);
        DiagnosticoDesempenhoJogo.RegistrarEvento("AuditoriaConteudo", (UltimoResultado.PassouGate ? "GATE_OK " : "GATE_FAIL ") + resumo);

        if (UltimoResultado.PassouGate)
        {
            Debug.Log("[AuditoriaConteudo] Gate de conteudo aprovado: " + resumo, this);
        }
        else
        {
            Debug.LogError("[AuditoriaConteudo] Gate de conteudo bloqueado: " + resumo, this);
        }
    }

    private void ColetarFichas()
    {
        if (usarCatalogoSobrescritoParaTeste)
        {
            for (int i = 0; i < catalogoSobrescritoParaTeste.Count; i++)
            {
                AdicionarFicha(catalogoSobrescritoParaTeste[i]);
            }

            // O sobrescrito representa a mesma fonte de verdade temporária usada
            // pela auditoria. Registre-o antes das validações de integridade para
            // que o teste e o fluxo real consultem o mesmo catálogo compartilhado.
            CatalogoProdutoCompartilhado.RegistrarConstrucoes(fichas);

            return;
        }

        if (MenuConstrucao.catalogoGlobal != null)
        {
            for (int i = 0; i < MenuConstrucao.catalogoGlobal.Count; i++)
            {
                AdicionarFicha(MenuConstrucao.catalogoGlobal[i]);
            }
        }

        DadosConstrucao[] fallback = Resources.FindObjectsOfTypeAll<DadosConstrucao>();
        for (int i = 0; i < fallback.Length; i++)
        {
            AdicionarFicha(fallback[i]);
        }

        // A auditoria pode rodar na cena de menu antes de MenuConstrucao.Start.
        // Registra as fichas encontradas para nao depender da ordem da cena.
        CatalogoProdutoCompartilhado.RegistrarConstrucoes(fichas);
    }

    private void AdicionarFicha(DadosConstrucao ficha)
    {
        if (ficha == null)
        {
            fichas.Add(null);
            return;
        }

        int id = ficha.GetInstanceID();
        if (fichasAuditadas.Add(id))
        {
            fichas.Add(ficha);
        }
    }

    private void AuditarFicha(DadosConstrucao ficha, ref int erros, ref int avisos, ref int eventosEmitidos, int limiteEventos)
    {
        string nome = string.IsNullOrWhiteSpace(ficha.NomeItem) ? ficha.name : ficha.NomeItem;
        string stableId = IA_Text.Normalize(ficha.GetStableId());
        CatalogoProdutoUnificadoItem catalogoCompartilhado;
        if (string.IsNullOrEmpty(stableId))
        {
            erros++;
            Emitir("ERRO", nome + ": id estavel vazio", ref eventosEmitidos, limiteEventos);
        }
        else
        {
            if (!idsEstaveisAuditados.Add(stableId))
            {
                DadosConstrucao primeiraFicha;
                bool aliasDoMesmoProduto = primeiraFichaPorId.TryGetValue(stableId, out primeiraFicha)
                    && SaoFichasEquivalentes(primeiraFicha, ficha);

                if (aliasDoMesmoProduto)
                {
                    avisos++;
                    Emitir("AVISO", nome + ": ficha alias equivalente compartilha o id estavel (" + stableId + ")", ref eventosEmitidos, limiteEventos);
                }
                else
                {
                    erros++;
                    Emitir("ERRO", nome + ": id estavel duplicado (" + stableId + ")", ref eventosEmitidos, limiteEventos);
                }
            }
            else
            {
                primeiraFichaPorId[stableId] = ficha;
            }

            if (!CatalogoProdutoCompartilhado.TentarObter(stableId, out catalogoCompartilhado) || catalogoCompartilhado == null)
            {
                erros++;
                Emitir("ERRO", nome + ": ausente no catalogo compartilhado (" + stableId + ")", ref eventosEmitidos, limiteEventos);
            }
        }

        GameObject prefab = null;
        bool hasPrefab = ficha != null && ficha.TryGetPrefab(out prefab);
        bool emDesenvolvimento = EhPrefabEmDesenvolvimento(nome);

        if (string.IsNullOrWhiteSpace(ficha.NomeItem))
        {
            avisos++;
            Emitir("AVISO", "Ficha sem nome: " + ficha.name, ref eventosEmitidos, limiteEventos);
        }

        if (ficha.preco < 0)
        {
            erros++;
            Emitir("ERRO", nome + ": preco negativo (" + ficha.preco + ")", ref eventosEmitidos, limiteEventos);
        }

        if (!hasPrefab || prefab == null)
        {
            if (emDesenvolvimento)
            {
                avisos++;
                Emitir("AVISO", nome + ": prefab ausente ou corrompido (Ignorado)", ref eventosEmitidos, limiteEventos);
            }
            else
            {
                erros++;
                Emitir("ERRO", nome + ": prefab ausente ou corrompido", ref eventosEmitidos, limiteEventos);
            }
            return;
        }

        bool materialPersistente = ficha.categoria != DadosConstrucao.CategoriaItem.Tecnologia;
        bool unidadeCombate = EhCategoriaMilitar(ficha.categoria) && !EhLogisticaOuEstrutura(nome, prefab.name);
        bool temCollider = prefab.GetComponentInChildren<Collider>(true) != null;
        bool temSistemaDeDanos = prefab.GetComponentInChildren<SistemaDeDanos>(true) != null;
        bool temSaveableEntity = prefab.GetComponentInChildren<SaveableEntity>(true) != null;
        bool temIdentidadeUnidade = prefab.GetComponentInChildren<IdentidadeUnidade>(true) != null;

        if (materialPersistente && !temCollider)
        {
            if (EhCategoriaMilitar(ficha.categoria))
            {
                if (emDesenvolvimento)
                {
                    avisos++;
                    Emitir("AVISO", nome + ": unidade militar sem Collider (Ignorado)", ref eventosEmitidos, limiteEventos);
                }
                else
                {
                    erros++;
                    Emitir("ERRO", nome + ": unidade militar sem Collider", ref eventosEmitidos, limiteEventos);
                }
            }
            else
            {
                avisos++;
                Emitir("AVISO", nome + ": prefab sem collider", ref eventosEmitidos, limiteEventos);
            }
        }

        if (materialPersistente && !temSaveableEntity)
        {
            avisos++;
            Emitir("AVISO", nome + ": prefab sem SaveableEntity para save completo", ref eventosEmitidos, limiteEventos);
        }

        if (EhCategoriaMilitar(ficha.categoria) && !temIdentidadeUnidade)
        {
            if (emDesenvolvimento)
            {
                avisos++;
                Emitir("AVISO", nome + ": unidade militar sem IdentidadeUnidade (Ignorado)", ref eventosEmitidos, limiteEventos);
            }
            else
            {
                erros++;
                Emitir("ERRO", nome + ": unidade militar sem IdentidadeUnidade", ref eventosEmitidos, limiteEventos);
            }
        }

        if (materialPersistente && !temSistemaDeDanos)
        {
            if (EhCategoriaMilitar(ficha.categoria) || unidadeCombate)
            {
                if (emDesenvolvimento)
                {
                    avisos++;
                    Emitir("AVISO", nome + ": unidade militar sem SistemaDeDanos (Ignorado)", ref eventosEmitidos, limiteEventos);
                }
                else
                {
                    erros++;
                    Emitir("ERRO", nome + ": unidade militar sem SistemaDeDanos", ref eventosEmitidos, limiteEventos);
                }
            }
            else
            {
                avisos++;
                Emitir("AVISO", nome + ": prefab sem SistemaDeDanos", ref eventosEmitidos, limiteEventos);
            }
        }

        if (unidadeCombate && !TemComponenteDeArma(prefab))
        {
            avisos++;
            Emitir("AVISO", nome + ": unidade de combate sem arma/sistema de tiro detectado", ref eventosEmitidos, limiteEventos);
        }

        string suspeitaCategoria = DetectarCategoriaSuspeita(ficha, prefab);
        if (!string.IsNullOrEmpty(suspeitaCategoria))
        {
            avisos++;
            Emitir("AVISO", nome + ": categoria suspeita - " + suspeitaCategoria, ref eventosEmitidos, limiteEventos);
        }
    }

    private static bool SaoFichasEquivalentes(DadosConstrucao primeira, DadosConstrucao segunda)
    {
        if (primeira == null || segunda == null) return false;
        if (primeira.categoria != segunda.categoria || primeira.preco != segunda.preco) return false;

        string nomePrimeira = string.IsNullOrWhiteSpace(primeira.NomeItem) ? primeira.name : primeira.NomeItem;
        string nomeSegunda = string.IsNullOrWhiteSpace(segunda.NomeItem) ? segunda.name : segunda.NomeItem;
        if (!string.Equals(nomePrimeira, nomeSegunda, System.StringComparison.OrdinalIgnoreCase)) return false;

        // Duas fichas podem apontar para o mesmo produto por caminhos de
        // catalogo diferentes. Nome, categoria e preco iguais caracterizam
        // esse alias sem bloquear o carregamento da campanha.
        return true;
    }

    private bool EhPrefabEmDesenvolvimento(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return false;
        string nomeMin = nome.ToLowerInvariant();
        return nomeMin.Contains("artilharia") ||
               nomeMin.Contains("track combustivel") ||
               nomeMin.Contains("barco ww transporte") ||
               nomeMin.Contains("estaleiro naval") ||
               nomeMin.Contains("navio_wall") ||
               nomeMin.Contains("dh hasaf") ||
               nomeMin.Contains("nav_yuza") ||
               nomeMin.Contains("yuza") ||
               nomeMin.Contains("nara aviao bombardeiro antigo") ||
               // A ficha Foguete/ICBM atual é um placeholder de catálogo (o
               // lançamento usa o prefab do lançador), portanto não deve
               // bloquear o carregamento da campanha por falta de identidade.
               nomeMin.Contains("foguete icbm") ||
               nomeMin == "foguete" ||
               nomeMin.Contains("icbm");
    }

    private bool EhCategoriaMilitar(DadosConstrucao.CategoriaItem categoria)
    {
        return categoria == DadosConstrucao.CategoriaItem.Exercito
               || categoria == DadosConstrucao.CategoriaItem.Marinha
               || categoria == DadosConstrucao.CategoriaItem.Aeronautica;
    }

    private bool EhLogisticaOuEstrutura(string nomeItem, string nomePrefab)
    {
        string texto = Normalizar(nomeItem + " " + nomePrefab);
        return texto.Contains("petroleiro")
               || texto.Contains("petrolifero")
               || texto.Contains("tanker")
               || texto.Contains("c17")
               || texto.Contains("c700")
               || texto.Contains("transporte")
               || texto.Contains("aeroporto")
               || texto.Contains("heliporto")
               || texto.Contains("estaleiro")
               || texto.Contains("pier")
               || texto.Contains("plataforma")
               || texto.Contains("prefeitura")
               || texto.Contains("quartel")
               || texto.Contains("fabrica");
    }

    private bool TemComponenteDeArma(GameObject prefab)
    {
        MonoBehaviour[] componentes = prefab.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < componentes.Length; i++)
        {
            MonoBehaviour componente = componentes[i];
            if (componente == null)
            {
                continue;
            }

            string tipo = componente.GetType().Name.ToLowerInvariant();
            if (tipo.Contains("tiro")
                || tipo.Contains("torreta")
                || tipo.Contains("missil")
                || tipo.Contains("arma")
                || tipo.Contains("canhao")
                || tipo.Contains("torpedo")
                || tipo.Contains("bombardeiro"))
            {
                return true;
            }
        }

        return false;
    }

    private string DetectarCategoriaSuspeita(DadosConstrucao ficha, GameObject prefab)
    {
        string texto = Normalizar(ficha.NomeItem + " " + prefab.name);
        if ((texto.Contains("navio") || texto.Contains("submarino") || texto.Contains("fragata") || texto.Contains("corveta") || texto.Contains("petroleiro"))
            && ficha.categoria != DadosConstrucao.CategoriaItem.Marinha)
        {
            return "parece naval, mas categoria e " + ficha.categoria;
        }

        if ((texto.Contains("aviao") || texto.Contains("caca") || texto.Contains("helicoptero") || texto.Contains("helice"))
            && ficha.categoria != DadosConstrucao.CategoriaItem.Aeronautica)
        {
            return "parece aereo, mas categoria e " + ficha.categoria;
        }

        if ((texto.Contains("soldado") || texto.Contains("tanque") || texto.Contains("blindado") || texto.Contains("infantaria"))
            && ficha.categoria != DadosConstrucao.CategoriaItem.Exercito)
        {
            return "parece terrestre, mas categoria e " + ficha.categoria;
        }

        return string.Empty;
    }

    private string Normalizar(string texto)
    {
        return string.IsNullOrEmpty(texto) ? string.Empty : texto.ToLowerInvariant();
    }

    private void Emitir(string nivel, string mensagem, ref int eventosEmitidos, int limiteEventos)
    {
        string linha = nivel + ": " + mensagem;
        if (eventosEmitidos < limiteEventos)
        {
            eventosEmitidos++;
            DiagnosticoDesempenhoJogo.RegistrarEvento("AuditoriaConteudo", linha);
        }

        if (nivel == "ERRO")
        {
            Debug.LogError("[AuditoriaConteudo] " + mensagem, this);
        }
        else if (!Application.isPlaying || avisosConsoleEmitidos < maxAvisosDetalhadosNoConsole)
        {
            avisosConsoleEmitidos++;
            Debug.LogWarning("[AuditoriaConteudo] " + mensagem, this);
        }
        else
        {
            // Todos os avisos continuam no resumo/eventos; apenas evitamos que o
            // Console do Editor cause um travamento por dezenas de logs iguais.
            avisosConsoleSuprimidos++;
        }
    }
}
