using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ======================================================================
// PISTA COMERCIAL - Dados de cada pista de pouso/decolagem
// ======================================================================
[System.Serializable]
public class PistaComercial
{
    public string nomePista;
    public Transform pista;       // Grupo de waypoints da pista
    public Transform taxiEntrada; // Saída do hangar → pista
    public Transform taxiSaida;   // Pista → hangar

    [HideInInspector] public List<Transform> waypointsDecolagem  = new List<Transform>();
    [HideInInspector] public List<Transform> waypointsDecida     = new List<Transform>();
    [HideInInspector] public List<Transform> waypointsTaxiEntrada= new List<Transform>();
    [HideInInspector] public List<Transform> waypointsTaxiSaida  = new List<Transform>();

    public enum EstadoPista { Livre, EmDecolagem, EmPouso }
    [HideInInspector] public EstadoPista estado = EstadoPista.Livre;
    [HideInInspector] public ControleAviaoComercial aviaoNaPista;

    public bool ocupada => estado != EstadoPista.Livre;

    public void Inicializar()
    {
        if (pista != null)
        {
            foreach (Transform t in pista) waypointsDecolagem.Add(t);
            waypointsDecida.AddRange(waypointsDecolagem);
            waypointsDecida.Reverse();
        }
        if (taxiEntrada != null) foreach (Transform t in taxiEntrada) waypointsTaxiEntrada.Add(t);
        if (taxiSaida   != null) foreach (Transform t in taxiSaida)   waypointsTaxiSaida.Add(t);
    }

    public void Liberar()
    {
        estado = EstadoPista.Livre;
        aviaoNaPista = null;
    }
}

// ======================================================================
// STATUS DE VOO
// ======================================================================
public enum StatusVoo
{
    VendasAbertas,
    VendasFechadas,
    Embarcando,
    NoHorario,
    Atrasado,
    Cancelado,
    EmVoo,
    Chegando,
    Pousou
}

// ======================================================================
// VOO AGENDADO - Registro completo de cada voo
// ======================================================================
[System.Serializable]
public class VooAgendado
{
    public string numeroVoo;
    public string nomeCompanhia;
    public string destino;
    public bool ehChegada;           // true = incoming, false = outgoing
    public int vagaIndex;            // índice na lista waypointsPatio
    public int passagensVendidas;
    public float horarioPartidaJogo; // Time.time em que parte/chegou
    public float horarioAgendado;    // Time.time previsto (pode ser atrasado)
    public StatusVoo status;
    public ControleAviaoComercial aviao;
    public int passageirosTuristas;  // turistas (temporários)
    public int passageirosImigrantes; // imigrantes (permanentes)
    public float timerTuristas;      // tempo restante dos turistas na cidade

    // Formata horário de partida em HH:MM baseado em Time.time
    public string HorarioFormatado()
    {
        int totalMin = Mathf.FloorToInt(horarioAgendado / 60f) % (24 * 60);
        int h = (totalMin / 60) % 24;
        int m = totalMin % 60;
        return $"{h:D2}:{m:D2}";
    }

    public string StatusTexto()
    {
        switch (status)
        {
            case StatusVoo.VendasAbertas:    return "<color=#00FF88>✈ VENDAS ABERTAS</color>";
            case StatusVoo.VendasFechadas:   return "<color=#AAAAAA>🔒 VENDAS FECHADAS</color>";
            case StatusVoo.Embarcando:       return "<color=#FFD700>🚶 EMBARCANDO</color>";
            case StatusVoo.NoHorario:        return "<color=#00CCFF>⏰ NO HORÁRIO</color>";
            case StatusVoo.Atrasado:         return "<color=#FF6600>⚠ ATRASADO</color>";
            case StatusVoo.Cancelado:        return "<color=#FF3333>✖ CANCELADO</color>";
            case StatusVoo.EmVoo:            return "<color=#00FFFF>🛫 EM VOO</color>";
            case StatusVoo.Chegando:         return "<color=#88FF00>🛬 CHEGANDO</color>";
            case StatusVoo.Pousou:           return "<color=#FFFFFF>🏁 POUSOU</color>";
        }
        return "—";
    }
}

// ======================================================================
// GERENCIADOR AEROPORTO COMERCIAL
// ======================================================================
public class GerenciadorAeroportoComercial : GerenciadorAeroporto
{
    // ── Contratos ──────────────────────────────────────────────────────
    [System.Serializable]
    public class ContratoAereoAtivo
    {
        public string nomeCompanhia;
        public int diasDuracao;
        public int diasRestantes;
        public int baiasOcupadas;
        public int valorPorPassagem;
        public float demandaTurismoBase;
        public int passagensVendidasHoje;
        public List<int> indicasVagas = new List<int>(); // quais vagas estão alocadas
    }

    [System.Serializable]
    public class ContratoAereoOferecido
    {
        public string nomeCompanhia;
        public int diasDuracao;
        public int baiasExigidas;
        public int baiasNegociadas;
        public int valorPorPassagem;
        public float demandaTurismoBase;
    }

    // ── Pistas ─────────────────────────────────────────────────────────
    [Header("=== Pistas de Pouso/Decolagem ===")]
    public PistaComercial pista1 = new PistaComercial { nomePista = "Pista 1" };
    public PistaComercial pista2 = new PistaComercial { nomePista = "Pista 2" };
    public PistaComercial pista3 = new PistaComercial { nomePista = "Pista 3" };
    private List<PistaComercial> todasPistas = new List<PistaComercial>();

    // Filas de espera por pista
    private Queue<ControleAviaoComercial> filaDecolagem = new Queue<ControleAviaoComercial>();
    private Queue<ControleAviaoComercial> filaPouso     = new Queue<ControleAviaoComercial>();

