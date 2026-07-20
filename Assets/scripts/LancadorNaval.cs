using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;

public class LancadorNaval : MonoBehaviour
{
    public enum ModoOperacao { Passivo, Manual, Automatico }

    [Header("Configurações do Lançador")]
    public ModoOperacao modoAtual = ModoOperacao.Passivo;
    public Transform cabecaRotativa; // Parte que gira (se houver)
    public Transform[] pontosDeSaida; // Onde os mísseis nascem (bocas do VLS)
    public Transform[] pontosDeSaidaTorpedo; // Onde os torpedos nascem (tubos de torpedo)
    public GameObject prefabMissel; // O prefab que tem o script MisselNaval
    public GameObject prefabTorpedo; // NOVO: Prefab do torpedo/missil anti-navio
    [Tooltip("Se ativo, torpedos ficam reservados para o comando ATIVO do navio e nao saem pelo automatico da tecla I.")]
    public bool torpedosSomenteNoModoAtivo = false;

    [Header("Configurações de Combate")]
    public int municaoTotal = 32;
    public int municaoMaxima = 32; // Limite para recarga
    public int torpedosTotal = 8; // NOVO: limite para torpedos
    public int torpedosMaximos = 8;
    public int tirosPorSalva = 4; // Quantos mísseis saem de uma vez
    public float intervaloEntreTiros = 0.5f; // Tempo entre mísseis da mesma salva
    public float tempoRecargaSalva = 5.0f; // Tempo entre salvas
    public float alcanceRadar = 500f;
    public float intervaloVarreduraAutomatica = 0.45f;
    public int maxMisseisSimultaneosPorAlvo = 6;
    public float cooldownAutorizacaoAlvo = 1.4f;
    public AudioClip somDisparo;
    public bool preaquecerMisseisNoStart = false;

    [Header("Configurações de Áudio")]
    [Range(0f, 1f)] public float volumeSom = 1.0f;
    [Range(0.1f, 3f)] public float pitchSom = 1.0f;
    public float distanciaSomMinima = 3f;
    public float distanciaSomMaxima = 50f;

    [Header("Tags de Alvos")]
    public List<string> tagsInimigas = new List<string> { "Inimigo", "Destrutivel" };

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    
    // Estado interno
    private float tempoUltimoDisparo = 0f;
    private int indicePontoSaida = 0; // Para alternar entre as bocas do VLS
    private int indicePontoSaidaTorpedo = 0; // Para alternar entre tubos de torpedo
    private AudioSource audioSource;
    private Transform alvoAtual;

    // --- Identidade Própria ---
    private IdentidadeUnidade minhaIdentidade;
    private ControleUnidade meuControle;
    
    // Timer para atrasar a ativação do modo automático
    private float tempoParaAtivarAutomatico = 0f;

    // Visualizador de Alcance
    private LineRenderer linhaDeAlcance;
    private Camera cameraPrincipal;

    // --- BANCO DE DADOS GLOBAL DE COMBATE DA FROTA ---
    // Compartilhado estaticamente por TODOS os navios! Impede que 5 navios atirem num barco que já vai morrer.
    private static Dictionary<Transform, float> bancoDanoProjetadoFrotas = new Dictionary<Transform, float>();
    private static readonly Dictionary<Transform, float> expiracaoDanoProjetadoFrotas = new Dictionary<Transform, float>();
    private static readonly Dictionary<int, float> cooldownAutorizacaoPorAlvo = new Dictionary<int, float>();
    private static readonly HashSet<int> prefabsPreaquecidos = new HashSet<int>();
    private static readonly HashSet<int> prefabsEmPreaquecimento = new HashSet<int>();
    private static readonly Collider[] radarBuffer = new Collider[128];
    private static readonly List<IdentidadeUnidade> unidadesRegistradasRadar = new List<IdentidadeUnidade>(256);
    private static MiniMapa miniMapaCache;
    private static float proximaBuscaMiniMapa;
    private readonly List<Transform> bufferAlvosValidos = new List<Transform>(32);
    private float proximaVarreduraAutomatica = 0f;

