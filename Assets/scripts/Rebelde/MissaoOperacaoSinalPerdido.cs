using System;
using System.Collections;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

public enum EstadoMissaoSinalPerdido
{
    Disponivel,
    ExpedicaoEmAndamento,
    AeronaveDesaparecida,
    BuscasIniciadas,
    Concluida
}

/// <summary>Evento único de desaparecimento que ativa a facção e libera buscas.</summary>
[DisallowMultipleComponent]
public sealed class MissaoOperacaoSinalPerdido : MonoBehaviour
{
    public static event Action OnOrganizacaoAtivada;
    public static MissaoOperacaoSinalPerdido Instancia { get; private set; }

    [Header("Missão")]
    [SerializeField, Min(0)] private long custoExpedicao = 250000;
    [SerializeField, Min(10f)] private float tempoAteDesaparecimento = 180f;
    [SerializeField, Min(1)] private int cientistasTransportados = 8;
    [SerializeField, Min(1000f)] private float distanciaDaRotaAteDesaparecimento = 80000f;
    [SerializeField] private EstadoMissaoSinalPerdido estado = EstadoMissaoSinalPerdido.Disponivel;
    [Header("Aeronave e rota")]
    [SerializeField] private ControleAviao aeronaveDaExpedicao;
    [SerializeField] private GameObject prefabAeronaveCientifica;
    [SerializeField] private GerenciadorAeroporto aeroportoCivil;
    [SerializeField] private Transform pontoCriacaoAviao;
    [SerializeField] private Transform destinoDaExpedicao;
    [SerializeField] private RebeldeControlador controladorRebelde;

    private Coroutine temporizadorDesaparecimento;
    private bool eventoExecutado;
    private string aeronaveExpedicaoUniqueId = string.Empty;
    private readonly List<string> nomesPesquisadores = new List<string>(8);
    public EstadoMissaoSinalPerdido Estado => estado;
    public long CustoExpedicao => custoExpedicao;
    public int CientistasTransportados => cientistasTransportados;
    public float TempoRestante => temporizadorDesaparecimento != null ? Mathf.Max(0f, tempoAteDesaparecimento - tempoDecorrido) : 0f;
    private float tempoDecorrido;
    public float TempoDecorrido => tempoDecorrido;
    public bool EventoExecutado => eventoExecutado;

    public SaveMissaoSinalPerdidoData CapturarEstadoSalvavel()
    {
        return new SaveMissaoSinalPerdidoData
        {
            estado = (int)estado,
            eventoExecutado = eventoExecutado,
            tempoDecorrido = tempoDecorrido,
            quantidadePesquisadores = cientistasTransportados,
            nomesPesquisadores = new List<string>(nomesPesquisadores),
            aeronaveExpedicaoUniqueId = CapturarIdAeronaveExpedicao()
        };
    }

