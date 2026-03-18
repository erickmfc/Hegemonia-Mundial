using UnityEngine;

/// <summary>
/// Sistema de Imóveis — Casas, Prédios e Apartamentos.
/// 
/// ╔═══════════════════════════════════════════════════════════════╗
/// ║  COMO FUNCIONA:                                              ║
/// ║  1. Cada imóvel tem uma CAPACIDADE de moradores              ║
/// ║  2. Moradores chegam gradualmente (não todos de uma vez)     ║
/// ║  3. Cada morador = +1 populacaoAtual no GerenciadorRecursos  ║
/// ║  4. Moradores geram renda (impostos) por segundo             ║
/// ║  5. Moradores podem ir embora se qualidade de vida cair      ║
/// ║  6. Ao destruir o imóvel, moradores são removidos            ║
/// ╚═══════════════════════════════════════════════════════════════╝
/// </summary>
public class Imovel : MonoBehaviour
{
    [Header("🏠 Configuração do Imóvel")]
    [Tooltip("Quantidade máxima de moradores que cabem neste imóvel")]
    public int capacidade = 10;

    // ═══════════════════════════════════════════════════════════════
    // VALORES INTERNOS (o jogo calcula sozinho)
    // ═══════════════════════════════════════════════════════════════
    private int moradoresAtuais = 0;
    private float rendaTotal = 0f;
    private int qualidadeAtual = 50;

    // Constantes internas — o jogo controla
    private const int MORADORES_POR_CICLO = 2;
    private const float INTERVALO_CICLO = 5f;
    private const float RENDA_POR_MORADOR = 0.5f;
    private const int QUALIDADE_BASE = 50;
    private const int QUALIDADE_MINIMA = 20;

    // Controle interno
    private float timerCiclo = 0f;
    private float timerRenda = 0f;
    private bool registrado = false;
    private int limitePopulacaoAdicionado = 0;
    private float rendaRegistradaNoSistema = 0f;

    // ═══════════════════════════════════════════════════════════════
    // PROPRIEDADES PÚBLICAS (para outros scripts lerem)
    // ═══════════════════════════════════════════════════════════════

    public int MoradoresAtuais => moradoresAtuais;
    public int Capacidade => capacidade;
    public int VagasLivres => capacidade - moradoresAtuais;
    public bool Lotado => moradoresAtuais >= capacidade;
    public float TaxaOcupacao => capacidade > 0 ? (float)moradoresAtuais / capacidade : 0f;
    public float RendaAtual => rendaTotal;
    public int QualidadeAtual => qualidadeAtual;

    // ═══════════════════════════════════════════════════════════════
    // INICIALIZAÇÃO
    // ═══════════════════════════════════════════════════════════════

    void Start()
    {
        qualidadeAtual = QUALIDADE_BASE;

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null)
        {
            Debug.LogError($"[Imóvel] {name}: GerenciadorRecursos não encontrado!");
            return;
        }

        // Cada imóvel adiciona sua capacidade ao teto máximo de população
        recursos.AumentarLimitePopulacao(capacidade);
        limitePopulacaoAdicionado = capacidade;
        registrado = true;

        // Timer randômico para não sincronizar todos os imóveis
        timerCiclo = Random.Range(0f, INTERVALO_CICLO);

        Debug.Log($"[Imovel] {name} construido! Capacidade: {capacidade}");
    }

    // ═══════════════════════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════════════════════

    void Update()
    {
        if (!registrado) return;

        // Ciclo de moradores
        timerCiclo += Time.deltaTime;
        if (timerCiclo >= INTERVALO_CICLO)
        {
            ProcessarCicloMoradores();
            timerCiclo = 0f;
        }

        // Renda (a cada segundo)
        if (moradoresAtuais > 0)
        {
            timerRenda += Time.deltaTime;
            if (timerRenda >= 1f)
            {
                timerRenda = 0f;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // LÓGICA DE MORADORES
    // ═══════════════════════════════════════════════════════════════

    void ProcessarCicloMoradores()
    {
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null) return;

        if (qualidadeAtual >= QUALIDADE_MINIMA)
            ChegadaDeMoradores(recursos);
        else
            SaidaDeMoradores(recursos);
    }

    void ChegadaDeMoradores(GerenciadorRecursos recursos)
    {
        if (Lotado) return;

        int querVir = Mathf.Min(MORADORES_POR_CICLO, VagasLivres);
        int aceitos = 0;

        for (int i = 0; i < querVir; i++)
        {
            if (recursos.AdicionarPopulacao(1))
                aceitos++;
            else
                break;
        }

        if (aceitos > 0)
        {
            moradoresAtuais += aceitos;
            AtualizarRenda();
        }
    }

    void SaidaDeMoradores(GerenciadorRecursos recursos)
    {
        if (moradoresAtuais <= 0) return;

        float fatorFuga = 1f - ((float)qualidadeAtual / QUALIDADE_MINIMA);
        int querSair = Mathf.Max(1, Mathf.RoundToInt(MORADORES_POR_CICLO * fatorFuga));
        querSair = Mathf.Min(querSair, moradoresAtuais);

        moradoresAtuais -= querSair;
        recursos.RemoverPopulacao(querSair);
        AtualizarRenda();

        Debug.Log($"[Imovel] {name}: -{querSair} moradores fugiram! Qualidade: {qualidadeAtual} ({moradoresAtuais}/{capacidade})");
    }

    // ═══════════════════════════════════════════════════════════════
    // RENDA
    // ═══════════════════════════════════════════════════════════════

    void AtualizarRenda()
    {
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null) return;

        // Remove renda antiga
        if (rendaRegistradaNoSistema > 0)
            recursos.ModificarGanhos(multDinheiro: -rendaRegistradaNoSistema);

        // Calcula e registra nova renda
        rendaTotal = moradoresAtuais * RENDA_POR_MORADOR;

        if (rendaTotal > 0)
            recursos.ModificarGanhos(multDinheiro: rendaTotal);

        rendaRegistradaNoSistema = rendaTotal;
    }

    // ═══════════════════════════════════════════════════════════════
    // QUALIDADE DE VIDA (API para futuro)
    // ═══════════════════════════════════════════════════════════════

    public void ModificarQualidade(int delta)
    {
        qualidadeAtual = Mathf.Clamp(qualidadeAtual + delta, 0, 100);
    }

    public void SetarQualidade(int novaQualidade)
    {
        qualidadeAtual = Mathf.Clamp(novaQualidade, 0, 100);
    }

    public void EvacuarTodos()
    {
        if (moradoresAtuais <= 0) return;

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos != null)
            recursos.RemoverPopulacao(moradoresAtuais);

        moradoresAtuais = 0;
        AtualizarRenda();
    }

    // ═══════════════════════════════════════════════════════════════
    // DESTRUIÇÃO
    // ═══════════════════════════════════════════════════════════════

    void OnDestroy()
    {
        if (!registrado) return;

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null) return;

        if (moradoresAtuais > 0)
            recursos.RemoverPopulacao(moradoresAtuais);

        if (rendaRegistradaNoSistema > 0)
            recursos.ModificarGanhos(multDinheiro: -rendaRegistradaNoSistema);

        if (limitePopulacaoAdicionado > 0)
            recursos.AumentarLimitePopulacao(-limitePopulacaoAdicionado);
    }
}
