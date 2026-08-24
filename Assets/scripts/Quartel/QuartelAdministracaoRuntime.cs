using System;
using System.Collections.Generic;
using UnityEngine;

public enum QuartelForcaV2
{
    Infantaria,
    Veiculos,
    Naval,
    Aerea
}

public enum QuartelStatusRecrutaV2
{
    EmFormacao,
    Ativo
}

[Serializable]
public sealed class QuartelRecrutaV2
{
    public string id;
    public QuartelForcaV2 forca;
    public QuartelStatusRecrutaV2 estado;
    public float progressoSegundos;
    public float tempoTotalSegundos;
    public int diaRecrutamento;
}

[Serializable]
public sealed class QuartelPerdaV2
{
    public string unidadeId;
    public string nomeUnidade;
    public QuartelForcaV2 forca;
    public int militares;
    public string motivo;
    public int dia;
}

[Serializable]
public sealed class QuartelForcaSnapshotV2
{
    public QuartelForcaV2 forca;
    public int unidades;
    public int unidadesEmMissao;
    public int unidadesDanificadas;
    public int unidadesInoperantes;
    public int pessoalExigido;
    public int pessoalAlocado;
}

[Serializable]
public sealed class QuartelAeronaveSnapshotV2
{
    public string id;
    public string nome;
    public string estadoVoo;
    public string estadoPortaAvioes;
    public string operacao;
    public string missao;
    public string baseAtual;
    public string vaga;
    public string autoridadeMovimento;
    public float combustivelAtual;
    public float combustivelCapacidade;
    public float combustivelPercentual;
    public float integridadePercentual;
    public float distanciaAoQuartel;
    public bool combustivelDisponivel;
    public bool conectadaAoQuartel;
}

[Serializable]
public sealed class QuartelComunicacaoSnapshotV2
{
    public string horario;
    public string origem;
    public string tipo;
    public string mensagem;
    public float distanciaAoQuartel;
    public bool inimigo;
}

/// <summary>
/// Camada administrativa exclusiva do Quartel.
///
/// Ela nao envia ordens de movimento e nao substitui os controladores de
/// unidades. Seu papel e manter pessoal, folha, tripulacao, treinamento e
/// alertas do Quartel com identidade estavel e sem duplicar soldados.
/// </summary>
[DisallowMultipleComponent]
public sealed class QuartelAdministracaoRuntime : MonoBehaviour
{
    [Header("Identidade administrativa")]
    [Min(1)] public int teamID = 1;
    [Min(1)] public int recrutamentoPorDia = 4;
    [Min(1f)] public float tempoFormacaoPadraoSegundos = 10f;
    [Min(0)] public int custoFolhaPorMilitarDia = 1;
    [Min(1)] public int periodoFolhaDias = 15;
    public bool cobrarFolhaDoCaixa = true;
    public bool retornoAutomaticoAposAtividade = true;
    [Min(1f)] public float tempoOciosoParaRetornoSegundos = 8f;

    [Header("Tripulacao minima quando a unidade nao declarou consumo")]
    [Min(1)] public int tripulacaoMinimaInfantaria = 1;
    [Min(1)] public int tripulacaoMinimaVeiculo = 1;
    [Min(1)] public int tripulacaoMinimaNaval = 4;
    [Min(1)] public int tripulacaoMinimaAerea = 2;

    [Header("Historico administrativo")]
    [SerializeField] private List<QuartelRecrutaV2> recrutas = new List<QuartelRecrutaV2>();
    [SerializeField] private List<QuartelPerdaV2> perdas = new List<QuartelPerdaV2>();

    private GerenciadorQuartel quartel;
    private readonly List<ControleUnidade> unidadesGerenciadas = new List<ControleUnidade>();
    private readonly Dictionary<string, UnidadeConhecida> unidadesConhecidas = new Dictionary<string, UnidadeConhecida>(StringComparer.Ordinal);
    private readonly HashSet<string> perdasRegistradas = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<QuartelForcaV2, int> recrutasPorForca = new Dictionary<QuartelForcaV2, int>();
    private readonly Dictionary<ControleUnidade, float> unidadesDesdobradas = new Dictionary<ControleUnidade, float>();
    private readonly Dictionary<QuartelForcaV2, ContagemForca> contagensForca = new Dictionary<QuartelForcaV2, ContagemForca>();
    private readonly List<IdentidadeUnidade> identidadesRegistradas = new List<IdentidadeUnidade>(256);
    private readonly List<IdentidadeUnidade> identidadesReconhecimento = new List<IdentidadeUnidade>(256);
    private readonly List<ControleAviao> avioesRegistrados = new List<ControleAviao>(64);
    private readonly List<QuartelAeronaveSnapshotV2> aeronavesConectadas = new List<QuartelAeronaveSnapshotV2>(32);
    private readonly List<QuartelComunicacaoSnapshotV2> comunicacoes = new List<QuartelComunicacaoSnapshotV2>(32);
    private readonly Dictionary<long, float> ultimaComunicacaoPorContato = new Dictionary<long, float>(128);
    private readonly Dictionary<int, float> ultimoHeartbeatAeronave = new Dictionary<int, float>(32);

    private float proximaReconciliacao;
    private int ultimoDiaRecrutamento = -1;
    private int ultimoDiaFolha = -1;
    private int ultimoDiaObservado = -1;
    private int recrutasConcluidos;
    private int recrutasRecrutados;
    private int pessoalExigido;
    private int pessoalAlocado;
    private int unidadesInoperantes;
    private int unidadesComAlertaResgate;
    private int unidadesAvaliadasResgate;
    private int pessoalAdministrado;
    private long custoFolhaCalculado;
    private long custoFolhaDiario;
    private long folhaPagaTotal;
    private bool folhaPendente;
    private bool ultimoPagamentoRealizado;
    private int proximoDiaFolha = -1;
    private int diasFolhaPendentes;
    private int contatosInimigos;
    private int contatosSubmarinos;
    private int contatosMisseis;
    private int unidadesNoRaio;
    private int unidadesEmMissao;
    private int unidadesDanificadas;
    private string ultimoEvento = "Quartel administrativo pronto";
    private string ultimoMotivoFolha = string.Empty;
    private string ultimoAvisoResgate = string.Empty;
    private int sequenciaRecruta;

    private sealed class UnidadeConhecida
    {
        public ControleUnidade unidade;
        public string id;
        public string nome;
        public TipoUnidade tipo;
        public int militares;
    }

    private sealed class ContagemForca
    {
        public int unidades;
        public int unidadesEmMissao;
        public int unidadesDanificadas;
        public int unidadesInoperantes;
        public int pessoalExigido;
        public int pessoalAlocado;
    }

