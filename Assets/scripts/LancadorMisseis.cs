using UnityEngine;
using System.Collections;

[DisallowMultipleComponent] // Evita que você coloque dois scripts iguais no mesmo objeto sem querer
public class LancadorMisseis : MonoBehaviour
{
    [Header("Configuração de Munição")]
    public int municaoAtual = 4;    // Começa cheio (igual ao máximo)
    public int municaoMaxima = 4;
    public int custoMissil = 200;

    [Header("Configuração de Lançamento")]
    public float alcanceMaximo = 500f;
    public float tempoRecarga = 2.0f;
    public Transform[] pontosDeSaida; // Canos de saída
    public GameObject missilPrefab;   // Prefab do MisselICBM

    [Header("Visual")]
    public Transform cabecaRotativa; // Parte que gira (opcional)

    [Header("Mira")]
    [Tooltip("Altura Y do plano de mira quando não há colisão sólida (ex: nível do mar)")]
    public float alturaPlanoMira = 0f; // Nível do mar = Y 0

    // Estado Interno
    private bool menuAberto = false;
    private bool mirando = false;
    private bool posicaoValida = false;   // TRUE só quando o fantasma está sobre algo
    private GameObject marcadorFantasma; // O círculo vermelho
    private float cronometroRecarga = 0f;
    private int indiceCano = 0;
    private GerenteDeJogo gerente;

    // --- CONTROLE ESTÁTICO PARA EVITAR MENUS DUPLICADOS ---
    private static LancadorMisseis menuAtivo; // Guarda quem está com o menu aberto agora
    private static float ultimoTempoInput = 0f; // Para evitar que dois scripts processem o 'L' no mesmo frame

    void Start()
    {
        gerente = FindFirstObjectByType<GerenteDeJogo>();
        CriarMarcadorFantasma();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        MissilePrefabAutoBinder.BindLancadorMisseis(this);
    }

    [ContextMenu("Auto configurar missil")]
    private void AutoConfigurarMissilEditor()
    {
        MissilePrefabAutoBinder.BindLancadorMisseis(this, true);
    }
#endif

    void Update()
    {
        cronometroRecarga -= Time.deltaTime;

        // TECLA L: Abre/Fecha o Menu do Lançador
        // Verifica se tempo já passou para evitar duplo processamento se houver múltiplos scripts na cena
        if (Input.GetKeyDown(KeyCode.L) && !mirando)
        {
            if (Time.time != ultimoTempoInput)
            {
                ultimoTempoInput = Time.time;
                GerenciarInputMenu();
            }
        }

        // LÓGICA DE MIRA (Só funciona se estiver no modo mirado DESTE script)
        if (mirando)
        {
            AtualizarPosicaoFantasma();

            // Clique ESQUERDO: Lança
            if (Input.GetMouseButtonDown(0))
            {
                if (!posicaoValida)
                {
                    Debug.LogWarning("[Lançador] Posicione o cursor no mapa antes de disparar.");
                }
                else if (cronometroRecarga <= 0)
                {
                    Disparar(marcadorFantasma.transform.position);

                    // Finaliza mira
                    mirando = false;
                    posicaoValida = false;
                    marcadorFantasma.SetActive(false);
                    FecharMenu();
                }
                else
                {
                    Debug.Log($"[Lançador] Recarregando... aguarde {cronometroRecarga:F1}s.");
                }
            }

            // Clique DIREITO: Cancela
            if (Input.GetMouseButtonDown(1))
            {
                CancelarMira();
            }
        }
    }

    // Gerencia quem abre/fecha quando aperta L
    void GerenciarInputMenu()
    {
        if (menuAberto)
        {
            // Se EU estou aberto, eu fecho.
            FecharMenu();
        }
        else
        {
            // Se eu estou fechado, quero abrir.
            // Mas primeiro, se tem OUTRO aberto, manda fechar.
            if (menuAtivo != null && menuAtivo != this)
            {
                menuAtivo.FecharMenu();
            }
            
            // Agora abro o meu
            AbrirMenu();
        }
    }

    void AbrirMenu()
    {
        menuAberto = true;
        menuAtivo = this;
        Debug.Log("[Lançador] Menu Aberto: " + gameObject.name);
    }

    void FecharMenu()
    {
        menuAberto = false;
        if (menuAtivo == this) menuAtivo = null;
    }

    void CancelarMira()
    {
        mirando = false;
        marcadorFantasma.SetActive(false);
        // Ao cancelar, reabre o menu deste lançador
        AbrirMenu();
        Debug.Log("[Lançador] Mira cancelada.");
    }

