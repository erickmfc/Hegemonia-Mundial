using UnityEngine;

/// <summary>
/// Lançador de mísseis ultra-simplificado.
/// Detecta inimigos no radar e dispara automaticamente.
/// Sem animações, sem modos, sem complicação.
/// </summary>
public class LancadorSimples : MonoBehaviour
{
    [Header("Arsenal")]
    [Tooltip("Arraste o prefab do míssil aqui (ex: Comar, ICBM)")]
    public GameObject misselPrefab;
    
    [Tooltip("Arraste os pontos de saída dos mísseis (os 12 tubos)")]
    public Transform[] pontosDeSaida;

    [Header("Radar e Combate")]
    [Tooltip("Alcance do radar em metros")]
    public float alcanceRadar = 300f; // AUMENTADO para garantir detecção
    
    [Tooltip("Tempo entre cada míssil da rajada (segundos)")]
    public float intervaloEntreMisseis = 0.2f;
    
    [Tooltip("Tempo de recarga após disparar todos os mísseis (segundos)")]
    public float tempoDeRecarga = 8.0f;

    [Header("Identificação de Alvos")]
    [Tooltip("Tags que o radar considera como inimigos")]
    public string[] tagsInimigas = { "Inimigo", "Destrutivel" };

    // Internas
    private float proximoDisparoPermitido = 0f;
    private IdentidadeUnidade meuID;
    private float ultimoLog = 0f;

    void Start()
    {
        meuID = GetComponent<IdentidadeUnidade>();
        
        // Auto-configuração: Busca saídas automaticamente se não foram atribuídas
        if (pontosDeSaida == null || pontosDeSaida.Length == 0)
        {
            Debug.Log("[LançadorSimples] Buscando pontos de saída automaticamente...");
            var filhos = GetComponentsInChildren<Transform>();
            var lista = new System.Collections.Generic.List<Transform>();
            
            foreach (var f in filhos)
            {
                if (f != transform && (f.name.Contains("Saida") || f.name.Contains("Element") || f.name.Contains("Tube")))
                {
                    lista.Add(f);
                }
            }
            
            pontosDeSaida = lista.ToArray();
            Debug.Log($"[LançadorSimples] Encontradas {pontosDeSaida.Length} saídas automaticamente.");
        }

        if (misselPrefab == null)
        {
            Debug.LogError("[LançadorSimples] ERRO: Nenhum prefab de míssil atribuído! Arraste o míssil no Inspector.");
        }
        else
        {
            Debug.Log($"[LançadorSimples] Prefab configurado: {misselPrefab.name}");
        }
    }

    void Update()
    {
        // LOG DE DIAGNÓSTICO (a cada 2 segundos)
        if (Time.time - ultimoLog > 2.0f)
        {
            ultimoLog = Time.time;
            bool podeAtirar = Time.time >= proximoDisparoPermitido;
            float tempoRestante = Mathf.Max(0, proximoDisparoPermitido - Time.time);
            Debug.Log($"[LançadorSimples] Status: Pode Atirar={podeAtirar} | Recarga={tempoRestante:F1}s | Prefab={(misselPrefab != null ? "OK" : "NULL")} | Saídas={pontosDeSaida?.Length ?? 0}");
        }

        // Só procura alvos se já passou o tempo de recarga
        if (Time.time >= proximoDisparoPermitido)
        {
            Transform alvo = BuscarAlvoMaisProximo();
            
            if (alvo != null)
            {
                Debug.LogWarning($"[LançadorSimples] ⚠️ ALVO DETECTADO: {alvo.name}! Iniciando rajada!");
                ExecutarRajada(alvo);
            }
        }
    }