    [Serializable]
    public sealed class Snapshot
    {
        public int dia;
        public int alistaveis;
        public int reservistas;
        public int militaresAtivos;
        public int recrutasEmFormacao;
        public int recrutasConcluidos;
        public int recrutasRecrutados;
        public int recrutasInfantaria;
        public int recrutasVeiculos;
        public int recrutasNaval;
        public int recrutasAerea;
        public int pessoalExigido;
        public int pessoalAlocado;
        public int unidadesInoperantes;
        public int unidadesAvaliadasResgate;
        public int unidadesComAlertaResgate;
        public int perdasRegistradas;
        public int pessoalAdministrado;
        public long custoFolhaCalculado;
        public long custoFolhaDiario;
        public long folhaPagaTotal;
        public bool folhaPendente;
        public bool ultimoPagamentoRealizado;
        public int periodoFolhaDias;
        public int proximoDiaFolha;
        public int diasFolhaPendentes;
        public int unidadesNoRaio;
        public int unidadesEmMissao;
        public int unidadesDanificadas;
        public int contatosInimigos;
        public int contatosSubmarinos;
        public int contatosMisseis;
        public int mortosAcumulados;
        public int aeronavesNoRaio;
        public QuartelAeronaveSnapshotV2[] aeronaves;
        public QuartelComunicacaoSnapshotV2[] comunicacoes;
        public QuartelForcaSnapshotV2[] forcas;
        public float progressoTreinamento;
        public float segundosRestantesTreinamento;
        public string forcaTreinamento;
        public string ultimoEvento;
        public string ultimoMotivoFolha;
        public string ultimoAvisoResgate;
    }

    public IReadOnlyList<QuartelRecrutaV2> Recrutas => recrutas;
    public IReadOnlyList<QuartelPerdaV2> Perdas => perdas;
    public int UnidadesInoperantes => unidadesInoperantes;
    public int UnidadesComAlertaResgate => unidadesComAlertaResgate;
    public long CustoFolhaCalculado => custoFolhaCalculado;
    public long CustoFolhaDiario => custoFolhaDiario;
    public bool FolhaPendente => folhaPendente;
    public string UltimoEvento => ultimoEvento;

    private void Awake()
    {
        quartel = GetComponent<GerenciadorQuartel>();
        if (quartel != null)
        {
            teamID = Mathf.Max(1, quartel.teamID);
            tempoFormacaoPadraoSegundos = Mathf.Max(1f, quartel.tempoFormacaoSegundos);
        }
        GarantirDicionarioForcas();
        GarantirContagensForca();
    }

    private void Start()
    {
        InscreverNoRelogio();
        ultimoDiaObservado = ObterDiaAtual();
        proximaReconciliacao = 0f;
        ReconciliarAgora();
        if (ultimoDiaFolha < 0)
        {
            ProcessarFolha(ultimoDiaObservado);
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            InscreverNoRelogio();
        }
        RegistroEntidadesJogo.EntidadesAlteradas += MarcarDadosAlterados;
        BoeingE3Reconhecimento.OnContatoTransmitido -= AoReceberContatoDoE3;
        BoeingE3Reconhecimento.OnContatoTransmitido += AoReceberContatoDoE3;
    }

    private void OnDisable()
    {
        if (GerenciadorTempo.Instancia != null)
        {
            GerenciadorTempo.Instancia.OnDataAlterada -= AoMudarDia;
        }
        RegistroEntidadesJogo.EntidadesAlteradas -= MarcarDadosAlterados;
        BoeingE3Reconhecimento.OnContatoTransmitido -= AoReceberContatoDoE3;
    }

    private void MarcarDadosAlterados()
    {
        proximaReconciliacao = 0f;
    }

    private void AoReceberContatoDoE3(BoeingE3Reconhecimento.ContatoReconhecimento contato)
    {
        if (contato == null || contato.equipeObservadora != teamID || quartel == null) return;

        float distancia = Vector3.Distance(quartel.transform.position, contato.origemAeronavePosicao);
        float raioPermitido = Mathf.Max(quartel.raioDeCobertura, contato.alcanceComunicacao);
        if (distancia > raioPermitido + 0.01f) return;

        long chave = ((long)contato.idAlvo << 32) ^ (uint)contato.tipo;
        float agora = Time.unscaledTime;
        float ultimo;
        if (ultimaComunicacaoPorContato.TryGetValue(chave, out ultimo) && agora - ultimo < 2.5f)
        {
            return;
        }

        ultimaComunicacaoPorContato[chave] = agora;
        if (ultimaComunicacaoPorContato.Count > 512) ultimaComunicacaoPorContato.Clear();

        string origem = string.IsNullOrWhiteSpace(contato.origemAeronave) ? "AERONAVE" : contato.origemAeronave;
        string alvo = string.IsNullOrWhiteSpace(contato.nomeAlvo) ? "contato desconhecido" : contato.nomeAlvo;
        string mensagem = alvo + " | posicao " + contato.ultimaPosicaoConhecida.ToString("F0");
        comunicacoes.Insert(0, new QuartelComunicacaoSnapshotV2
        {
            horario = DateTime.Now.ToString("HH:mm:ss"),
            origem = origem + " -> QUARTEL",
            tipo = contato.tipo.ToString(),
            mensagem = mensagem,
            distanciaAoQuartel = distancia,
            inimigo = contato.inimigo
        });
        while (comunicacoes.Count > 24) comunicacoes.RemoveAt(comunicacoes.Count - 1);
        ultimoEvento = "Radio E-3: " + contato.tipo + " recebido de " + origem;
    }

    private void RegistrarHeartbeatAeronave(ControleAviao aviao, float distancia)
    {
        BoeingE3Reconhecimento e3 = aviao != null ? aviao.GetComponent<BoeingE3Reconhecimento>() : null;
        if (e3 == null) return;

        int id = e3.GetInstanceID();
        float agora = Time.unscaledTime;
        float ultimo;
        if (ultimoHeartbeatAeronave.TryGetValue(id, out ultimo) && agora - ultimo < 5f) return;
        ultimoHeartbeatAeronave[id] = agora;

        comunicacoes.Insert(0, new QuartelComunicacaoSnapshotV2
        {
            horario = DateTime.Now.ToString("HH:mm:ss"),
            origem = e3.name + " -> QUARTEL",
            tipo = "STATUS",
            mensagem = "voo " + e3.estadoAtual + " | altitude " + e3.transform.position.y.ToString("0") + " m | contatos " + e3.QuantidadeContatosAtivos,
            distanciaAoQuartel = distancia,
            inimigo = false
        });
        while (comunicacoes.Count > 24) comunicacoes.RemoveAt(comunicacoes.Count - 1);
        ultimoEvento = "Radio E-3: status recebido de " + e3.name;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        float delta = Mathf.Max(0f, Time.deltaTime);
        if (treinamentoAutomaticoAtivo())
        {
            ProcessarTreinamento(delta);
        }

        if (Time.unscaledTime >= proximaReconciliacao)
        {
            proximaReconciliacao = Time.unscaledTime + 0.75f;
            ReconciliarAgora();
            ProcessarRetornoDeAtividades();
        }
    }

