using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// IA General Pro: Gerencia recrutamento contínuo e combate ativo.
/// Não fica parado esperando um batalhão completo - recruta e ataca progressivamente.
/// </summary>
public class IA_General_Pro : MonoBehaviour
{
    private IA_Comandante chefe;
    
    [Header("Composição Desejada (Mínimos)")]
    public int soldadosDesejados = 6;
    public int tanquesDesejados = 2;
    public int helicopterosDesejados = 1;

    [Header("Agressividade")]
    [Tooltip("Quantidade mínima de unidades para lançar um ataque")]
    public int minimoParaAtacar = 3;
    [Tooltip("Tempo entre tentativas de ataque (segundos)")]
    public float intervaloAtaque = 15f;

    // Listas de Controle por tipo
    private List<GameObject> grupoSoldados = new List<GameObject>();
    private List<GameObject> grupoTanques = new List<GameObject>();
    private List<GameObject> grupoHelis = new List<GameObject>();
    private List<GameObject> grupoOutros = new List<GameObject>();

    // Fábricas registradas pelo Arquiteto
    [SerializeField] private List<Fabrica> minhasFabricas = new List<Fabrica>();

    // Timers
    private float _timerRecrutamento;
    private float _timerAtaque;
    private float _timerReorganizar;
    private string _ultimoStatus = "Iniciando...";