    // ── Contratos e Vagas ──────────────────────────────────────────────
    [Header("=== Sistema de Aviação Comercial ===")]
    public int totalBaiasComerciais = 60;
    public List<ContratoAereoAtivo>   contratosAtivos     = new List<ContratoAereoAtivo>();
    public List<ContratoAereoOferecido> contratosDisponiveis = new List<ContratoAereoOferecido>();

    // Vagas reservadas: índice da vaga → contrato
    private Dictionary<int, ContratoAereoAtivo> vagasReservadas = new Dictionary<int, ContratoAereoAtivo>();

    // ── Voos ───────────────────────────────────────────────────────────
    [Header("=== Controle de Voos ===")]
    public List<VooAgendado> paineisVoos = new List<VooAgendado>();

    private float timerDiaComercial       = 0f;
    private float tempoUltimoTickEconomia = 0f;
    private int   estatisticaTurismoDia   = 0;
    private int   estatisticaPassagensVendidasDia = 0;
    private int   paisesConectados        = 0;

    // ── Spawn ──────────────────────────────────────────────────────────
    [Header("=== Spawn Visual ===")]
    public GameObject prefabAviaoComercial;
    private float tempoUltimoSpawnComercial = 0f;
    private List<ControleAviaoComercial> frotaComercialAtiva = new List<ControleAviaoComercial>();

    // ── Turismo / Imigração ────────────────────────────────────────────
    private int   totalTuristasTemporarios = 0;
    private int   totalImigrantesNovos     = 0;
    private float timerRetornoTuristas     = 0f;
    private const float INTERVALO_RETORNO_TURISTAS = 30f; // a cada 30s processa retorno

    // ── UI ─────────────────────────────────────────────────────────────
    private Vector2 scrollVoos;
    private Vector2 scrollContratos;
    private int abaMenuComercial = 0; // 0=Painel, 1=Contratos, 2=Estatísticas

    // Gerador de números de voo
    private int contadorVoo = 100;

    // ── Inicialização ─────────────────────────────────────────────────
    protected override void Awake()
    {
        // Só tenta popular pelo 'patio' se o usuário não atribuiu vagas manualmente
        if (patio != null && waypointsPatio.Count == 0)
        {
            foreach (Transform filho in patio)
            {
                if (filho != null && !waypointsPatio.Contains(filho))
                    waypointsPatio.Add(filho);
            }
        }

        base.Awake();

        if (prefabAviaoComercial == null)
            Debug.LogError("[Comercial] ERRO: prefabAviaoComercial NÃO ESTÁ ASSOCIADO no Inspector! Aviões não irão spawnar.");
        if (waypointsPatio.Count == 0)
            Debug.LogError("[Comercial] ERRO: Nenhuma vaga encontrada no patio! Associe o objeto com os creates de vagas no campo 'patio'.");

        // Remove vagas auto-geradas pela base (Vaga_Auto_*) que ficam fora do aeroporto comercial.
        // A base gera vagas em círculo se houver menos de 24, mas o comercial só tem 17.
        for (int i = waypointsPatio.Count - 1; i >= 0; i--)
        {
            Transform wp = waypointsPatio[i];
            if (wp != null && wp.name.StartsWith("Vaga_Auto_"))
            {
                Destroy(wp.gameObject);
                waypointsPatio.RemoveAt(i);
            }
        }

        todasPistas.Add(pista1);
        todasPistas.Add(pista2);
        todasPistas.Add(pista3);
        foreach (var p in todasPistas) p.Inicializar();

        if (contratosDisponiveis.Count == 0) GerarNovosContratos();
    }

    // ── Update principal ──────────────────────────────────────────────
    protected override void Update()
    {
        base.Update();
        ProcessarEconomia();
        ProcessarRetornoTuristas();
        ProcessarFilasRunway();
    }

    // ======================================================================
    // SOLICITAÇÃO DE PISTA — chamada pelo ControleAviaoComercial
    // ======================================================================
    public PistaComercial SolicitarPistaParaDecolagem(ControleAviaoComercial aviao)
    {
        // Prefere pista livre; evita conflito com pouso
        foreach (var p in todasPistas)
        {
            if (p.estado == PistaComercial.EstadoPista.Livre && p.waypointsDecolagem.Count > 0)
            {
                p.estado = PistaComercial.EstadoPista.EmDecolagem;
                p.aviaoNaPista = aviao;
                return p;
            }
        }
        return null; // Nenhuma livre agora — avião vai para fila
    }

    public PistaComercial SolicitarPistaParaPouso(ControleAviaoComercial aviao)
    {
        // Aceita pista livre OU pista que já está em decolagem (sentido oposto é ok em pistas separadas)
        foreach (var p in todasPistas)
        {
            if (p.estado == PistaComercial.EstadoPista.Livre && p.waypointsDecida.Count > 0)
            {
                p.estado = PistaComercial.EstadoPista.EmPouso;
                p.aviaoNaPista = aviao;
                return p;
            }
        }
        // Fallback de emergência: pista 1
        pista1.estado = PistaComercial.EstadoPista.EmPouso;
        pista1.aviaoNaPista = aviao;
        return pista1;
    }

    // Mantém compatibilidade com código antigo
    public PistaComercial SolicitarPistaLivre(ControleAviaoComercial aviao, bool forcarParaPouso = false)
    {
        return forcarParaPouso ? SolicitarPistaParaPouso(aviao) : SolicitarPistaParaDecolagem(aviao);
    }

    public void LiberarPista(PistaComercial pista, ControleAviaoComercial aviao)
    {
        if (pista != null && pista.aviaoNaPista == aviao)
            pista.Liberar();

        // Processa fila após liberar
        ProcessarFilasRunway();
    }