    // --- LÓGICA DO MENU (OnGUI Simples e Rápido) ---
    void OnGUI()
    {
        // Só desenha se estiver marcado como aberto
        if (!menuAberto) return;

        // Caixa do Menu no centro da tela
        float largura = 250;
        float altura = 180;
        float x = (Screen.width - largura) / 2;
        float y = (Screen.height - altura) / 2;

        GUI.Box(new Rect(x, y, largura, altura), "🎮 CONTROLE DE MÍSSEIS");

        // Info Munição
        GUI.Label(new Rect(x + 20, y + 30, 200, 20), $"Mísseis Prontos: {municaoAtual} / {municaoMaxima}");

        // Info Dinheiro (usando o novo sistema)
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if(recursos != null)
             GUI.Label(new Rect(x + 20, y + 50, 200, 20), $"Dinheiro: ${recursos.dinheiro}");

        // BOTÃO: COMPRAR
        if (GUI.Button(new Rect(x + 25, y + 80, 200, 30), $"Comprar Míssil (${custoMissil})"))
        {
            ComprarMissil();
        }

        // BOTÃO: MIRAR E ATIRAR
        if (municaoAtual > 0)
        {
            if (GUI.Button(new Rect(x + 25, y + 120, 200, 40), "🎯 MIRAR NO MAPA"))
            {
                AtivarMira();
            }
        }
        else
        {
            GUI.Label(new Rect(x + 50, y + 130, 200, 20), "Sem mísseis! Compre antes.");
        }
    }

    // --- AÇÕES ---