    void Start()
    {
        cameraPrincipal = Camera.main;
        if (preaquecerMisseisNoStart)
        {
            StartCoroutine(PreaquecerMisseisSemTravada());
        }
        // Se Maxima não foi configurada ou menor que total inicial, ajusta
        if (municaoMaxima < municaoTotal) municaoMaxima = municaoTotal;

        audioSource = gameObject.AddComponent<AudioSource>();
        
        // Configurações iniciais do AudioSource para 3D
        audioSource.spatialBlend = 1.0f; // Torna o som 3D
        audioSource.minDistance = distanciaSomMinima;
        audioSource.maxDistance = distanciaSomMaxima;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.playOnAwake = false;

        minhaIdentidade = GetComponentInParent<IdentidadeUnidade>();
        if (minhaIdentidade == null) minhaIdentidade = GetComponent<IdentidadeUnidade>();
        if (minhaIdentidade == null) minhaIdentidade = GetComponentInChildren<IdentidadeUnidade>(true);

        // Cache do ControleUnidade para saber se estou selecionado
        meuControle = GetComponent<ControleUnidade>();
        if (meuControle == null) meuControle = GetComponentInParent<ControleUnidade>();

        CriarVisualizadorAlcance();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        MissilePrefabAutoBinder.BindLancadorNaval(this);
    }

    [ContextMenu("Auto configurar misseis")]
    private void AutoConfigurarMisseisEditor()
    {
        MissilePrefabAutoBinder.BindLancadorNaval(this, true);
    }
#endif

    IEnumerator PreaquecerMisseisSemTravada()
    {
        if (prefabMissel == null)
        {
            yield break;
        }

        int prefabId = prefabMissel.GetInstanceID();
        int quantidade = Mathf.Clamp(tirosPorSalva + 2, 4, 8);
        if (prefabsPreaquecidos.Contains(prefabId)
            || prefabsEmPreaquecimento.Contains(prefabId)
            || PoolDeObjetosCombate.ObterQuantidadePreaquecida(prefabMissel) >= quantidade)
        {
            yield break;
        }

        prefabsEmPreaquecimento.Add(prefabId);
        DiagnosticoDesempenhoJogo.RegistrarEvento("Pool", "Prewarm naval agendado: " + prefabMissel.name);
        yield return new WaitForSeconds(1.5f);
        yield return PoolDeObjetosCombate.PrewarmIncremental(prefabMissel, quantidade, 1);
        prefabsEmPreaquecimento.Remove(prefabId);
        prefabsPreaquecidos.Add(prefabId);
        DiagnosticoDesempenhoJogo.RegistrarEvento("Pool", "Prewarm naval concluido: " + prefabMissel.name);
    }

    void CriarVisualizadorAlcance()
    {
        GameObject obj = new GameObject("Alcance_MissilNaval_UI");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;
        linhaDeAlcance = obj.AddComponent<LineRenderer>();
        linhaDeAlcance.useWorldSpace = true;
        
        Material mat = new Material(Shader.Find("Sprites/Default")); 
        Color corVermelha = Color.red; corVermelha.a = 0.5f; 
        linhaDeAlcance.material = mat;
        linhaDeAlcance.startColor = corVermelha; linhaDeAlcance.endColor = corVermelha;
        linhaDeAlcance.startWidth = 2.0f; linhaDeAlcance.endWidth = 2.0f;
        linhaDeAlcance.positionCount = 51;
        linhaDeAlcance.enabled = false;
    }

    void AtualizarVisualizadorAlcance()
    {
        if (linhaDeAlcance == null) return;
        
        bool deveMostrar = (meuControle != null && meuControle.selecionado);
        linhaDeAlcance.enabled = deveMostrar;
        
        if (deveMostrar)
        {
            float angulo = 0f;
            for (int i = 0; i <= 50; i++)
            {
                float x = Mathf.Sin(angulo) * alcanceRadar;
                float z = Mathf.Cos(angulo) * alcanceRadar;
                // Mantemos Y estático no WorldSpace para a linha não rotacionar com o balanço do barco
                Vector3 pos = new Vector3(transform.position.x + x, transform.position.y, transform.position.z + z);
                linhaDeAlcance.SetPosition(i, pos);
                angulo += (2 * Mathf.PI) / 50;
            }
        }
    }

    // --- SISTEMA DE RECARGA (USADO PELO PIER) ---
    public void Recarregar(int quantidade)
    {
        municaoTotal = Mathf.Min(municaoTotal + quantidade, municaoMaxima);
        torpedosTotal = Mathf.Min(torpedosTotal + (quantidade / 4), torpedosMaximos);
    }

