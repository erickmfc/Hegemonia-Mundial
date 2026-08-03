using UnityEngine;

/// <summary>
/// Cobra recursos operacionais em tempo real e manutencao financeira no avanco
/// de cada dia do jogo. A manutencao monetaria fica centralizada aqui para nao
/// competir com o relatorio economico.
/// </summary>
public class GestorDeConsumo : MonoBehaviour
{
    public static GestorDeConsumo Instancia { get; private set; }

    [Header("Custos legados de recursos por segundo")]
    public int custoInfantariaDinheiro = 120;
    public int custoVeiculoPetroleo = 1;
    public int custoVeiculoPeca = 1;
    public int custoNavalPetroleo = 2;
    public int custoNavalDinheiro = 10;
    public int custoAereoPetroleo = 4;
    public int custoAereoDinheiro = 5;
    public int custoEstruturaEnergia = 2;
    public int custoExtraCasaEnergia = 3;
    public int custoExtraPesquisaMilitarEnergia = 10;

    [Header("Combustivel")]
    public bool usarCombustivelPorUnidade = true;

    [Header("Status")]
    public long totalConsumoDinheiro;
    public int totalConsumoPetroleo;
    public int totalConsumoAco;
    public int totalConsumoEnergia;

    private float timer;
    private int ultimoDiaCobrado = -1;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        GerenciadorTempo.GarantirInstancia();
        if (GerenciadorTempo.Instancia != null)
            GerenciadorTempo.Instancia.OnDataAlterada += AoMudarData;
    }

    private void OnDestroy()
    {
        if (GerenciadorTempo.Instancia != null)
            GerenciadorTempo.Instancia.OnDataAlterada -= AoMudarData;
        if (Instancia == this) Instancia = null;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < 1f) return;
        timer = 0f;
        CalcularECobrarRecursosOperacionais();
    }

    private void AoMudarData()
    {
        if (GerenciadorTempo.Instancia == null) return;
        int diaAtual = GerenciadorTempo.Instancia.totalDias;
        if (diaAtual <= ultimoDiaCobrado) return;
        ultimoDiaCobrado = diaAtual;
        CalcularManutencaoMonetariaDiaria();
    }

    private void CalcularECobrarRecursosOperacionais()
    {
        if (CensoImperial.Instancia == null || GerenciadorRecursos.Instancia == null) return;

        CensoImperial censo = CensoImperial.Instancia;
        GerenciadorRecursos banco = GerenciadorRecursos.Instancia;
        totalConsumoPetroleo = 0;
        totalConsumoAco = 0;
        totalConsumoEnergia = 0;

        if (!usarCombustivelPorUnidade)
        {
            totalConsumoPetroleo += censo.veiculos * custoVeiculoPetroleo;
            totalConsumoPetroleo += censo.naval * custoNavalPetroleo;
            totalConsumoPetroleo += censo.aereo * custoAereoPetroleo;
        }
        totalConsumoAco += censo.veiculos * custoVeiculoPeca;

        if (totalConsumoPetroleo > 0) banco.RemoverRecurso("Petroleo", totalConsumoPetroleo);
        if (totalConsumoAco > 0) banco.RemoverRecurso("Aco", totalConsumoAco);
        if (totalConsumoEnergia > 0) banco.RemoverRecurso("Energia", totalConsumoEnergia);
    }

    private void CalcularManutencaoMonetariaDiaria()
    {
        GerenciadorRecursos banco = GerenciadorRecursos.Instancia;
        if (banco == null) return;

        long total = 0L;
        int teamJogador = SistemaGovernoMundial.Instancia != null
            ? SistemaGovernoMundial.Instancia.teamJogador
            : 1;
        IdentidadeUnidade[] unidades = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        for (int i = 0; i < unidades.Length; i++)
        {
            IdentidadeUnidade unidade = unidades[i];
            if (unidade == null || unidade.teamID != teamJogador || !unidade.gameObject.activeInHierarchy) continue;
            if (unidade.tipoUnidade == TipoUnidade.Estrutura)
            {
                EstruturaEconomica estrutura = unidade.GetComponent<EstruturaEconomica>();
                if (estrutura != null && !estrutura.Ativa) continue;
            }

            long custo = ValoresDefinitivosHegemonia.ObterManutencaoPorDia(null, unidade.gameObject.name);
            if (custo <= 0L)
            {
                switch (unidade.tipoUnidade)
                {
                    case TipoUnidade.Infantaria: custo = 120L; break;
                    case TipoUnidade.Naval: custo = 120000L; break;
                    case TipoUnidade.Aereo: custo = 80000L; break;
                    default: custo = 60000L; break;
                }
            }
            total += custo;
        }

        PerfilDificuldadeJogo perfil = GameDifficultyManager.Instancia != null
            ? GameDifficultyManager.PerfilAtual
            : null;
        float multiplicador = perfil != null ? perfil.MultiplicadorManutencao : 1f;
        totalConsumoDinheiro = (long)System.Math.Round(total * multiplicador, System.MidpointRounding.AwayFromZero);
        if (totalConsumoDinheiro > 0L)
            banco.RemoverRecurso("Dinheiro", totalConsumoDinheiro);
    }
}
