using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador de Logistica e Transporte Aereo de Suprimentos / Racao para o helicoptero HL S1.
/// Decola do aeroporto, voa ao pier mais proximo, paira ~10s carregando racao,
/// voa ate o navio/unidade solicitante (ate 5.000m), entrega os suprimentos e retorna ao pier/porta-avioes.
/// Projetado para custo computacional minimo (zero GC/polling pesado em Update).
/// </summary>
[DisallowMultipleComponent]
public class HelicopteroTransporteSuprimentos : MonoBehaviour
{
    public enum EstadoMissao
    {
        PousadoEmEspera,
        Decolando,
        VoandoParaPier,
        PairandoNoPierCarregando,
        VoandoParaNavio,
        DescarregandoNoNavio,
        RetornandoParaBase,
        Pousando
    }

    [Header("Identificacao e Estado")]
    public EstadoMissao estadoAtual = EstadoMissao.PousadoEmEspera;
    [SerializeField] private float capacidadeCargaRacao = 1000f;
    [SerializeField] private float racaoAtual = 0f;

    [Header("Desempenho de Voo")]
    [SerializeField] private float velocidadeVoo = 65f;
    [SerializeField] private float velocidadeSubida = 15f;
    [SerializeField] private float altitudeCruzeiro = 60f;
    [SerializeField] private float tempoPairandoPier = 10f;
    [SerializeField] private float tempoPairandoEntrega = 5f;

    [Header("Helices / Rotores")]
    [SerializeField] private Transform rotorEsquerdo;
    [SerializeField] private Transform rotorDireito;
    [SerializeField] private Vector3 eixoRotacaoHelice = new Vector3(0f, 1f, 0f);
    [SerializeField] private float velocidadeRotacaoHelice = 1200f;

    [Header("Referencias de Missao")]
    [SerializeField] private Transform alvoAtual;
    [SerializeField] private PierMarinha pierOrigem;
    [SerializeField] private Vector3 pontoPousoBase;

    private ControleUnidade controleUnidade;
    private IdentidadeUnidade identidade;
    private CombustivelUnidade combustivel;
    private Coroutine rotinaMissao;
    private float velocidadeHeliceAtual = 0f;

    public bool EmMissao => estadoAtual != EstadoMissao.PousadoEmEspera;
    public float RacaoAtual => racaoAtual;
    public float CapacidadeCargaRacao => capacidadeCargaRacao;

    void Awake()
    {
        controleUnidade = GetComponent<ControleUnidade>();
        identidade = GetComponent<IdentidadeUnidade>();
        combustivel = GetComponent<CombustivelUnidade>();

        ConfigurarCombustivelAutonomiaEstendida();
        LocalizarRotoresSeNecessario();
    }

    void Start()
    {
        pontoPousoBase = transform.position;
    }

    void Update()
    {
        AtualizarAnimacaoHelices();
    }

    private void ConfigurarCombustivelAutonomiaEstendida()
    {
        if (combustivel != null)
        {
            // Alta capacidade e baixo consumo para permitir longas viagens ate 5km sem abortar
            if (combustivel.capacidade < 3000f)
            {
                combustivel.preservarCapacidadeConfigurada = true;
                combustivel.capacidade = 4500f;
                combustivel.combustivelAtual = 4500f;
            }
        }
    }

