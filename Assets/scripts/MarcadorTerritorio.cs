using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(IdentidadeUnidade))]
public class MarcadorTerritorio : MonoBehaviour
{
    private static readonly Dictionary<int, CidadeComplexoUrbano> CidadeGuiaExpansaoPorTime = new Dictionary<int, CidadeComplexoUrbano>();
    private static readonly HashSet<int> TimesConstruindoCidade = new HashSet<int>();

    [Header("Área de Domínio")]
    [Tooltip("Metade do tamanho do lado do quadrado. Ex: Raio 100 cria um domínio de 200x200 metros quadrados.")]
    public float raioDeDominio = 100f; 
    public bool ehPrefeitura = false; // Se for Prefeitura, raio = 300 e define regras de ilha

    [Header("Visualização de Fronteira no Jogo")]
    [Tooltip("Mostra linhas e uma área espelhada transparente no campo de batalha para definir de quem é a área.")]
    public bool mostrarBordasNoJogo = true;
    public float larguraLinha = 1.5f; // Grossura do risco pintado no chão

    [Header("Marcos de cidade sequenciais")]
    [Tooltip("Quando ativo, este ponto manual cria uma cidade: primeiro após o atraso inicial e as seguintes quando a cidade-guia atingir a capacidade. Não registra fronteira.")]
    public bool construirCidadeAoIniciar = false;
    [Min(0f)] public float atrasoConstrucaoCidadeSegundos = 15f;
    public GameObject prefabCidadeInicial;

    private LineRenderer linhaFronteira;
    private GameObject areaTranslúcida;

    // Identidade cache
    public int teamID { get; private set; }
    private bool prontoParaConstruirCidade;
    private bool construindoCidade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimparSequenciaDeCidadesRuntime()
    {
        CidadeGuiaExpansaoPorTime.Clear();
        TimesConstruindoCidade.Clear();
    }

    void OnEnable()
    {
        RegistroEntidadesJogo.Register(this);
    }

    void OnDisable()
    {
        RegistroEntidadesJogo.Unregister(this);
    }

    void Start()
    {
        var id = GetComponent<IdentidadeUnidade>();
        if (construirCidadeAoIniciar)
        {
            ConfigureOwnership(id != null ? id.teamID : teamID, false, 0f);
            mostrarBordasNoJogo = false;
            StartCoroutine(PrepararConstrucaoCidadeInicial());
            return;
        }

        bool detectarPrefeitura = GetComponent<ComplexoGovernamental>() != null
            || gameObject.name.ToLower().Contains("prefeitura");
        ConfigureOwnership(id != null ? id.teamID : teamID, detectarPrefeitura, 0f);

        // Aguarda 1 frame para garantir que os Gerentes existam antes de registrar
        Invoke("RegistrarSe", 0.5f);
        
        // Inicia identificação in-game
        Invoke("CriarIdentificacaoVisual", 0.6f);
    }