    /// <summary>
    /// Varre o radar e retorna o inimigo mais próximo
    /// </summary>
    Transform BuscarAlvoMaisProximo()
    {
        Collider[] objetosNoRadar = Physics.OverlapSphere(transform.position, alcanceRadar);
        Transform melhorAlvo = null;
        float menorDistancia = Mathf.Infinity;

        int totalDetectado = objetosNoRadar.Length;
        int ignoradosPorRoot = 0;
        int ignoradosPorTag = 0;
        int ignoradosPorTime = 0;
        int alvosValidos = 0;

        foreach (var col in objetosNoRadar)
        {
            // 1. Ignora a si mesmo
            if (col.transform.root == transform.root)
            {
                ignoradosPorRoot++;
                continue;
            }

            // 2. Verifica se tem uma tag inimiga
            bool tagValida = false;
            foreach (string tag in tagsInimigas)
            {
                if (col.CompareTag(tag))
                {
                    tagValida = true;
                    break;
                }
            }
            
            // Se não tem tag válida, tenta ver se tem vida (SistemaDeDanos)
            if (!tagValida)
            {
                var vida = col.GetComponent<SistemaDeDanos>() ?? col.GetComponentInParent<SistemaDeDanos>();
                if (vida == null)
                {
                    ignoradosPorTag++;
                    continue; // Não é alvo válido
                }
            }

            // 3. IFF (Identificação Amigo/Inimigo)
            if (meuID != null)
            {
                IdentidadeUnidade idAlvo = col.GetComponent<IdentidadeUnidade>() ?? col.GetComponentInParent<IdentidadeUnidade>();
                
                if (idAlvo != null && idAlvo.teamID == meuID.teamID)
                {
                    ignoradosPorTime++;
                    continue; // É aliado, ignora
                }
            }

            // 4. Escolhe o mais próximo
            alvosValidos++;
            float distancia = Vector3.Distance(transform.position, col.transform.position);
            if (distancia < menorDistancia)
            {
                menorDistancia = distancia;
                melhorAlvo = col.transform;
            }
        }

        // LOG DETALHADO
        if (Time.time - ultimoLog > 2.0f)
        {
            if (totalDetectado == 0)
            {
                Debug.LogWarning($"[Radar] ⚠️ NENHUM OBJETO DETECTADO no raio de {alcanceRadar}m! Verifique se há inimigos perto e se eles têm Collider.");
            }
            else if (totalDetectado > 0)
            {
                Debug.Log($"[Radar] Detectados: {totalDetectado} | Próprio: {ignoradosPorRoot} | Sem Tag: {ignoradosPorTag} | Aliados: {ignoradosPorTime} | VÁLIDOS: {alvosValidos}");
            }
        }

        return melhorAlvo;
    }

    /// <summary>
    /// Dispara todos os mísseis em sequência rápida
    /// </summary>
    void ExecutarRajada(Transform alvo)
    {
        if (pontosDeSaida == null || pontosDeSaida.Length == 0)
        {
            Debug.LogError("[LançadorSimples] Nenhum ponto de saída configurado!");
            return;
        }

        // Define quando poderá atirar novamente
        proximoDisparoPermitido = Time.time + tempoDeRecarga;

        // Dispara cada míssil com um pequeno atraso
        for (int i = 0; i < pontosDeSaida.Length; i++)
        {
            if (pontosDeSaida[i] != null)
            {
                float atraso = i * intervaloEntreMisseis;
                StartCoroutine(DispararComAtraso(pontosDeSaida[i], alvo, atraso));
            }
        }

        Debug.Log($"[LançadorSimples] Rajada de {pontosDeSaida.Length} mísseis iniciada! Recarga: {tempoDeRecarga}s");
    }

    /// <summary>
    /// Dispara um único míssil após um atraso
    /// </summary>
    System.Collections.IEnumerator DispararComAtraso(Transform ponto, Transform alvo, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (ponto == null || misselPrefab == null) yield break;

        // Instancia o míssil
        GameObject missel = Instantiate(misselPrefab, ponto.position, ponto.rotation);

        // Calcula o destino (posição atual do alvo + previsão de movimento)
        Vector3 destino = alvo != null ? alvo.position : (transform.position + transform.forward * 100f);

        // Tenta inicializar o script do míssil
        var icbm = missel.GetComponent<MisselICBM>();
        if (icbm != null)
        {
            icbm.IniciarLancamento(destino);
            yield break;
        }

        var tatico = missel.GetComponent<MisselTatico>();
        if (tatico != null)
        {
            tatico.IniciarLancamento(destino);
            yield break;
        }

        // Fallback: Projétil genérico
        var projetil = missel.GetComponent<Projetil>();
        if (projetil != null)
        {
            projetil.SetDono(gameObject);
            if (alvo != null)
            {
                Vector3 direcao = (alvo.position - ponto.position).normalized;
                projetil.SetDirecao(direcao);
            }
        }
    }

    // Visualização do alcance no Editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, alcanceRadar);
    }
}