    private bool treinamentoAutomaticoAtivo()
    {
        return quartel == null || quartel.treinamentoAutomatico;
    }

    private void InscreverNoRelogio()
    {
        GerenciadorTempo.GarantirInstancia();
        if (GerenciadorTempo.Instancia == null) return;
        GerenciadorTempo.Instancia.OnDataAlterada -= AoMudarDia;
        GerenciadorTempo.Instancia.OnDataAlterada += AoMudarDia;
    }

    private void AoMudarDia()
    {
        ProcessarDia(ObterDiaAtual());
    }

    private int ObterDiaAtual()
    {
        return GerenciadorTempo.Instancia != null ? Mathf.Max(1, GerenciadorTempo.Instancia.totalDias) : 1;
    }

    public void ProcessarDia(int dia)
    {
        dia = Mathf.Max(1, dia);
        if (dia <= ultimoDiaObservado && ultimoDiaObservado >= 0) return;

        int inicio = ultimoDiaObservado < 0 ? dia : ultimoDiaObservado + 1;
        for (int diaProcessado = inicio; diaProcessado <= dia; diaProcessado++)
        {
            if (quartel == null || quartel.recrutamentoAutomatico)
            {
                RecrutarNoDia(diaProcessado);
            }

            if (ultimoDiaFolha < 0 || diaProcessado >= proximoDiaFolha)
            {
                ReconciliarAgora();
                ProcessarFolha(diaProcessado);
            }
        }

        ultimoDiaObservado = dia;
    }

    /// <summary>
    /// Permite testes e ferramentas internas avancarem a fila sem uma
    /// coroutine paralela. O fluxo de producao continua sendo do Quartel.
    /// </summary>
    public void ProcessarTreinamento(float segundos)
    {
        if (segundos <= 0f || recrutas == null) return;
        float tempoBase = Mathf.Max(1f, quartel != null ? quartel.tempoFormacaoSegundos : tempoFormacaoPadraoSegundos);

        for (int i = 0; i < recrutas.Count; i++)
        {
            QuartelRecrutaV2 recruta = recrutas[i];
            if (recruta == null || recruta.estado != QuartelStatusRecrutaV2.EmFormacao) continue;
            recruta.tempoTotalSegundos = tempoBase;
            recruta.progressoSegundos = Mathf.Min(tempoBase, recruta.progressoSegundos + segundos);
            if (recruta.progressoSegundos + 0.0001f < tempoBase) continue;
            ConcluirFormacao(recruta);
        }
    }

    public void RecrutarAgoraParaTesteOuComando()
    {
        RecrutarNoDia(ObterDiaAtual());
    }

    public void SolicitarRecrutamentoManual()
    {
        RecrutarNoDia(ObterDiaAtual());
    }

    public void SolicitarRecrutamentoManual(QuartelForcaV2 forca, int quantidade)
    {
        DadosPaisGoverno pais = ObterPais();
        if (pais == null)
        {
            ultimoEvento = "Recrutamento recusado: pais do Quartel nao encontrado";
            return;
        }

        int meta = quartel != null ? Mathf.Max(1, quartel.metaEfetivo) : 24;
        int emFormacao = ContarRecrutasEmFormacao();
        int falta = Mathf.Max(0, meta - pais.populacaoMilitarAtiva - emFormacao);
        int disponiveis = Mathf.Max(0, pais.alistaveis);
        int limite = Mathf.Clamp(quantidade, 1, 100);
        int aplicados = Mathf.Min(limite, Mathf.Min(falta, disponiveis));
        if (aplicados <= 0)
        {
            ultimoEvento = "Recrutamento recusado: sem alistaveis ou meta de efetivo atingida";
            return;
        }

        for (int i = 0; i < aplicados; i++)
        {
            pais.alistaveis = Mathf.Max(0, pais.alistaveis - 1);
            pais.reservistas += 1;
            recrutas.Add(new QuartelRecrutaV2
            {
                id = "quartel-recruta-" + (++sequenciaRecruta),
                forca = forca,
                estado = QuartelStatusRecrutaV2.EmFormacao,
                progressoSegundos = 0f,
                tempoTotalSegundos = Mathf.Max(1f, quartel != null ? quartel.tempoFormacaoSegundos : tempoFormacaoPadraoSegundos),
                diaRecrutamento = ObterDiaAtual()
            });
            recrutasPorForca[forca] = recrutasPorForca[forca] + 1;
            recrutasRecrutados++;
        }

        RecalcularPopulacao(pais);
        ultimoEvento = aplicados + " recruta(s) de " + NomeForca(forca) + " encaminhado(s) para formacao";
    }

    private void RecrutarNoDia(int dia)
    {
        if (ultimoDiaRecrutamento == dia) return;

        DadosPaisGoverno pais = ObterPais();
        if (pais == null) return;
        ultimoDiaRecrutamento = dia;

        int meta = quartel != null ? Mathf.Max(1, quartel.metaEfetivo) : 24;
        int emFormacao = ContarRecrutasEmFormacao();
        int falta = Mathf.Max(0, meta - pais.populacaoMilitarAtiva - emFormacao);
        int quantidade = Mathf.Min(Mathf.Max(0, recrutamentoPorDia), Mathf.Min(Mathf.Max(0, pais.alistaveis), falta));
        if (quantidade <= 0)
        {
            ultimoEvento = "Recrutamento diario aguardando alistaveis ou meta de efetivo";
            return;
        }

        for (int i = 0; i < quantidade; i++)
        {
            pais.alistaveis = Mathf.Max(0, pais.alistaveis - 1);
            pais.reservistas += 1;

            QuartelForcaV2 forca = EscolherForcaParaRecruta();
            recrutas.Add(new QuartelRecrutaV2
            {
                id = "quartel-recruta-" + (++sequenciaRecruta),
                forca = forca,
                estado = QuartelStatusRecrutaV2.EmFormacao,
                progressoSegundos = 0f,
                tempoTotalSegundos = Mathf.Max(1f, quartel != null ? quartel.tempoFormacaoSegundos : tempoFormacaoPadraoSegundos),
                diaRecrutamento = dia
            });
            recrutasConcluidos = ContarRecrutasConcluidos();
            recrutasPorForca[forca] = recrutasPorForca[forca] + 1;
            recrutasRecrutados++;
        }

        RecalcularPopulacao(pais);
        ultimoEvento = quantidade + " recruta(s) encaminhado(s) para formacao";
    }

