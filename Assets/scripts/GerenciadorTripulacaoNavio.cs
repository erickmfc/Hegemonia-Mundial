using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// GerenciadorTripulacaoNavio - Controla a movimentação visual de tripulantes (trabalhadores e militares)
/// de forma otimizada, leve e local, sem o uso de NavMeshAgent (ideal para navios em movimento).
/// Possui compensação de escala para evitar distorções/gigantismo nos personagens.
/// </summary>
public class GerenciadorTripulacaoNavio : MonoBehaviour
{
    [System.Serializable]
    public class MembroTripulacao
    {
        public GameObject instancia;
        public Transform transform;
        public Animator animator;
        public Transform pontoDestino;
        public bool andando;
        public float tempoEsperaRestante;
        public bool militar;
        public Vector3 escalaOriginalPrefab;
        public bool temParametroVelocidade;
        /// <summary>Nome real do parâmetro de velocidade encontrado no Animator (ex: "Speed", "Velocidade", "Walk").</summary>
        public string nomeParametroVelocidadeEncontrado;
    }

    [Header("=== PREFABS DA TRIPULAÇÃO ===")]
    [Tooltip("Prefabs dos trabalhadores/funcionários (escolhidos aleatoriamente para variedade).")]
    public GameObject[] prefabsTrabalhador;
    [Tooltip("Prefabs dos militares (escolhidos aleatoriamente para variedade).")]
    public GameObject[] prefabsMilitar;

    [Header("=== CONFIGURAÇÃO DE QUANTIDADE ===")]
    [Range(0, 30)]
    public int quantidadeTrabalhadores = 5;
    [Range(0, 30)]
    public int quantidadeMilitares = 5;

    [Header("=== PONTOS DE MOVIMENTAÇÃO (WAYPOINTS) ===")]
    [Tooltip("Pontos de parada (GameObjects vazios) onde os trabalhadores podem ir.")]
    public List<Transform> pontosTrabalhador = new List<Transform>();
    [Tooltip("Pontos de parada (GameObjects vazios) onde os militares podem ir.")]
    public List<Transform> pontosMilitar = new List<Transform>();

    [Header("=== PARÂMETROS DE MOVIMENTO ===")]
    public float velocidadeMovimento = 2.0f;
    public float velocidadeRotacao = 6.0f;
    public float tempoEsperaMin = 4.0f;
    public float tempoEsperaMax = 12.0f;
    public string nomeParametroVelocidade = "Velocidade";

    [Header("=== OTIMIZAÇÃO (LOD) ===")]
    [Tooltip("Ativa otimização de desempenho baseada na distância da câmera principal.")]
    public bool usarLOD = true;
    [Tooltip("Distância a partir da qual o Animator dos personagens é desativado.")]
    public float distanciaLODDesativarAnim = 120.0f;
    [Tooltip("Distância a partir da qual a movimentação local é pausada e os modelos são ocultados.")]
    public float distanciaLODDesativarMov = 250.0f;
    [Tooltip("Se true, oculta os renderers dos tripulantes quando o navio estiver muito distante.")]
    public bool ocultarRenderersLOD = true;

    // Estado interno
    private List<MembroTripulacao> _tripulacao = new List<MembroTripulacao>();
    private HashSet<Transform> _pontosOcupados = new HashSet<Transform>();
    private Transform _shipTransform;
    
    // Controle de LOD por intervalo (evita cálculo de distância todo frame)
    private float _timerLOD = 0.0f;
    private const float INTERVALO_LOD = 0.5f;
    private bool _movimentoAtivoLOD = true;
    private bool _animatorAtivoLOD = true;

    private void Start()
    {
        _shipTransform = this.transform;
        PreprocessarWaypoints();
        SpawnarTripulacao();
    }

    private void Update()
    {
        // 1. Atualiza e gerencia o LOD por intervalo
        if (usarLOD)
        {
            _timerLOD += Time.deltaTime;
            if (_timerLOD >= INTERVALO_LOD)
            {
                _timerLOD = 0.0f;
                AtualizarLOD();
            }
        }

        // Se a movimentação local estiver desativada pelo LOD, pula o Update físico
        if (!_movimentoAtivoLOD) return;

        // 2. Processa movimentação local dos tripulantes ativos
        AtualizarMovimentacaoTripulacao();
    }