    public void DefinirModoIA(ModoOperacao novoModo, bool usarDelay = true)
    {
        if (modoAtual == novoModo) return;

        if (novoModo == ModoOperacao.Automatico)
        {
            tempoParaAtivarAutomatico = usarDelay ? Time.time + 1.5f : Time.time;
        }
        else
        {
            tempoParaAtivarAutomatico = 0f;
        }

        modoAtual = novoModo;
    }

    void Update()
    {
        if (cameraPrincipal == null) cameraPrincipal = Camera.main;
        AtualizarVisualizadorAlcance();

        // 1. Controle de Modos (Tecla 'I')
        ChecarTrocaDeModo();

        // 2. Comportamento baseado no modo
        switch (modoAtual)
        {
            case ModoOperacao.Manual:
                ComportamentoManual();
                break;
            case ModoOperacao.Automatico:
                // Aguarda 3 segundos antes de iniciar o disparo (Safety Delay)
                if (Time.time >= tempoParaAtivarAutomatico)
                {
                    ComportamentoAutomatico();
                }
                break;
            case ModoOperacao.Passivo:
                // Não faz nada, descansa soldado
                break;
        }
    }

    void ChecarTrocaDeModo()
    {
        // VERIFICAÇÃO CRÍTICA: Só permite ação se ESTIVER SELECIONADO
        if (meuControle == null || !meuControle.selecionado) return;

        if (Input.GetKeyDown(KeyCode.I))
        {
            // Avança para o próximo modo na lista (ciclo: 0 -> 1 -> 2 -> 0...)
            int proximo = (int)modoAtual + 1;
            if (proximo > 2) proximo = 0;
            
            ModoOperacao novoModo = (ModoOperacao)proximo;
            
            // Se for entrar em Automático, define o delay de 3 segundos
            if (novoModo == ModoOperacao.Automatico)
            {
                tempoParaAtivarAutomatico = Time.time + 3.0f;
            }
            
            modoAtual = novoModo;

            if (debugLogs)
            {
                Debug.Log($"<color=cyan>[LANÇADOR]</color> Modo alterado para: {modoAtual}");
            }
        }
    }

    // --- MODO MANUAL (Mouse Direito) ---
    void ComportamentoManual()
    {
        // Só permite atirar manualmente se ESTIVER SELECIONADO
        if (meuControle == null || !meuControle.selecionado) return;

        // Se clicar com botão direito
        if (Input.GetMouseButtonDown(1))
        {
            if (cameraPrincipal == null) return;
            Ray raio = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Raio atinge o chão?
            if (Physics.Raycast(raio, out hit, 1000f))
            {
                // Verifica se tem munição e se o tempo de recarga passou
                if (PodeAtirar())
                {
                    if (debugLogs)
                    {
                        Debug.Log($"[MANUAL] Disparando em coordenadas: {hit.point}");
                    }
                    // Cria uma lista falsa com 1 posição nula para indicar disparo no chão
                    StartCoroutine(DispararSalvaManual(hit.point));
                }
            }
        }
    }

    IEnumerator DispararSalvaManual(Vector3 ponto)
    {
        tempoUltimoDisparo = Time.time;
        int misseisDisponiveisNaSalva = Mathf.Min(tirosPorSalva, municaoTotal);
        
        for (int i = 0; i < misseisDisponiveisNaSalva; i++)
        {
            DispararUnico(ponto, null);
            yield return new WaitForSeconds(intervaloEntreTiros);
        }
    }

    // --- MODO AUTOMÁTICO (Radar Inteligente) ---
    void ComportamentoAutomatico()
    {
        if (!PodeAtirar() || Time.time < proximaVarreduraAutomatica) return;
        proximaVarreduraAutomatica = Time.time + Mathf.Max(0.20f, intervaloVarreduraAutomatica);

        // 1. Escaneia a área em busca de TODOS os alvos válidos
        List<Transform> alvosValidos = BuscarTodosInimigos();

        if (alvosValidos.Count > 0)
        {
            // 2. Calcula distribuição de mísseis
            StartCoroutine(DispararSalvaInteligente(alvosValidos));
        }
    }

    bool PodeAtirar()
    {
        return Time.time > tempoUltimoDisparo + tempoRecargaSalva && (municaoTotal > 0 || torpedosTotal > 0);
    }