    private void ConcluirFormacao(QuartelRecrutaV2 recruta)
    {
        if (recruta == null || recruta.estado != QuartelStatusRecrutaV2.EmFormacao) return;
        DadosPaisGoverno pais = ObterPais();
        if (pais == null) return;

        if (pais.reservistas > 0) pais.reservistas--;
        pais.populacaoMilitarAtiva++;
        recruta.estado = QuartelStatusRecrutaV2.Ativo;
        recruta.progressoSegundos = recruta.tempoTotalSegundos;
        recrutasConcluidos = ContarRecrutasConcluidos();
        RecalcularPopulacao(pais);
        ultimoEvento = "Formacao concluida: " + NomeForca(recruta.forca);
    }

    private void ProcessarFolha(int dia)
    {
        if (ultimoDiaFolha == dia && ultimoDiaFolha >= 0) return;
        ultimoDiaFolha = dia;
        periodoFolhaDias = Mathf.Max(1, periodoFolhaDias);
        proximoDiaFolha = dia + periodoFolhaDias;

        int pessoal = Mathf.Max(0, pessoalAdministrado + ContarRecrutasEmFormacao());
        custoFolhaDiario = (long)pessoal * Mathf.Max(0, custoFolhaPorMilitarDia);
        custoFolhaCalculado = custoFolhaDiario * periodoFolhaDias;
        diasFolhaPendentes = 0;
        folhaPendente = false;
        ultimoPagamentoRealizado = custoFolhaCalculado == 0;
        ultimoMotivoFolha = string.Empty;

        if (!cobrarFolhaDoCaixa || custoFolhaCalculado <= 0)
        {
            folhaPagaTotal += custoFolhaCalculado;
            return;
        }

        if (GerenciadorRecursos.Instancia != null)
        {
            ultimoPagamentoRealizado = GerenciadorRecursos.Instancia.TentarGastarDinheiro(custoFolhaCalculado);
        }
        else
        {
            DadosPaisGoverno pais = ObterPais();
            if (pais != null && pais.saldo >= custoFolhaCalculado)
            {
                pais.saldo -= custoFolhaCalculado;
                ultimoPagamentoRealizado = true;
            }
        }

        if (ultimoPagamentoRealizado)
        {
            folhaPagaTotal += custoFolhaCalculado;
            ultimoEvento = "Folha do Quartel paga no dia " + dia;
        }
        else
        {
            folhaPendente = true;
            diasFolhaPendentes = periodoFolhaDias;
            ultimoMotivoFolha = "Caixa insuficiente para a folha do Quartel";
            ultimoEvento = ultimoMotivoFolha;
        }
    }

    private void ReconciliarAgora()
    {
        GarantirDicionarioForcas();
        GarantirContagensForca();
        LimparContagensForca();
        unidadesGerenciadas.Clear();
        pessoalAdministrado = 0;
        unidadesAvaliadasResgate = 0;
        unidadesComAlertaResgate = 0;
        unidadesNoRaio = 0;
        unidadesEmMissao = 0;
        unidadesDanificadas = 0;
        contatosInimigos = 0;
        contatosSubmarinos = 0;
        contatosMisseis = 0;

        if (quartel == null) quartel = GetComponent<GerenciadorQuartel>();
        float raio = quartel != null ? Mathf.Max(1f, quartel.raioDeCobertura) : 2000f;
        float raioSqr = raio * raio;

        RegistroEntidadesJogo.FillUnidades(identidadesRegistradas);
        for (int i = 0; i < identidadesRegistradas.Count; i++)
        {
            IdentidadeUnidade identidade = identidadesRegistradas[i];
            if (identidade == null || identidade.teamID != teamID || identidade.tipoUnidade == TipoUnidade.Estrutura) continue;
            if (quartel != null && (identidade.transform.position - quartel.transform.position).sqrMagnitude > raioSqr) continue;
            ControleUnidade controle = identidade.GetComponent<ControleUnidade>();
            AdicionarUnidadeGerenciada(controle, identidade);
            if (controle != null) unidadesNoRaio++;
        }

        if (quartel != null)
        {
            AdicionarListaArmazenada(quartel.soldadosNoDormitorio);
            AdicionarListaArmazenada(quartel.veiculosNoQuartel);
        }

        unidadesGerenciadas.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
        int disponiveis = ObterMilitaresAtivos();
        pessoalExigido = 0;
        pessoalAlocado = 0;
        unidadesInoperantes = 0;

        for (int i = 0; i < unidadesGerenciadas.Count; i++)
        {
            ControleUnidade unidade = unidadesGerenciadas[i];
            if (unidade == null) continue;
            IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>();
            int exigidos = CalcularTripulacao(identidade);
            QuartelForcaV2 forca = ConverterForca(identidade != null ? identidade.tipoUnidade : TipoUnidade.Infantaria);
            ContagemForca contagem = contagensForca[forca];
            contagem.unidades++;
            pessoalExigido += exigidos;
            int alocados = Mathf.Min(exigidos, disponiveis);
            disponiveis -= alocados;
            pessoalAlocado += alocados;
            bool inoperante = alocados < exigidos;
            if (inoperante) unidadesInoperantes++;
            contagem.pessoalExigido += exigidos;
            contagem.pessoalAlocado += alocados;
            if (inoperante) contagem.unidadesInoperantes++;
            if (EstaEmAtividade(unidade))
            {
                unidadesEmMissao++;
                contagem.unidadesEmMissao++;
            }

            if (inoperante != unidade.BloqueioAdministrativoQuartelAtivo)
            {
                unidade.DefinirBloqueioAdministrativo(inoperante, inoperante ? "Sem militares ativos para tripulacao" : string.Empty);
            }
            AvaliarResgate(unidade, contagem);
        }

        pessoalAdministrado = pessoalAlocado;

        RegistrarPerdasDeObjetosDestruidos();
        AtualizarContatosReconhecimento();
        AtualizarAeronavesConectadas(raio, raioSqr);
    }

