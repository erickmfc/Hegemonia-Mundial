using UnityEngine;

public class LancadorMisseis : MonoBehaviour
{
    [Header("Modo de Operação")]
    public bool modoManual = false; // Se TRUE, segue o mouse. Se FALSE, age sozinho.
    
    [Header("Munição")]
    public int municaoAtual = 4;
    public int municaoMaxima = 4;

    [Header("Configuração")]
    public float alcance = 200f;
    public float velocidadeGiro = 5f;
    public float tempoEntreDisparos = 1.0f;
    private float cronometro = 0f;

    [Header("Conexões")]
    public Transform cabecaRotativa; // A parte que gira
    public Transform[] pontosDeSaida; // Arraste os 4 pontos aqui (Saida 1, 2, 3, 4)
    public GameObject missilPrefab;

    [Header("Áudio")]
    public AudioClip somDisparo; // Som ao lançar o míssil
    private AudioSource fonteAudio;
    
    // Variáveis internas
    private Transform alvoAtual;
    private int indiceCanoAtual = 0; // Para saber qual dos 4 canos atira agora
    
    // Sistema Anti-Overkill: Rastreia alvos atacados recentemente
    [System.Serializable]
    private struct AlvoInfo
    {
        public Transform alvo;
        public float tempo;
        
        public AlvoInfo(Transform t, float time)
        {
            alvo = t;
            tempo = time;
        }
    }
    private System.Collections.Generic.List<AlvoInfo> alvosRecentes = new System.Collections.Generic.List<AlvoInfo>();

    void Start()
    {
        // Inicialização Inteligente do AudioSource
        fonteAudio = GetComponent<AudioSource>();
        if (fonteAudio == null)
        {
            fonteAudio = gameObject.AddComponent<AudioSource>();
        }
        fonteAudio.spatialBlend = 1.0f; // Som 3D espacial
    }

    void Update()
    {
        // Se acabou a munição, não faz nada
        if (municaoAtual <= 0) return;

        cronometro -= Time.deltaTime;

        if (modoManual)
        {
            MirarNoMouse();
            // Botão Esquerdo do Mouse para atirar
            if (Input.GetMouseButtonDown(0) && cronometro <= 0)
            {
                Atirar(null); // No manual, atira sem alvo travado (vai reto ou onde mirou)
            }
        }
        else
        {
            // Modo Automático (Radar)
            ProcurarInimigo();
            if (alvoAtual != null)
            {
                MirarNoAlvo();
                // Se estiver mirando perto o suficiente, atira
                if (cronometro <= 0)
                {
                    Atirar(alvoAtual);
                }
            }
        }
    }

    void MirarNoMouse()
    {
        Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit toque;
        
        // Cria um raio da câmera até o chão/mundo
        if (Physics.Raycast(raio, out toque, Mathf.Infinity))
        {
            Vector3 pontoMira = toque.point;
            pontoMira.y = cabecaRotativa.position.y; // Mantém a altura para não inclinar errado
            
            Vector3 direcao = pontoMira - cabecaRotativa.position;
            Quaternion rotacao = Quaternion.LookRotation(direcao);
            cabecaRotativa.rotation = Quaternion.Lerp(cabecaRotativa.rotation, rotacao, Time.deltaTime * velocidadeGiro);
        }
    }

    void ProcurarInimigo()
    {
        // Limpa alvos antigos da lista (após 3 segundos, considera "livre" novamente)
        alvosRecentes.RemoveAll(info => Time.time - info.tempo > 3f);

        // Procura inimigos aereos ou terrestres
        GameObject[] inimigos = GameObject.FindGameObjectsWithTag("Aereo"); 
        // DICA: Se quiser atacar tanque também, teria que buscar "Inimigo" ou fazer uma lista mista
        
        float menorDistancia = Mathf.Infinity;
        GameObject inimigoPerto = null;

        foreach (GameObject inimigo in inimigos)
        {
            float dist = Vector3.Distance(transform.position, inimigo.transform.position);
            
            // Só considera se estiver no alcance
            if (dist > alcance) continue;

            // Verifica se já está sendo atacado recentemente
            bool jaAtacado = alvosRecentes.Exists(info => info.alvo == inimigo.transform);
            
            // Prioriza alvos NÃO atacados recentemente
            if (!jaAtacado && dist < menorDistancia)
            {
                menorDistancia = dist;
                inimigoPerto = inimigo;
            }
        }

        // Se não encontrou nenhum alvo "livre", pega qualquer um dentro do alcance
        if (inimigoPerto == null)
        {
            foreach (GameObject inimigo in inimigos)
            {
                float dist = Vector3.Distance(transform.position, inimigo.transform.position);
                if (dist < menorDistancia && dist <= alcance)
                {
                    menorDistancia = dist;
                    inimigoPerto = inimigo;
                }
            }
        }

        if (inimigoPerto != null)
        {
            alvoAtual = inimigoPerto.transform;
        }
    }