    Transform ResolverTransformAlvo(Transform alvo)
    {
        if (alvo == null) return null;

        SistemaDeDanos vida = alvo.GetComponentInParent<SistemaDeDanos>();
        if (vida == null) vida = alvo.GetComponentInChildren<SistemaDeDanos>();
        if (vida != null) return vida.transform;

        ControleAviao aviao = alvo.GetComponentInParent<ControleAviao>();
        if (aviao == null) aviao = alvo.GetComponentInChildren<ControleAviao>();
        if (aviao != null) return aviao.transform;

        Helicoptero helicoptero = alvo.GetComponentInParent<Helicoptero>();
        if (helicoptero == null) helicoptero = alvo.GetComponentInChildren<Helicoptero>();
        if (helicoptero != null) return helicoptero.transform;

        IdentidadeUnidade identidade = alvo.GetComponentInParent<IdentidadeUnidade>();
        if (identidade == null) identidade = alvo.GetComponentInChildren<IdentidadeUnidade>();
        if (identidade != null) return identidade.transform;

        return alvo.root != null ? alvo.root : alvo;
    }

    bool EhAlvoAereo(Transform alvo, IdentidadeUnidade identidade)
    {
        if (alvo == null) return false;

        string nomeAlvo = alvo.name.ToLowerInvariant();

        return alvo.position.y > 8f
               || alvo.GetComponentInParent<ControleAviao>() != null
               || alvo.GetComponentInParent<ControleAviaoCaca>() != null
               || alvo.GetComponentInParent<AviaoBombardeiro>() != null
               || alvo.GetComponentInParent<Helicoptero>() != null
               || (identidade != null && identidade.tipoUnidade == TipoUnidade.Aereo)
               || nomeAlvo.Contains("aviao")
               || nomeAlvo.Contains("heli")
               || nomeAlvo.Contains("caca")
               || nomeAlvo.Contains("jato")
               || nomeAlvo.Contains("drone")
               || nomeAlvo.Contains("bombard")
               || nomeAlvo.Contains("bombardeiro")
               || nomeAlvo.Contains("bomber")
               || nomeAlvo.Contains("b260")
               || TagSafe.Matches(alvo, "Areo")
               || TagSafe.Matches(alvo, "Aereo");
    }

    bool EhBombardeiro(Transform alvo)
    {
        if (alvo == null) return false;

        string nomeAlvo = alvo.name.ToLowerInvariant();
        return alvo.GetComponentInParent<AviaoBombardeiro>() != null
               || nomeAlvo.Contains("bombard")
               || nomeAlvo.Contains("bombardeiro")
               || nomeAlvo.Contains("bomber")
               || nomeAlvo.Contains("b52")
               || nomeAlvo.Contains("b260")
               || nomeAlvo.Contains("b2");
    }

    int ObterPrioridadeAlvo(Transform alvo)
    {
        IdentidadeUnidade identidade = alvo != null ? alvo.GetComponentInParent<IdentidadeUnidade>() : null;
        if (EhBombardeiro(alvo)) return 0;
        if (EhAlvoAereo(alvo, identidade)) return 1;
        return 2;
    }

    bool TentarRegistrarAlvoDetectado(Transform candidato, int meuTime)
    {
        if (candidato == null)
        {
            return false;
        }

        Transform minhaRaiz = transform.root != null ? transform.root : transform;
        if (candidato == minhaRaiz || candidato.IsChildOf(minhaRaiz))
        {
            return false;
        }

        IdentidadeUnidade idAlvo = candidato.GetComponentInParent<IdentidadeUnidade>();
        if (idAlvo == null) idAlvo = candidato.GetComponentInChildren<IdentidadeUnidade>();
        if (idAlvo == null || idAlvo.teamID == 0 || idAlvo.teamID == meuTime)
        {
            return false;
        }

        Transform alvoResolvido = ResolverTransformAlvo(idAlvo.transform);
        if (alvoResolvido == null || alvoResolvido == minhaRaiz || alvoResolvido.IsChildOf(minhaRaiz))
        {
            return false;
        }

        SistemaDeDanos vida = alvoResolvido.GetComponentInParent<SistemaDeDanos>();
        if (vida == null) vida = alvoResolvido.GetComponentInChildren<SistemaDeDanos>();
        if (vida == null || vida.vidaAtual <= 0f)
        {
            return false;
        }

        Transform alvoFinal = ResolverTransformAlvo(vida.transform);
        if (alvoFinal == null || bufferAlvosValidos.Contains(alvoFinal))
        {
            return false;
        }

        bufferAlvosValidos.Add(alvoFinal);
        RegistrarAlvoNoMiniMapa(alvoFinal);
        return true;
    }