    private void AtualizarAeronavesConectadas(float raio, float raioSqr)
    {
        aeronavesConectadas.Clear();
        RegistroEntidadesJogo.FillAvioes(avioesRegistrados);

        if (avioesRegistrados.Count == 0)
        {
            ControleAviao[] encontrados = FindObjectsByType<ControleAviao>(FindObjectsSortMode.None);
            for (int i = 0; i < encontrados.Length; i++)
            {
                if (encontrados[i] != null && !avioesRegistrados.Contains(encontrados[i]))
                    avioesRegistrados.Add(encontrados[i]);
            }
        }

        for (int i = 0; i < avioesRegistrados.Count; i++)
        {
            ControleAviao aviao = avioesRegistrados[i];
            if (aviao == null) continue;

            IdentidadeUnidade identidade = aviao.GetComponent<IdentidadeUnidade>();
            if (identidade == null) identidade = aviao.GetComponentInParent<IdentidadeUnidade>();
            if (identidade == null || identidade.teamID != teamID) continue;

            BoeingE3Reconhecimento e3 = aviao.GetComponent<BoeingE3Reconhecimento>();
            float raioAeronave = e3 != null ? Mathf.Max(raio, e3.alcanceReconhecimento) : raio;
            float raioAeronaveSqr = raioAeronave * raioAeronave;
            float distanciaSqr = quartel == null
                ? 0f
                : (aviao.transform.position - quartel.transform.position).sqrMagnitude;
            if (quartel != null && distanciaSqr > raioAeronaveSqr) continue;

            float distancia = Mathf.Sqrt(distanciaSqr);
            aeronavesConectadas.Add(CriarSnapshotAeronave(aviao, distancia, raioAeronave));
            RegistrarHeartbeatAeronave(aviao, distancia);
        }
    }

    private QuartelAeronaveSnapshotV2 CriarSnapshotAeronave(ControleAviao aviao, float distancia, float raio)
    {
        AeronaveEmbarcadaV2 aeronaveV2 = aviao.GetComponent<AeronaveEmbarcadaV2>();
        RegistroAeronavePortaAvioesV2 registroV2 = aeronaveV2 != null ? aeronaveV2.Registro : null;
        ControleUnidade controleUnidade = aviao.GetComponent<ControleUnidade>();
        CombustivelUnidade combustivel = aviao.GetComponent<CombustivelUnidade>();
        SistemaDeDanos danos = aviao.GetComponent<SistemaDeDanos>();

        float capacidade = combustivel != null ? combustivel.Capacidade : 0f;
        float atual = combustivel != null ? combustivel.CombustivelAtual : 0f;
        float integridade = danos != null && danos.vidaMaxima > 0f
            ? Mathf.Clamp01(danos.vidaAtual / danos.vidaMaxima)
            : 1f;

        string id = registroV2 != null && !string.IsNullOrWhiteSpace(registroV2.id)
            ? registroV2.id
            : ObterIdPersistenteAeronave(aviao.gameObject);
        string baseAtual = registroV2 != null && !string.IsNullOrWhiteSpace(registroV2.portaAvioesAtual)
            ? registroV2.portaAvioesAtual
            : aviao.aeroportoOrigem != null ? aviao.aeroportoOrigem.name : "Fora de base registrada";
        string operacao = registroV2 != null && !string.IsNullOrWhiteSpace(registroV2.operacaoAtual)
            ? registroV2.operacaoAtual
            : "Nenhuma operacao V2 registrada";
        string missao = registroV2 != null && !string.IsNullOrWhiteSpace(registroV2.missaoAtual)
            ? registroV2.missaoAtual
            : controleUnidade != null && controleUnidade.OrdemAtual == OrdemControleUnidade.Patrulhando
                ? "Patrulha"
                : "Nenhuma missao registrada";
        string vaga = registroV2 != null
            ? !string.IsNullOrWhiteSpace(registroV2.vagaOcupada) ? registroV2.vagaOcupada : registroV2.vagaReservada
            : string.Empty;

        return new QuartelAeronaveSnapshotV2
        {
            id = id,
            nome = aviao.name,
            estadoVoo = aviao.estadoAtual.ToString(),
            estadoPortaAvioes = registroV2 != null ? registroV2.estado.ToString() : "Nao vinculada ao porta-avioes V2",
            operacao = operacao,
            missao = missao,
            baseAtual = baseAtual,
            vaga = string.IsNullOrWhiteSpace(vaga) ? "Sem vaga registrada" : vaga,
            autoridadeMovimento = aeronaveV2 != null ? aeronaveV2.DonoMovimento : string.Empty,
            combustivelAtual = atual,
            combustivelCapacidade = capacidade,
            combustivelPercentual = capacidade > 0f ? Mathf.Clamp01(atual / capacidade) : 0f,
            integridadePercentual = integridade,
            distanciaAoQuartel = distancia,
            combustivelDisponivel = combustivel != null && combustivel.usaCombustivel,
            conectadaAoQuartel = quartel == null || distancia <= raio + 0.01f
        };
    }

    private string ObterIdPersistenteAeronave(GameObject objeto)
    {
        SaveableEntity saveable = objeto != null ? objeto.GetComponent<SaveableEntity>() : null;
        if (saveable != null && !string.IsNullOrWhiteSpace(saveable.UniqueId)) return saveable.UniqueId;
        return objeto == null ? "Aeronave sem objeto" : "runtime-" + objeto.GetInstanceID();
    }

    private void AdicionarUnidadeGerenciada(ControleUnidade unidade, IdentidadeUnidade identidade)
    {
        if (unidade == null || identidade == null || unidadesGerenciadas.Contains(unidade)) return;
        unidadesGerenciadas.Add(unidade);

        string id = ObterIdEstavel(unidade.gameObject);
        if (!unidadesConhecidas.ContainsKey(id))
        {
            unidadesConhecidas.Add(id, new UnidadeConhecida
            {
                unidade = unidade,
                id = id,
                nome = unidade.name,
                tipo = identidade.tipoUnidade,
                militares = CalcularTripulacao(identidade)
            });
        }
    }

    private void AdicionarListaArmazenada(List<ControleUnidade> lista)
    {
        if (lista == null) return;
        for (int i = 0; i < lista.Count; i++)
        {
            ControleUnidade unidade = lista[i];
            if (unidade == null) continue;
            AdicionarUnidadeGerenciada(unidade, unidade.GetComponent<IdentidadeUnidade>());
        }
    }

    private void AvaliarResgate(ControleUnidade unidade, ContagemForca contagem)
    {
        SistemaDeDanos danos = unidade != null ? unidade.GetComponent<SistemaDeDanos>() : null;
        if (danos == null) return;
        unidadesAvaliadasResgate++;
        if (danos.vidaAtual + 0.01f < danos.vidaMaxima)
        {
            unidadesComAlertaResgate++;
            unidadesDanificadas++;
            if (contagem != null) contagem.unidadesDanificadas++;
            ultimoAvisoResgate = unidade.name + " precisa de resgate/reparo";
        }
    }

    private bool EstaEmAtividade(ControleUnidade unidade)
    {
        if (unidade == null) return false;
        if (unidade.PossuiOrdemMovimentoAtiva) return true;
        if (unidade.OrdemAtual == OrdemControleUnidade.Movendo
            || unidade.OrdemAtual == OrdemControleUnidade.Patrulhando
            || unidade.OrdemAtual == OrdemControleUnidade.Seguindo
            || unidade.OrdemAtual == OrdemControleUnidade.Recuando)
        {
            return true;
        }

        ControleAviao aviao = unidade.GetComponent<ControleAviao>();
        return aviao != null
            && (aviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao
                || aviao.estadoAtual == ControleAviao.EstadoAviao.Decolando
                || aviao.estadoAtual == ControleAviao.EstadoAviao.Pousando
                || aviao.estadoAtual == ControleAviao.EstadoAviao.Taxiando
                || aviao.estadoAtual == ControleAviao.EstadoAviao.RetornandoPraVaga);
    }