    private void SpawnarTripulacao()
    {
        // Limpa instâncias anteriores caso existam
        LimparTripulacao();

        // 1. Spawna Trabalhadores
        if (prefabsTrabalhador != null && prefabsTrabalhador.Length > 0 && pontosTrabalhador.Count > 0)
        {
            for (int i = 0; i < quantidadeTrabalhadores; i++)
            {
                Transform pontoInicial = ObterPontoLivre(pontosTrabalhador);
                if (pontoInicial == null) pontoInicial = pontosTrabalhador[Random.Range(0, pontosTrabalhador.Count)];

                GameObject prefab = prefabsTrabalhador[Random.Range(0, prefabsTrabalhador.Length)];
                CriarTripulante(prefab, pontoInicial, false);
            }
        }

        // 2. Spawna Militares
        if (prefabsMilitar != null && prefabsMilitar.Length > 0 && pontosMilitar.Count > 0)
        {
            for (int i = 0; i < quantidadeMilitares; i++)
            {
                Transform pontoInicial = ObterPontoLivre(pontosMilitar);
                if (pontoInicial == null) pontoInicial = pontosMilitar[Random.Range(0, pontosMilitar.Count)];

                GameObject prefab = prefabsMilitar[Random.Range(0, prefabsMilitar.Length)];
                CriarTripulante(prefab, pontoInicial, true);
            }
        }
    }

    private void CriarTripulante(GameObject prefab, Transform pontoInicial, bool ehMilitar)
    {
        if (prefab == null || pontoInicial == null) return;

        // Tenta desativar temporariamente o NavMeshAgent no prefab para evitar avisos de NavMesh no console ao instanciar
        UnityEngine.AI.NavMeshAgent prefabAgent = prefab.GetComponent<UnityEngine.AI.NavMeshAgent>();
        bool prefabAgentWasEnabled = false;
        if (prefabAgent != null && prefabAgent.enabled)
        {
            try
            {
                prefabAgent.enabled = false;
                prefabAgentWasEnabled = true;
            }
            catch (System.Exception) { }
        }

        // Instancia no mundo na posição do ponto inicial
        GameObject go = Instantiate(prefab, pontoInicial.position, pontoInicial.rotation);

        // Restaura o estado do prefab
        if (prefabAgent != null && prefabAgentWasEnabled)
        {
            try
            {
                prefabAgent.enabled = true;
            }
            catch (System.Exception) { }
        }

        // Remove ou desativa o NavMeshAgent na instância para que possamos controlar o movimento local
        // manualmente e evitar conflitos com o movimento do navio.
        UnityEngine.AI.NavMeshAgent instAgent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (instAgent != null)
        {
            instAgent.enabled = false;
            Destroy(instAgent);
        }

        // Garante que rigidbodies e colisores não interfiram com a movimentação local parentada ao navio
        foreach (Collider col in go.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = true;
        }

        // Parentesco ao navio: garante que ele se mova fisicamente com o navio
        go.transform.SetParent(_shipTransform, true);

        // CORREÇÃO CRÍTICA DE ESCALA:
        // Se o navio tiver escala diferente de (1,1,1), evitamos deformação ou gigantismo dividindo a escala do prefab pela do navio.
        Vector3 lossyScale = _shipTransform.lossyScale;
        Vector3 prefabScale = prefab.transform.localScale;
        go.transform.localScale = new Vector3(
            lossyScale.x > 0.001f ? prefabScale.x / lossyScale.x : prefabScale.x,
            lossyScale.y > 0.001f ? prefabScale.y / lossyScale.y : prefabScale.y,
            lossyScale.z > 0.001f ? prefabScale.z / lossyScale.z : prefabScale.z
        );

        // Preenche os dados do membro da tripulação
        MembroTripulacao membro = new MembroTripulacao();
        membro.instancia = go;
        membro.transform = go.transform;
        membro.animator = go.GetComponent<Animator>();
        if (membro.animator == null)
        {
            // Fallback: procura Animator nos filhos (caso o Animator não esteja no root do prefab)
            membro.animator = go.GetComponentInChildren<Animator>();
        }
        membro.militar = ehMilitar;
        membro.escalaOriginalPrefab = prefabScale;

        // Tenta detectar o parâmetro de velocidade no Animator (pode falhar se o controller não estiver carregado ainda)
        TentarDetectarParametroAnimator(membro);

        // Define o primeiro destino
        DefinirNovoDestino(membro);

        _tripulacao.Add(membro);
    }

