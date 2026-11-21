using UnityEngine;

/// <summary>
/// Opera um cruzeiro civil em ciclos de viagem e registra licencas turísticas
/// no tesouro nacional ao concluir cada rota.
/// </summary>
[DisallowMultipleComponent]
public sealed class NavioCruzeiroTuristico : MonoBehaviour
{
    [Header("Operacao de viagem")]
    [Min(1)] public int capacidadePassageiros = 3000;
    [Min(1f)] public float duracaoViagemSegundos = 180f;
    [Min(1f)] public float tempoEmPortoSegundos = 30f;
    [Min(1)] public int teamId = 1;
    public bool operacaoAtiva = true;

    [Header("Impacto turistico")]
    [Range(0f, 1f)] public float demandaBase = 0.55f;
    [Range(0.05f, 0.07f)] public float ganhoFelicidadePercentual = 0.06f;
    [Min(15000000L)] public long receitaMinimaPorViagem = 15000000L;
    [Min(15000000L)] public long receitaMaximaPorViagem = 55000000L;

    [SerializeField] private bool emViagem;
    [SerializeField] private float tempoRestante;
    [SerializeField] private float demandaAtual;
    [SerializeField] private long ultimaReceitaLicencas;
    [SerializeField] private int viagensConcluidas;

    private float proximaAmostraDemanda;
    private float proximaPartida;

    public bool EmViagem => emViagem;
    public float DemandaAtual => demandaAtual;
    public int CapacidadePassageiros => capacidadePassageiros;
    public long UltimaReceitaLicencas => ultimaReceitaLicencas;
    public int ViagensConcluidas => viagensConcluidas;
    public int TeamId => ResolverTeamId();
    public int PassageirosAtuais => Mathf.RoundToInt(capacidadePassageiros * demandaAtual);

    private void Awake()
    {
        demandaAtual = Mathf.Clamp(demandaBase * Random.Range(0.75f, 1.25f), 0.1f, 1f);
        proximaPartida = Time.unscaledTime + Random.Range(1f, Mathf.Max(2f, tempoEmPortoSegundos));
    }

    private void Update()
    {
        if (!operacaoAtiva || Hegemonia.RTS.RTSSimulationClock.SimulacaoPausada) return;

        if (Time.unscaledTime >= proximaAmostraDemanda)
        {
            proximaAmostraDemanda = Time.unscaledTime + Random.Range(20f, 45f);
            AtualizarDemanda();
        }

        if (!emViagem)
        {
            if (Time.unscaledTime >= proximaPartida && PassageirosAtuais > 0)
                IniciarViagem();
            return;
        }

        tempoRestante -= Time.unscaledDeltaTime;
        if (tempoRestante <= 0f) ConcluirViagem();
    }

    public bool IniciarViagem()
    {
        if (!operacaoAtiva || emViagem || PassageirosAtuais <= 0) return false;
        emViagem = true;
        tempoRestante = Mathf.Max(1f, duracaoViagemSegundos);
        return true;
    }

    public void PausarOperacao(bool pausada)
    {
        operacaoAtiva = !pausada;
        if (operacaoAtiva && !emViagem)
            proximaPartida = Time.unscaledTime + Mathf.Max(1f, tempoEmPortoSegundos);
    }

    private void AtualizarDemanda()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(TeamId) : null;
        float turismoNacional = pais != null ? Mathf.Clamp01(pais.indiceAtratividade) : 0.5f;
        float estabilidade = pais != null ? Mathf.Clamp01(pais.estabilidade / 100f) : 0.6f;
        float felicidade = pais != null ? Mathf.Clamp01(pais.felicidade / 100f) : 0.6f;
        float alvo = demandaBase * Random.Range(0.65f, 1.35f)
            * (0.55f + turismoNacional * 0.25f + estabilidade * 0.1f + felicidade * 0.1f);
        demandaAtual = Mathf.Clamp(Mathf.Lerp(demandaAtual, alvo, 0.45f), 0.1f, 1f);
    }

    private void ConcluirViagem()
    {
        emViagem = false;
        viagensConcluidas++;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(TeamId) : null;
        if (pais != null)
        {
            // A frota aumenta a base, mas cada navio distribui a faixa de
            // 15–55 milhões por viagem conforme ocupacao e turismo.
            NavioCruzeiroTuristico[] cruzeiros = FindObjectsByType<NavioCruzeiroTuristico>(FindObjectsSortMode.None);
            int frota = 0;
            for (int i = 0; i < cruzeiros.Length; i++)
            {
                if (cruzeiros[i] != null && cruzeiros[i].TeamId == TeamId) frota++;
            }
            frota = Mathf.Max(1, frota);
            float escalaFrota = Mathf.Clamp(0.75f + 0.25f * Mathf.Sqrt(frota), 0.75f, 1.5f);
            float faixa = Mathf.Lerp(receitaMinimaPorViagem, receitaMaximaPorViagem, demandaAtual);
            ultimaReceitaLicencas = (long)(faixa * escalaFrota);
            ultimaReceitaLicencas = (long)Mathf.Clamp((float)ultimaReceitaLicencas,
                receitaMinimaPorViagem, receitaMaximaPorViagem);
            governo.AdicionarSaldo(TeamId, ultimaReceitaLicencas, "Licencas de turismo de cruzeiro",
                "Viagem internacional de cruzeiro", TipoLancamentoFinanceiro.ReceitaTransacao);
            float ganho = Mathf.Clamp(ganhoFelicidadePercentual, 0.05f, 0.07f) * 100f;
            pais.felicidade = Mathf.Clamp(pais.felicidade + ganho, 0f, 100f);
        }
        demandaAtual = Mathf.Clamp(demandaAtual + Random.Range(-0.12f, 0.12f), 0.1f, 1f);
        proximaPartida = Time.unscaledTime + Mathf.Max(1f, tempoEmPortoSegundos);
    }

    private int ResolverTeamId()
    {
        IdentidadeUnidade unidade = GetComponentInParent<IdentidadeUnidade>();
        if (unidade != null && unidade.teamID > 0) return unidade.teamID;
        IdentidadeIA ia = GetComponentInParent<IdentidadeIA>();
        if (ia != null && ia.teamID > 0) return ia.teamID;
        return SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.teamJogador : Mathf.Max(1, teamId);
    }
}