    private IEnumerator PrepararConstrucaoCidadeInicial()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, atrasoConstrucaoCidadeSegundos));
        if (this == null) yield break;

        SistemaSaveGame save = SistemaSaveGame.Instancia;
        if (save != null && save.carregouDeSave)
        {
            Destroy(gameObject);
            yield break;
        }

        prontoParaConstruirCidade = true;
        TentarConstruirProximaCidade(teamID);
    }

    private static void TentarConstruirProximaCidade(int timeId)
    {
        if (timeId <= 0 || TimesConstruindoCidade.Contains(timeId)) return;

        CidadeGuiaExpansaoPorTime.TryGetValue(timeId, out CidadeComplexoUrbano cidadeGuia);
        if (cidadeGuia != null && cidadeGuia.populacaoResidente < cidadeGuia.capacidadeHabitacional)
            return;

        MarcadorTerritorio proximo = null;
        MarcadorTerritorio[] marcadores = FindObjectsByType<MarcadorTerritorio>(FindObjectsSortMode.None);
        for (int i = 0; i < marcadores.Length; i++)
        {
            MarcadorTerritorio candidato = marcadores[i];
            if (candidato == null || !candidato.isActiveAndEnabled || !candidato.construirCidadeAoIniciar
                || !candidato.prontoParaConstruirCidade || candidato.construindoCidade || candidato.teamID != timeId)
                continue;

            if (proximo == null || CompararOrdemNaHierarquia(candidato.transform, proximo.transform) < 0)
                proximo = candidato;
        }

        if (proximo == null) return;

        TimesConstruindoCidade.Add(timeId);
        proximo.construindoCidade = true;
        proximo.StartCoroutine(proximo.ConstruirCidadeNoTerreno());
    }

    private static int CompararOrdemNaHierarquia(Transform a, Transform b)
    {
        string chaveA = ObterCaminhoHierarquia(a);
        string chaveB = ObterCaminhoHierarquia(b);
        return string.CompareOrdinal(chaveA, chaveB);
    }

    private static string ObterCaminhoHierarquia(Transform alvo)
    {
        if (alvo == null) return string.Empty;
        List<string> segmentos = new List<string>(8);
        for (Transform atual = alvo; atual != null; atual = atual.parent)
            segmentos.Add(atual.GetSiblingIndex().ToString("D5") + ":" + atual.name);
        segmentos.Reverse();
        return alvo.gameObject.scene.path + "/" + string.Join("/", segmentos);
    }

    private IEnumerator ConstruirCidadeNoTerreno()
    {

        if (prefabCidadeInicial == null)
        {
            Debug.LogError($"[Cidade inicial] O marco '{name}' não tem prefab de cidade configurado; o marco foi mantido.", this);
            FinalizarTentativaConstrucaoCidade();
            yield break;
        }

        CidadeComplexoUrbano cidadeExistente = EncontrarCidadeDoTimeNestePonto();
        if (cidadeExistente != null)
        {
            RegistrarCidadeGuiaExpansao(teamID, cidadeExistente);
            FinalizarTentativaConstrucaoCidade();
            Destroy(gameObject);
            yield break;
        }

        for (int tentativa = 0; tentativa < 10; tentativa++)
        {
            if (TryObterPosicaoNoTerreno(out Vector3 posicao, out bool pontoNaAgua))
            {
                GameObject cidadeObjeto = Instantiate(prefabCidadeInicial, posicao, Quaternion.Euler(-90f, 0f, 0f));
                CidadeComplexoUrbano cidade = cidadeObjeto != null
                    ? cidadeObjeto.GetComponentInChildren<CidadeComplexoUrbano>(true)
                    : null;
                if (cidade == null)
                {
                    if (cidadeObjeto != null) Destroy(cidadeObjeto);
                    Debug.LogError($"[Cidade inicial] O prefab '{prefabCidadeInicial.name}' não contém CidadeComplexoUrbano; o marco foi mantido.", this);
                    FinalizarTentativaConstrucaoCidade();
                    yield break;
                }

                cidadeObjeto.name = "Nova Aurora";
                cidade.nomeCidade = "Nova Aurora";
                cidade.teamId = teamID;
                cidade.populacaoResidente = 10000;

                IdentidadeUnidade identidade = cidade.GetComponent<IdentidadeUnidade>();
                if (identidade == null) identidade = cidade.GetComponentInChildren<IdentidadeUnidade>(true);
                if (identidade != null)
                {
                    identidade.teamID = teamID;
                    IdentidadeUnidade identidadeMarco = GetComponent<IdentidadeUnidade>();
                    if (identidadeMarco != null) identidade.nomeDoPais = identidadeMarco.nomeDoPais;
                }

                MarcadorTerritorio marcadorCidade = cidade.GetComponent<MarcadorTerritorio>();
                if (marcadorCidade != null)
                {
                    marcadorCidade.construirCidadeAoIniciar = false;
                    marcadorCidade.mostrarBordasNoJogo = false;
                    marcadorCidade.ConfigureOwnership(teamID, false, 0f);
                }

                Renderer[] renderizadores = cidadeObjeto.GetComponentsInChildren<Renderer>(true);
                float menorY = float.PositiveInfinity;
                for (int i = 0; i < renderizadores.Length; i++)
                {
                    if (renderizadores[i] != null)
                        menorY = Mathf.Min(menorY, renderizadores[i].bounds.min.y);
                }
                if (!float.IsInfinity(menorY))
                    cidadeObjeto.transform.position += Vector3.up * (posicao.y - menorY);

                cidade.SincronizarEstruturaEconomica();
                SaveableEntity.Garantir(cidadeObjeto, prefabCidadeInicial.name);
                RegistrarCidadeGuiaExpansao(teamID, cidade);
                FinalizarTentativaConstrucaoCidade();
                Destroy(gameObject);
                Debug.Log($"[Cidade inicial] Nova Aurora criada para o Team {teamID} com 10.000 habitantes em {cidadeObjeto.transform.position}.", cidadeObjeto);
                yield break;
            }

            if (pontoNaAgua)
            {
                Debug.LogError($"[Cidade inicial] O marco '{name}' está sobre água; mova-o para terreno antes de iniciar uma nova partida.", this);
                FinalizarTentativaConstrucaoCidade();
                yield break;
            }

            if (tentativa < 9) yield return new WaitForSeconds(1f);
        }

        Debug.LogWarning($"[Cidade inicial] Não foi possível identificar o terreno do marco '{name}' após 10 tentativas; o marco foi mantido.", this);
        FinalizarTentativaConstrucaoCidade();
    }

    private static void RegistrarCidadeGuiaExpansao(int timeId, CidadeComplexoUrbano cidade)
    {
        if (timeId > 0 && cidade != null)
            CidadeGuiaExpansaoPorTime[timeId] = cidade;
    }

    private void FinalizarTentativaConstrucaoCidade()
    {
        construindoCidade = false;
        prontoParaConstruirCidade = false;
        TimesConstruindoCidade.Remove(teamID);
        TentarConstruirProximaCidade(teamID);
    }

    internal static void NotificarCidadeAtingiuCapacidade(CidadeComplexoUrbano cidade)
    {
        if (cidade == null || cidade.teamId <= 0
            || cidade.populacaoResidente < cidade.capacidadeHabitacional)
            return;

        if (CidadeGuiaExpansaoPorTime.TryGetValue(cidade.teamId, out CidadeComplexoUrbano cidadeGuia)
            && cidadeGuia == cidade)
        {
            TentarConstruirProximaCidade(cidade.teamId);
        }
    }

    internal static void RemoverCidadeGuiaExpansao(CidadeComplexoUrbano cidade)
    {
        if (cidade == null || !CidadeGuiaExpansaoPorTime.TryGetValue(cidade.teamId, out CidadeComplexoUrbano atual)
            || atual != cidade)
            return;
        CidadeGuiaExpansaoPorTime.Remove(cidade.teamId);
    }

    private CidadeComplexoUrbano EncontrarCidadeDoTimeNestePonto()
    {
        float distanciaMinima = Mathf.Max(10f, raioDeDominio * 0.1f);
        float distanciaMinimaSqr = distanciaMinima * distanciaMinima;
        CidadeComplexoUrbano[] cidades = FindObjectsByType<CidadeComplexoUrbano>(FindObjectsSortMode.None);
        for (int i = 0; i < cidades.Length; i++)
        {
            CidadeComplexoUrbano cidade = cidades[i];
            if (cidade == null || cidade.teamId != teamID) continue;
            Vector2 delta = new Vector2(cidade.transform.position.x - transform.position.x,
                cidade.transform.position.z - transform.position.z);
            if (delta.sqrMagnitude <= distanciaMinimaSqr) return cidade;
        }
        return null;
    }

    private bool TryObterPosicaoNoTerreno(out Vector3 posicao, out bool pontoNaAgua)
    {
        posicao = transform.position;
        pontoNaAgua = false;

        if (RegistroSuperficieMapa.TryClassify(posicao, out ClassificacaoSuperficieMapa superficie, out float altura))
        {
            if (superficie == ClassificacaoSuperficieMapa.Agua)
            {
                pontoNaAgua = true;
                return false;
            }

            if (superficie == ClassificacaoSuperficieMapa.Chao || superficie == ClassificacaoSuperficieMapa.Costa)
            {
                if (RegistroSuperficieMapa.TryGetAltura(posicao, TipoSuperficieMapa.Chao, out float alturaDoChao))
                    altura = alturaDoChao;
                posicao.y = altura;
                return true;
            }
        }

        Terrain[] terrenos = Terrain.activeTerrains;
        for (int i = 0; i < terrenos.Length; i++)
        {
            Terrain terreno = terrenos[i];
            if (terreno == null || terreno.terrainData == null) continue;
            Vector3 origem = terreno.transform.position;
            Vector3 tamanho = terreno.terrainData.size;
            if (posicao.x < origem.x || posicao.x > origem.x + tamanho.x
                || posicao.z < origem.z || posicao.z > origem.z + tamanho.z) continue;

            posicao.y = terreno.SampleHeight(posicao) + origem.y;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Configura uma fundação territorial criada em tempo de jogo. Isso permite que
    /// uma IA use a própria posição como capital sem procurar ou alterar outra cidade.
    /// </summary>
    public void ConfigureOwnership(int novoTeamId, bool definirComoPrefeitura, float raioMinimo)
    {
        teamID = Mathf.Max(0, novoTeamId);
        IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>();
        if (identidade != null)
        {
            identidade.teamID = teamID;
        }

        ehPrefeitura |= definirComoPrefeitura;

        if (ehPrefeitura)
        {
            raioDeDominio = Mathf.Max(300f, raioDeDominio, raioMinimo);
        }
        else if (raioMinimo > 0f)
        {
            raioDeDominio = Mathf.Max(raioDeDominio, raioMinimo);
        }
    }

    void RegistrarSe()
    {
        if (GerenteDeTerritorio.Instancia != null)
        {
            GerenteDeTerritorio.Instancia.RegistrarMarcador(this);
        }
        GerenciadorDivisaoTerritorial.GarantirInstancia();
        if (GerenciadorDivisaoTerritorial.Instancia != null)
        {
            GerenciadorDivisaoTerritorial.Instancia.RegistrarCidade(this);
        }
    }

    void OnDestroy()
    {
        RegistroEntidadesJogo.Unregister(this);
        if (GerenteDeTerritorio.Instancia != null)
        {
            GerenteDeTerritorio.Instancia.RemoverMarcador(this);
        }
        if (GerenciadorDivisaoTerritorial.Instancia != null)
        {
            GerenciadorDivisaoTerritorial.Instancia.RemoverCidade(this);
        }
    }

    // --- RENDERIZA O PISO "HOLOGRÁFICO" COM A COR DO PAÍS QUE VOCÊ PEDIU ---
    void CriarIdentificacaoVisual()
    {
        // A MD História usa o horizonte natural do Sea. A presença do
        // configurador da própria cena suprime apenas o overlay holográfico
        // territorial; posse, registro, raio e regras de território continuam
        // funcionando normalmente. Em outras cenas o comportamento original
        // permanece inalterado.
        if (!mostrarBordasNoJogo || FindFirstObjectByType<MdHistoriaMapaRuntime>() != null) return;

        // --- 1. A Linha da Fronteira (Quadrada) ---
        GameObject linhaObj = new GameObject("Visual_Fronteira_" + teamID);
        linhaObj.transform.SetParent(this.transform);
        linhaObj.transform.localPosition = Vector3.zero;
        
        linhaFronteira = linhaObj.AddComponent<LineRenderer>();
        linhaFronteira.useWorldSpace = false; // Em relação ao objeto pai
        linhaFronteira.startWidth = larguraLinha;
        linhaFronteira.endWidth = larguraLinha;
        linhaFronteira.positionCount = 5; 
        linhaFronteira.loop = true;
        linhaFronteira.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linhaFronteira.receiveShadows = false;
        
        Material mat = new Material(Shader.Find("Sprites/Default")); // Material brilhante limpo
        
        // Cores Dinâmicas pelo número da equipe (TeamID)
        Color corBase;
        switch (teamID)
        {
            case 1: corBase = new Color(0f, 0.4f, 1f, 1f); break; // Azul (Player)
            case 2: corBase = new Color(1f, 0.1f, 0.1f, 1f); break; // Vermelho (Inimigo)
            case 3: corBase = new Color(0f, 0.8f, 0f, 1f); break; // Verde
            case 4: corBase = new Color(1f, 0.8f, 0f, 1f); break; // Amarelo
            case 5: corBase = new Color(0.6f, 0f, 1f, 1f); break; // Roxo
            case 6: corBase = new Color(1f, 0f, 0.8f, 1f); break; // Rosa
            case 7: corBase = new Color(0f, 0.8f, 0.8f, 1f); break; // Ciano
            default: corBase = new Color(0.5f, 0.5f, 0.5f, 1f); break; // Cinza (Neutro ou 8+)
        }

        // BEM transparente:
        Color corBorda = new Color(corBase.r, corBase.g, corBase.b, 0.15f); // Era 0.8f
        Color corFundo = new Color(corBase.r, corBase.g, corBase.b, 0.02f); // Era 0.15f

        // Prefeituras são levemente mais visíveis (mas ainda transparentes)
        if (ehPrefeitura)
        {
            corBorda = new Color(corBase.r, corBase.g, corBase.b, 0.3f); 
        }

        linhaFronteira.material = mat;
        linhaFronteira.startColor = corBorda;
        linhaFronteira.endColor = corBorda;

        float r = raioDeDominio;
        float y = 0.5f; // Altura do chão para não clipar

        // Desenha as 4 quinas do quadrado usando coordenadas locais
        linhaFronteira.SetPosition(0, new Vector3(-r, y, -r));
        linhaFronteira.SetPosition(1, new Vector3(-r, y, r));
        linhaFronteira.SetPosition(2, new Vector3(r, y, r));
        linhaFronteira.SetPosition(3, new Vector3(r, y, -r));
        linhaFronteira.SetPosition(4, new Vector3(-r, y, -r));

        // --- 2. O Chão pintado translucido (Região interna inteira) ---
        areaTranslúcida = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(areaTranslúcida.GetComponent<Collider>()); // Tira colisão mecânica
        areaTranslúcida.name = "Piso_Holografico_Territorio";
        areaTranslúcida.transform.SetParent(this.transform);
        areaTranslúcida.transform.localPosition = new Vector3(0, 0.45f, 0); // Fica debaixo da linha
        areaTranslúcida.transform.localRotation = Quaternion.Euler(90, 0, 0); // Deita o flat no chão
        areaTranslúcida.transform.localScale = new Vector3(r * 2f, r * 2f, 1f); // Quad mede 1x1, multiplica pra preencher 100%
        
        Renderer rendQuad = areaTranslúcida.GetComponent<Renderer>();
        rendQuad.material = new Material(Shader.Find("Sprites/Default"));
        rendQuad.material.color = corFundo;
        rendQuad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rendQuad.receiveShadows = false;
    }

    // Desenhar um QUADRADO no editor permanentemente para você conseguir ver o mapa organizando a fase
    void OnDrawGizmos()
    {
        if (construirCidadeAoIniciar) return;

        Gizmos.color = ehPrefeitura ? new Color(1, 0.8f, 0, 0.2f) : new Color(0, 0.8f, 1, 0.2f);

        Vector3 size = new Vector3(raioDeDominio * 2, 0.1f, raioDeDominio * 2);

        // Preenchimento Suave do Quadrado
        Gizmos.DrawCube(transform.position, size);

        // Linha grossa nas bordas (Quadrado)
        Gizmos.color = ehPrefeitura ? Color.yellow : Color.cyan;
        Gizmos.DrawWireCube(transform.position, size);
    }
}