    private void DefinirNovoDestino(MembroTripulacao membro)
    {
        List<Transform> listaPontos = membro.militar ? pontosMilitar : pontosTrabalhador;
        if (listaPontos == null || listaPontos.Count == 0) return;

        // Libera o ponto antigo
        if (membro.pontoDestino != null)
        {
            _pontosOcupados.Remove(membro.pontoDestino);
        }

        // Busca um ponto livre
        Transform novoPonto = ObterPontoLivre(listaPontos);
        if (novoPonto != null)
        {
            membro.pontoDestino = novoPonto;
            _pontosOcupados.Add(novoPonto);
            membro.andando = true;
            membro.tempoEsperaRestante = 0f;

            if (membro.animator != null && membro.temParametroVelocidade && _animatorAtivoLOD)
            {
                membro.animator.SetFloat(membro.nomeParametroVelocidadeEncontrado, 1.0f);
            }
        }
        else
        {
            // Se não houver pontos livres, fica parado e tenta novamente em 2 segundos
            membro.pontoDestino = null;
            membro.andando = false;
            membro.tempoEsperaRestante = Random.Range(1.5f, 3.0f);

            if (membro.animator != null && membro.temParametroVelocidade && _animatorAtivoLOD)
            {
                membro.animator.SetFloat(membro.nomeParametroVelocidadeEncontrado, 0.0f);
            }
        }
    }

    private Transform ObterPontoLivre(List<Transform> lista)
    {
        List<Transform> livres = new List<Transform>();
        foreach (Transform t in lista)
        {
            if (t != null && !_pontosOcupados.Contains(t))
            {
                livres.Add(t);
            }
        }

        if (livres.Count > 0)
        {
            return livres[Random.Range(0, livres.Count)];
        }

        return null; // Nenhum ponto livre no momento
    }

    private bool TentarDetectarParametroAnimator(MembroTripulacao membro)
    {
        if (membro.animator == null || membro.animator.runtimeAnimatorController == null)
            return false;

        string[] candidatos = new string[] {
            nomeParametroVelocidade,
            "Speed",
            "speed",
            "Walk",
            "walk",
            "Velocity",
            "velocity",
            "moveSpeed",
            "MoveSpeed",
            "Forward",
            "forward",
            "Blend"
        };

        foreach (AnimatorControllerParameter param in membro.animator.parameters)
        {
            if (param.type != UnityEngine.AnimatorControllerParameterType.Float)
                continue;

            foreach (string candidato in candidatos)
            {
                if (string.IsNullOrEmpty(candidato)) continue;
                if (string.Equals(param.name, candidato, System.StringComparison.Ordinal))
                {
                    membro.temParametroVelocidade = true;
                    membro.nomeParametroVelocidadeEncontrado = param.name;
                    return true;
                }
            }
        }

        return false;
    }

    private void AtualizarMovimentacaoTripulacao()
    {
        for (int i = 0; i < _tripulacao.Count; i++)
        {
            MembroTripulacao membro = _tripulacao[i];
            if (membro == null || membro.instancia == null) continue;

            if (membro.animator != null && !membro.temParametroVelocidade)
            {
                TentarDetectarParametroAnimator(membro);
            }

            if (membro.andando)
            {
                if (membro.pontoDestino == null)
                {
                    DefinirNovoDestino(membro);
                    continue;
                }

                // Calcula posições locais para evitar tremores e problemas de física com o navio em movimento
                Vector3 localTargetPos = _shipTransform.InverseTransformPoint(membro.pontoDestino.position);
                Vector3 currentLocalPos = membro.transform.localPosition;

                // Move em direção ao ponto
                membro.transform.localPosition = Vector3.MoveTowards(
                    currentLocalPos, 
                    localTargetPos, 
                    velocidadeMovimento * Time.deltaTime
                );

                // Rotaciona em direção ao ponto local
                Vector3 localDirection = localTargetPos - currentLocalPos;
                localDirection.y = 0f; // Mantém rotação horizontal estável no convés

                if (localDirection.sqrMagnitude > 0.001f)
                {
                    Quaternion targetLocalRot = Quaternion.LookRotation(localDirection, Vector3.up);
                    membro.transform.localRotation = Quaternion.Slerp(
                        membro.transform.localRotation, 
                        targetLocalRot, 
                        velocidadeRotacao * Time.deltaTime
                    );
                }

                // Verifica se chegou ao destino (margem de tolerância local de 10cm)
                if (Vector3.Distance(membro.transform.localPosition, localTargetPos) < 0.1f)
                {
                    // Chegou! Entra em estado de espera (Idle)
                    membro.andando = false;
                    membro.tempoEsperaRestante = Random.Range(tempoEsperaMin, tempoEsperaMax);

                    if (membro.animator != null && membro.temParametroVelocidade && _animatorAtivoLOD)
                    {
                        membro.animator.SetFloat(membro.nomeParametroVelocidadeEncontrado, 0.0f);
                    }
                }
            }
            else
            {
                // Contagem regressiva do tempo de espera
                membro.tempoEsperaRestante -= Time.deltaTime;
                if (membro.tempoEsperaRestante <= 0f)
                {
                    DefinirNovoDestino(membro);
                }
            }
        }
    }