    void RegistrarAlvosDoRegistroGlobal(int meuTime)
    {
        RegistroEntidadesJogo.FillUnidades(unidadesRegistradasRadar);
        float alcanceSqr = alcanceRadar * alcanceRadar;

        for (int i = 0; i < unidadesRegistradasRadar.Count; i++)
        {
            IdentidadeUnidade unidade = unidadesRegistradasRadar[i];
            if (unidade == null || !unidade.gameObject.activeInHierarchy)
            {
                continue;
            }

            if ((unidade.transform.position - transform.position).sqrMagnitude > alcanceSqr)
            {
                continue;
            }

            TentarRegistrarAlvoDetectado(unidade.transform, meuTime);
        }

        unidadesRegistradasRadar.Clear();
    }

    void RegistrarAlvoNoMiniMapa(Transform alvo)
    {
        MiniMapa miniMapa = ObterMiniMapa();
        if (miniMapa != null && miniMapa.mostrarInimigos)
        {
            miniMapa.RegistrarUnidadeNoMapa(alvo, true);
        }
    }

    static MiniMapa ObterMiniMapa()
    {
        if (miniMapaCache != null)
        {
            return miniMapaCache;
        }

        if (Time.time < proximaBuscaMiniMapa)
        {
            return null;
        }

        proximaBuscaMiniMapa = Time.time + 1f;
        miniMapaCache = UnityEngine.Object.FindFirstObjectByType<MiniMapa>();
        return miniMapaCache;
    }
    
    // Retorna lista de inimigos ordenados por proximidade
    List<Transform> BuscarTodosInimigos()
    {
        LimparDanoProjetadoExpirado();
        LimparCooldownAutorizacaoExpirado();
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, alcanceRadar, radarBuffer, Physics.AllLayers, QueryTriggerInteraction.Collide);
        int meuTime = (minhaIdentidade != null) ? minhaIdentidade.teamID : 1; 