    void MirarNoAlvo()
    {
        Vector3 direcao = alvoAtual.position - cabecaRotativa.position;
        Quaternion rotacao = Quaternion.LookRotation(direcao);
        cabecaRotativa.rotation = Quaternion.Lerp(cabecaRotativa.rotation, rotacao, Time.deltaTime * velocidadeGiro);
    }

    void Atirar(Transform alvoDestino)
    {
        if (pontosDeSaida.Length == 0) return;

        // Pega o cano da vez
        Transform canoAtual = pontosDeSaida[indiceCanoAtual];

        // Cria o míssil
        GameObject missil = Instantiate(missilPrefab, canoAtual.position, canoAtual.rotation);
        
        // Configura o alvo do míssil
        MissilTeleguiado scriptMissil = missil.GetComponent<MissilTeleguiado>();
        if (scriptMissil != null && alvoDestino != null)
        {
            scriptMissil.DefinirAlvo(alvoDestino);
        }

        municaoAtual--; // Gasta uma bala
        cronometro = tempoEntreDisparos;

        // Registra o alvo como "recentemente atacado" para evitar overkill
        if (alvoDestino != null)
        {
            alvosRecentes.Add(new AlvoInfo(alvoDestino, Time.time));
            Debug.Log($"Míssil disparado em {alvoDestino.name}! Restam: {municaoAtual}");
        }
        else
        {
            Debug.Log("Míssil disparado! Restam: " + municaoAtual);
        }

        // Executa o Som de Disparo
        if (fonteAudio != null && somDisparo != null)
        {
            fonteAudio.PlayOneShot(somDisparo);
        }

        // Passa para o próximo cano (0 -> 1 -> 2 -> 3 -> 0)
        indiceCanoAtual++;
        if (indiceCanoAtual >= pontosDeSaida.Length)
        {
            indiceCanoAtual = 0;
        }
        
        // IMPORTANTE: Limpa o alvo atual para forçar busca de novo alvo
        alvoAtual = null;
    }

    // ========== VISUALIZAÇÃO DE DEBUG ==========
    void OnDrawGizmos()
    {
        // Desenha o alcance quando o objeto NÃO está selecionado (círculo fino)
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // Vermelho transparente
        DrawRangeCircle();
    }

    void OnDrawGizmosSelected()
    {
        // Quando o objeto ESTÁ selecionado, mostra mais detalhes
        
        // Cor do alcance muda se tem alvo ou não
        if (alvoAtual != null)
        {
            Gizmos.color = Color.yellow; // Amarelo = tem alvo
            
            // Desenha linha até o alvo
            Gizmos.DrawLine(transform.position, alvoAtual.position);
            
            // Desenha uma esfera no alvo
            Gizmos.DrawWireSphere(alvoAtual.position, 2f);
        }
        else
        {
            Gizmos.color = Color.red; // Vermelho = sem alvo
        }
        
        DrawRangeCircle();
        
        // Informações no topo da esfera de alcance
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 5f, 
            $"Munição: {municaoAtual}/{municaoMaxima}\n" +
            $"Modo: {(modoManual ? "MANUAL" : "AUTO")}\n" +
            $"Alcance: {alcance}m");
        #endif
    }

    void DrawRangeCircle()
    {
        // Desenha um círculo representando o alcance
        int segmentos = 50;
        float angulo = 0f;
        float incremento = 360f / segmentos;
        
        Vector3 pontoAnterior = transform.position + new Vector3(alcance, 0, 0);
        
        for (int i = 0; i <= segmentos; i++)
        {
            angulo = i * incremento * Mathf.Deg2Rad;
            Vector3 pontoAtual = transform.position + new Vector3(
                Mathf.Cos(angulo) * alcance,
                0,
                Mathf.Sin(angulo) * alcance
            );
            
            Gizmos.DrawLine(pontoAnterior, pontoAtual);
            pontoAnterior = pontoAtual;
        }
    }
}