    // ── Processa filas de decolagem/pouso ─────────────────────────────
    private void ProcessarFilasRunway()
    {
        // Tenta despachar aviões esperando para decolar
        while (filaDecolagem.Count > 0)
        {
            var aviao = filaDecolagem.Peek();
            if (aviao == null) { filaDecolagem.Dequeue(); continue; }
            var pista = SolicitarPistaParaDecolagem(aviao);
            if (pista == null) break;
            filaDecolagem.Dequeue();
            aviao.AtribuirPistaEDecolar(pista);
        }

        // Tenta despachar aviões esperando para pousar
        while (filaPouso.Count > 0)
        {
            var aviao = filaPouso.Peek();
            if (aviao == null) { filaPouso.Dequeue(); continue; }
            var pista = SolicitarPistaParaPouso(aviao);
            if (pista == null) break;
            filaPouso.Dequeue();
            aviao.AtribuirPistaEPousar(pista);
        }
    }

    public void EntrarNaFilaDecolagem(ControleAviaoComercial aviao)
    {
        if (!filaDecolagem.Contains(aviao))
            filaDecolagem.Enqueue(aviao);
    }

    public void EntrarNaFilaPouso(ControleAviaoComercial aviao)
    {
        if (!filaPouso.Contains(aviao))
            filaPouso.Enqueue(aviao);
    }

    // ======================================================================
    // GESTÃO DE VAGAS E CONTRATOS
    // ======================================================================
    /// <summary>
    /// Ao assinar um contrato, reserva vagas específicas para a companhia
    /// e spawna os aviões nessas vagas imediatamente.
    /// </summary>
    private void AlocarVagasParaContrato(ContratoAereoAtivo contrato)
    {
        if (prefabAviaoComercial == null)
        {
            Debug.LogError($"[Comercial] Falha ao alocar vagas para {contrato.nomeCompanhia}: prefabAviaoComercial é NULL.");
            return;
        }

        if (waypointsPatio.Count == 0)
        {
            Debug.LogError($"[Comercial] Falha ao alocar vagas para {contrato.nomeCompanhia}: waypointsPatio está VAZIO. Verifique o campo 'patio'.");
            return;
        }

        int vagasAlocar = Mathf.Min(contrato.baiasOcupadas, waypointsPatio.Count);

        for (int i = 0; i < waypointsPatio.Count && contrato.indicasVagas.Count < vagasAlocar; i++)
        {
            if (!vagasReservadas.ContainsKey(i))
            {
                vagasReservadas[i] = contrato;
                contrato.indicasVagas.Add(i);
                SpawnarAviaoNaVaga(i, contrato);
            }
        }
    }

    private void SpawnarAviaoNaVaga(int indiceVaga, ContratoAereoAtivo contrato)
    {
        if (indiceVaga < 0 || indiceVaga >= waypointsPatio.Count) return;

        Transform vaga = waypointsPatio[indiceVaga];
        if (vaga == null) return;

        Vector3 posSpawn = vaga.position;
        float altEstac = 0.5f; // altura padrão até o avião se auto-ajustar no Start
        posSpawn.y += altEstac;

        GameObject obj = Instantiate(prefabAviaoComercial, posSpawn, vaga.rotation);
        
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        ControleAviaoComercial ca = obj.GetComponent<ControleAviaoComercial>();
        if (ca == null) ca = obj.AddComponent<ControleAviaoComercial>();

        ca.estaEmModoVooFisico = false;
        ca.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;

        ca.aeroportoOrigemComercial = this;
        ca.aeroportoOrigem   = this;
        ca.vagaRetorno       = vaga;
        ca.nomeCompanhia     = contrato.nomeCompanhia;
        ca.passagensVendidas = Random.Range(
            (int)(contrato.passagensVendidasHoje * 0.1f),
            (int)(contrato.passagensVendidasHoje * 0.5f) + 10);

        if (!avioesNoPatio.Contains(ca)) avioesNoPatio.Add(ca);
        frotaComercialAtiva.Add(ca);

        // Cria voo agendado para este avião
        VooAgendado voo = CriarVooSaida(ca, contrato, indiceVaga);
        ca.vooAssociado = voo;

        StartCoroutine(RotinaEstacionarAviao(ca));
    }

    private IEnumerator RotinaEstacionarAviao(ControleAviaoComercial aviao)
    {
        if (aviao == null) yield break;

        Transform vaga = aviao.vagaRetorno;
        if (vaga != null)
        {
            float alt = aviao.ObterAlturaEstacionamento();
            // Posiciona e parenteia à vaga imediatamente para garantir que fique fixo nela
            aviao.transform.SetParent(vaga, false);
            aviao.transform.localPosition = new Vector3(0f, alt, 0f);
            aviao.transform.localRotation = Quaternion.identity;
        }

        aviao.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;
    }

    // ── Voos ──────────────────────────────────────────────────────────
    private VooAgendado CriarVooSaida(ControleAviaoComercial aviao, ContratoAereoAtivo contrato, int indiceVaga)
    {
        VooAgendado voo = new VooAgendado();
        voo.numeroVoo    = $"{contrato.nomeCompanhia.Substring(0, Mathf.Min(2, contrato.nomeCompanhia.Length)).ToUpper()}{contadorVoo++:D3}";
        voo.nomeCompanhia = contrato.nomeCompanhia;
        voo.ehChegada    = false;
        voo.vagaIndex    = indiceVaga;
        voo.aviao        = aviao;
        voo.passagensVendidas = aviao.passagensVendidas;
        voo.status       = StatusVoo.VendasAbertas;
        // Horário de partida: entre 5 e 60 minutos de jogo
        voo.horarioAgendado = Time.time + Random.Range(5f * 60f, 45f * 60f);
        voo.destino      = "Exterior";

        // Chance de atraso ou cancelamento
        float roll = Random.value;
        if (roll < 0.05f)      voo.status = StatusVoo.Cancelado;
        else if (roll < 0.18f) { voo.status = StatusVoo.Atrasado; voo.horarioAgendado += Random.Range(3f * 60f, 15f * 60f); }

        if (!paineisVoos.Contains(voo)) paineisVoos.Add(voo);
        return voo;
    }

