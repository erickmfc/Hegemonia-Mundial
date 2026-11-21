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
    public Transform pistaPouso;   // Grupo de waypoints de pouso, quando separado da decolagem
    public Transform taxiEntrada; // Saída do hangar → pista
    public Transform taxiSaida;   // Pista → hangar
    public Transform taxiEntradaPontoFinal; // Ponto de alinhamento criado pelo autor do prefab
    public bool inverterTaxiEntrada; // Reutiliza uma rota de táxi no sentido pátio → pista
    public bool inverterTaxiSaida; // Usa a mesma rota de táxi no sentido pista → pátio

    [HideInInspector] public List<Transform> waypointsDecolagem  = new List<Transform>();
    [HideInInspector] public List<Transform> waypointsDecida     = new List<Transform>();
    [HideInInspector] public List<Transform> waypointsTaxiEntrada= new List<Transform>();
    [HideInInspector] public List<Transform> waypointsTaxiSaida  = new List<Transform>();

    public enum EstadoPista { Livre, EmDecolagem, EmPouso }
    [HideInInspector] public EstadoPista estado = EstadoPista.Livre;
    [HideInInspector] public ControleAviaoComercial aviaoNaPista;
    private bool inicializada;

    public bool ocupada => estado != EstadoPista.Livre;

    public void Inicializar()
    {
        if (inicializada) return;
        inicializada = true;

        if (pista != null && (waypointsDecolagem == null || waypointsDecolagem.Count == 0))
        {
            foreach (Transform t in pista) waypointsDecolagem.Add(t);
        }

        if (waypointsDecida == null || waypointsDecida.Count == 0)
        {
            Transform grupoPouso = pistaPouso != null ? pistaPouso : pista;
            if (grupoPouso != null)
            {
                foreach (Transform t in grupoPouso) waypointsDecida.Add(t);
                waypointsDecida.Reverse();
            }
        }

        if (taxiEntrada != null && (waypointsTaxiEntrada == null || waypointsTaxiEntrada.Count == 0))
            foreach (Transform t in taxiEntrada) waypointsTaxiEntrada.Add(t);
        if (taxiEntradaPontoFinal != null && (waypointsTaxiEntrada == null || !waypointsTaxiEntrada.Contains(taxiEntradaPontoFinal)))
            waypointsTaxiEntrada.Add(taxiEntradaPontoFinal);
        if (inverterTaxiEntrada && waypointsTaxiEntrada != null)
            waypointsTaxiEntrada.Reverse();
        if (taxiSaida != null && (waypointsTaxiSaida == null || waypointsTaxiSaida.Count == 0))
            foreach (Transform t in taxiSaida) waypointsTaxiSaida.Add(t);
        if (inverterTaxiSaida && waypointsTaxiSaida != null)
            waypointsTaxiSaida.Reverse();
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
    public bool longoAlcance;
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
        public bool longoAlcance;
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
        public bool longoAlcance;
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
    private readonly Dictionary<PistaComercial, C700TransporteAereo> pistasEmUsoTransporte = new Dictionary<PistaComercial, C700TransporteAereo>();

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
    public int   estatisticaTurismoDia   = 0;
    public int   estatisticaPassagensVendidasDia = 0;
    private int   paisesConectados        = 0;

    // Cooldown para evitar que todos os aviões decolem ao mesmo tempo
    private float timerProximoVooPermitido = 0f;

    // ── Spawn ──────────────────────────────────────────────────────────
    [Header("=== Spawn Visual ===")]
    public GameObject prefabAviaoComercial;
    [Tooltip("Aeronaves de longo curso habilitadas para contratos intercontinentais. Se vazio, o aeroporto mantém a frota comercial padrão.")]
    public List<GameObject> frotaAvioesLongoAlcance = new List<GameObject>();
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
    private const int RESERVA_MINIMA_DINHEIRO_COMBUSTIVEL = 500;
    private float _proximoLogSemWaypointsTime = -999f;
    private float _proximoLogCombustivelTime = -999f;
    private float _proximoLogPousoTime = -999f;

    private static readonly string[] destinosLongoAlcance =
    {
        "Lisboa", "Tóquio", "Nova York", "Dubai", "Singapura", "Paris", "Cidade do Cabo", "Santiago"
    };

    private GameObject SelecionarAeronaveComercial(bool longoAlcance)
    {
        if (longoAlcance && frotaAvioesLongoAlcance != null)
        {
            int validos = 0;
            for (int i = 0; i < frotaAvioesLongoAlcance.Count; i++)
                if (frotaAvioesLongoAlcance[i] != null) validos++;
            if (validos > 0)
            {
                int indice = Random.Range(0, validos);
                for (int i = 0; i < frotaAvioesLongoAlcance.Count; i++)
                {
                    GameObject prefab = frotaAvioesLongoAlcance[i];
                    if (prefab == null) continue;
                    if (indice-- == 0) return prefab;
                }
            }
        }
        return prefabAviaoComercial;
    }

    private bool PossuiAeronavesLongoAlcance()
    {
        if (frotaAvioesLongoAlcance == null) return false;
        for (int i = 0; i < frotaAvioesLongoAlcance.Count; i++)
            if (frotaAvioesLongoAlcance[i] != null) return true;
        return false;
    }

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
    public bool PossuiRotaPousoValida()
    {
        for (int i = 0; i < todasPistas.Count; i++)
        {
            PistaComercial p = todasPistas[i];
            if (p == null) continue;
            p.Inicializar();
            if (p.waypointsDecida != null && p.waypointsDecida.Count >= 2)
            {
                return true;
            }
        }
        return false;
    }

    public override Transform ObterParadaRecebimentoTransporteAereo()
    {
        Transform paradaGrande = ObterParadaGrandePreferencial(false);
        if (paradaGrande != null) return paradaGrande;

        Transform baiaLivre = ObterBaiaLivreParaTransporte();
        if (baiaLivre != null) return baiaLivre;

        return base.ObterParadaRecebimentoTransporteAereo();
    }

    public Transform ObterBaiaLivreParaTransporte()
    {
        if (waypointsPatio == null || waypointsPatio.Count == 0) return null;

        RemoveNulls(frotaComercialAtiva);

        for (int pass = 0; pass < 2; pass++)
        {
            bool ignorarReservaContratual = pass == 1;
            for (int i = 0; i < waypointsPatio.Count; i++)
            {
                Transform wp = waypointsPatio[i];
                if (wp == null) continue;
                if (!ignorarReservaContratual && vagasReservadas.ContainsKey(i)) continue;
                if (BaiaOcupadaNoAeroportoComercial(wp)) continue;

                return wp;
            }
        }

        return null;
    }

    private bool BaiaOcupadaNoAeroportoComercial(Transform baia)
    {
        if (baia == null) return true;
        Vector3 pos = baia.position;

        for (int i = 0; i < frotaComercialAtiva.Count; i++)
        {
            ControleAviaoComercial comercial = frotaComercialAtiva[i];
            if (comercial == null) continue;
            if (comercial.vagaRetorno == baia || (comercial.transform.position - pos).sqrMagnitude <= 100f)
                return true;
        }

        for (int i = transportesC700NoPatio.Count - 1; i >= 0; i--)
        {
            C700TransporteAereo transporte = transportesC700NoPatio[i];
            if (transporte == null) { transportesC700NoPatio.RemoveAt(i); continue; }
            if (transporte.pontoEstacionamentoPreferencial == baia || (transporte.transform.position - pos).sqrMagnitude <= 100f)
                return true;
        }

        for (int i = avioesNoPatio.Count - 1; i >= 0; i--)
        {
            ControleAviao av = avioesNoPatio[i];
            if (av == null) { avioesNoPatio.RemoveAt(i); continue; }
            if (av.vagaRetorno == baia || (av.transform.position - pos).sqrMagnitude <= 100f)
                return true;
        }

        return false;
    }

    public bool PossuiPistaDecolagemComTaxi()
    {
        for (int i = 0; i < todasPistas.Count; i++)
        {
            PistaComercial p = todasPistas[i];
            if (p == null) continue;
            p.Inicializar();
            if (p.waypointsDecolagem != null && p.waypointsDecolagem.Count >= 2
                && p.waypointsTaxiEntrada != null && p.waypointsTaxiEntrada.Count > 0)
            {
                return true;
            }
        }
        return false;
    }

    public PistaComercial SolicitarPistaParaDecolagemTransporte(C700TransporteAereo transporte)
    {
        LimparPistasTransporteMortos();
        for (int pass = 0; pass < 2; pass++)
        {
            bool exigirTaxi = pass == 0;
            for (int i = 0; i < todasPistas.Count; i++)
            {
                PistaComercial p = todasPistas[i];
                if (p == null) continue;
                p.Inicializar();
                if (p.estado != PistaComercial.EstadoPista.Livre) continue;
                if (p.waypointsDecolagem == null || p.waypointsDecolagem.Count < 2) continue;
                if (exigirTaxi && (p.waypointsTaxiEntrada == null || p.waypointsTaxiEntrada.Count == 0)) continue;

                p.estado = PistaComercial.EstadoPista.EmDecolagem;
                p.aviaoNaPista = null;
                pistasEmUsoTransporte[p] = transporte;
                return p;
            }
        }
        return null;
    }

    public void LiberarPistaTransporte(PistaComercial pista, C700TransporteAereo transporte)
    {
        if (pista == null) return;
        if (pistasEmUsoTransporte.TryGetValue(pista, out C700TransporteAereo dono)
            && (dono == null || dono == transporte))
        {
            pistasEmUsoTransporte.Remove(pista);
            pista.Liberar();
            ProcessarFilasRunway();
        }
    }

    private void LimparPistasTransporteMortos()
    {
        if (pistasEmUsoTransporte.Count == 0) return;
        List<PistaComercial> liberar = null;
        foreach (KeyValuePair<PistaComercial, C700TransporteAereo> par in pistasEmUsoTransporte)
        {
            if (par.Value != null) continue;
            if (liberar == null) liberar = new List<PistaComercial>();
            liberar.Add(par.Key);
        }
        if (liberar == null) return;
        for (int i = 0; i < liberar.Count; i++)
        {
            PistaComercial p = liberar[i];
            pistasEmUsoTransporte.Remove(p);
            if (p != null && p.aviaoNaPista == null) p.Liberar();
        }
    }

    public PistaComercial SolicitarPistaParaDecolagem(ControleAviaoComercial aviao)
    {
        // Prefere pista livre com waypoints válidos; evita conflito com pouso
        foreach (var p in todasPistas)
        {
            if (p.estado == PistaComercial.EstadoPista.Livre && p.waypointsDecolagem.Count > 0)
            {
                p.estado = PistaComercial.EstadoPista.EmDecolagem;
                p.aviaoNaPista = aviao;
                return p;
            }
        }

        // Fallback: se nenhuma pista configurada tem waypoints, tenta qualquer pista livre
        // (decolagem sem waypoints de taxi — avião decola verticalmente)
        foreach (var p in todasPistas)
        {
            if (p.estado == PistaComercial.EstadoPista.Livre)
            {
                p.estado = PistaComercial.EstadoPista.EmDecolagem;
                p.aviaoNaPista = aviao;
                if (Time.unscaledTime >= _proximoLogSemWaypointsTime)
                {
                    _proximoLogSemWaypointsTime = Time.unscaledTime + 25f;
                    Debug.LogWarning($"[Comercial] {p.nomePista} sem waypoints configurados. Avião decolará sem taxi.");
                }
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
            p.Inicializar();
            if (p.estado == PistaComercial.EstadoPista.Livre && p.waypointsDecida.Count >= 2)
            {
                p.estado = PistaComercial.EstadoPista.EmPouso;
                p.aviaoNaPista = aviao;
                return p;
            }
        }
        // Fallback de emergência: pista 1
        // Sem rota vÃ¡lida, aguarda na fila. Nunca entrega uma pista sem rota,
        // pois isso faria o aviÃ£o cair no caminho genÃ©rico direto para a vaga.
        return null;
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
        LimparPistasTransporteMortos();
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
        if (SelecionarAeronaveComercial(contrato != null && contrato.longoAlcance) == null)
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

        GameObject prefabAviao = SelecionarAeronaveComercial(contrato != null && contrato.longoAlcance);
        if (prefabAviao == null) return;
        GameObject obj = Instantiate(prefabAviao, posSpawn, vaga.rotation);
        
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
        voo.destino      = contrato.longoAlcance
            ? destinosLongoAlcance[Random.Range(0, destinosLongoAlcance.Length)]
            : "Exterior";
        voo.longoAlcance = contrato.longoAlcance;
        if (voo.longoAlcance) aviao.nomeDestinoIA = voo.destino;

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
        ContratoAereoAtivo contrato = contratosAtivos.Find(c => c != null && c.nomeCompanhia == companhia);
        voo.longoAlcance = contrato != null && contrato.longoAlcance;
        voo.destino = voo.longoAlcance
            ? destinosLongoAlcance[Random.Range(0, destinosLongoAlcance.Length)] + " → Nova Aurora"
            : "Local";
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
                    {
                        if (Time.time >= timerProximoVooPermitido)
                        {
                            timerProximoVooPermitido = Time.time + Random.Range(10f, 20f);
                            voo.aviao.SolicitarDecolagem();
                        }
                    }
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
                if (cid == null) continue;
                bool possuiPosicao = cid.marcador != null || cid.ehCidadeNaoMaterializada;
                if (!possuiPosicao) continue;
                Vector3 posicaoCidade = cid.marcador != null ? cid.marcador.transform.position : cid.localizacao;
                float d = Vector3.Distance(transform.position, posicaoCidade);
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
        if (Time.unscaledTime >= _proximoLogPousoTime)
        {
            _proximoLogPousoTime = Time.unscaledTime + 20f;
            Debug.Log($"[Comercial] Voo {voo.numeroVoo} pousou: {voo.passageirosTuristas} turistas + {voo.passageirosImigrantes} imigrantes chegaram.");
        }
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
        if (Random.value > 0.03f) return; // Fluxo moderadamente maior, sem varredura ou spawn por frame.

        // Escolhe um contrato ativo aleatório para simular um voo de chegada
        var contrato = contratosAtivos[Random.Range(0, contratosAtivos.Count)];
        int passageiros = contrato.longoAlcance
            ? Random.Range(100, Mathf.Max(101, contrato.passagensVendidasHoje / 3))
            : Random.Range(50, Mathf.Max(51, contrato.passagensVendidasHoje / 4));
        CriarVooChegada(contrato.nomeCompanhia, passageiros);
    }

    private void SpawnarAviaoDeChegada(VooAgendado voo)
    {
        if (voo == null) return;
        GameObject prefabAviao = SelecionarAeronaveComercial(voo.longoAlcance);
        if (prefabAviao == null) return;

        // ── Calcula ponto de spawn longe do aeroporto ──
        // O avião deve aparecer NO AR, a ~2000m, na direção oposta à pista
        // para que faça o percurso completo de aproximação → pista → táxi → vaga.
        Vector3 posSpawn = transform.position;
        Quaternion rotSpawn = Quaternion.identity;

        // Determina a direção da pista de pouso (do 1º wp de decida invertido = final da pista)
        // O avião deve vir na direção OPOSTA ao vetor de decolagem para pousar corretamente.
        Vector3 direcaoAproximacao = Vector3.forward; // fallback

        // Pega a direção da pista usando os waypointsDecida da pista 1 (referência)
        PistaComercial pistaRef = null;
        foreach (var p in todasPistas)
        {
            if (p.waypointsDecida != null && p.waypointsDecida.Count >= 2)
            {
                pistaRef = p;
                break;
            }
        }

        if (pistaRef != null && pistaRef.waypointsDecida.Count >= 2)
        {
            // waypointsDecida[0] = ponto de toque (início do pouso no chão)
            // waypointsDecida[last] = ponto mais distante (onde o avião entra)
            // A direção de aproximação é do último ponto para o primeiro
            Transform wpEntrada = pistaRef.waypointsDecida[pistaRef.waypointsDecida.Count - 1];
            Transform wpToque   = pistaRef.waypointsDecida[0];
            Vector3 direcaoPista = (wpToque.position - wpEntrada.position);
            direcaoPista.y = 0f;
            if (direcaoPista.sqrMagnitude > 0.01f)
                direcaoAproximacao = direcaoPista.normalized;
            else
                direcaoAproximacao = wpEntrada.forward;
        }
        else if (decida != null && decida.childCount > 1)
        {
            // Fallback: usa os filhos do decida bruto (ainda não invertidos)
            Transform wp0 = decida.GetChild(0);
            Transform wp1 = decida.GetChild(1);
            Vector3 d = (wp1.position - wp0.position);
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) direcaoAproximacao = d.normalized;
        }

        // Spawn a 2000m NA DIREÇÃO OPOSTA ao pouso (ou seja, de onde o avião vem)
        const float distanciaSpawn = 2000f;
        const float alturaSpawn = 150f;
        posSpawn = transform.position - direcaoAproximacao * distanciaSpawn;
        posSpawn.y = alturaSpawn;
        // Avião deve apontar na direção do aeroporto (mesma da pista)
        rotSpawn = Quaternion.LookRotation(direcaoAproximacao);

        // Instancia o avião
        GameObject obj = Instantiate(prefabAviao, posSpawn, rotSpawn);
        
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        ControleAviaoComercial ca = obj.GetComponent<ControleAviaoComercial>();
        if (ca == null) ca = obj.AddComponent<ControleAviaoComercial>();

        ca.estaEmModoVooFisico = true;
        ca.estadoAtual = ControleAviao.EstadoAviao.EmMissao; // Voando para o aeroporto

        ca.aeroportoOrigemComercial = this;
        ca.aeroportoOrigem = this;
        ca.nomeCompanhia = voo.nomeCompanhia;
        ca.nomeDestinoIA = voo.destino;
        ca.passagensVendidas = voo.passagensVendidas;
        ca.vooAssociado = voo;
        voo.aviao = ca;

        // Define o ponto de aproximação como alvo de voo inicial
        // (primeiro waypoint de decida = entrada da pista de pouso)
        if (pistaRef != null && pistaRef.waypointsDecida.Count > 0)
        {
            Vector3 wpChegada = pistaRef.waypointsDecida[pistaRef.waypointsDecida.Count - 1].position;
            wpChegada.y = alturaSpawn;
            ca.alvoGPSVoo = wpChegada;
        }
        else
        {
            ca.alvoGPSVoo = new Vector3(transform.position.x, alturaSpawn, transform.position.z);
        }

        frotaComercialAtiva.Add(ca);

        ca.IniciarSequenciaPousoComercial();
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

        // ── Consumo Diário de Combustível pelo Aeroporto ──
        int combustivelNecessario = 0;
        foreach (var cia in contratosAtivos)
        {
            // Base 100 por contrato + extra por demanda e passageiros
            combustivelNecessario += 100 + Mathf.RoundToInt((cia.passagensVendidasHoje / 10f) * cia.demandaTurismoBase);
        }

        if (combustivelNecessario > 0 && GerenciadorRecursos.Instancia != null)
        {
            int combustivelDisponivel = GerenciadorRecursos.Instancia.petroleo;
            if (combustivelDisponivel >= combustivelNecessario)
            {
                GerenciadorRecursos.Instancia.petroleo -= combustivelNecessario; // Subtrai direto
            }
            else
            {
                // Compra automática de combustível somente se houver saldo positivo acima da reserva mínima,
                // evitando endividamento infinito na virada do dia.
                int custoFaltante = Mathf.RoundToInt(receitaDia * Random.Range(0.05f, 0.15f));
                DadosPaisGoverno pais = (SistemaGovernoMundial.Instancia != null && _identidadeCacheada != null)
                    ? SistemaGovernoMundial.Instancia.ObterPais(_identidadeCacheada.teamID)
                    : null;

                bool saldoLocalSuficiente = custoFaltante > 0
                    && GerenciadorRecursos.Instancia.dinheiro > RESERVA_MINIMA_DINHEIRO_COMBUSTIVEL + custoFaltante;
                bool saldoPaisSuficiente = pais == null
                    || pais.saldo > RESERVA_MINIMA_DINHEIRO_COMBUSTIVEL + custoFaltante;

                if (saldoLocalSuficiente && saldoPaisSuficiente)
                {
                    SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
                    bool tesouroCentralDisponivel = governo != null && pais != null;
                    bool pagamentoAprovado = tesouroCentralDisponivel
                        ? governo.TentarPagar(pais.teamId, custoFaltante)
                        : true;
                    if (pagamentoAprovado)
                    {
                        if (combustivelDisponivel > 0)
                        {
                            GerenciadorRecursos.Instancia.petroleo = 0;
                            combustivelNecessario -= combustivelDisponivel;
                        }
                        if (!tesouroCentralDisponivel)
                        {
                            GerenciadorRecursos.Instancia.dinheiro -= custoFaltante;
                            if (pais != null) pais.saldo -= custoFaltante;
                        }
                        if (pais != null) pais.petroleo = GerenciadorRecursos.Instancia.petroleo;
                        if (Time.unscaledTime >= _proximoLogCombustivelTime)
                        {
                            _proximoLogCombustivelTime = Time.unscaledTime + 25f;
                            Debug.Log($"[Comercial] Faltou combustível para o aeroporto! Compra automática consumiu {custoFaltante} de dinheiro.");
                        }
                    }
                }
                else if (Time.unscaledTime >= _proximoLogCombustivelTime)
                {
                    _proximoLogCombustivelTime = Time.unscaledTime + 25f;
                    Debug.Log($"[Comercial] Compra automática de combustível ignorada: saldo insuficiente acima da reserva mínima ({RESERVA_MINIMA_DINHEIRO_COMBUSTIVEL}).");
                }
            }
            
            // Força atualização visual
            if (GerenciadorRecursos.Instancia != null)
                GerenciadorRecursos.Instancia.GetType().GetMethod("NotificarAtualizacao", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(GerenciadorRecursos.Instancia, null);
        }

        if (receitaDia > 0)
        {
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            int teamId = _identidadeCacheada != null ? _identidadeCacheada.teamID : 1;
            if (governo != null)
                governo.AdicionarSaldo(teamId, receitaDia, "Passageiros e contratos", "Aeroporto comercial", TipoLancamentoFinanceiro.ReceitaDiaria);
            else if (GerenciadorRecursos.Instancia != null)
                GerenciadorRecursos.Instancia.AdicionarRecursos(addDinheiro: receitaDia);
        }

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
            longoAlcance    = oferta.longoAlcance,
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
            bool longoAlcance = PossuiAeronavesLongoAlcance() && (i == 0 || Random.value < 0.28f);
            int tier = Random.Range(0, 100);
            int duracao;
            if (longoAlcance) duracao = Random.Range(240, 541); // contrato de longo curso: 8 a 18 meses do calendário do jogo
            else if (tier < 60) duracao = Random.Range(3, 15); // Fim de dias
            else if (tier < 90) duracao = Random.Range(160, 200); // ~6 meses
            else duracao = Random.Range(350, 380); // ~1 ano

            contratosDisponiveis.Add(new ContratoAereoOferecido
            {
                nomeCompanhia   = longoAlcance
                    ? new[] { "Atlas Intercontinental", "Oceanic Longo Curso", "SkyBridge Global" }[Random.Range(0, 3)]
                    : nomes[Random.Range(0, nomes.Length)],
                longoAlcance    = longoAlcance,
                baiasExigidas   = Random.Range(1, 4),
                baiasNegociadas = Random.Range(1, 4),
                diasDuracao     = duracao,
                valorPorPassagem= longoAlcance ? Random.Range(55, 96) : Random.Range(5, 40),
                demandaTurismoBase = longoAlcance ? Random.Range(1.4f, 2.5f) : Random.Range(0.5f, 2f)
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
        System.Reflection.FieldInfo fi = null;
        try
        {
            fi = this.GetType().BaseType.GetField("menuAtivo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi != null) menuAtivo = (bool)fi.GetValue(this);
        }
        catch { return; }
        if (!menuAtivo) return;
        if (!GestorMenusExclusivos.EstaAtivo(this))
        {
            try { fi?.SetValue(this, false); } catch { }
            return;
        }

        float topoHud = MenuFixadoController.ObterMargemSuperiorHudNaTela(82f);
        float baseHud = MenuComandoController.ObterMargemInferiorHudGlobalNaTela(180f);
        float topSafe = Mathf.Max(8f, topoHud + 10f);
        float bottomSafe = Mathf.Max(12f, baseHud + 14f);
        float alturaDisponivel = Screen.height - topSafe - bottomSafe;
        if (alturaDisponivel < 260f)
        {
            topSafe = Mathf.Min(topSafe, Mathf.Max(6f, topoHud + 4f));
            bottomSafe = Mathf.Min(bottomSafe, Mathf.Max(10f, baseHud + 8f));
            alturaDisponivel = Mathf.Max(210f, Screen.height - topSafe - bottomSafe);
        }

        float x = Screen.width < 1000f ? 12f : 22f;
        float wMax = Mathf.Max(340f, Screen.width - x * 2f);
        float w = Mathf.Min(Mathf.Clamp(Screen.width * 0.62f, 840f, 1020f), wMax);
        float y = topSafe;
        float h = Mathf.Min(alturaDisponivel, Mathf.Max(460f, Screen.height * 0.72f));
        Rect janela = new Rect(x, y, w, h);
        GestorMenusExclusivos.RegistrarAreaBloqueio(this, janela);

        // Fundo escuro + moldura tática alinhada ao HUD
        DrawRect(janela, new Color(0.027f, 0.066f, 0.105f, 0.96f));
        DrawRect(new Rect(x, y, w, 46f), new Color(0.043f, 0.105f, 0.165f, 0.98f));
        DrawRect(new Rect(x, y, w, 2f), new Color(0.0f, 0.898f, 1.0f, 0.95f));
        Color borda = new Color(0.243f, 0.486f, 0.596f, 0.92f);
        DrawRect(new Rect(x, janela.yMax - 1f, w, 1f), borda);
        DrawRect(new Rect(x, y, 1f, h), borda);
        DrawRect(new Rect(janela.xMax - 1f, y, 1f, h), borda);
        DrawRect(new Rect(x, y + 46f, w, 1f), borda);

        GUI.Label(new Rect(x + 15f, y + 9f, Mathf.Max(180f, w - 72f), 30f),
            "<size=18><b>✈  CENTRO DE CONTROLE — AEROPORTO COMERCIAL</b></size>");

        Color bgPrev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.75f, 0.22f, 0.24f, 1f);
        if (GUI.Button(new Rect(janela.xMax - 40f, y + 10f, 30f, 26f), "<b>✕</b>"))
        {
            GUI.backgroundColor = bgPrev;
            try { fi?.SetValue(this, false); } catch { }
            GestorMenusExclusivos.Fechar(this);
            if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
            return;
        }
        GUI.backgroundColor = bgPrev;

        // Abas — 3 abas comerciais
        float tabY = y + 52f;
        string[] abas = { "📋 VOOS", "📄 CONTRATOS", "📊 ESTATÍSTICAS" };
        float tabW = (w - 30f) / abas.Length;
        for (int i = 0; i < abas.Length; i++)
        {
            Rect tabRect = new Rect(x + 15 + i * tabW, tabY, tabW - 2f, 30f);
            Color tabColor = (abaMenuComercial == i) ? new Color(0.08f, 0.36f, 0.52f) : new Color(0.06f, 0.13f, 0.20f);
            DrawRect(tabRect, tabColor);
            if (GUI.Button(tabRect, abas[i])) abaMenuComercial = i;
        }

        // Conteúdo
        Rect conteudo = new Rect(x + 10f, tabY + 36f, w - 20f, Mathf.Max(120f, h - 98f));
        GUILayout.BeginArea(conteudo);

        switch (abaMenuComercial)
        {
            case 0: DesenharPainelVoos(); break;
            case 1: DesenharAbaCOntratos(); break;
            case 2: DesenharAbaEstatisticas(); break;
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
            GUILayout.Label($"  {(cia.longoAlcance ? "LONGO CURSO · " : string.Empty)}{cia.diasDuracao}d  |  ${cia.valorPorPassagem}/pax", GUILayout.Width(190));
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
                if (ativo.longoAlcance) GUILayout.Label("✈ LONGO CURSO", GUILayout.Width(110));
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
