using UnityEngine;
using System.Collections;

[DisallowMultipleComponent] // Evita que você coloque dois scripts iguais no mesmo objeto sem querer
public class LancadorMisseis : MonoBehaviour
{
    [Header("Configuração de Munição")]
    public int municaoAtual = 0;
    public int municaoMaxima = 4;
    public int custoMissil = 200;

    [Header("Configuração de Lançamento")]
    public float alcanceMaximo = 500f;
    public float tempoRecarga = 2.0f;
    public Transform[] pontosDeSaida; // Canos de saída
    public GameObject missilPrefab;   // Prefab do MisselICBM

    [Header("Visual")]
    public Transform cabecaRotativa; // Parte que gira (opcional)

    // Estado Interno
    private bool menuAberto = false;
    private bool mirando = false;
    private GameObject marcadorFantasma; // O círculo vermelho
    private float cronometroRecarga = 0f;
    private int indiceCano = 0;
    private GerenteDeJogo gerente;

    // --- CONTROLE ESTÁTICO PARA EVITAR MENUS DUPLICADOS ---
    private static LancadorMisseis menuAtivo; // Guarda quem está com o menu aberto agora
    private static float ultimoTempoInput = 0f; // Para evitar que dois scripts processem o 'L' no mesmo frame

    void Start()
    {
        gerente = FindObjectOfType<GerenteDeJogo>();
        CriarMarcadorFantasma();
    }

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
                if (cronometroRecarga <= 0)
                {
                    Disparar(marcadorFantasma.transform.position);
                    
                    // Finaliza mira
                    mirando = false; 
                    marcadorFantasma.SetActive(false);
                    // Garante que o menu permaneça fechado
                    FecharMenu();
                }
                else
                {
                    Debug.Log("[Lançador] Carregando misseis... aguarde.");
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

        // Info Dinheiro (se tiver gerente)
        if(gerente != null)
             GUI.Label(new Rect(x + 20, y + 50, 200, 20), $"Dinheiro: ${gerente.dinheiroAtual}");

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

        if (gerente != null)
        {
            if (gerente.dinheiroAtual >= custoMissil)
            {
                gerente.dinheiroAtual -= custoMissil;
                municaoAtual++;
                Debug.Log("[Lançador] Míssil comprado!");
            }
            else
            {
                Debug.Log("[Lançador] Sem dinheiro!");
            }
        }
        else
        {
            // Se não tiver gerente (teste), dá o míssil de graça
            municaoAtual++;
            Debug.Log("[Lançador] Modo Teste: Míssil adicionado (Grátis)");
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

        // Tenta bater no chão
        if (Physics.Raycast(raio, out toque, 5000f))
        {
            marcadorFantasma.transform.position = toque.point + Vector3.up * 0.5f;

            // Se tiver cabeça rotativa, mira ela pro fantasma
            if (cabecaRotativa != null)
            {
                Vector3 dir = toque.point - cabecaRotativa.position;
                dir.y = 0; // Não inclina pra cima/baixo
                if(dir != Vector3.zero) 
                    cabecaRotativa.rotation = Quaternion.Lerp(cabecaRotativa.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
            }
        }
    }

    void Disparar(Vector3 alvo)
    {
        if (municaoAtual <= 0) return;

        // Escolhe o cano de saída
        Transform saida = transform;
        if (pontosDeSaida != null && pontosDeSaida.Length > 0)
        {
            saida = pontosDeSaida[indiceCano];
            indiceCano = (indiceCano + 1) % pontosDeSaida.Length;
        }

        // Instancia o míssil respeitando a rotação do ponto de saída (cano)
        GameObject missil = Instantiate(missilPrefab, saida.position, saida.rotation);
        
        // Passa o alvo para o script de voo (MisselICBM)
        MisselICBM scriptVoo = missil.GetComponent<MisselICBM>();
        if (scriptVoo != null)
        {
            scriptVoo.IniciarLancamento(alvo);
        }

        municaoAtual--;
        cronometroRecarga = tempoRecarga;
        Debug.Log("[Lançador] LANÇAMENTO CONFIRMADO! Destino: " + alvo);
        
        // Efeito Sonoro
        AudioSource audio = GetComponent<AudioSource>();
        if(audio != null) audio.Play();
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
