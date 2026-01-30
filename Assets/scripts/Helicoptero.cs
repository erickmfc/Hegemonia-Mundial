using UnityEngine;

/// <summary>
/// Script para colocar nos helicópteros.
/// Registra o helicóptero no GerenciadorHelicopteros e permite que seja chamado por um heliporto.
/// Trabalha em conjunto com o HelicopterController existente.
/// </summary>
public class Helicoptero : MonoBehaviour
{
    [Header("Identificação")]
    [Tooltip("Nome do helicóptero para mostrar no menu")]
    public string nomeHelicoptero = "Helicóptero";
    
    [Tooltip("Tipo/Modelo do helicóptero (ex: Transporte, Ataque, Reconhecimento)")]
    public string tipoHelicoptero = "Transporte";

    [Header("Estado")]
    [Tooltip("Se o helicóptero está ocupado com uma missão")]
    public bool emMissao = false;
    
    [Tooltip("Se o helicóptero está atualmente voando para um heliporto")]
    public bool indoParaHeliporto = false;
    
    [Tooltip("Heliporto de destino atual")]
    public Heliporto heliportoDestino;

    [Header("Configurações de Voo para Heliporto")]
    [Tooltip("Velocidade ao voar para o heliporto")]
    public float velocidadeParaHeliporto = 25f;
    
    [Tooltip("Altura de voo ao se deslocar")]
    public float alturaDeVoo = 15f;
    
    [Tooltip("Distância mínima para considerar que chegou ao heliporto (horizontal)")]
    public float distanciaChegada = 3f;

    [Header("Referências")]
    private HelicopterController controlador;
    private Vector3 posicaoPousoAlvo;
    private bool pousando = false;

    [Header("Evolução")]
    public int nivel = 1;
    public int custoUpgrade = 200;

    void Start()
    {
        // Busca o controlador existente
        controlador = GetComponent<HelicopterController>();

        // Auto-nome se não definido
        if (string.IsNullOrEmpty(nomeHelicoptero) || nomeHelicoptero == "Helicóptero")
        {
            nomeHelicoptero = gameObject.name;
        }

        // Tenta registrar imediatamente
        RegistrarNoGerenciador();

        // Inicia verificação periódica para garantir que não perdeu o registro
        InvokeRepeating("VerificarRegistro", 2f, 5f);
    }

    void VerificarRegistro()
    {
        if (GerenciadorHelicopteros.Instancia == null)
        {
            RegistrarNoGerenciador();
            return;
        }

        if (!GerenciadorHelicopteros.Instancia.helicopterosRegistrados.Contains(this))
        {
            Debug.Log($"[Helicoptero] {nomeHelicoptero} não estava registrado. Registrando novamente...");
            RegistrarNoGerenciador();
        }
    }

    void RegistrarNoGerenciador()
    {
        // 1. Tenta pegar via Singleton
        GerenciadorHelicopteros gerenciador = GerenciadorHelicopteros.Instancia;
        
        // 2. Se falhar, procura na cena (pode ter sido criado mas Awake ainda não setou Instancia)
        if (gerenciador == null)
        {
            gerenciador = FindFirstObjectByType<GerenciadorHelicopteros>();
        }

        // 3. Se ainda não existe, cria um
        if (gerenciador == null)
        {
            GameObject gerenciadorObj = new GameObject("GerenciadorHelicopteros");
            gerenciador = gerenciadorObj.AddComponent<GerenciadorHelicopteros>();
        }

        gerenciador.RegistrarHelicoptero(this);
    }

    /// <summary>
    /// Aplica melhorias no helicóptero (chamado pelo menu do Heliporto).
    /// </summary>
    public void MelhorarHelicoptero()
    {
        nivel++;
        custoUpgrade += 150; // Aumenta o custo para o próximo

        // 1. Melhora Velocidade
        if(controlador != null)
        {
            controlador.velocidadeNavegacao += 5f; // +5 m/s
        }

        // 2. Melhora Vida
        SistemaDeDanos vida = GetComponent<SistemaDeDanos>();
        if(vida != null)
        {
            vida.AtualizarVidaMaxima(50); // +50 HP
        }

        Debug.Log($"[Helicoptero] {nomeHelicoptero} evoluiu para Nível {nivel}!");
    }



    void OnDestroy()
    {
        // Remove do gerenciador quando destruído
        if (GerenciadorHelicopteros.Instancia != null)
        {
            GerenciadorHelicopteros.Instancia.RemoverHelicoptero(this);
        }
    }

    void Update()
    {
        // Se está indo para um heliporto, processa o movimento
        if (indoParaHeliporto && heliportoDestino != null)
        {
            ProcessarVooParaHeliporto();
        }
    }

    /// <summary>
    /// Verifica se o helicóptero está disponível para ser chamado.
    /// </summary>
    public bool EstaDisponivel()
    {
        return !emMissao && !indoParaHeliporto;
    }