    private void AtualizarLOD()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float distSqr = (transform.position - cam.transform.position).sqrMagnitude;
        float limiteDesativarMovSqr = distanciaLODDesativarMov * distanciaLODDesativarMov;
        float limiteDesativarAnimSqr = distanciaLODDesativarAnim * distanciaLODDesativarAnim;

        bool deveMover = distSqr < limiteDesativarMovSqr;
        bool deveAnimar = distSqr < limiteDesativarAnimSqr;

        // 1. Gerencia Movimentação e Renderers (LOD Distância Longa)
        if (deveMover != _movimentoAtivoLOD)
        {
            _movimentoAtivoLOD = deveMover;

            if (ocultarRenderersLOD)
            {
                for (int i = 0; i < _tripulacao.Count; i++)
                {
                    var membro = _tripulacao[i];
                    if (membro != null && membro.instancia != null)
                    {
                        var renderers = membro.instancia.GetComponentsInChildren<Renderer>();
                        for (int r = 0; r < renderers.Length; r++)
                        {
                            renderers[r].enabled = deveMover;
                        }
                    }
                }
            }
        }

        // 2. Gerencia Animator (LOD Distância Média)
        if (deveAnimar != _animatorAtivoLOD)
        {
            _animatorAtivoLOD = deveAnimar;
            for (int i = 0; i < _tripulacao.Count; i++)
            {
                var membro = _tripulacao[i];
                if (membro != null && membro.animator != null)
                {
                    membro.animator.enabled = deveAnimar;
                }
            }
        }
    }

    private void LimparTripulacao()
    {
        for (int i = 0; i < _tripulacao.Count; i++)
        {
            if (_tripulacao[i] != null && _tripulacao[i].instancia != null)
            {
                Destroy(_tripulacao[i].instancia);
            }
        }
        _tripulacao.Clear();
        _pontosOcupados.Clear();
    }

    private void OnDestroy()
    {
        LimparTripulacao();
    }

    private void LimparTripulacaoAnteriorEInstancia()
    {
        LimparTripulacao();
    }

    // Facilita visualização dos caminhos e pontos no Editor da Unity
    private void OnDrawGizmosSelected()
    {
        // 1. Renderiza pontos de trabalhadores em Azul
        if (pontosTrabalhador != null && pontosTrabalhador.Count > 0)
        {
            Gizmos.color = new Color(0.1f, 0.5f, 1.0f, 0.7f);
            for (int i = 0; i < pontosTrabalhador.Count; i++)
            {
                Transform pt = pontosTrabalhador[i];
                if (pt != null)
                {
                    Gizmos.DrawSphere(pt.position, 0.4f);
                    Gizmos.DrawWireSphere(pt.position, 0.6f);
                }
            }
        }

        // 2. Renderiza pontos de militares em Verde/Amarelo
        if (pontosMilitar != null && pontosMilitar.Count > 0)
        {
            Gizmos.color = new Color(0.1f, 1.0f, 0.2f, 0.7f);
            for (int i = 0; i < pontosMilitar.Count; i++)
            {
                Transform pt = pontosMilitar[i];
                if (pt != null)
                {
                    Gizmos.DrawSphere(pt.position, 0.4f);
                    Gizmos.DrawWireSphere(pt.position, 0.6f);
                }
            }
        }
    }

    private void PreprocessarWaypoints()
    {
        if (pontosTrabalhador == null) pontosTrabalhador = new List<Transform>();
        if (pontosTrabalhador.Count == 0)
        {
            Transform objTrabalho = transform.Find("Trabalho");
            if (objTrabalho != null)
            {
                pontosTrabalhador.Add(objTrabalho);
            }
        }

        ExpandirPontos(pontosTrabalhador);
        ExpandirPontos(pontosMilitar);
    }

    private void ExpandirPontos(List<Transform> lista)
    {
        if (lista == null) return;
        
        List<Transform> novosPontos = new List<Transform>();
        foreach (Transform t in lista)
        {
            if (t == null) continue;
            
            // Se o transform tem filhos (e não é o próprio navio ou o script principal), usamos os filhos
            if (t.childCount > 0 && t != this.transform)
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    Transform filho = t.GetChild(i);
                    if (filho != null && !novosPontos.Contains(filho))
                    {
                        novosPontos.Add(filho);
                    }
                }
            }
            else
            {
                if (!novosPontos.Contains(t))
                {
                    novosPontos.Add(t);
                }
            }
        }
        
        lista.Clear();
        lista.AddRange(novosPontos);
    }
}