    void ComprarMissil()
    {
        if (municaoAtual >= municaoMaxima)
        {
            Debug.Log("[Lançador] Silo cheio!");
            return;
        }

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos != null)
        {
            if (recursos.TentarGastar(custoDinheiro: custoMissil))
            {
                municaoAtual++;
                Debug.Log($"[Lançador] 🚀 Míssil comprado! Restam ${recursos.dinheiro}");
            }
            else
            {
                Debug.Log("[Lançador] ❌ Sem dinheiro para comprar míssil!");
            }
        }
        else
        {
            // Se não tiver GerenciadorRecursos (teste), dá o míssil de graça
            municaoAtual++;
            Debug.Log("[Lançador] ⚠️ Modo Teste: Míssil adicionado (Grátis - GerenciadorRecursos não encontrado)");
        }
    }

    void AtivarMira()
    {
        FecharMenu(); // Garante que fecha o menu visualmente
        mirando = true;
        marcadorFantasma.SetActive(true);
        Debug.Log("[Lançador] Modo Mira Ativo: Clique no mapa para lançar!");
    }

    void AtualizarPosicaoFantasma()
    {
        Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit toque;
        Vector3 pontoMira;

        // Tenta bater em qualquer collider sólido (inclusive chão e terreno)
        // Ignora apenas IgnoreRaycast (layer 2)
        int mascara = ~(1 << 2);
        if (Physics.Raycast(raio, out toque, 5000f, mascara))
        {
            pontoMira = toque.point;
            posicaoValida = true;
        }
        else
        {
            // FALLBACK: Usa um plano horizontal na altura do nível do mar
            // Isso permite mirar sobre a água mesmo que ela não tenha collider sólido
            UnityEngine.Plane planoMar = new UnityEngine.Plane(Vector3.up, new Vector3(0, alturaPlanoMira, 0));
            float distancia;
            if (planoMar.Raycast(raio, out distancia))
            {
                pontoMira = raio.GetPoint(distancia);
                posicaoValida = true;
            }
            else
            {
                posicaoValida = false;
                return; // Cursor fora da tela ou câmera paralela — não atualiza
            }
        }

        marcadorFantasma.transform.position = pontoMira + Vector3.up * 0.5f;

        // Gira a cabeça rotativa em direção ao alvo (apenas eixo Y)
        if (cabecaRotativa != null)
        {
            Vector3 dir = pontoMira - cabecaRotativa.position;
            dir.y = 0;
            if (dir != Vector3.zero)
                cabecaRotativa.rotation = Quaternion.Lerp(
                    cabecaRotativa.rotation,
                    Quaternion.LookRotation(dir, transform.up),
                    Time.deltaTime * 10f);
        }
    }

    void Disparar(Vector3 alvo)
    {
        Disparar(alvo, null);
    }

    void Disparar(Vector3 alvo, Transform alvoMovel)
    {
        if (municaoAtual <= 0)
        {
            Debug.LogWarning("[Lançador] Sem mísseis! Compre mais no menu (tecla L).");
            return;
        }

        if (missilPrefab == null)
        {
            Debug.LogError("[Lançador] ERRO: Prefab do míssil não atribuído no Inspector!");
            return;
        }

        // Escolhe o cano de saída
        Transform saida = transform;
        if (pontosDeSaida != null && pontosDeSaida.Length > 0)
        {
            saida = pontosDeSaida[indiceCano];
            indiceCano = (indiceCano + 1) % pontosDeSaida.Length;
        }

        if (saida == null)
        {
            saida = transform;
        }

        // Instancia o míssil respeitando a rotação do ponto de saída (cano)
        GameObject missil = PoolDeObjetosCombate.Spawn(missilPrefab, saida.position, saida.rotation);
        if (missil == null)
        {
            Debug.LogError("[Lançador] ERRO: o pool não conseguiu criar o míssil.", this);
            return;
        }
        
        // O prefab pode ser ICBM, tático, naval, torpedo, ar-ar ou um
        // componente legado. Todos precisam receber o mesmo destino antes
        // de a munição ser consumida; caso contrário o objeto nascia, mas
        // ficava sem controlador e parecia voar para um ponto aleatório.
        if (!InicializadorLancamentoMissil.Inicializar(
                missil,
                alvo,
                alvoMovel,
                this,
                saida,
                gameObject))
        {
            PoolDeObjetosCombate.Release(missil);
            Debug.LogError("[Lançador] ERRO: o prefab do míssil não possui uma API de voo válida.", this);
            return;
        }

        municaoAtual--;
        cronometroRecarga = tempoRecarga;
        Debug.Log("[Lançador] LANÇAMENTO CONFIRMADO! Destino: " + alvo);
        
        // Efeito Sonoro
        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null)
        {
            AudioRuntime.ConfigurarFonteDeMissel(audio);
            audio.Play();
        }
    }

    /// <summary>
    /// Entrada usada pelo Quartel para o lançador estratégico legado. O
    /// lançamento continua passando pelo mesmo Disparar usado pelo menu L;
    /// esta API apenas expõe a validação sem criar um segundo armamento.
    /// </summary>
    public bool PodeLancarCoordenado(Vector3 destino, bool modoAutomatico, out string motivo)
    {
        motivo = string.Empty;
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            motivo = "unidade desativada";
            return false;
        }
        if (modoAutomatico)
        {
            motivo = "este lancador estrategico opera somente em modo manual";
            return false;
        }
        if (municaoAtual <= 0)
        {
            motivo = "sem misseis";
            return false;
        }
        if (missilPrefab == null)
        {
            motivo = "prefab de missil nao configurado";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Dispara uma coordenada manual a partir do ponto de saída configurado.
    /// Não move a unidade e não aplica limite automático de alcance.
    /// </summary>
    public bool TentarLancarCoordenado(Vector3 destino, bool modoAutomatico, out string motivo)
    {
        return TentarLancarCoordenado(destino, null, modoAutomatico, out motivo);
    }

    /// <summary>
    /// Variante usada pelo Quartel quando o alvo veio de um contato E-3.
    /// Mantém a coordenada como fallback, mas permite ao MisselICBM atualizar
    /// o ponto vivo de um alvo que manobra durante o voo.
    /// </summary>
    public bool TentarLancarCoordenado(Vector3 destino, Transform alvoMovel, bool modoAutomatico, out string motivo)
    {
        if (!PodeLancarCoordenado(destino, modoAutomatico, out motivo)) return false;

        int municaoAntes = municaoAtual;
        float recargaAntes = cronometroRecarga;
        if (recargaAntes > 0f)
        {
            motivo = "lancador em recarga";
            return false;
        }

        Disparar(destino, alvoMovel);
        if (municaoAtual == municaoAntes)
        {
            motivo = "o lancador nao conseguiu criar o missil";
            return false;
        }

        motivo = string.Empty;
        return true;
    }

    // Utilitário: Cria o círculo vermelho via código pra você não ter que fazer prefab
    void CriarMarcadorFantasma()
    {
        marcadorFantasma = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marcadorFantasma.name = "Mira_Laser_Fantasma";
        Destroy(marcadorFantasma.GetComponent<Collider>()); // Tira colisão
        marcadorFantasma.transform.localScale = new Vector3(10, 0.1f, 10); // Grande e achatado
        
        // Tenta criar material vermelho transparente
        Renderer rend = marcadorFantasma.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Standard"));
        
        // Define o modo de renderização para Transparente no Standard Shader
        rend.material.SetFloat("_Mode", 3); // 3 = Transparent
        rend.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        rend.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        rend.material.SetInt("_ZWrite", 0);
        rend.material.DisableKeyword("_ALPHATEST_ON");
        rend.material.EnableKeyword("_ALPHABLEND_ON");
        rend.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        rend.material.renderQueue = 3000;

        rend.material.color = new Color(1, 0, 0, 0.2f); // Vermelho COM MAIS TRANSPARÊNCIA (0.2f)
        
        // Desliga por padrão
        marcadorFantasma.SetActive(false);
    }

    // Desenha o alcance no Editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alcanceMaximo);
    }
}