    private string CapturarIdAeronaveExpedicao()
    {
        if (aeronaveDaExpedicao == null) return aeronaveExpedicaoUniqueId;
        SaveableEntity salvavel = SaveableEntity.Garantir(aeronaveDaExpedicao.gameObject);
        return salvavel != null && !string.IsNullOrWhiteSpace(salvavel.UniqueId)
            ? salvavel.UniqueId : aeronaveExpedicaoUniqueId;
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this) { enabled = false; return; }
        Instancia = this;
    }

    private void OnDestroy() { if (Instancia == this) Instancia = null; }

    private bool GarantirAeronaveEAeroporto(SistemaGovernoMundial governo, out string mensagem)
    {
        mensagem = string.Empty;
        if (prefabAeronaveCientifica == null)
        {
            DadosConstrucao fichaTransporte = Resources.Load<DadosConstrucao>("Construcoes/C400");
            if (fichaTransporte == null || !fichaTransporte.TryGetPrefabBasico(out prefabAeronaveCientifica)
                || prefabAeronaveCientifica == null)
            { mensagem = "O transporte científico C-400 não está disponível no catálogo de aeronaves."; return false; }
        }
        if (aeroportoCivil == null)
        {
            GerenciadorAeroportoComercial[] aeroportos = FindObjectsByType<GerenciadorAeroportoComercial>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < aeroportos.Length; i++)
            {
                GerenciadorAeroportoComercial candidato = aeroportos[i];
                if (candidato != null && candidato.isActiveAndEnabled && !candidato.semEnergia
                    && candidato.ObterTeamIdSeguro() == governo.teamJogador)
                { aeroportoCivil = candidato; break; }
            }
        }
        if (aeroportoCivil == null)
        { mensagem = "Construa ou configure um aeroporto comercial operacional do jogador."; return false; }
        if (!(aeroportoCivil is GerenciadorAeroportoComercial))
        { mensagem = "A Operação Sinal Perdido precisa de um aeroporto comercial operacional do jogador."; return false; }
        GerenciadorAeroportoComercial aeroportoComercial = (GerenciadorAeroportoComercial)aeroportoCivil;
        if (!aeroportoComercial.isActiveAndEnabled || aeroportoComercial.semEnergia
            || !aeroportoComercial.PossuiRotaPousoValida())
        { mensagem = "O aeroporto comercial do jogador precisa estar energizado e ter pista com rota de pouso configurada."; return false; }
        if (aeroportoCivil.ObterTeamIdSeguro() != governo.teamJogador)
        { mensagem = "O aeroporto comercial precisa pertencer ao jogador."; return false; }
        if (pontoCriacaoAviao == null) pontoCriacaoAviao = aeroportoCivil.transform;
        if (pontoCriacaoAviao != aeroportoCivil.transform && !pontoCriacaoAviao.IsChildOf(aeroportoCivil.transform))
        { mensagem = "PontoCriacaoAviao precisa pertencer ao aeroporto civil configurado."; return false; }
        return true;
    }

    private Vector3 ObterDestinoExpedicao()
    {
        if (destinoDaExpedicao != null) return destinoDaExpedicao.position;
        if (controladorRebelde != null
            && controladorRebelde.TentarObterDestinoOperacao(out Vector3 destinoOperacional))
            return destinoOperacional;
        Vector3 direcao = aeroportoCivil != null ? aeroportoCivil.transform.forward : Vector3.forward;
        direcao.y = 0f;
        if (direcao.sqrMagnitude < 0.001f) direcao = Vector3.forward;
        Vector3 origem = aeroportoCivil != null ? aeroportoCivil.transform.position : transform.position;
        return origem + direcao.normalized * Mathf.Max(1000f, distanciaDaRotaAteDesaparecimento) + Vector3.up * 1500f;
    }

    private bool PrepararAutonomiaExpedicao(C700TransporteAereo transporte, Vector3 destino, out string mensagem)
    {
        mensagem = string.Empty;
        if (transporte == null) return true;

        CombustivelUnidade combustivel = transporte.GetComponent<CombustivelUnidade>();
        if (combustivel == null || !combustivel.usaCombustivel || combustivel.combustivelInfinito) return true;

        Vector3 origem = transporte.transform.position;
        GerenciadorAeroporto aeroportoRetorno = transporte.aeroportoOrigem != null
            ? transporte.aeroportoOrigem : aeroportoCivil;
        Vector3 destinoRetorno = aeroportoRetorno != null && aeroportoRetorno.wpPronto != null
            ? aeroportoRetorno.wpPronto.position
            : aeroportoRetorno != null && aeroportoRetorno.hangarAviao != null
                ? aeroportoRetorno.hangarAviao.position
                : aeroportoRetorno != null ? aeroportoRetorno.transform.position : origem;

        float velocidade = Mathf.Max(1f, transporte.velocidadeCruzeiro);
        float distanciaIda = Vector3.Distance(origem, destino) + 2000f;
        float distanciaVolta = Vector3.Distance(destino, destinoRetorno) + 2000f;
        float consumoIda = combustivel.EstimarConsumoParaDistancia(distanciaIda, velocidade);
        float consumoVolta = combustivel.EstimarConsumoParaDistancia(distanciaVolta, velocidade);
        float reservaRetorno = Mathf.Clamp(transporte.reservaRetornoPercentual, 0f, 0.8f);
        float autonomiaNecessaria = Mathf.Max(consumoIda + consumoVolta,
            consumoIda / Mathf.Max(0.2f, 1f - reservaRetorno));
        autonomiaNecessaria = Mathf.Ceil(autonomiaNecessaria * 1.15f);
        if (float.IsNaN(autonomiaNecessaria) || float.IsInfinity(autonomiaNecessaria) || autonomiaNecessaria <= 0f)
        {
            mensagem = "Não foi possível calcular a autonomia necessária para a expedição.";
            return false;
        }

        // O C-400 criado para esta expedição recebe combustível de missão
        // suficiente para a ida, o retorno e a reserva operacional. A
        // configuração do prefab e das outras aeronaves continua intacta.
        if (combustivel.Capacidade < autonomiaNecessaria || combustivel.CombustivelAtual < autonomiaNecessaria)
        {
            combustivel.preservarCapacidadeConfigurada = true;
            combustivel.capacidade = Mathf.Max(combustivel.Capacidade, autonomiaNecessaria);
            combustivel.combustivelAtual = combustivel.capacidade;
        }

        return true;
    }

    private void PrepararManifestoPesquisadores(GameObject aeronave)
    {
        nomesPesquisadores.Clear();
        for (int i = 0; i < cientistasTransportados; i++) nomesPesquisadores.Add("Pesquisador " + (i + 1).ToString("00"));
        ManifestoPesquisadoresExpedicao manifesto = aeronave.GetComponent<ManifestoPesquisadoresExpedicao>();
        if (manifesto == null) manifesto = aeronave.AddComponent<ManifestoPesquisadoresExpedicao>();
        manifesto.Configurar(cientistasTransportados, nomesPesquisadores);
    }

    public bool FinanciarExpedicao(out string mensagem)
    {
        mensagem = string.Empty;
        if (estado != EstadoMissaoSinalPerdido.Disponivel || eventoExecutado)
        { mensagem = "A Operação Sinal Perdido já foi iniciada."; return false; }
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null)
        { mensagem = "Sistema de governo indisponível."; return false; }
        if (!GarantirAeronaveEAeroporto(governo, out mensagem)) return false;
        if (governo.ObterSaldoDisponivelParaOperacao(governo.teamJogador) < custoExpedicao)
        { mensagem = "Saldo insuficiente para financiar a expedição."; return false; }
        bool aeronaveCriadaAgora = false;
        if (aeronaveDaExpedicao == null)
        {
            GameObject objeto = Instantiate(prefabAeronaveCientifica, pontoCriacaoAviao.position, pontoCriacaoAviao.rotation, aeroportoCivil.transform);
            aeronaveDaExpedicao = objeto.GetComponent<ControleAviao>();
            if (aeronaveDaExpedicao == null)
            {
                Destroy(objeto);
                aeronaveDaExpedicao = null;
                mensagem = "O prefab científico não possui ControleAviao.";
                return false;
            }
            aeronaveCriadaAgora = true;
            IdentidadeUnidade identidade = objeto.GetComponent<IdentidadeUnidade>();
            if (identidade == null) identidade = objeto.AddComponent<IdentidadeUnidade>();
            identidade.teamID = governo.teamJogador;
            identidade.nomeDoPais = governo.NomePais(governo.teamJogador);
            identidade.tipoUnidade = TipoUnidade.Aereo;
            C700TransporteAereo transporteC700 = aeronaveDaExpedicao.GetComponent<C700TransporteAereo>();
            bool registrada = transporteC700 != null
                ? aeroportoCivil.RegistrarTransporteAereoRecebido(transporteC700)
                : aeroportoCivil.RegistrarAeronaveRecebida(aeronaveDaExpedicao);
            if (!registrada)
            {
                Destroy(objeto);
                aeronaveDaExpedicao = null;
                mensagem = "O aeroporto civil não conseguiu estacionar a aeronave científica.";
                return false;
            }
        }
        C700TransporteAereo c700 = aeronaveDaExpedicao.GetComponent<C700TransporteAereo>();
        if (c700 != null && (c700.aeroportoOrigem != aeroportoCivil
            || !aeroportoCivil.transportesC700NoPatio.Contains(c700)))
        {
            if (!aeroportoCivil.RegistrarTransporteAereoRecebido(c700))
            {
                if (aeronaveCriadaAgora) DescartarAeronaveDaExpedicao();
                mensagem = "O aeroporto comercial não conseguiu estacionar o transporte científico.";
                return false;
            }
        }
        PrepararManifestoPesquisadores(aeronaveDaExpedicao.gameObject);
        SaveableEntity aeronaveSalvavel = SaveableEntity.Garantir(aeronaveDaExpedicao.gameObject);
        aeronaveExpedicaoUniqueId = aeronaveSalvavel != null ? aeronaveSalvavel.UniqueId : string.Empty;
        if (c700 == null && aeronaveDaExpedicao.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio
            && aeronaveDaExpedicao.estadoAtual != ControleAviao.EstadoAviao.ReservaHangar)
        {
            if (aeronaveCriadaAgora) DescartarAeronaveDaExpedicao();
            mensagem = "A aeronave científica não está disponível no aeroporto.";
            return false;
        }
        if (c700 != null && c700.estadoAtual != C700TransporteAereo.EstadoC700.Solo
            && c700.estadoAtual != C700TransporteAereo.EstadoC700.Estacionado)
        {
            if (aeronaveCriadaAgora) DescartarAeronaveDaExpedicao();
            mensagem = "O transporte científico não está livre no aeroporto comercial.";
            return false;
        }
        Vector3 destinoExpedicao = ObterDestinoExpedicao();
        if (c700 != null && !PrepararAutonomiaExpedicao(c700, destinoExpedicao, out mensagem))
        {
            if (aeronaveCriadaAgora) DescartarAeronaveDaExpedicao();
            return false;
        }
        if (c700 == null && !aeronaveDaExpedicao.PodeExecutarMissaoComRetorno(destinoExpedicao))
        {
            string motivoRecusa = aeronaveDaExpedicao.UltimoMotivoRecusaOrdem;
            if (aeronaveCriadaAgora) DescartarAeronaveDaExpedicao();
            mensagem = "Aeronave científica indisponível para voo: " + motivoRecusa;
            return false;
        }
        IdentidadeUnidade[] identidadesAeronave = aeronaveDaExpedicao.GetComponentsInChildren<IdentidadeUnidade>(true);
        if (identidadesAeronave.Length == 0)
            identidadesAeronave = new[] { aeronaveDaExpedicao.gameObject.AddComponent<IdentidadeUnidade>() };
        for (int i = 0; i < identidadesAeronave.Length; i++)
        {
            if (identidadesAeronave[i] == null) continue;
            identidadesAeronave[i].teamID = governo.teamJogador;
            identidadesAeronave[i].nomeDoPais = governo.NomePais(governo.teamJogador);
            identidadesAeronave[i].tipoUnidade = TipoUnidade.Aereo;
            IA_WorldState.NotifyEntityChanged(identidadesAeronave[i]);
        }
        RegistroEntidadesJogo.NotificarAlteracao();
        bool ordemAceita = c700 != null
            ? c700.TentarReceberOrdemMover(destinoExpedicao, out mensagem)
            : aeronaveDaExpedicao.ReceberOrdemManual(destinoExpedicao);
        if (!ordemAceita)
        {
            string motivoRecusa = c700 != null ? mensagem : aeronaveDaExpedicao.UltimoMotivoRecusaOrdem;
            if (aeronaveCriadaAgora) DescartarAeronaveDaExpedicao();
            mensagem = "A aeronave recusou a ordem de decolagem: " + motivoRecusa;
            return false;
        }
        if (!governo.TentarPagar(governo.teamJogador, custoExpedicao))
        {
            // O comando só registra a missão; retorne a aeronave à vaga e não
            // cobre a expedição quando o saldo do jogador for insuficiente.
            if (c700 != null) c700.OrdenarRetornoAoAeroporto();
            else aeronaveDaExpedicao.ComandoRetornarBase();
            if (aeronaveCriadaAgora) DescartarAeronaveDaExpedicao();
            mensagem = "Saldo insuficiente para financiar a expedição.";
            return false;
        }
        estado = EstadoMissaoSinalPerdido.ExpedicaoEmAndamento;
        temporizadorDesaparecimento = StartCoroutine(ExecutarDesaparecimentoUnico());
        governo.RegistrarNoticia("Operação Sinal Perdido: a expedição científica de oito pesquisadores decolou.");
        mensagem = "Expedição financiada. A aeronave está a caminho.";
        return true;
    }

    private void DescartarAeronaveDaExpedicao()
    {
        if (aeronaveDaExpedicao == null) return;
        GerenciadorAeroporto aeroporto = aeronaveDaExpedicao.aeroportoOrigem != null
            ? aeronaveDaExpedicao.aeroportoOrigem : aeroportoCivil;
        C700TransporteAereo transporteC700 = aeronaveDaExpedicao.GetComponent<C700TransporteAereo>();
        if (aeroporto != null)
        {
            aeroporto.avioesNoPatio.Remove(aeronaveDaExpedicao);
            aeroporto.avioesNoHangar.Remove(aeronaveDaExpedicao);
            if (transporteC700 != null) aeroporto.transportesC700NoPatio.Remove(transporteC700);
        }
        Destroy(aeronaveDaExpedicao.gameObject);
        aeronaveDaExpedicao = null;
    }

    private IEnumerator ExecutarDesaparecimentoUnico()
    {
        while (tempoDecorrido < tempoAteDesaparecimento)
        {
            tempoDecorrido += Time.deltaTime;
            yield return null;
        }
        DescartarAeronaveDaExpedicao();
        temporizadorDesaparecimento = null;
        MarcarDesaparecimento();
    }

    private void MarcarDesaparecimento()
    {
        if (estado != EstadoMissaoSinalPerdido.ExpedicaoEmAndamento) return;
        eventoExecutado = true;
        estado = EstadoMissaoSinalPerdido.AeronaveDesaparecida;
        SistemaGovernoMundial.Instancia?.RegistrarNoticia("Operação Sinal Perdido: a aeronave científica desapareceu; seu rastreamento foi interrompido.");
        if (controladorRebelde != null) controladorRebelde.Ativar();
        OnOrganizacaoAtivada?.Invoke();
    }

    public bool IniciarBuscas(out string mensagem)
    {
        if (estado != EstadoMissaoSinalPerdido.AeronaveDesaparecida)
        { mensagem = "As buscas só podem começar após o desaparecimento da aeronave."; return false; }
        estado = EstadoMissaoSinalPerdido.BuscasIniciadas;
        SistemaGovernoMundial.Instancia?.RegistrarNoticia("Operação Sinal Perdido: buscas iniciadas. Unidades aéreas e navais podem ser enviadas à última área conhecida.");
        mensagem = "Buscas iniciadas. Use unidades adequadas para investigar a última área conhecida.";
        return true;
    }

    public void RestaurarEstadoSalvo(SaveMissaoSinalPerdidoData salvo)
    {
        if (salvo == null) return;
        estado = (EstadoMissaoSinalPerdido)Mathf.Clamp(salvo.estado, 0, Enum.GetValues(typeof(EstadoMissaoSinalPerdido)).Length - 1);
        eventoExecutado = salvo.eventoExecutado;
        tempoDecorrido = Mathf.Clamp(salvo.tempoDecorrido, 0f, tempoAteDesaparecimento);
        cientistasTransportados = Mathf.Max(0, salvo.quantidadePesquisadores);
        nomesPesquisadores.Clear();
        if (salvo.nomesPesquisadores != null) nomesPesquisadores.AddRange(salvo.nomesPesquisadores);
        aeronaveExpedicaoUniqueId = salvo.aeronaveExpedicaoUniqueId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(aeronaveExpedicaoUniqueId))
        {
            SaveableEntity[] aeronavesSalvas = FindObjectsByType<SaveableEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < aeronavesSalvas.Length; i++)
                if (aeronavesSalvas[i] != null && aeronavesSalvas[i].UniqueId == aeronaveExpedicaoUniqueId)
                { aeronaveDaExpedicao = aeronavesSalvas[i].GetComponent<ControleAviao>(); break; }
        }
        if (eventoExecutado || estado >= EstadoMissaoSinalPerdido.AeronaveDesaparecida)
        {
            DescartarAeronaveDaExpedicao();
            eventoExecutado = true;
        }
        if (estado == EstadoMissaoSinalPerdido.ExpedicaoEmAndamento && !eventoExecutado)
        {
            if (aeronaveDaExpedicao != null) temporizadorDesaparecimento = StartCoroutine(ExecutarDesaparecimentoUnico());
            else
            {
                tempoDecorrido = tempoAteDesaparecimento;
                MarcarDesaparecimento();
            }
        }
        if (estado >= EstadoMissaoSinalPerdido.AeronaveDesaparecida) OnOrganizacaoAtivada?.Invoke();
    }
}