    // Estado
    private bool jaAtacou = false;
    private Vector3 ultimoAlvoPosicao;

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
    }

    void Update()
    {
        if (chefe == null) return;

        // 1. Recrutamento contínuo (a cada 2s) - NUNCA para de recrutar
        _timerRecrutamento += Time.deltaTime;
        if (_timerRecrutamento >= 2.0f)
        {
            _timerRecrutamento = 0;
            TentarRecrutar();
        }

        // 2. Reorganizar tropas (a cada 5s)
        _timerReorganizar += Time.deltaTime;
        if (_timerReorganizar >= 5.0f)
        {
            _timerReorganizar = 0;
            LimparMortos();
            
            // Se não está atacando, manda tropas pro ponto de encontro
            if (!jaAtacou)
            {
                MoverTropasParaPontoDeEncontro();
            }
        }

        // 3. Combate (a cada X segundos, verifica se pode atacar)
        _timerAtaque += Time.deltaTime;
        if (_timerAtaque >= intervaloAtaque)
        {
            _timerAtaque = 0;
            AvaliarCombate();
        }
    }

    // =============================================
    // RECRUTAMENTO - Recruta continuamente, variando tipos
    // =============================================
    // =============================================
    // RECRUTAMENTO - Recruta continuamente, variando tipos
    // =============================================
    void TentarRecrutar()
    {
        if (chefe.dinheiro < 100) 
        {
            _ultimoStatus = "💰 Sem dinheiro para recrutar";
            return; 
        }

        LimparMortos();
        int totalUnidades = TotalUnidades();

        // Verifica quais fábricas temos disponíveis
        bool temQuartel = minhasFabricas.Any(f => f.ehQuartel);
        bool temFabricaVeiculos = minhasFabricas.Any(f => !f.ehQuartel && !f.name.Contains("Naval") && !f.name.Contains("Pier"));

        // Prioridade 1: Soldados (Base do exército)
        if (grupoSoldados.Count < soldadosDesejados)
        {
            if (temQuartel)
            {
                if (ComprarUnidadePorCategoria(true, "Soldado", "Rifle", "Infantaria", "Tropa"))
                {
                    _ultimoStatus = "🎖️ Recrutando Infantaria...";
                    return;
                }
            }
            else
            {
                _ultimoStatus = "⚠️ Preciso de um Quartel!";
                // Opcional: Pedir ao Arquiteto para construir (se houvesse comunicação direta)
            }
        }

        // Prioridade 2: Tanques (Força pesada terrestre)
        if (grupoTanques.Count < tanquesDesejados && chefe.dinheiro > 300)
        {
            if (temFabricaVeiculos)
            {
                // Tenta comprar TANQUES especificamente (evita hovercrafts/navios por enquanto)
                if (ComprarUnidadePorCategoria(false, "Tanque", "Tank", "Leopard", "Blindado", "Arthur"))
                {
                    _ultimoStatus = "🎖️ Comprando Tanque...";
                    return;
                }
            }
            else
            {
                _ultimoStatus = "⚠️ Preciso de Fábrica de Veículos!";
            }
        }

        // Prioridade 3: Helicópteros (Apoio Aéreo)
        if (grupoHelis.Count < helicopterosDesejados && chefe.dinheiro > 500)
        {
            // Helicópteros geralmente feitos em Heliporto ou Fábrica Avançada
             if (ComprarUnidadePorCategoria(false, "Heli", "Helicoptero", "Apache", "Cobra"))
            {
                _ultimoStatus = "🎖️ Comprando Helicóptero...";
                return;
            }
        }

        // Prioridade 4: Se o exército já está base ok, reforça com o que tiver fábrica
        if (totalUnidades >= minimoParaAtacar)
        {
            if (temFabricaVeiculos && chefe.dinheiro > 400 && Random.value > 0.6f)
            {
                 ComprarUnidadePorCategoria(false, "Tanque", "Leopard");
            }
            else if (temQuartel)
            {
                 ComprarUnidadePorCategoria(true, "Soldado", "Rifle");
            }
        }
    }

    /// <summary>
    /// Tenta comprar uma unidade que contenha qualquer uma das keywords no nome.
    /// </summary>
    bool ComprarUnidadePorCategoria(bool requerQuartel, params string[] keywords)
    {
        // 1. Achar Fábrica compatível
        Fabrica fabrica = null;
        
        // Primeiro tenta na lista registrada
        foreach (var f in minhasFabricas)
        {
            // Filtra fábricas navais se não estamos pedindo barcos
            if (f != null && f.ehQuartel == requerQuartel)
            {
                // Evita estaleiros se estamos querendo tanques
                if (!requerQuartel && (f.name.Contains("Naval") || f.name.Contains("Pier"))) continue;

                fabrica = f;
                break;
            }
        }
        
        // Fallback: busca global na cena
        if (fabrica == null)
        {
            var todasFabricas = FindObjectsByType<Fabrica>(FindObjectsSortMode.None);
            foreach (var f in todasFabricas)
            {
                var id = f.GetComponentInParent<IdentidadeUnidade>();
                if (id != null && id.teamID == 2 && f.ehQuartel == requerQuartel)
                {
                     // Evita estaleiros se estamos querendo tanques
                    if (!requerQuartel && (f.name.Contains("Naval") || f.name.Contains("Pier"))) continue;

                    fabrica = f;
                    RegistrarFabrica(f);
                    break;
                }
            }
        }
        
        if (fabrica == null) return false;

        // 2. Achar Prefab no Catálogo
        if (MenuConstrucao.catalogoGlobal == null || MenuConstrucao.catalogoGlobal.Count == 0) 
            return false;

        // Busca por qualquer keyword strictamente
        DadosConstrucao ficha = null;
        foreach (var item in MenuConstrucao.catalogoGlobal)
        {
            if (item == null || item.prefabDaUnidade == null) continue;
            if (item.preco > chefe.dinheiro) continue;
            
            string nomeLower = item.nomeItem.ToLower();

            // Lógica Exclusiva: Se pedir Tanque, NÃO aceita Hovercraft/Navio
            bool ehNaval = nomeLower.Contains("hover") || nomeLower.Contains("navio") || nomeLower.Contains("barco") || nomeLower.Contains("submarino");
            if (ehNaval) continue; // Pula unidades navais/anfíbias que não foram pedidas explicitamente

            foreach (string kw in keywords)
            {
                if (nomeLower.Contains(kw.ToLower()))
                {
                    ficha = item;
                    break;
                }
            }
            if (ficha != null) break;
        }

        // Fallback genérico APENAS para soldados
        if (ficha == null && requerQuartel)
        {
            ficha = MenuConstrucao.catalogoGlobal.FirstOrDefault(item => 
                item != null && 
                item.prefabDaUnidade != null &&
                item.categoria == DadosConstrucao.CategoriaItem.Exercito && 
                item.preco <= chefe.dinheiro &&
                item.preco < 300);
        }

        if (ficha == null) return false;

        // 3. Produzir
        if (chefe.GastarDinheiro(ficha.preco))
        {
            GameObject novaUnidade = fabrica.ProduzirUnidade(ficha.prefabDaUnidade);
            if (novaUnidade != null)
            {
                RegistrarSoldado(novaUnidade);
                
                // Manda para o ponto de encontro (NÃO deixa parado na fábrica!)
                Vector3 pontoEncontro = CalcularPontoDeEncontro();
                MoverUnidade(novaUnidade, pontoEncontro);
                
                Debug.Log($"[IA General Pro] ✅ Recrutou: {ficha.nomeItem} (${ficha.preco})");
                return true;
            }
            else
            {
                // Devolve o dinheiro se falhou
                chefe.AdicionarDinheiro(ficha.preco);
            }
        }
        return false;
    }

    // =============================================
    // COMBATE - Avalia e lança ataques progressivos
    // =============================================
    void AvaliarCombate()
    {
        // 0. Verifica Tempo de Paz
        if (chefe.tempoDePaz > 0)
        {
            float tempo = chefe.tempoDePaz;
            _ultimoStatus = $"🕊️ TEMPO DE PAZ: {tempo:F0}s (Recrutando apenas)";
            jaAtacou = false;
            return;
        }

        LimparMortos();
        int total = TotalUnidades();

        if (total < minimoParaAtacar)
        {
            _ultimoStatus = $"🛡️ Preparando forças ({total}/{minimoParaAtacar})";
            jaAtacou = false;
            return;
        }

        // Busca um alvo
        Transform alvo = BuscarAlvo();
        if (alvo == null)
        {
            _ultimoStatus = "👀 Procurando inimigos...";
            return;
        }

        _ultimoStatus = $"⚔️ ATACANDO com {total} unidades!";
        jaAtacou = true;
        ultimoAlvoPosicao = alvo.position;
        LancarAtaqueCoordenado(alvo.position);
    }

    void LancarAtaqueCoordenado(Vector3 destino)
    {
        Vector3 direcao = (destino - chefe.transform.position).normalized;
        if (direcao == Vector3.zero) direcao = Vector3.forward;

        // Vetor lateral (Direita 90 graus)
        Vector3 flancoDir = Vector3.Cross(Vector3.up, direcao).normalized;
        
        // 1. TANQUES: Vanguarda Centralizada
        // Formação de Cunha ou Linha frontal
        MoverEmFormacao(grupoTanques, destino, direcao, 5.0f); // 5m de espaçamento

        // 2. SOLDADOS: Dividir em 2 Esquadrões (Esquerda e Direita)
        List<GameObject> esquadraoEsq = new List<GameObject>();
        List<GameObject> esquadraoDir = new List<GameObject>();

        for (int i = 0; i < grupoSoldados.Count; i++)
        {
            if (i % 2 == 0) esquadraoEsq.Add(grupoSoldados[i]);
            else esquadraoDir.Add(grupoSoldados[i]);
        }

        // Define posições afastadas do centro para não misturar com tanques
        Vector3 posEsq = destino - flancoDir * 15f; // 15m para esquerda
        Vector3 posDir = destino + flancoDir * 15f; // 15m para direita

        // Move os esquadrões em formação de grid
        MoverEmFormacao(esquadraoEsq, posEsq, direcao, 2.5f);
        MoverEmFormacao(esquadraoDir, posDir, direcao, 2.5f);

        // 3. HELICÓPTEROS: Flanco Aéreo Distante
        // Ficam mais recuados e abertos
        MoverEmFormacao(grupoHelis, destino + flancoDir * 30f - direcao * 10f, direcao, 10.0f);

        // 4. OUTROS (Veículos leves/Caminhões): Retaguarda
        MoverEmFormacao(grupoOutros, destino - direcao * 15f, direcao, 6.0f);
    }

    /// <summary>
    /// Move uma lista de unidades para um ponto central, organizando-as em Grid para não encavalar.
    /// </summary>
    void MoverEmFormacao(List<GameObject> grupo, Vector3 centro, Vector3 direcaoFrente, float espacamento)
    {
        if (grupo.Count == 0) return;

        Vector3 direita = Vector3.Cross(Vector3.up, direcaoFrente).normalized;
        
        // Calcula quantas colunas para ficar "quadrado"
        int colunas = Mathf.CeilToInt(Mathf.Sqrt(grupo.Count)); 
        if (colunas < 2) colunas = 2; // Mínimo 2 de largura

        for (int i = 0; i < grupo.Count; i++)
        {
            if (grupo[i] == null) continue;

            int linha = i / colunas;
            int col = i % colunas;

            // Centraliza o grid no ponto 'centro'
            // X: offset lateral
            float offsetX = (col - (colunas - 1) / 2f) * espacamento;
            // Z: offset de profundidade (linhas vão ficando para trás do ponto de ataque)
            float offsetZ = -linha * espacamento; 

            Vector3 posFinal = centro + (direita * offsetX) + (direcaoFrente * offsetZ);
            
            MoverUnidade(grupo[i], posFinal);
        }
    }

    // =============================================
    // ORGANIZAÇÃO DE TROPAS (Wait/Rally Logic)
    // =============================================
    Vector3 CalcularPontoDeEncontro()
    {
        Vector3 centro = (chefe.basePrincipal != null) 
            ? chefe.basePrincipal.position 
            : chefe.transform.position;
        
        // Ponto de encontro: 25m à frente da base
        return centro + Vector3.forward * 25f;
    }

    void MoverTropasParaPontoDeEncontro()
    {
        Vector3 ponto = CalcularPontoDeEncontro();
        
        // Usa a mesma lógica de formação, mas todos juntos num grande "Exército Parado"
        var todos = new List<GameObject>();
        todos.AddRange(grupoTanques);
        todos.AddRange(grupoSoldados);
        todos.AddRange(grupoHelis);
        todos.AddRange(grupoOutros);

        // Direção padrão para frente da base
        Vector3 frente = Vector3.forward;
        if (chefe.basePrincipal != null) frente = chefe.basePrincipal.forward;

        // Grid mais apertado para esperar
        MoverEmFormacao(todos, ponto, frente, 3.0f); 
    }

    // =============================================
    // REGISTRO E CLASSIFICAÇÃO
    // =============================================
    public void RegistrarSoldado(GameObject unidade)
    {
        if (unidade == null) return;
        
        // Evita duplicatas
        if (grupoTanques.Contains(unidade) || grupoSoldados.Contains(unidade) || 
            grupoHelis.Contains(unidade) || grupoOutros.Contains(unidade)) return;

        string n = unidade.name.ToLower();
        
        if (n.Contains("tank") || n.Contains("leopard") || n.Contains("blindado") || n.Contains("arthur"))
        {
            grupoTanques.Add(unidade);
        }
        else if (n.Contains("heli") || n.Contains("apache") || n.Contains("cobra") || 
                 unidade.GetComponent<Helicoptero>() != null)
        {
            grupoHelis.Add(unidade);
        }
        else if (n.Contains("truck") || n.Contains("transp") || n.Contains("houver") || n.Contains("hover"))
        {
            grupoOutros.Add(unidade);
        }
        else
        {
            grupoSoldados.Add(unidade); // Default: infantaria
        }
    }

    public void RegistrarFabrica(Fabrica fab)
    {
        if (fab != null && !minhasFabricas.Contains(fab))
        {
            minhasFabricas.Add(fab);
            Debug.Log($"[IA General Pro] 🏭 Fábrica registrada: {fab.name} (Quartel={fab.ehQuartel})");
        }
    }

    // =============================================
    // UTILIDADES
    // =============================================
    void MoverGrupo(List<GameObject> grupo, Vector3 destino)
    {
        foreach (var u in grupo)
        {
            if (u != null) MoverUnidade(u, destino);
        }
    }

    void MoverUnidade(GameObject u, Vector3 destino)
    {
        if (u == null) return;
        
        var controle = u.GetComponent<ControleUnidade>();
        if (controle != null) 
        { 
            controle.MoverParaPonto(destino); 
            return; 
        }
        
        var nav = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null && nav.isOnNavMesh) 
        { 
            nav.SetDestination(destino); 
            nav.isStopped = false; 
        }
    }

    void LimparMortos()
    {
        grupoTanques.RemoveAll(u => u == null);
        grupoSoldados.RemoveAll(u => u == null);
        grupoHelis.RemoveAll(u => u == null);
        grupoOutros.RemoveAll(u => u == null);
        minhasFabricas.RemoveAll(f => f == null);
    }

    int TotalUnidades()
    {
        return grupoTanques.Count + grupoSoldados.Count + grupoHelis.Count + grupoOutros.Count;
    }

    Transform BuscarAlvo()
    {
        var inimigos = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        Transform melhorAlvo = null;
        float menorDist = float.MaxValue;

        foreach (var ini in inimigos)
        {
            if (ini == null || ini.teamID == 2) continue; // Ignora aliados
            if (ini.teamID == 1) // Jogador
            {
                float dist = Vector3.Distance(chefe.transform.position, ini.transform.position);
                if (dist < menorDist)
                {
                    menorDist = dist;
                    melhorAlvo = ini.transform;
                }
            }
        }
        return melhorAlvo;
    }

    // =============================================
    // DEBUG VISUAL NA TELA
    // =============================================
    void OnGUI()
    {
        if (chefe == null) return;

        GUI.Box(new Rect(10, 10, 320, 180), "CÉREBRO DA IA");

        string status = $"Estado: {_ultimoStatus}\n" +
                        $"Dinheiro: ${chefe.dinheiro:F0}\n" +
                        $"--------------------------\n" +
                        $"Fábricas: {minhasFabricas.Count}\n" +
                        $"--------------------------\n" +
                        $"Soldados: {grupoSoldados.Count} / {soldadosDesejados}\n" +
                        $"Tanques: {grupoTanques.Count} / {tanquesDesejados}\n" +
                        $"Helis: {grupoHelis.Count} / {helicopterosDesejados}\n" +
                        $"Outros: {grupoOutros.Count}\n" +
                        $"TOTAL: {TotalUnidades()} (min ataque: {minimoParaAtacar})";

        GUI.Label(new Rect(20, 35, 300, 160), status);
    }
}