    private void LocalizarRotoresSeNecessario()
    {
        if (rotorEsquerdo != null && rotorDireito != null) return;

        Transform[] filhos = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < filhos.Length; i++)
        {
            Transform t = filhos[i];
            string nome = t.name.ToLowerInvariant();
            if (rotorEsquerdo == null && (nome.Contains("propl") || nome.Contains("spinnerl")))
            {
                rotorEsquerdo = t;
            }
            else if (rotorDireito == null && (nome.Contains("propr") || nome.Contains("spinnerr") || nome.Contains("spinner_08")))
            {
                rotorDireito = t;
            }
        }
    }

    private void AtualizarAnimacaoHelices()
    {
        bool girar = EmMissao || (controleUnidade != null && !controleUnidade.EstaDesligado);
        float velocidadeAlvo = girar ? velocidadeRotacaoHelice : 0f;
        velocidadeHeliceAtual = Mathf.MoveTowards(velocidadeHeliceAtual, velocidadeAlvo, 400f * Time.deltaTime);

        if (velocidadeHeliceAtual > 1f)
        {
            float angulo = velocidadeHeliceAtual * Time.deltaTime;
            if (rotorEsquerdo != null) rotorEsquerdo.Rotate(eixoRotacaoHelice, angulo, Space.Self);
            if (rotorDireito != null) rotorDireito.Rotate(eixoRotacaoHelice, -angulo, Space.Self);
        }
    }

    /// <summary>
    /// Inicia a missao completa de suprimento a pedido do Pier ou Navio.
    /// </summary>
    public bool IniciarMissaoSuprimento(Transform navioAlvo, PierMarinha pierDestino = null)
    {
        if (navioAlvo == null || EmMissao) return false;

        alvoAtual = navioAlvo;
        if (pierDestino != null)
        {
            pierOrigem = pierDestino;
        }
        else
        {
            pierOrigem = EncontrarPierMaisProximo(transform.position);
        }

        if (rotinaMissao != null) StopCoroutine(rotinaMissao);
        rotinaMissao = StartCoroutine(RotinaCicloLogistica());
        return true;
    }

    private IEnumerator RotinaCicloLogistica()
    {
        pontoPousoBase = transform.position;

        // 1. Decolagem
        estadoAtual = EstadoMissao.Decolando;
        Vector3 pontoSubida = transform.position + Vector3.up * altitudeCruzeiro;
        yield return MoverPara(pontoSubida, velocidadeSubida);

        // 2. Voo para o Pier (se houver pier valido)
        if (pierOrigem != null)
        {
            estadoAtual = EstadoMissao.VoandoParaPier;
            Vector3 pontoSobrePier = pierOrigem.transform.position + Vector3.up * 25f;
            yield return MoverPara(pontoSobrePier, velocidadeVoo);

            // 3. Pairando 10s no Pier para carregamento de racao
            estadoAtual = EstadoMissao.PairandoNoPierCarregando;
            float tempoInicioHover = Time.time;
            while (Time.time < tempoInicioHover + tempoPairandoPier)
            {
                // Hover suave
                transform.position = pontoSobrePier + new Vector3(0f, Mathf.Sin(Time.time * 2f) * 0.4f, 0f);
                yield return null;
            }
            racaoAtual = capacidadeCargaRacao;
        }

        // 4. Voo em direcao ao navio de destino
        if (alvoAtual != null)
        {
            estadoAtual = EstadoMissao.VoandoParaNavio;
            float distancia = Vector3.Distance(transform.position, alvoAtual.position);
            while (alvoAtual != null && distancia > 35f)
            {
                Vector3 pontoAlvoVoo = alvoAtual.position + Vector3.up * 28f;
                Vector3 direcao = (pontoAlvoVoo - transform.position).normalized;
                transform.position = Vector3.MoveTowards(transform.position, pontoAlvoVoo, velocidadeVoo * Time.deltaTime);

                if (direcao.sqrMagnitude > 0.001f)
                {
                    Quaternion rotAlvo = Quaternion.LookRotation(new Vector3(direcao.x, 0f, direcao.z));
                    transform.rotation = Quaternion.Slerp(transform.rotation, rotAlvo, 3f * Time.deltaTime);
                }

                distancia = Vector3.Distance(transform.position, alvoAtual.position);
                yield return null;
            }

            // 5. Descarregar racao / suprimentos no navio
            estadoAtual = EstadoMissao.DescarregandoNoNavio;
            float tempoEntrega = Time.time;
            while (Time.time < tempoEntrega + tempoPairandoEntrega && alvoAtual != null)
            {
                Vector3 hoverPos = alvoAtual.position + Vector3.up * 22f;
                transform.position = Vector3.Lerp(transform.position, hoverPos, 2f * Time.deltaTime);
                yield return null;
            }

            // Entrega concluida para a tripulacao do navio
            if (alvoAtual != null)
            {
                TripulacaoUnidade tripulacao = alvoAtual.GetComponentInChildren<TripulacaoUnidade>();
                if (tripulacao != null)
                {
                    tripulacao.tripulacaoAtual = Mathf.Min(tripulacao.TripulacaoMaxima, tripulacao.TripulacaoAtual + 20);
                }
            }
            racaoAtual = 0f;
        }

        // 6. Retorno (para o pier de origem ou heliponto de partida)
        estadoAtual = EstadoMissao.RetornandoParaBase;
        Vector3 pontoRetorno = pierOrigem != null ? (pierOrigem.transform.position + Vector3.up * altitudeCruzeiro) : (pontoPousoBase + Vector3.up * altitudeCruzeiro);
        yield return MoverPara(pontoRetorno, velocidadeVoo);

        // 7. Pouso final
        estadoAtual = EstadoMissao.Pousando;
        Vector3 pontoPousoFinal = pierOrigem != null ? (pierOrigem.transform.position + Vector3.up * 3f) : pontoPousoBase;
        yield return MoverPara(pontoPousoFinal, velocidadeSubida);

        estadoAtual = EstadoMissao.PousadoEmEspera;
        rotinaMissao = null;
    }

    private IEnumerator MoverPara(Vector3 destino, float vel)
    {
        while (Vector3.Distance(transform.position, destino) > 1.5f)
        {
            Vector3 dir = (destino - transform.position).normalized;
            transform.position = Vector3.MoveTowards(transform.position, destino, vel * Time.deltaTime);
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion rotAlvo = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, rotAlvo, 3f * Time.deltaTime);
            }
            yield return null;
        }
    }

    public static PierMarinha EncontrarPierMaisProximo(Vector3 posicao)
    {
        PierMarinha[] piers = FindObjectsByType<PierMarinha>(FindObjectsSortMode.None);
        PierMarinha maisProximo = null;
        float menorDistSqr = float.MaxValue;
        for (int i = 0; i < piers.Length; i++)
        {
            if (piers[i] == null) continue;
            float dSqr = (piers[i].transform.position - posicao).sqrMagnitude;
            if (dSqr < menorDistSqr)
            {
                menorDistSqr = dSqr;
                maisProximo = piers[i];
            }
        }
        return maisProximo;
    }
}