    private void AtualizarContatosReconhecimento()
    {
        if (teamID <= 0) return;
        identidadesReconhecimento.Clear();
        RegistroEntidadesJogo.FillUnidades(identidadesReconhecimento);
        if (identidadesReconhecimento.Count == 0)
        {
            IdentidadeUnidade[] encontrados = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            for (int i = 0; i < encontrados.Length; i++)
            {
                if (encontrados[i] != null && !identidadesReconhecimento.Contains(encontrados[i]))
                    identidadesReconhecimento.Add(encontrados[i]);
            }
        }

        for (int i = 0; i < identidadesReconhecimento.Count; i++)
        {
            IdentidadeUnidade alvo = identidadesReconhecimento[i];
            if (alvo == null || alvo.teamID == teamID || alvo.teamID <= 0) continue;
            BoeingE3Reconhecimento.ContatoReconhecimento contato;
            if (!BoeingE3Reconhecimento.TryObterContato(teamID, alvo.GetInstanceID(), out contato) || contato == null) continue;
            contatosInimigos++;
            if (contato.tipo == BoeingE3Reconhecimento.TipoContatoReconhecimento.SubmarinoNaSuperficie) contatosSubmarinos++;
        }

        List<MissileThreatTracker> ameacas = new List<MissileThreatTracker>(32);
        MissileThreatTracker.CopiarAmeacasAtivas(ameacas);
        for (int i = 0; i < ameacas.Count; i++)
        {
            MissileThreatTracker ameaca = ameacas[i];
            if (ameaca != null && ameaca.TeamOrigem > 0 && ameaca.TeamOrigem != teamID) contatosMisseis++;
        }
    }

    private void RegistrarPerdasDeObjetosDestruidos()
    {
        foreach (KeyValuePair<string, UnidadeConhecida> par in unidadesConhecidas)
        {
            UnidadeConhecida conhecida = par.Value;
            if (conhecida == null || conhecida.unidade != null || perdasRegistradas.Contains(par.Key)) continue;

            perdasRegistradas.Add(par.Key);
            perdas.Add(new QuartelPerdaV2
            {
                unidadeId = par.Key,
                nomeUnidade = conhecida.nome,
                forca = ConverterForca(conhecida.tipo),
                militares = Mathf.Max(1, conhecida.militares),
                motivo = "Unidade destruida enquanto administrada pelo Quartel",
                dia = ObterDiaAtual()
            });
            ultimoEvento = "Perda registrada: " + conhecida.nome;
        }
    }

    private void ProcessarRetornoDeAtividades()
    {
        if (!retornoAutomaticoAposAtividade || quartel == null) return;

        List<ControleUnidade> remover = new List<ControleUnidade>();
        foreach (KeyValuePair<ControleUnidade, float> par in unidadesDesdobradas)
        {
            ControleUnidade unidade = par.Key;
            if (unidade == null || !unidade.gameObject.activeInHierarchy || quartel.soldadosNoDormitorio.Contains(unidade) || quartel.veiculosNoQuartel.Contains(unidade))
            {
                remover.Add(unidade);
                continue;
            }

            if (Time.time - par.Value < tempoOciosoParaRetornoSegundos) continue;
            if (unidade.selecionado || unidade.PossuiOrdemMovimentoAtiva) continue;
            if (unidade.OrdemAtual != OrdemControleUnidade.Ociosa && unidade.OrdemAtual != OrdemControleUnidade.Parada) continue;
            if ((unidade.transform.position - quartel.transform.position).sqrMagnitude > quartel.raioDeCobertura * quartel.raioDeCobertura) continue;

            quartel.ReceberUnidade(unidade);
            remover.Add(unidade);
            ultimoEvento = "Unidade ociosa retornando ao Quartel: " + unidade.name;
        }

        for (int i = 0; i < remover.Count; i++) unidadesDesdobradas.Remove(remover[i]);
    }

    public void RegistrarUnidadeDesdobrada(ControleUnidade unidade)
    {
        if (unidade != null) unidadesDesdobradas[unidade] = Time.time;
    }

    public void RegistrarResgateManual()
    {
        int alertasAntes = unidadesComAlertaResgate;
        if (quartel != null) quartel.SolicitarReparosNoRaio();
        ReconciliarAgora();
        int reparadas = Mathf.Max(0, alertasAntes - unidadesComAlertaResgate);
        ultimoAvisoResgate = unidadesComAlertaResgate > 0
            ? reparadas + " unidade(s) recuperada(s); " + unidadesComAlertaResgate + " ainda danificada(s)"
            : (reparadas > 0 ? reparadas + " unidade(s) recuperada(s)" : "Nenhuma unidade danificada na cobertura");
        ultimoEvento = "Recuperacao manual executada: " + reparadas + " unidade(s) reparada(s)";
    }

    public void CapturarEstadoSave(SaveQuartelStateData destino)
    {
        if (destino == null) return;
        destino.quartelTeamID = teamID;
        destino.adminUltimoDiaObservado = ultimoDiaObservado;
        destino.adminUltimoDiaRecrutamento = ultimoDiaRecrutamento;
        destino.adminUltimoDiaFolha = ultimoDiaFolha;
        destino.adminProximoDiaFolha = proximoDiaFolha;
        destino.adminSequenciaRecruta = sequenciaRecruta;
        destino.adminRecrutasConcluidos = recrutasConcluidos;
        destino.adminRecrutasRecrutados = recrutasRecrutados;
        destino.adminFolhaPagaTotal = folhaPagaTotal;
        destino.adminCustoFolhaDiario = custoFolhaDiario;
        destino.adminCustoFolhaPeriodo = custoFolhaCalculado;
        destino.adminDiasFolhaPendentes = diasFolhaPendentes;
        destino.adminPerdasRegistradas = perdas.Count;
        destino.adminRecrutas.Clear();
        for (int i = 0; i < recrutas.Count; i++)
        {
            QuartelRecrutaV2 recruta = recrutas[i];
            if (recruta == null) continue;
            destino.adminRecrutas.Add(new SaveQuartelRecrutaData
            {
                id = recruta.id,
                forca = (int)recruta.forca,
                estado = (int)recruta.estado,
                progressoSegundos = recruta.progressoSegundos,
                tempoTotalSegundos = recruta.tempoTotalSegundos,
                diaRecrutamento = recruta.diaRecrutamento
            });
        }

        destino.adminPerdas.Clear();
        for (int i = 0; i < perdas.Count; i++)
        {
            QuartelPerdaV2 perda = perdas[i];
            if (perda == null) continue;
            destino.adminPerdas.Add(new SaveQuartelPerdaData
            {
                unidadeId = perda.unidadeId,
                nomeUnidade = perda.nomeUnidade,
                forca = (int)perda.forca,
                militares = perda.militares,
                motivo = perda.motivo,
                dia = perda.dia
            });
        }
    }