    /// <summary>
    /// Chamado pelo Heliporto para trazer o helicóptero até ele.
    /// </summary>
    public void ChamarParaHeliporto(Heliporto heliporto)
    {
        if (heliporto == null)
        {
            Debug.LogWarning($"[Helicoptero] {nomeHelicoptero}: Heliporto inválido!");
            return;
        }

        if (!EstaDisponivel())
        {
            Debug.LogWarning($"[Helicoptero] {nomeHelicoptero}: Não está disponível!");
            return;
        }

        heliportoDestino = heliporto;
        posicaoPousoAlvo = heliporto.ObterPontoDePousoMundial();
        indoParaHeliporto = true;
        pousando = false;

        Debug.Log($"[Helicoptero] {nomeHelicoptero} decolando para heliporto em {posicaoPousoAlvo}");

        // Se tiver o controlador, usa ele para decolar
        if (controlador != null)
        {
            // Define destino usando o método do controlador
            controlador.DefinirDestino(posicaoPousoAlvo);
        }
    }

    /// <summary>
    /// Processa o voo automático até o heliporto.
    /// </summary>
    void ProcessarVooParaHeliporto()
    {
        if (heliportoDestino == null)
        {
            CancelarVooParaHeliporto();
            return;
        }

        // Atualiza posição de pouso (caso o heliporto se mova)
        posicaoPousoAlvo = heliportoDestino.ObterPontoDePousoMundial();

        // Calcula distância horizontal até o destino
        Vector3 posAtual = transform.position;
        Vector3 destino = posicaoPousoAlvo;
        
        float distanciaHorizontal = Vector3.Distance(
            new Vector3(posAtual.x, 0, posAtual.z),
            new Vector3(destino.x, 0, destino.z)
        );

        // Se chegou perto horizontalmente
        if (distanciaHorizontal <= distanciaChegada)
        {
            if (!pousando)
            {
                pousando = true;
                Debug.Log($"[Helicoptero] {nomeHelicoptero} iniciando pouso no heliporto");
                
                // Manda pousar
                if (controlador != null)
                {
                    controlador.Pousar();
                }
            }

            // Verifica se já está no chão (altura baixa)
            float alturaDoChao = posAtual.y - posicaoPousoAlvo.y;
            if (alturaDoChao < 2f)
            {
                FinalizarChegadaNoHeliporto();
            }
        }
        else
        {
            // Se ainda não chegou, continua atualizando o destino
            // (o HelicopterController já faz o movimento)
            if (controlador != null && !controlador.enabled)
            {
                // Caso o controlador esteja desabilitado, move manualmente
                MoverManualmente();
            }
        }
    }

    /// <summary>
    /// Movimento manual para quando não há controlador.
    /// </summary>
    void MoverManualmente()
    {
        Vector3 direcao = (posicaoPousoAlvo - transform.position);
        direcao.y = 0; // Ignora altura

        if (direcao.magnitude > distanciaChegada)
        {
            // Move horizontalmente
            Vector3 movimento = direcao.normalized * velocidadeParaHeliporto * Time.deltaTime;
            transform.position += movimento;

            // Mantém altura de voo
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, posicaoPousoAlvo.y + alturaDeVoo, Time.deltaTime * 2f);
            transform.position = pos;

            // Rotaciona para o destino
            if (direcao != Vector3.zero)
            {
                Quaternion rotAlvo = Quaternion.LookRotation(direcao);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotAlvo, Time.deltaTime * 3f);
            }
        }
    }

    /// <summary>
    /// Finaliza a chegada no heliporto.
    /// </summary>
    void OnEnable()
    {
        RegistrarNoGerenciador();
    }

    void OnDisable()
    {
        if (GerenciadorHelicopteros.Instancia != null)
        {
            GerenciadorHelicopteros.Instancia.RemoverHelicoptero(this);
        }
    }

    void FinalizarChegadaNoHeliporto()
    {
        Debug.Log($"[Helicoptero] {nomeHelicoptero} pousou no heliporto!");

        indoParaHeliporto = false;
        pousando = false;

        // Notifica o heliporto
        if (heliportoDestino != null)
        {
            heliportoDestino.HelicopteroPousou(this);
        }

        // Ajuste Fino de Pouso com Raycast
        RaycastHit hit;
        Vector3 origem = transform.position + Vector3.up * 5f;
        if (Physics.Raycast(origem, Vector3.down, out hit, 10f))
        {
            transform.position = hit.point;
        }
        else
        {
            transform.position = posicaoPousoAlvo;
        }
        
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);

        heliportoDestino = null;

        // --- SOLTAR TROPAS ---
        if(controlador != null)
        {
            controlador.Pousar(); // Garante estado de pouso
            controlador.ForcarDesembarqueAgora(); // SOLTA OS SOLDADOS!
        }
    }

    /// <summary>
    /// Cancela o voo para heliporto.
    /// </summary>
    public void CancelarVooParaHeliporto()
    {
        indoParaHeliporto = false;
        pousando = false;
        heliportoDestino = null;
        Debug.Log($"[Helicoptero] {nomeHelicoptero}: Voo para heliporto cancelado.");
    }

    /// <summary>
    /// Define se o helicóptero está em missão.
    /// </summary>
    public void DefinirEmMissao(bool emMissao)
    {
        this.emMissao = emMissao;
    }

    /// <summary>
    /// Retorna descrição para o menu.
    /// </summary>
    public string ObterDescricaoMenu()
    {
        string status = EstaDisponivel() ? "Disponível" : (emMissao ? "Em Missão" : "Ocupado");
        return $"Lv.{nivel} {nomeHelicoptero} - {status}";
    }
}
