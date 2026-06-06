using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class CaminhaoCombustivel : MonoBehaviour
{
    public static bool AbastecimentoAutomaticoGlobal = false;
    
    public enum EstadoCaminhao { Ocioso, IndoAbastecerAlvo, AbastecendoAlvo, IndoRecarregar, Recarregando }

    [Header("Status Atual")]
    public EstadoCaminhao estadoAtual = EstadoCaminhao.Ocioso;
    public float reservaAtual;
    
    [Header("Configurações")]
    public float capacidadeReservaMaxima = 2000f; // Quanto de combustível ele carrega para os outros
    public float raioBuscaAtuacao = 1500f; // Área de busca por unidades sem combustível
    public float raioAbastecimento = 15f; // Distância de engate
    public float tempoPorUnidade = 4f; // Tempo que fica parado abastecendo 1 unidade
    [Range(0.05f, 0.80f)] public float limiteCombustivelParaAtender = 0.20f;
    public bool repararUnidades = true;
    public bool abastecerUnidades = true;
    public bool usarNavioTransporteComoBase = true;
    [Tooltip("Centro fixo da area que o Track deve proteger. Se vazio, usa onde ele nasceu ou o quartel que o chamou.")]
    public Transform centroAreaAtuacao;
    public GerenciadorQuartel quartelPreferencial;
    
    // Alvos
    private Transform alvoUnidade;
    private CombustivelUnidade componenteAlvoCombustivel;
    private SistemaDeDanos componenteAlvoDanos;
    
    private Transform alvoRecargaBase;

    private NavMeshAgent agente;
    private ControleUnidade controleUnidade; // Para impedir conflitos se o jogador mandar ele mover
    private IdentidadeUnidade identidade;

    // LineRenderer opcional para a mangueira
    private LineRenderer linhaAbastecimento;

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        controleUnidade = GetComponent<ControleUnidade>();
        identidade = GetComponent<IdentidadeUnidade>();
        
        reservaAtual = capacidadeReservaMaxima;

        linhaAbastecimento = GetComponent<LineRenderer>();
        if (linhaAbastecimento == null)
        {
            linhaAbastecimento = gameObject.AddComponent<LineRenderer>();
            linhaAbastecimento.startWidth = 0.3f;
            linhaAbastecimento.endWidth = 0.3f;
            linhaAbastecimento.positionCount = 2;
            linhaAbastecimento.enabled = false;
            
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = Color.yellow;
            linhaAbastecimento.material = mat;
        }
        else
        {
            // Garante que o LineRenderer comece desabilitado para evitar piscar na tela
            linhaAbastecimento.enabled = false;
        }

        // Garante o uso de coordenadas globais (world space) para alinhar a mangueira corretamente
        linhaAbastecimento.useWorldSpace = true;
    }

    void OnEnable()
    {
        StartCoroutine(RotinaPrincipal());
    }

    private IEnumerator RotinaPrincipal()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            
            if (identidade != null && identidade.teamID != 1) continue; // Só time 1 
            if (!AbastecimentoAutomaticoGlobal) continue; // Se o botão no menu do quartel não estiver ativo
            
            // Se o jogador selecionou o caminhão e deu ordem, interrompemos
            if (controleUnidade != null && controleUnidade.selecionado && agente.hasPath)
            {
                // Deixa o jogador mover manualmente
                LimparAlvos();
                estadoAtual = EstadoCaminhao.Ocioso;
                continue;
            }

            switch (estadoAtual)
            {
                case EstadoCaminhao.Ocioso:
                    if (reservaAtual <= 0.01f)
                    {
                        BuscarBaseRecarga();
                    }
                    else
                    {
                        BuscarUnidadePrecisando();
                    }
                    break;

                case EstadoCaminhao.IndoAbastecerAlvo:
                    if (alvoUnidade == null || !alvoUnidade.gameObject.activeInHierarchy)
                    {
                        LimparAlvos();
                        estadoAtual = EstadoCaminhao.Ocioso;
                        break;
                    }
                    
                    if (Vector3.Distance(transform.position, alvoUnidade.position) <= raioAbastecimento)
                    {
                        agente.ResetPath();
                        StartCoroutine(RotinaAbastecerUnidade());
                    }
                    else
                    {
                        agente.SetDestination(alvoUnidade.position);
                    }
                    break;

                case EstadoCaminhao.IndoRecarregar:
                    if (alvoRecargaBase == null)
                    {
                        estadoAtual = EstadoCaminhao.Ocioso;
                        break;
                    }
                    
                    if (Vector3.Distance(transform.position, alvoRecargaBase.position) <= 25f)
                    {
                        agente.ResetPath();
                        StartCoroutine(RotinaRecarregarNaBase());
                    }
                    else
                    {
                        agente.SetDestination(alvoRecargaBase.position);
                    }
                    break;
            }
        }
    }

    private void BuscarUnidadePrecisando()
    {
        IdentidadeUnidade[] todas = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        float menorDist = Mathf.Infinity;
        Transform melhorAlvo = null;
        CombustivelUnidade melhorCombustivel = null;
        SistemaDeDanos melhorDanos = null;
        Vector3 centroBusca = ObterCentroAreaAtuacao();
        float raioSqr = raioBuscaAtuacao * raioBuscaAtuacao;

        foreach (var id in todas)
        {
            if (id.teamID != 1) continue;
            if (id.gameObject == this.gameObject) continue; // Não abastece a si mesmo
            
            // Só veículos terrestres ou tropas
            if (id.tipoUnidade != TipoUnidade.Veiculo && id.tipoUnidade != TipoUnidade.Infantaria) continue;

            if ((id.transform.position - centroBusca).sqrMagnitude > raioSqr) continue;

            CombustivelUnidade comb = id.GetComponent<CombustivelUnidade>();
            SistemaDeDanos dmg = id.GetComponent<SistemaDeDanos>();
            
            bool precisa = false;
            if (abastecerUnidades && comb != null && comb.usaCombustivel && comb.Percentual <= limiteCombustivelParaAtender) precisa = true;
            if (repararUnidades && dmg != null && dmg.vidaAtual < dmg.vidaMaxima) precisa = true;

            float dist = Vector3.Distance(transform.position, id.transform.position);
            if (precisa && dist < menorDist)
            {
                menorDist = dist;
                melhorAlvo = id.transform;
                melhorCombustivel = comb;
                melhorDanos = dmg;
            }
        }

        if (melhorAlvo != null)
        {
            alvoUnidade = melhorAlvo;
            componenteAlvoCombustivel = melhorCombustivel;
            componenteAlvoDanos = melhorDanos;
            estadoAtual = EstadoCaminhao.IndoAbastecerAlvo;
            agente.SetDestination(melhorAlvo.position);
        }
    }

    private void BuscarBaseRecarga()
    {
        // Procura um Quartel ou um Navio Transporte na Terra
        float menorDist = Mathf.Infinity;
        Transform melhorBase = null;

        // 1. Usar quartel preferencial quando ele controla este Track
        if (quartelPreferencial != null && quartelPreferencial.gameObject.activeInHierarchy)
        {
            menorDist = Vector3.Distance(transform.position, quartelPreferencial.transform.position);
            melhorBase = quartelPreferencial.transform;
        }

        // 2. Procurar Quartel
        GerenciadorQuartel[] quarteis = FindObjectsByType<GerenciadorQuartel>(FindObjectsSortMode.None);
        foreach(var q in quarteis)
        {
            if (!PertenceAoTimeJogador(q.gameObject)) continue;
            float dist = Vector3.Distance(transform.position, q.transform.position);
            if (dist < menorDist)
            {
                menorDist = dist;
                melhorBase = q.transform;
            }
        }

        // 3. Procurar Navio de Transporte (Liberty) que tenha fila/parada em terra
        if (usarNavioTransporteComoBase)
        {
            NavioTransporteTropas[] transportes = FindObjectsByType<NavioTransporteTropas>(FindObjectsSortMode.None);
            foreach(var navio in transportes)
            {
                if (!PertenceAoTimeJogador(navio.gameObject)) continue;
                Transform pontoTerra = EncontrarPontoTerraTransporte(navio);
                if (pontoTerra == null) continue;

                float dist = Vector3.Distance(transform.position, pontoTerra.position);
                if (dist < menorDist)
                {
                    menorDist = dist;
                    melhorBase = pontoTerra;
                }
            }
        }

        if (melhorBase != null)
        {
            alvoRecargaBase = melhorBase;
            estadoAtual = EstadoCaminhao.IndoRecarregar;
            if (agente != null && agente.enabled && agente.isOnNavMesh)
            {
                agente.SetDestination(alvoRecargaBase.position);
            }
        }
    }

    private IEnumerator RotinaAbastecerUnidade()
    {
        estadoAtual = EstadoCaminhao.AbastecendoAlvo;
        if (agente != null && agente.enabled) agente.ResetPath();
        
        if (linhaAbastecimento != null)
        {
            linhaAbastecimento.useWorldSpace = true;
            linhaAbastecimento.enabled = true;
        }

        float tempoDecorrido = 0f;
        while (tempoDecorrido < tempoPorUnidade)
        {
            if (linhaAbastecimento != null && alvoUnidade != null)
            {
                linhaAbastecimento.SetPosition(0, transform.position + Vector3.up);
                linhaAbastecimento.SetPosition(1, alvoUnidade.position + Vector3.up);
            }
            tempoDecorrido += Time.deltaTime;
            yield return null;
        }

        if (linhaAbastecimento != null) linhaAbastecimento.enabled = false;

        if (alvoUnidade != null && reservaAtual > 0)
        {
            if (componenteAlvoCombustivel != null)
            {
                float falta = componenteAlvoCombustivel.Capacidade - componenteAlvoCombustivel.CombustivelAtual;
                float tentativa = (falta > reservaAtual) ? reservaAtual : falta;
                
                float foi = componenteAlvoCombustivel.Abastecer(tentativa);
                reservaAtual -= foi;
            }

            if (componenteAlvoDanos != null)
            {
                componenteAlvoDanos.Reparar(componenteAlvoDanos.vidaMaxima); 
            }
        }

        LimparAlvos();
        estadoAtual = EstadoCaminhao.Ocioso;
    }

    private IEnumerator RotinaRecarregarNaBase()
    {
        estadoAtual = EstadoCaminhao.Recarregando;
        if (agente != null && agente.enabled) agente.ResetPath();
        
        yield return new WaitForSeconds(4f); // Carregamento rápido na base

        reservaAtual = capacidadeReservaMaxima;
        alvoRecargaBase = null;
        estadoAtual = EstadoCaminhao.Ocioso;
    }

    private void LimparAlvos()
    {
        alvoUnidade = null;
        componenteAlvoCombustivel = null;
        componenteAlvoDanos = null;
        if (linhaAbastecimento != null) linhaAbastecimento.enabled = false;
    }
    
    // Chamado pelo botão do menu B
    public void ForcarRetornoBase()
    {
        reservaAtual = 0;
        LimparAlvos();
        estadoAtual = EstadoCaminhao.Ocioso;
        BuscarBaseRecarga();
    }

    public void DefinirQuartelPreferencial(GerenciadorQuartel quartel)
    {
        quartelPreferencial = quartel;
    }

    public void ForcarRecarregarNoQuartel(GerenciadorQuartel quartel)
    {
        DefinirQuartelPreferencial(quartel);
        if (quartel == null) return;

        LimparAlvos();
        alvoRecargaBase = quartel.transform;
        estadoAtual = EstadoCaminhao.IndoRecarregar;
        if (agente != null && agente.enabled && agente.isOnNavMesh)
        {
            agente.SetDestination(alvoRecargaBase.position);
        }
    }

    private Vector3 ObterCentroAreaAtuacao()
    {
        if (centroAreaAtuacao != null) return centroAreaAtuacao.position;
        if (quartelPreferencial != null) return quartelPreferencial.transform.position;
        return transform.position;
    }

    private bool PertenceAoTimeJogador(GameObject alvo)
    {
        if (alvo == null) return false;
        IdentidadeUnidade id = alvo.GetComponentInParent<IdentidadeUnidade>();
        return id == null || id.teamID == 0 || id.teamID == 1;
    }

    private Transform EncontrarPontoTerraTransporte(NavioTransporteTropas navio)
    {
        if (navio == null) return null;

        Transform melhor = null;
        float menorDist = Mathf.Infinity;
        Transform[] filhos = navio.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < filhos.Length; i++)
        {
            Transform f = filhos[i];
            if (f == null || f == navio.transform) continue;

            string nome = f.name.ToLowerInvariant();
            bool pontoValido = nome.Contains("fila")
                || nome.Contains("parada")
                || nome.Contains("embarque")
                || nome.Contains("atraca")
                || nome.Contains("saida")
                || nome.Contains("saída");

            if (!pontoValido) continue;

            float dist = Vector3.Distance(transform.position, f.position);
            if (dist < menorDist)
            {
                menorDist = dist;
                melhor = f;
            }
        }

        return melhor;
    }
}