    public void RestaurarEstadoSave(SaveQuartelStateData origem)
    {
        if (origem == null) return;
        if (origem.quartelTeamID > 0) teamID = origem.quartelTeamID;
        ultimoDiaObservado = origem.adminUltimoDiaObservado;
        ultimoDiaRecrutamento = origem.adminUltimoDiaRecrutamento;
        ultimoDiaFolha = origem.adminUltimoDiaFolha;
        proximoDiaFolha = origem.adminProximoDiaFolha;
        ultimoDiaObservado = Mathf.Max(ultimoDiaObservado, ultimoDiaRecrutamento, ultimoDiaFolha);
        sequenciaRecruta = Mathf.Max(0, origem.adminSequenciaRecruta);
        recrutasConcluidos = Mathf.Max(0, origem.adminRecrutasConcluidos);
        recrutasRecrutados = Mathf.Max(0, origem.adminRecrutasRecrutados);
        folhaPagaTotal = Math.Max(0L, origem.adminFolhaPagaTotal);
        custoFolhaDiario = Math.Max(0L, origem.adminCustoFolhaDiario);
        custoFolhaCalculado = Math.Max(0L, origem.adminCustoFolhaPeriodo);
        diasFolhaPendentes = Mathf.Max(0, origem.adminDiasFolhaPendentes);
        if (proximoDiaFolha < 0 && ultimoDiaFolha >= 0)
        {
            proximoDiaFolha = ultimoDiaFolha + Mathf.Max(1, periodoFolhaDias);
        }

        recrutas.Clear();
        GarantirDicionarioForcas();
        for (int i = 0; i < 4; i++) recrutasPorForca[(QuartelForcaV2)i] = 0;
        if (origem.adminRecrutas != null)
        {
            for (int i = 0; i < origem.adminRecrutas.Count; i++)
            {
                SaveQuartelRecrutaData salvo = origem.adminRecrutas[i];
                if (salvo == null) continue;
                recrutas.Add(new QuartelRecrutaV2
                {
                    id = salvo.id,
                    forca = (QuartelForcaV2)Mathf.Clamp(salvo.forca, 0, 3),
                    estado = (QuartelStatusRecrutaV2)Mathf.Clamp(salvo.estado, 0, 1),
                    progressoSegundos = Mathf.Max(0f, salvo.progressoSegundos),
                    tempoTotalSegundos = Mathf.Max(1f, salvo.tempoTotalSegundos),
                    diaRecrutamento = Mathf.Max(1, salvo.diaRecrutamento)
                });
                recrutasPorForca[(QuartelForcaV2)Mathf.Clamp(salvo.forca, 0, 3)]++;
            }
        }

        perdas.Clear();
        perdasRegistradas.Clear();
        if (origem.adminPerdas != null)
        {
            for (int i = 0; i < origem.adminPerdas.Count; i++)
            {
                SaveQuartelPerdaData salvo = origem.adminPerdas[i];
                if (salvo == null) continue;
                QuartelPerdaV2 perda = new QuartelPerdaV2
                {
                    unidadeId = salvo.unidadeId,
                    nomeUnidade = salvo.nomeUnidade,
                    forca = (QuartelForcaV2)Mathf.Clamp(salvo.forca, 0, 3),
                    militares = Mathf.Max(1, salvo.militares),
                    motivo = salvo.motivo,
                    dia = Mathf.Max(1, salvo.dia)
                };
                perdas.Add(perda);
                if (!string.IsNullOrWhiteSpace(perda.unidadeId)) perdasRegistradas.Add(perda.unidadeId);
            }
        }
    }

    public Snapshot ObterSnapshot()
    {
        DadosPaisGoverno pais = ObterPais();
        QuartelRecrutaV2 emFormacao = null;
        for (int i = 0; i < recrutas.Count; i++)
        {
            if (recrutas[i] != null && recrutas[i].estado == QuartelStatusRecrutaV2.EmFormacao)
            {
                emFormacao = recrutas[i];
                break;
            }
        }

        float total = emFormacao != null ? Mathf.Max(1f, emFormacao.tempoTotalSegundos) : 0f;
        float progresso = emFormacao != null && total > 0f ? Mathf.Clamp01(emFormacao.progressoSegundos / total) : 0f;
        return new Snapshot
        {
            dia = ObterDiaAtual(),
            alistaveis = pais != null ? pais.alistaveis : 0,
            reservistas = pais != null ? pais.reservistas : 0,
            militaresAtivos = pais != null ? pais.populacaoMilitarAtiva : 0,
            recrutasEmFormacao = ContarRecrutasEmFormacao(),
            recrutasConcluidos = ContarRecrutasConcluidos(),
            recrutasRecrutados = recrutasRecrutados,
            recrutasInfantaria = recrutasPorForca[QuartelForcaV2.Infantaria],
            recrutasVeiculos = recrutasPorForca[QuartelForcaV2.Veiculos],
            recrutasNaval = recrutasPorForca[QuartelForcaV2.Naval],
            recrutasAerea = recrutasPorForca[QuartelForcaV2.Aerea],
            pessoalExigido = pessoalExigido,
            pessoalAlocado = pessoalAlocado,
            unidadesInoperantes = unidadesInoperantes,
            unidadesAvaliadasResgate = unidadesAvaliadasResgate,
            unidadesComAlertaResgate = unidadesComAlertaResgate,
            perdasRegistradas = perdas.Count,
            pessoalAdministrado = pessoalAdministrado,
            custoFolhaCalculado = custoFolhaCalculado,
            custoFolhaDiario = custoFolhaDiario,
            folhaPagaTotal = folhaPagaTotal,
            folhaPendente = folhaPendente,
            ultimoPagamentoRealizado = ultimoPagamentoRealizado,
            periodoFolhaDias = Mathf.Max(1, periodoFolhaDias),
            proximoDiaFolha = proximoDiaFolha,
            diasFolhaPendentes = diasFolhaPendentes,
            unidadesNoRaio = unidadesNoRaio,
            unidadesEmMissao = unidadesEmMissao,
            unidadesDanificadas = unidadesDanificadas,
            contatosInimigos = contatosInimigos,
            contatosSubmarinos = contatosSubmarinos,
            contatosMisseis = contatosMisseis,
            mortosAcumulados = pais != null ? Mathf.Max(0, pais.mortosAcumulados) : 0,
            aeronavesNoRaio = aeronavesConectadas.Count,
            aeronaves = aeronavesConectadas.ToArray(),
            comunicacoes = comunicacoes.ToArray(),
            forcas = CriarSnapshotForcas(),
            progressoTreinamento = progresso,
            segundosRestantesTreinamento = emFormacao != null ? Mathf.Max(0f, total - emFormacao.progressoSegundos) : 0f,
            forcaTreinamento = emFormacao != null ? NomeForca(emFormacao.forca) : "Nenhuma",
            ultimoEvento = ultimoEvento,
            ultimoMotivoFolha = ultimoMotivoFolha,
            ultimoAvisoResgate = ultimoAvisoResgate
        };
    }