    private VooAgendado CriarVooChegada(string companhia, int passageiros)
    {
        VooAgendado voo = new VooAgendado();
        voo.numeroVoo    = $"CH{contadorVoo++:D3}";
        voo.nomeCompanhia = companhia;
        voo.ehChegada    = true;
        voo.vagaIndex    = -1;
        voo.passagensVendidas = passageiros;
        voo.status       = StatusVoo.Chegando;
        voo.horarioAgendado = Time.time + Random.Range(2f * 60f, 20f * 60f);
        voo.destino      = "Local";
        voo.passageirosTuristas   = Mathf.RoundToInt(passageiros * Random.Range(0.6f, 0.85f));
        voo.passageirosImigrantes = passageiros - voo.passageirosTuristas;
        voo.timerTuristas = Random.Range(60f, 300f); // 1-5 min de jogo

        if (!paineisVoos.Contains(voo)) paineisVoos.Add(voo);
        return voo;
    }

    // ── Processa voos agendados ────────────────────────────────────────
    private void ProcessarVoosAgendados()
    {
        for (int i = paineisVoos.Count - 1; i >= 0; i--)
        {
            VooAgendado voo = paineisVoos[i];
            if (voo == null) { paineisVoos.RemoveAt(i); continue; }

            // Atualiza status de saída baseado no horário
            if (!voo.ehChegada && voo.status != StatusVoo.Cancelado
                && voo.status != StatusVoo.EmVoo && voo.status != StatusVoo.Pousou)
            {
                float restante = voo.horarioAgendado - Time.time;

                if (voo.aviao == null || voo.aviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
                {
                    voo.status = StatusVoo.EmVoo;
                    continue;
                }

                if (restante > 15f * 60f)       voo.status = StatusVoo.VendasAbertas;
                else if (restante > 5f * 60f)   voo.status = StatusVoo.VendasFechadas;
                else if (restante > 1f * 60f)   voo.status = StatusVoo.Embarcando;
                else if (restante > 0f)          voo.status = voo.status == StatusVoo.Atrasado ? StatusVoo.Atrasado : StatusVoo.NoHorario;
                else if (restante <= 0f && voo.aviao != null && voo.aviao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
                {
                    // Hora de partir!
                    if (voo.status != StatusVoo.Cancelado)
                        voo.aviao.SolicitarDecolagem();
                }
            }
            // Chegadas
            if (voo.ehChegada && voo.status != StatusVoo.Cancelado && voo.status != StatusVoo.Pousou && voo.status != StatusVoo.VendasFechadas)
            {
                float restante = voo.horarioAgendado - Time.time;
                
                if (voo.aviao != null)
                {
                    // O avião já existe e está pousando
                    if (voo.aviao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
                        voo.status = StatusVoo.Pousou;
                    else
                        voo.status = StatusVoo.Chegando;
                }
                else
                {
                    if (restante > 5f * 60f) voo.status = StatusVoo.VendasAbertas;
                    else if (restante > 1f * 60f) voo.status = StatusVoo.Embarcando;
                    else if (restante > 45f) voo.status = StatusVoo.EmVoo;
                    else if (restante <= 45f) 
                    {
                        voo.status = StatusVoo.Chegando;
                        SpawnarAviaoDeChegada(voo);
                    }
                }
            }

            // Chegadas que pousaram
            if (voo.ehChegada && voo.status == StatusVoo.Pousou)
            {
                // Aplicar impacto na população
                AplicarImpactoPopulacaoChegada(voo);
                voo.status = StatusVoo.VendasFechadas; // marca como processado
            }
        }

        // Remove voos muito antigos (mais de 10 min após conclusão)
        paineisVoos.RemoveAll(v => v != null && v.status == StatusVoo.VendasFechadas
            && !v.ehChegada && v.aviao == null);
    }

    // ── Impacto de chegada na população ───────────────────────────────
    private void AplicarImpactoPopulacaoChegada(VooAgendado voo)
    {
        if (voo == null || voo.passagensVendidas <= 0) return;

        // Turistas: ficam temporariamente, saem depois
        totalTuristasTemporarios += voo.passageirosTuristas;

        // Imigrantes: ficam permanentemente (adiciona à população)
        if (voo.passageirosImigrantes > 0 && GerenciadorRecursos.Instancia != null)
        {
            GerenciadorRecursos.Instancia.AdicionarPopulacao(voo.passageirosImigrantes);
        }

        // Aumenta população da cidade mais próxima do aeroporto
        if (GerenciadorDivisaoTerritorial.Instancia != null)
        {
            float menorDist = float.MaxValue;
            CidadeEstado cidadeMaisProxima = null;
            foreach (var cid in GerenciadorDivisaoTerritorial.Instancia.cidades)
            {
                if (cid.marcador == null) continue;
                float d = Vector3.Distance(transform.position, cid.marcador.transform.position);
                if (d < menorDist) { menorDist = d; cidadeMaisProxima = cid; }
            }

            if (cidadeMaisProxima != null)
            {
                // Imigrantes aumentam população civil da cidade
                cidadeMaisProxima.populacaoCivil = Mathf.Min(
                    cidadeMaisProxima.populacaoCivil + voo.passageirosImigrantes,
                    cidadeMaisProxima.capacidadeHabitacional > 0 ? cidadeMaisProxima.capacidadeHabitacional : int.MaxValue);
            }
        }

        estatisticaTurismoDia += voo.passagensVendidas;
        Debug.Log($"[Comercial] Voo {voo.numeroVoo} pousou: {voo.passageirosTuristas} turistas + {voo.passageirosImigrantes} imigrantes chegaram.");
    }

    // ── Retorno de turistas ────────────────────────────────────────────
    private void ProcessarRetornoTuristas()
    {
        if (totalTuristasTemporarios <= 0) return;
        timerRetornoTuristas += Time.deltaTime;
        if (timerRetornoTuristas < INTERVALO_RETORNO_TURISTAS) return;
        timerRetornoTuristas = 0f;

        // Uma fração dos turistas vai embora a cada intervalo
        int saindo = Mathf.RoundToInt(totalTuristasTemporarios * Random.Range(0.05f, 0.15f));
        saindo = Mathf.Min(saindo, totalTuristasTemporarios);
        totalTuristasTemporarios -= saindo;
    }

    // ── Economia ──────────────────────────────────────────────────────
    private void ProcessarEconomia()
    {
        if (Time.time < tempoUltimoTickEconomia + 4f) return;
        tempoUltimoTickEconomia = Time.time;
        // Deixa a economia rodar mesmo sem energia, para os contratos não travarem
        // if (semEnergia) return;

        timerDiaComercial += 4f;
        if (timerDiaComercial >= 120f)
        {
            timerDiaComercial = 0f;
            VirarDiaComercial();
            if (_identidadeCacheada != null && _identidadeCacheada.teamID > 1)
                AutoAssinarContratosIA();
        }

        ProcessarVoosAgendados();
        TentarSpawnarVooDeChegada();
    }

    // Spawna um voo de chegada aleatório com passageiros de fora
    private void TentarSpawnarVooDeChegada()
    {
        if (contratosAtivos.Count == 0) return;
        if (Random.value > 0.02f) return; // ~2% por tick de 4s → voo de chegada a cada ~3 min

        // Escolhe um contrato ativo aleatório para simular um voo de chegada
        var contrato = contratosAtivos[Random.Range(0, contratosAtivos.Count)];
        int passageiros = Random.Range(50, Mathf.Max(51, contrato.passagensVendidasHoje / 4));
        CriarVooChegada(contrato.nomeCompanhia, passageiros);
    }

    private void SpawnarAviaoDeChegada(VooAgendado voo)
    {
        if (prefabAviaoComercial == null || voo == null) return;

        // Spawna alto no céu e longe
        Vector2 dir = Random.insideUnitCircle.normalized * 3000f;
        Vector3 posSpawn = new Vector3(transform.position.x + dir.x, 150f, transform.position.z + dir.y);

        // Instancia virado para o aeroporto
        GameObject obj = Instantiate(prefabAviaoComercial, posSpawn, Quaternion.LookRotation((transform.position - posSpawn).normalized));
        
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        ControleAviaoComercial ca = obj.GetComponent<ControleAviaoComercial>();
        if (ca == null) ca = obj.AddComponent<ControleAviaoComercial>();

        ca.estaEmModoVooFisico = true;
        ca.estadoAtual = ControleAviao.EstadoAviao.EmMissao; // Voando
        
        ca.aeroportoOrigemComercial = this;
        ca.aeroportoOrigem = this;
        ca.nomeCompanhia = voo.nomeCompanhia;
        ca.passagensVendidas = voo.passagensVendidas;
        ca.vooAssociado = voo;
        voo.aviao = ca;

        frotaComercialAtiva.Add(ca);

        // Faz ele voltar para o aeroporto imediatamente
        ca.DefinirBaseAlternativaEIniciarRetorno(this);
    }

    private void VirarDiaComercial()
    {
        GerarNovosContratos();
        estatisticaTurismoDia           = 0;
        estatisticaPassagensVendidasDia = 0;
        int receitaDia = 0;

        float qualidadeVida = 50f;
        if (SistemaGovernoMundial.Instancia != null && _identidadeCacheada != null)
        {
            var pais = SistemaGovernoMundial.Instancia.ObterPais(_identidadeCacheada.teamID);
            if (pais != null) qualidadeVida = pais.qualidadeVida;
        }
        float fatorAtrai = qualidadeVida / 100f;
        if (qualidadeVida < 25f) fatorAtrai *= 0.1f;

        for (int i = contratosAtivos.Count - 1; i >= 0; i--)
        {
            var cia = contratosAtivos[i];
            cia.diasRestantes--;
            if (cia.diasRestantes <= 0)
            {
                // Libera vagas
                foreach (int vi in cia.indicasVagas) vagasReservadas.Remove(vi);
                contratosAtivos.RemoveAt(i);
                continue;
            }

            int min = (int)(1000 * cia.demandaTurismoBase * fatorAtrai);
            int max = (int)(10000 * cia.demandaTurismoBase * fatorAtrai);
            if (max <= min) max = min + 10;
            int turismo = Random.Range(min, max);
            cia.passagensVendidasHoje   = turismo;
            estatisticaTurismoDia       += turismo;
            estatisticaPassagensVendidasDia += turismo;
            receitaDia                  += turismo * cia.valorPorPassagem;
        }

        if (GerenciadorRecursos.Instancia != null)
            GerenciadorRecursos.Instancia.dinheiro += receitaDia;

        paisesConectados = 0;
        if (GerenciadorDivisaoTerritorial.Instancia != null)
        {
            int myTeam = _identidadeCacheada != null ? _identidadeCacheada.teamID : 1;
            var seen = new HashSet<int>();
            foreach (var cid in GerenciadorDivisaoTerritorial.Instancia.cidades)
                if (cid.temAeroporto && cid.teamID != myTeam) seen.Add(cid.teamID);
            paisesConectados = seen.Count;
        }
    }

    private void AutoAssinarContratosIA()
    {
        int ocupadas = 0;
        foreach (var c in contratosAtivos) ocupadas += c.baiasOcupadas;

        foreach (var cia in contratosDisponiveis)
        {
            if (cia.nomeCompanhia == "ASSINADO" || cia.nomeCompanhia == "RECUSADO") continue;
            if (ocupadas + cia.baiasNegociadas <= totalBaiasComerciais)
            {
                var ativo = AssinarContrato(cia);
                if (ativo != null) { ocupadas += ativo.baiasOcupadas; cia.nomeCompanhia = "ASSINADO"; }
            }
        }
    }

    private ContratoAereoAtivo AssinarContrato(ContratoAereoOferecido oferta)
    {
        var ativo = new ContratoAereoAtivo
        {
            nomeCompanhia   = oferta.nomeCompanhia,
            diasDuracao     = oferta.diasDuracao,
            diasRestantes   = oferta.diasDuracao,
            baiasOcupadas   = oferta.baiasNegociadas,
            valorPorPassagem= oferta.valorPorPassagem,
            demandaTurismoBase = oferta.demandaTurismoBase + oferta.baiasNegociadas * 0.2f,
            passagensVendidasHoje = Random.Range(200, 800)
        };
        contratosAtivos.Add(ativo);
        AlocarVagasParaContrato(ativo);
        return ativo;
    }

    private void GerarNovosContratos()
    {
        contratosDisponiveis.Clear();
        string[] nomes = { "Atlas Global", "Solaris Fly", "Carmesim Airways", "Boreal Charter", "Valeriana Express", "Oceanic Air", "Pinnacle Jet", "AeroNova", "SkyBridge", "TransAtlas" };
        int qtd = Random.Range(2, 5);
        for (int i = 0; i < qtd; i++)
        {
            contratosDisponiveis.Add(new ContratoAereoOferecido
            {
                nomeCompanhia   = nomes[Random.Range(0, nomes.Length)],
                baiasExigidas   = Random.Range(1, 4),
                baiasNegociadas = Random.Range(1, 4),
                diasDuracao     = Random.Range(3, 15),
                valorPorPassagem= Random.Range(5, 40),
                demandaTurismoBase = Random.Range(0.5f, 2f)
            });
        }
    }

    // ======================================================================
    // CALLBACK DO AVIÃO — chamado quando pousa e está pronto
    // ======================================================================
    public void AviaoChegou(ControleAviaoComercial aviao)
    {
        if (aviao == null || aviao.vooAssociado == null) return;
        aviao.vooAssociado.status = StatusVoo.Pousou;
        // Population impact acontece na próxima iteração de ProcessarVoosAgendados()
    }

    // ======================================================================
    // UI — Menu Z
    // ======================================================================
    protected override void OnGUI()
    {
        if (menuAeroportoUI != null && menuAeroportoUI.activeInHierarchy) return;

        bool menuAtivo = false;
        try
        {
            var fi = this.GetType().BaseType.GetField("menuAtivo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi != null) menuAtivo = (bool)fi.GetValue(this);
        }
        catch { return; }
        if (!menuAtivo) return;

        float w = Mathf.Max(900f, Screen.width * 0.58f);
        float x = 25f, y = 60f;
        float h = Screen.height - 80f;
        Rect janela = new Rect(x, y, w, h);

        // Fundo escuro
        DrawRect(janela, new Color(0.05f, 0.07f, 0.12f, 0.97f));
        DrawRect(new Rect(x, y, w, 48f), new Color(0.08f, 0.18f, 0.35f, 1f));

        GUI.Label(new Rect(x + 15, y + 10, w - 30, 32),
            "<size=20><b>✈  CENTRO DE CONTROLE — AEROPORTO COMERCIAL</b></size>");

        // Abas — 4 abas: comercial + militar
        float tabY = y + 52f;
        string[] abas = { "📋 VOOS", "📄 CONTRATOS", "📊 ESTATÍSTICAS", "🪖 MILITAR" };
        float tabW = (w - 30f) / abas.Length;
        for (int i = 0; i < abas.Length; i++)
        {
            Rect tabRect = new Rect(x + 15 + i * tabW, tabY, tabW - 2f, 30f);
            Color tabColor;
            if (i == 3)      tabColor = (abaMenuComercial == i) ? new Color(0.6f, 0.2f, 0.2f) : new Color(0.25f, 0.1f, 0.1f);
            else             tabColor = (abaMenuComercial == i) ? new Color(0.2f, 0.5f, 1f)   : new Color(0.1f, 0.15f, 0.25f);
            DrawRect(tabRect, tabColor);
            if (GUI.Button(tabRect, abas[i])) abaMenuComercial = i;
        }

        // Conteúdo
        Rect conteudo = new Rect(x + 10, tabY + 36f, w - 20, h - 100f);
        GUILayout.BeginArea(conteudo);

        switch (abaMenuComercial)
        {
            case 0: DesenharPainelVoos(); break;
            case 1: DesenharAbaCOntratos(); break;
            case 2: DesenharAbaEstatisticas(); break;
            case 3: DesenharAbaFrotaMilitar(); break;
        }

        GUILayout.EndArea();
    }

    // ── Painel de Voos ────────────────────────────────────────────────
    private void DesenharPainelVoos()
    {
        RemoveNulls(frotaComercialAtiva);

        GUILayout.Label("<size=15><b>🛫 PARTIDAS  /  🛬 CHEGADAS</b></size>");
        GUILayout.Space(6);

        // Cabeçalho da tabela
        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>Voo</b>",        GUILayout.Width(70));
        GUILayout.Label("<b>Companhia</b>",   GUILayout.Width(140));
        GUILayout.Label("<b>Destino</b>",     GUILayout.Width(130));
        GUILayout.Label("<b>Horário</b>",     GUILayout.Width(65));
        GUILayout.Label("<b>Pax</b>",         GUILayout.Width(55));
        GUILayout.Label("<b>Status</b>",      GUILayout.Width(200));
        GUILayout.Label("<b>Vaga</b>",        GUILayout.Width(60));
        GUILayout.EndHorizontal();
        GUILayout.Box("", GUILayout.Height(1), GUILayout.ExpandWidth(true));

        scrollVoos = GUILayout.BeginScrollView(scrollVoos);

        foreach (var voo in paineisVoos)
        {
            if (voo == null) continue;

            GUILayout.BeginHorizontal("box");

            // Tipo
            string icone = voo.ehChegada ? "🛬" : "🛫";
            GUILayout.Label($"{icone}<b>{voo.numeroVoo}</b>", GUILayout.Width(70));
            GUILayout.Label(voo.nomeCompanhia,  GUILayout.Width(140));
            GUILayout.Label(voo.destino,         GUILayout.Width(130));
            GUILayout.Label(voo.HorarioFormatado(), GUILayout.Width(65));
            GUILayout.Label($"{voo.passagensVendidas}", GUILayout.Width(55));
            GUILayout.Label(voo.StatusTexto(),   GUILayout.Width(200));
            GUILayout.Label(voo.vagaIndex >= 0 ? $"V{voo.vagaIndex + 1}" : "—", GUILayout.Width(60));

            GUILayout.EndHorizontal();
        }

        if (paineisVoos.Count == 0)
            GUILayout.Label("<color=gray>Nenhum voo agendado. Assine contratos para iniciar operações.</color>");

        GUILayout.EndScrollView();

        // Resumo de turistas
        GUILayout.Space(8);
        GUILayout.BeginHorizontal("box");
        GUILayout.Label($"<b>Turistas presentes:</b> <color=cyan>{totalTuristasTemporarios:N0}</color>", GUILayout.Width(230));
        GUILayout.Label($"<b>Imigrantes (hoje):</b> <color=lime>{totalImigrantesNovos:N0}</color>", GUILayout.Width(230));
        GUILayout.Label($"<b>Vagas usadas:</b> <color=yellow>{vagasReservadas.Count}/{totalBaiasComerciais}</color>", GUILayout.Width(180));
        GUILayout.EndHorizontal();
    }

    // ── Aba Contratos ─────────────────────────────────────────────────
    private void DesenharAbaCOntratos()
    {
        int baiasOcupadas = 0;
        foreach (var c in contratosAtivos) baiasOcupadas += c.baiasOcupadas;

        GUILayout.Label($"<size=15><b>🏢 COMPANHIAS DISPONÍVEIS  (Baias: {baiasOcupadas}/{totalBaiasComerciais})</b></size>");
        GUILayout.Space(4);

        scrollContratos = GUILayout.BeginScrollView(scrollContratos);

        // ── Ofertas disponíveis
        foreach (var cia in contratosDisponiveis)
        {
            if (cia == null) continue;
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();

            GUILayout.Label($"<b>{cia.nomeCompanhia}</b>", GUILayout.Width(160));
            GUILayout.Label("Baias: ");
            if (GUILayout.Button("–", GUILayout.Width(22))) cia.baiasNegociadas = Mathf.Max(1, cia.baiasNegociadas - 1);
            GUILayout.Label($"<b>{cia.baiasNegociadas}</b>", GUILayout.Width(22));
            if (GUILayout.Button("+", GUILayout.Width(22))) cia.baiasNegociadas = Mathf.Min(cia.baiasNegociadas + 1, totalBaiasComerciais - baiasOcupadas + cia.baiasNegociadas);
            GUILayout.Label($"  {cia.diasDuracao}d  |  ${cia.valorPorPassagem}/pax", GUILayout.Width(140));
            GUILayout.FlexibleSpace();

            bool temVagas = (baiasOcupadas + cia.baiasNegociadas <= totalBaiasComerciais);
            bool jaNome  = (cia.nomeCompanhia == "ASSINADO" || cia.nomeCompanhia == "RECUSADO");

            if (jaNome) { GUILayout.Label(cia.nomeCompanhia, GUILayout.Width(100)); }
            else if (!temVagas) { GUI.color = Color.red; GUILayout.Label("SEM VAGAS", GUILayout.Width(100)); GUI.color = Color.white; }
            else
            {
                if (GUILayout.Button("Assinar", GUILayout.Width(90), GUILayout.Height(24)))
                {
                    float chance = 100f; // Todos os contratos devem ser sempre aceitos (100% de chance)

                    if (Random.Range(0f, 100f) <= chance)
                    {
                        AssinarContrato(cia);
                        cia.nomeCompanhia = "ASSINADO";
                        baiasOcupadas += cia.baiasNegociadas;
                    }
                    else cia.nomeCompanhia = "RECUSADO";
                }
            }
            GUILayout.EndHorizontal();

            if (cia.nomeCompanhia == "RECUSADO")
            { GUI.color = Color.red; GUILayout.Label("A companhia recusou sua oferta."); GUI.color = Color.white; }

            GUILayout.EndVertical();
        }

        contratosDisponiveis.RemoveAll(c => c.nomeCompanhia == "ASSINADO" || c.nomeCompanhia == "RECUSADO");

        // ── Contratos ativos
        if (contratosAtivos.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("<size=14><b>📑 CONTRATOS ATIVOS</b></size>");
            foreach (var ativo in contratosAtivos)
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"<b>{ativo.nomeCompanhia}</b>", GUILayout.Width(160));
                GUILayout.Label($"Baias: {ativo.baiasOcupadas}", GUILayout.Width(80));
                GUILayout.Label($"Restam: {ativo.diasRestantes}d", GUILayout.Width(80));
                GUILayout.Label($"Vagas: {string.Join(",", ativo.indicasVagas.ConvertAll(v => $"V{v + 1}"))}", GUILayout.Width(130));
                GUILayout.Label($"Hoje: {ativo.passagensVendidasHoje:N0} pax  |  ${ativo.valorPorPassagem}/pax", GUILayout.Width(200));
                GUILayout.EndHorizontal();
            }
        }

        GUILayout.EndScrollView();
    }

    // ── Aba Estatísticas ──────────────────────────────────────────────
    private void DesenharAbaEstatisticas()
    {
        GUILayout.Label("<size=15><b>📊 ESTATÍSTICAS DE OPERAÇÕES</b></size>");
        GUILayout.Space(8);

        GUILayout.BeginVertical("box");
        GUILayout.Label($"<b>Turismo acumulado (hoje):</b>  <color=cyan>+{estatisticaTurismoDia:N0}</color>");
        GUILayout.Label($"<b>Passagens vendidas (hoje):</b> <color=cyan>+{estatisticaPassagensVendidasDia:N0}</color>");
        GUILayout.Label($"<b>Países conectados:</b>         <color=yellow>{paisesConectados}</color>");
        GUILayout.Label($"<b>Turistas presentes:</b>        <color=lime>{totalTuristasTemporarios:N0}</color>");
        GUILayout.Label($"<b>Contratos ativos:</b>          <color=white>{contratosAtivos.Count}</color>");
        GUILayout.Label($"<b>Vagas ocupadas:</b>            <color=orange>{vagasReservadas.Count}/{totalBaiasComerciais}</color>");
        GUILayout.Space(4);

        // Pistas
        GUILayout.Label("<b>Estado das Pistas:</b>");
        foreach (var p in todasPistas)
        {
            string cor = p.estado == PistaComercial.EstadoPista.Livre ? "lime"
                : p.estado == PistaComercial.EstadoPista.EmDecolagem ? "yellow" : "orange";
            GUILayout.Label($"  • {p.nomePista}: <color={cor}>{p.estado}</color>"
                + (p.aviaoNaPista != null ? $"  [{p.aviaoNaPista.nomeCompanhia}]" : ""));
        }
        GUILayout.EndVertical();

        GUILayout.Space(8);
        GUILayout.Label("<size=14><b>✈ FROTA EM OPERAÇÃO</b></size>");
        foreach (var av in frotaComercialAtiva)
        {
            if (av == null) continue;
            string status = av.estadoAtual.ToString();
            GUILayout.Label($"  • <b>{av.nomeCompanhia}</b> | {status} | Pax: {av.passagensVendidas}");
        }
    }

    // ── Aba Militar (acessa frota herdada do GerenciadorAeroporto) ─────
    private Vector2 scrollMilitar;
    private void DesenharAbaFrotaMilitar()
    {
        GUILayout.Label("<size=15><b>🪖 FROTA MILITAR — AEROPORTO BASE</b></size>");
        GUILayout.Space(6);

        // Informações gerais
        GUILayout.BeginHorizontal("box");
        GUILayout.Label($"<b>Pátio:</b> <color=yellow>{avioesNoPatio.Count - frotaComercialAtiva.Count}</color> militares", GUILayout.Width(200));
        GUILayout.Label($"<b>Hangar:</b> <color=yellow>{avioesNoHangar.Count}</color>", GUILayout.Width(130));
        GUILayout.Label($"<b>Energia:</b> <color={(semEnergia ? "red" : "lime")}>{(semEnergia ? "SEM ENERGIA" : "OK")}</color>", GUILayout.Width(160));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);

        scrollMilitar = GUILayout.BeginScrollView(scrollMilitar);

        // Aviões no pátio (excluindo comerciais)
        GUILayout.Label("<b>NO PÁTIO (Militares):</b>");
        bool algum = false;
        foreach (var av in avioesNoPatio)
        {
            if (av == null) continue;
            if (av is ControleAviaoComercial) continue; // pula comerciais
            algum = true;
            string estado = av.estadoAtual.ToString();
            string cor = av.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio ? "lime"
                : av.estadoAtual == ControleAviao.EstadoAviao.EmMissao ? "cyan" : "yellow";
            GUILayout.Label($"  ▸ <b>{av.name}</b>  |  <color={cor}>{estado}</color>  |  " +
                $"HP: {(av.GetComponent<SistemaDeDanos>() != null ? Mathf.RoundToInt(av.GetComponent<SistemaDeDanos>().vidaAtual).ToString() : "N/A")}");
        }
        if (!algum) GUILayout.Label("  <color=gray>Nenhum avião militar no pátio.</color>");

        GUILayout.Space(6);
        GUILayout.Label("<b>NO HANGAR:</b>");
        algum = false;
        foreach (var av in avioesNoHangar)
        {
            if (av == null) continue;
            algum = true;
            GUILayout.Label($"  ▸ <b>{av.name}</b>  |  <color=gray>Em Reserva</color>");
        }
        if (!algum) GUILayout.Label("  <color=gray>Hangar vazio.</color>");

        GUILayout.Space(6);
        GUILayout.Label("<b>HELICÓPTEROS:</b>");
        algum = false;
        foreach (var heli in helicopterosDoAeroporto)
        {
            if (heli == null) continue;
            algum = true;
            GUILayout.Label($"  ▸ <b>{heli.name}</b>");
        }
        if (!algum) GUILayout.Label("  <color=gray>Nenhum helicóptero.</color>");

        GUILayout.EndScrollView();
    }

    // ── Utilidade: Draw rect ───────────────────────────────────────────
    private static Texture2D _texBranca;
    private static void DrawRect(Rect r, Color c)
    {
        if (_texBranca == null) { _texBranca = new Texture2D(1, 1); _texBranca.SetPixel(0, 0, Color.white); _texBranca.Apply(); }
        Color prev = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, _texBranca);
        GUI.color = prev;
    }
}