        bufferAlvosValidos.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = radarBuffer[i];
            if (hit == null)
            {
                continue;
            }

            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            TentarRegistrarAlvoDetectado(hit.transform, meuTime);
        }

        RegistrarAlvosDoRegistroGlobal(meuTime);
        
        // Prioriza bombardeiros, depois demais alvos aéreos, e por fim proximidade.
        bufferAlvosValidos.Sort((a, b) =>
        {
            int prioridade = ObterPrioridadeAlvo(a).CompareTo(ObterPrioridadeAlvo(b));
            if (prioridade != 0) return prioridade;

            float distanciaA = (a.position - transform.position).sqrMagnitude;
            float distanciaB = (b.position - transform.position).sqrMagnitude;
            return distanciaA.CompareTo(distanciaB);
        });
        
        for (int i = 0; i < hitCount; i++)
        {
            radarBuffer[i] = null;
        }

        return bufferAlvosValidos;
    }

    void LimparDanoProjetadoExpirado()
    {
        if (expiracaoDanoProjetadoFrotas.Count == 0)
        {
            return;
        }

        List<Transform> expirados = null;
        foreach (var entry in expiracaoDanoProjetadoFrotas)
        {
            if (entry.Key == null || entry.Value <= Time.time)
            {
                if (expirados == null)
                {
                    expirados = new List<Transform>();
                }

                expirados.Add(entry.Key);
            }
        }

        if (expirados == null)
        {
            return;
        }

        for (int i = 0; i < expirados.Count; i++)
        {
            Transform alvo = expirados[i];
            bancoDanoProjetadoFrotas.Remove(alvo);
            expiracaoDanoProjetadoFrotas.Remove(alvo);
        }
    }

    void RegistrarDanoProjetado(Transform alvo, float dano)
    {
        if (alvo == null) return;
        if (!bancoDanoProjetadoFrotas.ContainsKey(alvo)) bancoDanoProjetadoFrotas[alvo] = 0f;
        
        bancoDanoProjetadoFrotas[alvo] += dano;
        
        // O míssil expira da conta após 15 segundos se não acertar (Segurança)
        expiracaoDanoProjetadoFrotas[alvo] = Time.time + 15f;
    }

    void LimparCooldownAutorizacaoExpirado()
    {
        if (cooldownAutorizacaoPorAlvo.Count == 0)
        {
            return;
        }

        List<int> expirados = null;
        foreach (var entry in cooldownAutorizacaoPorAlvo)
        {
            if (entry.Value <= Time.time)
            {
                if (expirados == null)
                {
                    expirados = new List<int>();
                }

                expirados.Add(entry.Key);
            }
        }

        if (expirados == null)
        {
            return;
        }

        for (int i = 0; i < expirados.Count; i++)
        {
            cooldownAutorizacaoPorAlvo.Remove(expirados[i]);
        }
    }

    IEnumerator DispararSalvaInteligente(List<Transform> alvos)
    {
        tempoUltimoDisparo = Time.time;
        int misseisDisponiveisNaSalva = Mathf.Min(tirosPorSalva, municaoTotal);
        
        // Simulação de dano do Míssil (seguro contra nulos se for um lançador apenas de torpedo)
        float danoMissel = 200f; 
        if (prefabMissel != null)
        {
            MisselNaval refMissel = prefabMissel.GetComponent<MisselNaval>();
            if (refMissel != null) danoMissel = refMissel.dano;
        }

        for (int i = 0; i < misseisDisponiveisNaSalva; i++)
        {
            Transform alvoDaVez = null;

            // PROCURA O MELHOR ALVO: Um que esteja vivo e não tenha mísseis suficientes indo para matá-lo
            for (int j = 0; j < alvos.Count; j++)
            {
                Transform potencialAlvo = alvos[j];
                if (potencialAlvo == null) continue;

                SistemaDeDanos vidaScript = potencialAlvo.GetComponent<SistemaDeDanos>();
                if (vidaScript == null) vidaScript = potencialAlvo.GetComponentInParent<SistemaDeDanos>();

                if (vidaScript != null && vidaScript.vidaAtual > 0)
                {
                    float danoFuturo = 0f;
                    // Consulta a Rede Global de Combate (Todos os navios amigos informam aqui)
                    if (bancoDanoProjetadoFrotas.ContainsKey(potencialAlvo)) 
                    {
                        danoFuturo = bancoDanoProjetadoFrotas[potencialAlvo];
                    }

                    int alvoId = potencialAlvo.GetInstanceID();
                    float cooldownAte;
                    if (cooldownAutorizacaoPorAlvo.TryGetValue(alvoId, out cooldownAte) && cooldownAte > Time.time)
                    {
                        continue;
                    }

                    int misseisEstimadosNoAlvo = Mathf.CeilToInt(danoFuturo / Mathf.Max(1f, danoMissel));
                    if (misseisEstimadosNoAlvo >= Mathf.Max(1, maxMisseisSimultaneosPorAlvo))
                    {
                        continue;
                    }

                    // Verifica: A vida real dele é MAIOR que os mísseis que já estão voando pra cabeça dele?
                    if (vidaScript.vidaAtual > danoFuturo)
                    {
                        // Bingo! Achamos um alvo precisando de mais porrada.
                        alvoDaVez = potencialAlvo;
                        
                        // Agenda o dano na nuvem militar para outros não focarem atoa
                        RegistrarDanoProjetado(potencialAlvo, danoMissel);
                        cooldownAutorizacaoPorAlvo[alvoId] = Time.time + Mathf.Max(0.1f, cooldownAutorizacaoAlvo);
                        break;
                    }
                }
            }

            // Se varremos TUDO e não tem mais alvo sobrando, ou todo mundo já está "Virtualmente morto"
            if (alvoDaVez == null)
            {
                if (debugLogs)
                {
                    Debug.Log($"<color=green>[LANÇADOR]</color> Inimigos já possuem mísseis letais a caminho. Parando a salva para economizar munição.");
                }
                break; // Cancela o resto da salva!
            }

            // Mira visual da torre para o alvo que vai atirar
            if (cabecaRotativa != null)
            {
                // Respeita a inclinação realista do navio sobre as ondas!
                Vector3 direcao = alvoDaVez.position - cabecaRotativa.position;
                // O eixo vertical do pai pode carregar roll/pitch do modelo
                // importado e fazer a torreta "deitar" ao mirar. Canhoes
                // navais mantem o plano de tiro nivelado com o mundo; o
                // balanço visual do casco nao deve alterar o eixo de yaw.
                Vector3 upDoNavio = Vector3.up;
                
                // Projeta o alvo no "chão" do navio para a torre não focar pra cima/baixo torta
                Vector3 direcaoNoConves = Vector3.ProjectOnPlane(direcao, upDoNavio).normalized;

                if (direcaoNoConves != Vector3.zero)
                {
                    cabecaRotativa.rotation = Quaternion.LookRotation(direcaoNoConves, upDoNavio);
                }
            }

            // Dispara e vai diminuir munição original no método
            DispararUnico(alvoDaVez.position, alvoDaVez);
            
            yield return new WaitForSeconds(intervaloEntreTiros);
        }
    }

    void DispararUnico(Vector3 destino, Transform alvoFixo)
    {
        // Vai checar limites depois de decidir qual disparar


        if (prefabMissel == null && prefabTorpedo == null) return; // Segurança

        bool alvoNavalOuSubmarino = false;
        if (alvoFixo != null)
        {
            alvoNavalOuSubmarino = alvoFixo.GetComponentInParent<ControleNavioRealista>() != null || 
                                   alvoFixo.GetComponentInChildren<ControleNavioRealista>() != null ||
                                   alvoFixo.GetComponentInParent<ControleSubmarino>() != null || 
                                   alvoFixo.GetComponentInChildren<ControleSubmarino>() != null ||
                                   TagSafe.Matches(alvoFixo, "Navio") || TagSafe.Matches(alvoFixo, "Submarino");
        }

        GameObject prefabASpawnar = prefabMissel;
        bool podeUsarTorpedoNesteLancador = !torpedosSomenteNoModoAtivo;
        // No automatico, use primeiro o missil guiado. O torpedo fica como
        // fallback quando a carga de misseis acabou; assim navios de combate
        // realmente engajam alvos navais com a arma esperada.
        if (alvoNavalOuSubmarino && prefabMissel != null && municaoTotal > 0)
        {
            prefabASpawnar = prefabMissel;
        }
        else if (alvoNavalOuSubmarino && podeUsarTorpedoNesteLancador && prefabTorpedo != null && torpedosTotal > 0)
        {
            prefabASpawnar = prefabTorpedo;
        }
        else if (prefabASpawnar == null && podeUsarTorpedoNesteLancador && torpedosTotal > 0)
        {
            prefabASpawnar = prefabTorpedo; // Fallback se não houver missil mas tiver torpedo
        }
        else if (municaoTotal <= 0)
        {
            // Se nao decidiu usar torpedo e ta sem missil normal
            if (podeUsarTorpedoNesteLancador && torpedosTotal > 0 && prefabTorpedo != null) prefabASpawnar = prefabTorpedo;
            else return; // Sem municao disponivel
        }

        if (prefabASpawnar == null) return;
        
        if (prefabASpawnar == prefabTorpedo) torpedosTotal--;
        else municaoTotal--;

        // Pega o próximo ponto de saída correto (rodízio entre os tubos)
        Transform pontoDeSaida = transform; // Fallback
        if (prefabASpawnar == prefabTorpedo && pontosDeSaidaTorpedo != null && pontosDeSaidaTorpedo.Length > 0)
        {
            if (pontosDeSaidaTorpedo[indicePontoSaidaTorpedo] != null)
            {
                pontoDeSaida = pontosDeSaidaTorpedo[indicePontoSaidaTorpedo];
            }
            indicePontoSaidaTorpedo = (indicePontoSaidaTorpedo + 1) % pontosDeSaidaTorpedo.Length;
        }
        else if (pontosDeSaida != null && pontosDeSaida.Length > 0)
        {
            if (pontosDeSaida[indicePontoSaida] != null)
            {
                pontoDeSaida = pontosDeSaida[indicePontoSaida];
            }
            indicePontoSaida = (indicePontoSaida + 1) % pontosDeSaida.Length;
        }

        // Cria o projétil (míssil ou torpedo)
        GameObject misselObj = PoolDeObjetosCombate.Spawn(prefabASpawnar, pontoDeSaida.position, pontoDeSaida.rotation);
        
        // Se tivermos um alvo fixo (Auto), atualizamos a posição, senão vai no chão (Manual)
        Vector3 alvoFinal = alvoFixo != null ? alvoFixo.position : destino;
        
        // Configura o míssil ou torpedo
        MisselNaval scriptMissel = misselObj.GetComponent<MisselNaval>();
        if (scriptMissel != null)
        {
            scriptMissel.IniciarAtaque(alvoFinal, alvoFixo, transform);
            MissileThreatTracker.RegistrarLancamento(misselObj, this, alvoFinal, alvoFixo, MissileThreatTracker.EstimarVelocidade(misselObj));
        }
        else
        {
            Torpedo scriptTorpedo = misselObj.GetComponent<Torpedo>();
            if (scriptTorpedo != null)
            {
                scriptTorpedo.DefinirAlvo(alvoFixo);
                int meuTime = minhaIdentidade != null ? minhaIdentidade.teamID : -1;
                scriptTorpedo.DefinirLancador(transform, meuTime);
                MissileThreatTracker.RegistrarLancamento(misselObj, this, alvoFinal, alvoFixo, MissileThreatTracker.EstimarVelocidade(misselObj));
            }
        }

        // Som
        if (somDisparo != null && audioSource != null)
        {
            // Aplica configurações de volume e pitch antes de tocar
            audioSource.volume = volumeSom;
            audioSource.pitch = pitchSom;
            audioSource.PlayOneShot(somDisparo);
        }
    }
    
    // Desenha o raio do radar no editor para facilitar ajuste
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alcanceRadar);
    }

    void OnGUI()
    {
        CombustivelUnidade indicadorCompartilhado = GetComponent<CombustivelUnidade>();
        if (indicadorCompartilhado == null) indicadorCompartilhado = GetComponentInParent<CombustivelUnidade>();
        if (indicadorCompartilhado != null && indicadorCompartilhado.mostrarIndicadorMundo)
        {
            return;
        }

        // 1. Só mostra se estiver selecionado!
        if (meuControle == null || !meuControle.selecionado) return;
        
        if (MenuConstrucao.EstaAberto || MenuPier.EstaAberto) return;

        if (Camera.main == null) return;

        // Pega a posição do lançador na tela
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);

        // Só desenha se estiver na frente da câmera
        if (screenPos.z > 0)
        {
            // Ajusta eixo Y (Unity GUI é invertido em relação a coordenadas de tela)
            float y = Screen.height - screenPos.y;
            
            // Define estilo
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperCenter;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 14;

            int misseisLancados = municaoMaxima - municaoTotal;
            int torpsLancados = torpedosMaximos - torpedosTotal;
            
            string texto = "";
            string textoBase = $"\nMísseis (Longo Alcance): {municaoTotal}/{municaoMaxima} (Lançados: {misseisLancados})\nTorpedos: {torpedosTotal}/{torpedosMaximos} (Lançados: {torpsLancados})";

            // Define cor baseada no modo
            switch (modoAtual)
            {
                case ModoOperacao.Passivo: 
                    style.normal.textColor = Color.gray; 
                    texto = $"[{modoAtual}]" + textoBase;
                    break;
                case ModoOperacao.Manual: 
                    style.normal.textColor = Color.yellow; 
                    texto = $"[{modoAtual}]" + textoBase;
                    break;
                case ModoOperacao.Automatico: 
                    style.normal.textColor = Color.red;
                    // Se estiver no delay de armar
                    if (Time.time < tempoParaAtivarAutomatico)
                    {
                        float restante = tempoParaAtivarAutomatico - Time.time;
                        texto = $"[ARMANDO {restante:F1}s]" + textoBase;
                        style.normal.textColor = Color.Lerp(Color.yellow, Color.red, Mathf.PingPong(Time.time * 5f, 1f));
                    }
                    else
                    {
                        texto = $"[{modoAtual}]" + textoBase;
                    }
                    break;
            }

            // Cria mensagem final
            
            // Desenha sombra (hack simples)
            GUI.color = Color.black;
            GUI.Label(new Rect(screenPos.x - 51, y - 61, 100, 50), texto, style);
            
            // Desenha texto
            GUI.color = Color.white; // Reseta cor
            GUI.Label(new Rect(screenPos.x - 50, y - 60, 100, 50), texto, style);
        }
    }
}