    private DadosPaisGoverno ObterPais()
    {
        return SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(teamID) : null;
    }

    private int ObterMilitaresAtivos()
    {
        DadosPaisGoverno pais = ObterPais();
        return pais != null ? Mathf.Max(0, pais.populacaoMilitarAtiva) : 0;
    }

    private int CalcularTripulacao(IdentidadeUnidade identidade)
    {
        if (identidade == null) return 0;
        if (identidade.militaresConsumidos > 0) return identidade.militaresConsumidos;
        switch (identidade.tipoUnidade)
        {
            case TipoUnidade.Naval: return Mathf.Max(1, tripulacaoMinimaNaval);
            case TipoUnidade.Aereo: return Mathf.Max(1, tripulacaoMinimaAerea);
            case TipoUnidade.Veiculo: return Mathf.Max(1, tripulacaoMinimaVeiculo);
            case TipoUnidade.Infantaria: return Mathf.Max(1, tripulacaoMinimaInfantaria);
            default: return 0;
        }
    }

    private QuartelForcaV2 EscolherForcaParaRecruta()
    {
        QuartelForcaV2 melhor = QuartelForcaV2.Infantaria;
        int maiorDeficit = -1;
        for (int i = 0; i < 4; i++)
        {
            QuartelForcaV2 forca = (QuartelForcaV2)i;
            int deficit = CalcularDeficitForca(forca);
            if (deficit > maiorDeficit)
            {
                maiorDeficit = deficit;
                melhor = forca;
            }
        }
        return melhor;
    }

    private int CalcularDeficitForca(QuartelForcaV2 forca)
    {
        int exigido = 0;
        for (int i = 0; i < unidadesGerenciadas.Count; i++)
        {
            ControleUnidade unidade = unidadesGerenciadas[i];
            IdentidadeUnidade identidade = unidade != null ? unidade.GetComponent<IdentidadeUnidade>() : null;
            if (identidade == null || ConverterForca(identidade.tipoUnidade) != forca) continue;
            exigido += CalcularTripulacao(identidade);
        }
        return Mathf.Max(0, exigido - ObterMilitaresAtivos());
    }

    private int ContarRecrutasEmFormacao()
    {
        int total = 0;
        for (int i = 0; i < recrutas.Count; i++)
            if (recrutas[i] != null && recrutas[i].estado == QuartelStatusRecrutaV2.EmFormacao) total++;
        return total;
    }

    private int ContarRecrutasConcluidos()
    {
        int total = 0;
        for (int i = 0; i < recrutas.Count; i++)
            if (recrutas[i] != null && recrutas[i].estado == QuartelStatusRecrutaV2.Ativo) total++;
        return total;
    }

    private void GarantirDicionarioForcas()
    {
        for (int i = 0; i < 4; i++)
        {
            QuartelForcaV2 forca = (QuartelForcaV2)i;
            if (!recrutasPorForca.ContainsKey(forca)) recrutasPorForca.Add(forca, 0);
        }
    }

    private void GarantirContagensForca()
    {
        for (int i = 0; i < 4; i++)
        {
            QuartelForcaV2 forca = (QuartelForcaV2)i;
            if (!contagensForca.ContainsKey(forca)) contagensForca.Add(forca, new ContagemForca());
        }
    }

    private void LimparContagensForca()
    {
        foreach (KeyValuePair<QuartelForcaV2, ContagemForca> par in contagensForca)
        {
            ContagemForca contagem = par.Value;
            if (contagem == null) continue;
            contagem.unidades = 0;
            contagem.unidadesEmMissao = 0;
            contagem.unidadesDanificadas = 0;
            contagem.unidadesInoperantes = 0;
            contagem.pessoalExigido = 0;
            contagem.pessoalAlocado = 0;
        }
    }

    private QuartelForcaSnapshotV2[] CriarSnapshotForcas()
    {
        QuartelForcaSnapshotV2[] resultado = new QuartelForcaSnapshotV2[4];
        for (int i = 0; i < resultado.Length; i++)
        {
            QuartelForcaV2 forca = (QuartelForcaV2)i;
            ContagemForca contagem = contagensForca[forca];
            resultado[i] = new QuartelForcaSnapshotV2
            {
                forca = forca,
                unidades = contagem.unidades,
                unidadesEmMissao = contagem.unidadesEmMissao,
                unidadesDanificadas = contagem.unidadesDanificadas,
                unidadesInoperantes = contagem.unidadesInoperantes,
                pessoalExigido = contagem.pessoalExigido,
                pessoalAlocado = contagem.pessoalAlocado
            };
        }
        return resultado;
    }

    private string ObterIdEstavel(GameObject objeto)
    {
        if (objeto == null) return string.Empty;
        SaveableEntity saveable = SaveableEntity.Garantir(objeto);
        return saveable != null && !string.IsNullOrWhiteSpace(saveable.UniqueId)
            ? saveable.UniqueId
            : "runtime-" + objeto.GetInstanceID();
    }

    private static QuartelForcaV2 ConverterForca(TipoUnidade tipo)
    {
        switch (tipo)
        {
            case TipoUnidade.Veiculo: return QuartelForcaV2.Veiculos;
            case TipoUnidade.Naval: return QuartelForcaV2.Naval;
            case TipoUnidade.Aereo: return QuartelForcaV2.Aerea;
            default: return QuartelForcaV2.Infantaria;
        }
    }

    private static string NomeForca(QuartelForcaV2 forca)
    {
        switch (forca)
        {
            case QuartelForcaV2.Veiculos: return "Veiculos";
            case QuartelForcaV2.Naval: return "Naval";
            case QuartelForcaV2.Aerea: return "Aerea";
            default: return "Infantaria";
        }
    }

    private static void RecalcularPopulacao(DadosPaisGoverno pais)
    {
        if (pais == null) return;
        pais.populacao = Mathf.Clamp(
            pais.populacaoCivil + pais.populacaoMilitarAtiva + pais.reservistas + pais.alistaveis,
            0,
            Mathf.Max(1, pais.populacaoMaxima));
    }
}
