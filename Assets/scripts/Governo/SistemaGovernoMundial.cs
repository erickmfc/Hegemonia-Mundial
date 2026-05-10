using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SistemaGovernoMundial : MonoBehaviour
{
    public static SistemaGovernoMundial Instancia { get; private set; }
    private static bool encerrando;

    [Header("Config")]
    public int teamJogador = 1;
    public float intervaloEconomia = 3f;

    [Header("Estado")]
    public List<DadosPaisGoverno> paises = new List<DadosPaisGoverno>();
    public List<RelacaoPaisGoverno> relacoes = new List<RelacaoPaisGoverno>();
    public List<string> noticias = new List<string>();
    public List<PropostaInternacional> propostas = new List<PropostaInternacional>();

    public event Action OnGovernoAtualizado;
    public event Action<string> OnNoticia;
    public event Action<PropostaInternacional> OnPropostaCriada;

    private float proximoTick;

    public IReadOnlyList<DadosPaisGoverno> Paises => paises;
    public IReadOnlyList<RelacaoPaisGoverno> Relacoes => relacoes;
    public IReadOnlyList<PropostaInternacional> Propostas => propostas;

    public static void GarantirInstancia()
    {
        if (encerrando) return;
        if (Instancia != null) return;

#if UNITY_2023_1_OR_NEWER
        SistemaGovernoMundial existente = FindFirstObjectByType<SistemaGovernoMundial>();
#else
        SistemaGovernoMundial existente = FindObjectOfType<SistemaGovernoMundial>();
#endif
        if (existente != null)
        {
            Instancia = existente;
            return;
        }

        GameObject go = new GameObject("SistemaGovernoMundial_Runtime");
        Instancia = go.AddComponent<SistemaGovernoMundial>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        DontDestroyOnLoad(gameObject);
        InicializarDadosPadrao();
        GarantirMercado();
        GarantirEconomiaViva();
    }

    private void OnApplicationQuit()
    {
        encerrando = true;
    }

    private void OnEnable()
    {
        if (GerenciadorRecursos.Instancia != null)
            GerenciadorRecursos.Instancia.OnRecursosAtualizados += SincronizarJogador;
    }

    private void OnDisable()
    {
        if (GerenciadorRecursos.Instancia != null)
            GerenciadorRecursos.Instancia.OnRecursosAtualizados -= SincronizarJogador;
    }

    private void Update()
    {
        if (Time.unscaledTime < proximoTick) return;
        proximoTick = Time.unscaledTime + Mathf.Max(1f, intervaloEconomia);
        if (SistemaEconomiaImoveis.Instancia != null) SistemaEconomiaImoveis.Instancia.Recalcular();
        SincronizarJogador();
        DescobrirIAsDaCena();
        ProcessarEconomia();
    }

    public void InicializarDadosPadrao()
    {
        if (paises == null) paises = new List<DadosPaisGoverno>();
        if (relacoes == null) relacoes = new List<RelacaoPaisGoverno>();

        if (paises.Count == 0)
        {
            paises.Add(new DadosPaisGoverno { teamId = 1, nomePais = "Republica Atlas", nomeMoeda = "Atlas", simboloMoeda = "AT$", bloco = "Ordem Atlas", saldo = 5000, comida = 500, petroleo = 500, aco = 300, armamentos = 500, emprego = 78f, moradia = 72f, estabilidade = 76f, producao = 78f, aliadoPrioritarioTeamId = 2, rivalTeamId = 3, perfilIA = PerfilPaisIA.Neutro, modoInicialIA = ModoInicialPaisIA.Crescimento, nivelEconomico = 62, nivelIndustrial = 58, nivelMilitar = 54, nivelDiplomatico = 65, pesoComercio = 0.58f, pesoDiplomacia = 0.62f });
            paises.Add(new DadosPaisGoverno { teamId = 2, nomePais = "Republica Boreal", nomeMoeda = "Boreal", simboloMoeda = "BO$", bloco = "Ordem Atlas", saldo = 18000, comida = 1800, petroleo = 2600, aco = 700, armamentos = 900, emprego = 82f, moradia = 78f, estabilidade = 84f, producao = 74f, perfilIA = PerfilPaisIA.Aliado, modoInicialIA = ModoInicialPaisIA.Comercial, nivelEconomico = 78, nivelIndustrial = 66, nivelMilitar = 55, nivelDiplomatico = 76, pesoLealdadeAliados = 0.82f, pesoComercio = 0.72f });
            paises.Add(new DadosPaisGoverno { teamId = 3, nomePais = "Uniao Carmesim", nomeMoeda = "Carmesim", simboloMoeda = "CA$", bloco = "Pacto Solaris", saldo = 22000, comida = 900, petroleo = 4800, aco = 1200, armamentos = 1600, emprego = 61f, moradia = 52f, estabilidade = 44f, producao = 81f, emGuerra = true, perfilIA = PerfilPaisIA.ProdutorPetroleo, modoInicialIA = ModoInicialPaisIA.GuerraFria, nivelEconomico = 70, nivelIndustrial = 62, nivelMilitar = 78, nivelDiplomatico = 36, pesoAgressividade = 0.72f, pesoOdioRivais = 0.80f });
            paises.Add(new DadosPaisGoverno { teamId = 4, nomePais = "Dominio Valerian", nomeMoeda = "Valer", simboloMoeda = "VA$", bloco = "Liga Continental", saldo = 16000, comida = 600, petroleo = 900, aco = 1800, armamentos = 2100, emprego = 66f, moradia = 58f, estabilidade = 48f, producao = 76f, sancionado = true, perfilIA = PerfilPaisIA.Militarista, modoInicialIA = ModoInicialPaisIA.Mobilizacao, nivelEconomico = 58, nivelIndustrial = 78, nivelMilitar = 86, nivelDiplomatico = 32, pesoMilitarismo = 0.88f, pesoControleEstoque = 0.75f });
            paises.Add(new DadosPaisGoverno { teamId = 5, nomePais = "Federacao Alvorada", nomeMoeda = "Aurora", simboloMoeda = "AU$", bloco = "Nenhum", saldo = 12500, comida = 3400, petroleo = 600, aco = 500, armamentos = 350, emprego = 74f, moradia = 80f, estabilidade = 69f, producao = 67f, perfilIA = PerfilPaisIA.Pequeno, modoInicialIA = ModoInicialPaisIA.Crescimento, nivelEconomico = 52, nivelIndustrial = 34, nivelMilitar = 24, nivelDiplomatico = 58, pesoDependenciaExterna = 0.80f, pesoDiplomacia = 0.70f });
        }

        if (relacoes.Count == 0)
        {
            relacoes.Add(new RelacaoPaisGoverno { teamA = 1, teamB = 2, valor = 75, tratadoComercial = true, pactoMilitar = true });
            relacoes.Add(new RelacaoPaisGoverno { teamA = 1, teamB = 3, valor = -82, tratadoComercial = false, guerraDeclarada = true, sancaoAtiva = true });
            relacoes.Add(new RelacaoPaisGoverno { teamA = 1, teamB = 4, valor = -55, tratadoComercial = false, sancaoAtiva = true });
            relacoes.Add(new RelacaoPaisGoverno { teamA = 1, teamB = 5, valor = 28, tratadoComercial = true, pedidoPendente = true });
        }

        SincronizarJogador();
    }

    public void GarantirPaisIA(int teamId, string nomePais, string nomeMoeda, string simboloMoeda, PerfilPaisIA perfil, ModoInicialPaisIA modo)
    {
        if (teamId <= 0) return;
        bool mudou = false;
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null)
        {
            pais = new DadosPaisGoverno
            {
                teamId = teamId,
                nomePais = string.IsNullOrEmpty(nomePais) ? "Pais IA " + teamId : nomePais,
                nomeMoeda = string.IsNullOrEmpty(nomeMoeda) ? "Moeda " + teamId : nomeMoeda,
                simboloMoeda = string.IsNullOrEmpty(simboloMoeda) ? "IA$" : simboloMoeda,
                perfilIA = perfil,
                modoInicialIA = modo,
                bloco = "IA",
                saldo = 12000
            };
            paises.Add(pais);
            mudou = true;
        }
        else
        {
            if (!string.IsNullOrEmpty(nomePais) && pais.nomePais != nomePais) { pais.nomePais = nomePais; mudou = true; }
            if (!string.IsNullOrEmpty(nomeMoeda) && pais.nomeMoeda != nomeMoeda) { pais.nomeMoeda = nomeMoeda; mudou = true; }
            if (!string.IsNullOrEmpty(simboloMoeda) && pais.simboloMoeda != simboloMoeda) { pais.simboloMoeda = simboloMoeda; mudou = true; }
            if (pais.perfilIA != perfil) { pais.perfilIA = perfil; mudou = true; }
            if (pais.modoInicialIA != modo) { pais.modoInicialIA = modo; mudou = true; }
        }

        AplicarPerfilPadrao(pais);
        if (mudou)
        {
            OnGovernoAtualizado?.Invoke();
        }
    }

    public DadosPaisGoverno ObterPais(int teamId)
    {
        return paises.FirstOrDefault(p => p != null && p.teamId == teamId);
    }

    public RelacaoPaisGoverno ObterRelacao(int a, int b)
    {
        RelacaoPaisGoverno rel = relacoes.FirstOrDefault(r => r != null && r.Envolve(a, b));
        if (rel != null) return rel;
        rel = new RelacaoPaisGoverno { teamA = a, teamB = b, valor = 0, tratadoComercial = true };
        relacoes.Add(rel);
        return rel;
    }

    public IEnumerable<DadosPaisGoverno> ObterAliados(int teamId)
    {
        return relacoes
            .Where(r => r != null && r.pactoMilitar && r.Envolve(teamId, r.Outro(teamId)))
            .Select(r => ObterPais(r.Outro(teamId)))
            .Where(p => p != null);
    }

    public void ProcessarEconomia()
    {
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        float petroleoVariacao = mercado != null ? (mercado.ObterItem("petroleo")?.variacaoPercentual ?? 0f) : 0f;
        SistemaEconomiaImoveis economiaImoveis = SistemaEconomiaImoveis.Instancia;

        foreach (DadosPaisGoverno pais in paises)
        {
            if (pais == null) continue;

            float scoreAntes = pais.PontuacaoEconomica();
            DadosEconomiaPais economia = economiaImoveis != null ? economiaImoveis.ObterEconomia(pais.teamId) : null;
            if (economia != null)
            {
                AplicarEconomiaImoveis(pais, economia);
                SistemaPopulacao.Processar(pais, economia);
            }

            pais.estabilidade += pais.emprego > 70f ? 0.25f : -0.35f;
            pais.estabilidade += pais.moradia > 65f ? 0.18f : -0.30f;
            pais.estabilidade += pais.qualidadeVida > 70f ? 0.18f : pais.qualidadeVida < 40f ? -0.45f : 0f;
            float cargaFiscal = CargaFiscalMedia(pais);
            if (cargaFiscal > 18f) pais.estabilidade -= (cargaFiscal - 18f) * 0.045f;
            if (cargaFiscal >= 25f) pais.qualidadeVida = Mathf.Clamp(pais.qualidadeVida - (cargaFiscal - 20f) * 0.06f, 0f, 100f);
            if (pais.emGuerra) pais.estabilidade -= 0.55f;
            if (pais.sancionado) pais.estabilidade -= 0.35f;
            if (pais.deficitEnergia > 0f) pais.estabilidade -= Mathf.Clamp(pais.deficitEnergia * 0.08f, 0f, 0.60f);
            if (pais.deficitComida > 0f) pais.estabilidade -= Mathf.Clamp(pais.deficitComida * 0.08f, 0f, 0.60f);
            if (pais.petroleo > 1000 && petroleoVariacao > 0f) pais.estabilidade += 0.12f;
            pais.estabilidade = Mathf.Clamp(pais.estabilidade, 5f, 100f);

            pais.inflacao += pais.emGuerra ? 0.08f : -0.03f;
            pais.inflacao += pais.sancionado ? 0.06f : -0.01f;
            pais.inflacao += (pais.deficitComida + pais.deficitEnergia + pais.deficitPetroleo) > 0f ? 0.04f : -0.02f;
            pais.inflacao = Mathf.Clamp(pais.inflacao, 0.5f, 40f);

            SistemaMoeda.Processar(pais, economia);

            if (pais.teamId != teamJogador)
            {
                pais.saldo += Mathf.RoundToInt(Mathf.Max(1f, pais.rendaPorSegundo - pais.gastosPorSegundo));
                if (economia != null)
                {
                    pais.comida = Mathf.Max(0, pais.comida + Mathf.RoundToInt(economia.comidaProduzida - Mathf.Max(1f, pais.populacao * 0.01f)));
                    pais.petroleo = Mathf.Max(0, pais.petroleo + Mathf.RoundToInt(economia.petroleoProduzido - economia.industriaProduzida * 0.10f));
                    pais.aco = Mathf.Max(0, pais.aco + Mathf.RoundToInt(economia.industriaProduzida * 0.55f));
                    pais.armamentos = Mathf.Max(0, pais.armamentos + Mathf.RoundToInt(economia.industriaProduzida * (pais.pesoMilitarismo > 0.65f ? 0.22f : 0.08f)));
                }
            }

            float scoreDepois = pais.PontuacaoEconomica();
            if (scoreAntes > 65f && scoreDepois < 55f)
                RegistrarNoticia(pais.nomePais + " entrou em deterioracao economica.");
        }

        AtualizarReferenciasMoeda();

        OnGovernoAtualizado?.Invoke();
    }

    public void SincronizarJogador()
    {
        DadosPaisGoverno jogador = ObterPais(teamJogador);
        GerenciadorRecursos gr = GerenciadorRecursos.Instancia;
        if (jogador == null || gr == null) return;
        DadosEconomiaPais economia = SistemaEconomiaImoveis.Instancia != null ? SistemaEconomiaImoveis.Instancia.ObterEconomia(teamJogador) : null;

        jogador.saldo = gr.dinheiro;
        jogador.comida = gr.comida;
        jogador.petroleo = gr.petroleo;
        jogador.aco = gr.aco;
        if (economia != null)
        {
            jogador.populacao = Mathf.Max(gr.populacaoAtual, economia.populacaoTotal);
            jogador.populacaoMaxima = Mathf.Max(gr.populacaoMaxima, economia.moradiaTotal);
            jogador.rendaPorSegundo = Mathf.Max(gr.dinheiroPorSegundo, economia.dinheiroGerado);
            jogador.producao = Mathf.Clamp(40f + economia.industriaProduzida * 4f + economia.petroleoProduzido * 2f, 10f, 100f);
            AplicarEconomiaImoveis(jogador, economia);
        }
        else
        {
            jogador.populacao = gr.populacaoAtual;
            jogador.populacaoMaxima = gr.populacaoMaxima;
            jogador.rendaPorSegundo = gr.dinheiroPorSegundo;
            jogador.producao = Mathf.Clamp(55f + gr.acoPorSegundo * 3f + gr.petroleoPorSegundo * 2f, 10f, 100f);
        }
    }

    public void AtualizarIdentidadeNacional(int teamId, string nomePais, string nomePresidente, string nomeMoeda)
    {
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null) return;

        bool mudou = false;

        if (!string.IsNullOrWhiteSpace(nomePais))
        {
            string valor = nomePais.Trim();
            if (pais.nomePais != valor)
            {
                pais.nomePais = valor;
                mudou = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(nomePresidente))
        {
            string valor = nomePresidente.Trim();
            if (pais.nomePresidente != valor)
            {
                pais.nomePresidente = valor;
                mudou = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(nomeMoeda))
        {
            string valor = nomeMoeda.Trim();
            if (pais.nomeMoeda != valor)
            {
                pais.nomeMoeda = valor;
                pais.simboloMoeda = GerarSimboloMoeda(valor);
                mudou = true;
            }
        }

        if (mudou)
            OnGovernoAtualizado?.Invoke();
    }

    public bool AjustarImposto(int teamId, string categoria, int deltaFaixas)
    {
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null || string.IsNullOrEmpty(categoria) || deltaFaixas == 0) return false;

        int delta = deltaFaixas * 5;
        string categoriaNormalizada = categoria.Trim().ToLowerInvariant();
        bool mudou = false;

        if (categoriaNormalizada == "moradia")
        {
            int novo = NormalizarImposto(pais.impostoMoradia + delta);
            mudou = novo != pais.impostoMoradia;
            pais.impostoMoradia = novo;
        }
        else if (categoriaNormalizada == "industria")
        {
            int novo = NormalizarImposto(pais.impostoIndustria + delta);
            mudou = novo != pais.impostoIndustria;
            pais.impostoIndustria = novo;
        }
        else if (categoriaNormalizada == "comercio")
        {
            int novo = NormalizarImposto(pais.impostoComercio + delta);
            mudou = novo != pais.impostoComercio;
            pais.impostoComercio = novo;
        }

        if (!mudou) return false;
        RegistrarNoticia(pais.nomePais + " ajustou impostos de " + categoriaNormalizada + ".");
        if (teamId == teamJogador) SincronizarJogador();
        ProcessarEconomia();
        return true;
    }

    public bool DefinirPlanoEstrategico(int teamId, string plano)
    {
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null || string.IsNullOrWhiteSpace(plano)) return false;

        string valor = plano.Trim();
        if (pais.planoEstrategico == valor) return false;
        pais.planoEstrategico = valor;
        RegistrarNoticia(pais.nomePais + " mudou o foco nacional para " + valor + ".");
        OnGovernoAtualizado?.Invoke();
        return true;
    }

    public bool CriarPropostaJogador(int alvoTeamId, TipoPropostaInternacional tipo, RecursoMercado recurso, int quantidade, int precoUnitario, string motivo, string dedupKey = null)
    {
        if (alvoTeamId <= 0 || alvoTeamId == teamJogador) return false;
        PropostaInternacional proposta = new PropostaInternacional
        {
            origemTeamId = teamJogador,
            alvoTeamId = alvoTeamId,
            tipo = tipo,
            recurso = recurso,
            quantidade = Mathf.Max(1, quantidade),
            precoUnitario = Mathf.Max(1, precoUnitario),
            prioridade = tipo == TipoPropostaInternacional.PedidoAjuda ? 80 : 65,
            motivo = string.IsNullOrWhiteSpace(motivo) ? "Negociacao diplomatica." : motivo.Trim(),
            expiraEm = Time.unscaledTime + 95f,
            dedupKey = string.IsNullOrWhiteSpace(dedupKey) ? "player:" + teamJogador + ":" + alvoTeamId + ":" + tipo + ":" + recurso : dedupKey
        };

        bool criada = TentarCriarProposta(proposta);
        if (!criada) return false;

        RelacaoPaisGoverno rel = ObterRelacao(teamJogador, alvoTeamId);
        rel.pedidoPendente = true;
        OnGovernoAtualizado?.Invoke();
        return true;
    }

    public bool TentarPagar(int teamId, int valor)
    {
        if (valor <= 0) return true;
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null || pais.saldo < valor) return false;

        if (teamId == teamJogador && GerenciadorRecursos.Instancia != null)
        {
            if (!GerenciadorRecursos.Instancia.TentarGastar(custoDinheiro: valor)) return false;
            SincronizarJogador();
            return true;
        }

        pais.saldo -= valor;
        OnGovernoAtualizado?.Invoke();
        return true;
    }

    public void AdicionarSaldo(int teamId, int valor)
    {
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null || valor == 0) return;

        if (teamId == teamJogador && GerenciadorRecursos.Instancia != null)
        {
            GerenciadorRecursos.Instancia.AdicionarRecursos(addDinheiro: valor);
            SincronizarJogador();
        }
        else
        {
            pais.saldo += valor;
        }

        OnGovernoAtualizado?.Invoke();
    }

    public int ObterEstoque(int teamId, RecursoMercado recurso)
    {
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null) return 0;
        switch (recurso)
        {
            case RecursoMercado.Comida: return pais.comida;
            case RecursoMercado.Petroleo: return pais.petroleo;
            case RecursoMercado.Aco: return pais.aco;
            case RecursoMercado.Armamentos: return pais.armamentos;
            case RecursoMercado.Uranio: return pais.uranio;
            default: return 0;
        }
    }

    public void AdicionarEstoque(int teamId, RecursoMercado recurso, int quantidade)
    {
        AlterarEstoque(teamId, recurso, Mathf.Abs(quantidade));
    }

    public IEnumerable<PropostaInternacional> ObterPropostasPendentesPara(int teamId)
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < propostas.Count; i++)
        {
            PropostaInternacional proposta = propostas[i];
            if (proposta == null) continue;
            if (proposta.EstaPendente && proposta.expiraEm > 0f && now > proposta.expiraEm)
            {
                proposta.status = StatusPropostaInternacional.Expirada;
                ObterRelacao(proposta.origemTeamId, proposta.alvoTeamId).pedidoPendente = false;
                continue;
            }

            if (proposta.EstaPendente && proposta.alvoTeamId == teamId)
            {
                yield return proposta;
            }
        }
    }

    public bool TentarCriarProposta(PropostaInternacional proposta)
    {
        if (proposta == null || proposta.origemTeamId <= 0 || proposta.alvoTeamId <= 0 || proposta.quantidade <= 0)
        {
            return false;
        }

        string dedup = string.IsNullOrEmpty(proposta.dedupKey)
            ? proposta.tipo + ":" + proposta.origemTeamId + ":" + proposta.alvoTeamId + ":" + proposta.recurso
            : proposta.dedupKey;
        proposta.dedupKey = dedup;

        float now = Time.unscaledTime;
        for (int i = 0; i < propostas.Count; i++)
        {
            PropostaInternacional existente = propostas[i];
            if (existente == null || !existente.EstaPendente) continue;
            if (existente.dedupKey == dedup && existente.expiraEm > now)
            {
                return false;
            }
        }

        proposta.id = string.IsNullOrEmpty(proposta.id)
            ? "prop-" + proposta.origemTeamId + "-" + proposta.alvoTeamId + "-" + proposta.recurso + "-" + Mathf.RoundToInt(now * 10f)
            : proposta.id;
        proposta.criadaEm = now;
        if (proposta.expiraEm <= now) proposta.expiraEm = now + 90f;
        proposta.status = StatusPropostaInternacional.Pendente;
        propostas.Insert(0, proposta);
        while (propostas.Count > 40) propostas.RemoveAt(propostas.Count - 1);
        ObterRelacao(proposta.origemTeamId, proposta.alvoTeamId).pedidoPendente = true;

        RegistrarNoticia(NomePais(proposta.origemTeamId) + " enviou proposta: " + proposta.motivo);
        OnPropostaCriada?.Invoke(proposta);
        OnGovernoAtualizado?.Invoke();
        return true;
    }

    public bool ResolverProposta(string propostaId, StatusPropostaInternacional novoStatus, out string mensagem)
    {
        mensagem = string.Empty;
        PropostaInternacional proposta = propostas.FirstOrDefault(p => p != null && p.id == propostaId);
        if (proposta == null)
        {
            mensagem = "Proposta nao encontrada.";
            return false;
        }

        if (!proposta.EstaPendente)
        {
            mensagem = "Proposta ja foi resolvida.";
            return false;
        }

        if (novoStatus == StatusPropostaInternacional.Recusada)
        {
            proposta.status = StatusPropostaInternacional.Recusada;
            RelacaoPaisGoverno relRecusa = ObterRelacao(proposta.origemTeamId, proposta.alvoTeamId);
            relRecusa.valor = Mathf.Clamp(relRecusa.valor - 2, -100, 100);
            relRecusa.pedidoPendente = false;
            mensagem = "Proposta recusada.";
            OnGovernoAtualizado?.Invoke();
            return true;
        }

        if (novoStatus == StatusPropostaInternacional.Negociando)
        {
            proposta.status = StatusPropostaInternacional.Negociando;
            proposta.precoUnitario = Mathf.Max(1, Mathf.RoundToInt(proposta.precoUnitario * 1.08f));
            proposta.expiraEm = Time.unscaledTime + 75f;
            ObterRelacao(proposta.origemTeamId, proposta.alvoTeamId).pedidoPendente = true;
            mensagem = "Contraoferta enviada.";
            OnGovernoAtualizado?.Invoke();
            return true;
        }

        if (novoStatus != StatusPropostaInternacional.Aceita)
        {
            proposta.status = novoStatus;
            ObterRelacao(proposta.origemTeamId, proposta.alvoTeamId).pedidoPendente = false;
            mensagem = "Proposta atualizada.";
            OnGovernoAtualizado?.Invoke();
            return true;
        }

        bool executou = ExecutarProposta(proposta, out mensagem);
        proposta.status = executou ? StatusPropostaInternacional.Executada : StatusPropostaInternacional.Pendente;
        ObterRelacao(proposta.origemTeamId, proposta.alvoTeamId).pedidoPendente = !executou;
        OnGovernoAtualizado?.Invoke();
        return executou;
    }

    public void RemoverEstoque(int teamId, RecursoMercado recurso, int quantidade)
    {
        AlterarEstoque(teamId, recurso, -Mathf.Abs(quantidade));
    }

    public void AlterarEmprego(int teamId, float delta)
    {
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null) return;
        pais.emprego = Mathf.Clamp(pais.emprego + delta, 0f, 100f);
        RegistrarNoticia(pais.nomePais + (delta >= 0 ? " gerou empregos." : " perdeu empregos."));
        ProcessarEconomia();
    }

    public void AlterarMoradia(int teamId, float delta)
    {
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null) return;
        pais.moradia = Mathf.Clamp(pais.moradia + delta, 0f, 100f);
        RegistrarNoticia(pais.nomePais + (delta >= 0 ? " ampliou moradias." : " sofreu crise habitacional."));
        ProcessarEconomia();
    }

    public void NotificarGuerra(int teamId)
    {
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null) return;
        pais.emGuerra = true;
        pais.estabilidade = Mathf.Clamp(pais.estabilidade - 14f, 0f, 100f);
        RelacaoPaisGoverno rel = ObterRelacao(teamJogador, teamId);
        rel.guerraDeclarada = true;
        rel.valor = Mathf.Clamp(rel.valor - 35, -100, 100);
        RegistrarNoticia("Guerra declarada envolvendo " + pais.nomePais + ".");
        SistemaMercadoGlobal.Instancia?.SimularMercado();
        ProcessarEconomia();
    }

    public void ProporAlianca(int alvoTeamId)
    {
        RelacaoPaisGoverno rel = ObterRelacao(teamJogador, alvoTeamId);
        rel.valor = Mathf.Clamp(rel.valor + 18, -100, 100);
        rel.pedidoPendente = rel.valor < 60;
        if (rel.valor >= 60)
        {
            rel.pactoMilitar = true;
            rel.tratadoComercial = true;
        }
        RegistrarNoticia("Proposta de alianca enviada para " + NomePais(alvoTeamId) + ".");
        OnGovernoAtualizado?.Invoke();
    }

    public void RomperAlianca(int alvoTeamId)
    {
        RelacaoPaisGoverno rel = ObterRelacao(teamJogador, alvoTeamId);
        rel.pactoMilitar = false;
        rel.valor = Mathf.Clamp(rel.valor - 22, -100, 100);
        RegistrarNoticia("Alianca rompida com " + NomePais(alvoTeamId) + ".");
        OnGovernoAtualizado?.Invoke();
    }

    public void ProporPactoDefensivo(int alvoTeamId)
    {
        RelacaoPaisGoverno rel = ObterRelacao(teamJogador, alvoTeamId);
        rel.valor = Mathf.Clamp(rel.valor + 10, -100, 100);
        rel.pactoMilitar = rel.valor >= 45;
        rel.pedidoPendente = !rel.pactoMilitar;
        RegistrarNoticia("Pacto defensivo negociado com " + NomePais(alvoTeamId) + ".");
        OnGovernoAtualizado?.Invoke();
    }

    public void AplicarSancao(int alvoTeamId)
    {
        DadosPaisGoverno alvo = ObterPais(alvoTeamId);
        if (alvo == null) return;
        alvo.sancionado = true;
        alvo.estabilidade = Mathf.Clamp(alvo.estabilidade - 8f, 0f, 100f);
        RelacaoPaisGoverno rel = ObterRelacao(teamJogador, alvoTeamId);
        rel.sancaoAtiva = true;
        rel.tratadoComercial = false;
        rel.valor = Mathf.Clamp(rel.valor - 18, -100, 100);
        RegistrarNoticia("Sancoes aplicadas contra " + alvo.nomePais + ".");
        SistemaMercadoGlobal.Instancia?.SimularMercado();
        ProcessarEconomia();
    }

    public void RemoverSancao(int alvoTeamId)
    {
        DadosPaisGoverno alvo = ObterPais(alvoTeamId);
        if (alvo == null) return;
        alvo.sancionado = false;
        RelacaoPaisGoverno rel = ObterRelacao(teamJogador, alvoTeamId);
        rel.sancaoAtiva = false;
        rel.tratadoComercial = true;
        rel.valor = Mathf.Clamp(rel.valor + 12, -100, 100);
        RegistrarNoticia("Sancoes removidas de " + alvo.nomePais + ".");
        ProcessarEconomia();
    }

    public float PressaoGlobalGuerra()
    {
        if (paises.Count == 0) return 0f;
        return Mathf.Clamp01(paises.Count(p => p != null && p.emGuerra) / (float)paises.Count);
    }

    public float PressaoGlobalSancoes()
    {
        if (paises.Count == 0) return 0f;
        return Mathf.Clamp01(paises.Count(p => p != null && p.sancionado) / (float)paises.Count);
    }

    public DadosPaisGoverno PaisLiderMoeda()
    {
        return paises
            .Where(p => p != null)
            .OrderByDescending(p => p.valorMoeda)
            .ThenByDescending(p => p.PontuacaoEconomica())
            .FirstOrDefault();
    }

    public string NomePais(int teamId)
    {
        DadosPaisGoverno pais = ObterPais(teamId);
        return pais != null ? pais.nomePais : "Pais " + teamId;
    }

    public void RegistrarNoticia(string mensagem)
    {
        if (string.IsNullOrEmpty(mensagem)) return;
        noticias.Insert(0, mensagem);
        while (noticias.Count > 12) noticias.RemoveAt(noticias.Count - 1);
        OnNoticia?.Invoke(mensagem);
    }

    private bool ExecutarProposta(PropostaInternacional proposta, out string mensagem)
    {
        mensagem = string.Empty;
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        DadosItemMercado item = mercado != null ? mercado.ObterItem(IdRecurso(proposta.recurso)) : null;

        if (proposta.tipo == TipoPropostaInternacional.PactoDefensivo)
        {
            RelacaoPaisGoverno rel = ObterRelacao(proposta.origemTeamId, proposta.alvoTeamId);
            rel.pactoMilitar = true;
            rel.tratadoComercial = true;
            rel.valor = Mathf.Clamp(rel.valor + 12, -100, 100);
            mensagem = "Pacto defensivo aceito.";
            RegistrarNoticia(mensagem);
            return true;
        }

        if (proposta.tipo == TipoPropostaInternacional.Sancao)
        {
            AplicarSancao(proposta.alvoTeamId);
            mensagem = "Sancao aplicada.";
            return true;
        }

        if (item == null)
        {
            mensagem = "Recurso sem item de mercado.";
            return false;
        }

        if (proposta.tipo == TipoPropostaInternacional.Compra || proposta.tipo == TipoPropostaInternacional.PedidoAjuda)
        {
            return mercado.Comprar(proposta.origemTeamId, proposta.alvoTeamId, item.id, proposta.quantidade, out mensagem);
        }

        if (proposta.tipo == TipoPropostaInternacional.Venda || proposta.tipo == TipoPropostaInternacional.Anuncio)
        {
            return mercado.Vender(proposta.origemTeamId, proposta.alvoTeamId, item.id, proposta.quantidade, out mensagem);
        }

        if (proposta.tipo == TipoPropostaInternacional.Emprestimo || proposta.tipo == TipoPropostaInternacional.Doacao)
        {
            int disponivel = ObterEstoque(proposta.origemTeamId, proposta.recurso);
            int qtd = Mathf.Min(disponivel, proposta.quantidade);
            if (qtd <= 0)
            {
                mensagem = "Pais sem estoque para ajuda.";
                return false;
            }

            RemoverEstoque(proposta.origemTeamId, proposta.recurso, qtd);
            AdicionarEstoque(proposta.alvoTeamId, proposta.recurso, qtd);
            mensagem = NomePais(proposta.origemTeamId) + " enviou " + qtd + " de " + item.nome + " para " + NomePais(proposta.alvoTeamId) + ".";
            RegistrarNoticia(mensagem);
            return true;
        }

        mensagem = "Tipo de proposta sem execucao direta.";
        return false;
    }

    public static string IdRecurso(RecursoMercado recurso)
    {
        switch (recurso)
        {
            case RecursoMercado.Comida: return "comida";
            case RecursoMercado.Petroleo: return "petroleo";
            case RecursoMercado.Aco: return "aco";
            case RecursoMercado.Armamentos: return "armamentos";
            case RecursoMercado.Uranio: return "uranio";
            default: return string.Empty;
        }
    }

    private static void AplicarPerfilPadrao(DadosPaisGoverno pais)
    {
        if (pais == null) return;
        switch (pais.perfilIA)
        {
            case PerfilPaisIA.Pequeno:
                pais.pesoDependenciaExterna = Mathf.Max(pais.pesoDependenciaExterna, 0.75f);
                pais.pesoDiplomacia = Mathf.Max(pais.pesoDiplomacia, 0.65f);
                break;
            case PerfilPaisIA.Industrial:
                pais.pesoIndustria = Mathf.Max(pais.pesoIndustria, 0.78f);
                pais.pesoComercio = Mathf.Max(pais.pesoComercio, 0.65f);
                break;
            case PerfilPaisIA.ProdutorPetroleo:
                pais.pesoComercio = Mathf.Max(pais.pesoComercio, 0.70f);
                pais.pesoControleEstoque = Mathf.Max(pais.pesoControleEstoque, 0.70f);
                break;
            case PerfilPaisIA.Militarista:
                pais.pesoMilitarismo = Mathf.Max(pais.pesoMilitarismo, 0.82f);
                pais.pesoAgressividade = Mathf.Max(pais.pesoAgressividade, 0.62f);
                break;
            case PerfilPaisIA.Rival:
                pais.pesoOdioRivais = Mathf.Max(pais.pesoOdioRivais, 0.78f);
                pais.pesoAgressividade = Mathf.Max(pais.pesoAgressividade, 0.68f);
                break;
            case PerfilPaisIA.Aliado:
                pais.pesoLealdadeAliados = Mathf.Max(pais.pesoLealdadeAliados, 0.78f);
                pais.pesoDiplomacia = Mathf.Max(pais.pesoDiplomacia, 0.68f);
                break;
        }
    }

    private void AlterarEstoque(int teamId, RecursoMercado recurso, int delta)
    {
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null) return;

        switch (recurso)
        {
            case RecursoMercado.Comida:
                pais.comida = Mathf.Max(0, pais.comida + delta);
                break;
            case RecursoMercado.Petroleo:
                pais.petroleo = Mathf.Max(0, pais.petroleo + delta);
                if (teamId == teamJogador && GerenciadorRecursos.Instancia != null)
                    GerenciadorRecursos.Instancia.petroleo = pais.petroleo;
                break;
            case RecursoMercado.Aco:
                pais.aco = Mathf.Max(0, pais.aco + delta);
                if (teamId == teamJogador && GerenciadorRecursos.Instancia != null)
                    GerenciadorRecursos.Instancia.aco = pais.aco;
                break;
            case RecursoMercado.Armamentos:
                pais.armamentos = Mathf.Max(0, pais.armamentos + delta);
                break;
            case RecursoMercado.Uranio:
                pais.uranio = Mathf.Max(0, pais.uranio + delta);
                break;
        }

        OnGovernoAtualizado?.Invoke();
    }

    private void AplicarEconomiaImoveis(DadosPaisGoverno pais, DadosEconomiaPais economia)
    {
        if (pais == null || economia == null) return;
        pais.populacaoMaxima = Mathf.Max(1, economia.moradiaTotal);
        pais.populacao = Mathf.Clamp(economia.populacaoTotal > 0 ? economia.populacaoTotal : pais.populacao, 0, pais.populacaoMaxima);
        pais.emprego = economia.populacaoTotal <= 0 ? 100f : Mathf.Clamp01(economia.empregosOcupados / (float)Mathf.Max(1, economia.populacaoTotal)) * 100f;
        pais.moradia = economia.populacaoTotal <= 0 ? 100f : Mathf.Clamp01(economia.moradiaTotal / (float)Mathf.Max(1, economia.populacaoTotal)) * 100f;
        pais.producao = Mathf.Clamp(35f + economia.industriaProduzida * 4f + economia.petroleoProduzido * 2f + economia.comidaProduzida * 1.2f - economia.deficitEnergia * 4f, 5f, 100f);
        pais.impostoMoradia = NormalizarImposto(pais.impostoMoradia);
        pais.impostoIndustria = NormalizarImposto(pais.impostoIndustria);
        pais.impostoComercio = NormalizarImposto(pais.impostoComercio);

        pais.receitaMoradia = economia.receitaMoradia * FatorImposto(pais.impostoMoradia);
        pais.receitaIndustria = economia.receitaIndustria * FatorImposto(pais.impostoIndustria);
        pais.receitaComercio = economia.receitaComercio * FatorImposto(pais.impostoComercio);
        pais.receitaEnergia = economia.receitaEnergia;
        pais.custoManutencao = economia.custoManutencao + economia.energiaConsumida * 0.35f + pais.populacao * 0.01f;
        pais.saldoOperacional = (pais.receitaMoradia + pais.receitaIndustria + pais.receitaComercio + pais.receitaEnergia) - pais.custoManutencao;
        pais.rendaPorSegundo = Mathf.Max(0f, pais.receitaMoradia + pais.receitaIndustria + pais.receitaComercio + pais.receitaEnergia);
        pais.gastosPorSegundo = Mathf.Max(0f, pais.custoManutencao);
        pais.qualidadeVida = economia.qualidadeVida;
        pais.energiaProduzida = economia.energiaProduzida;
        pais.energiaConsumida = economia.energiaConsumida;
        pais.deficitComida = economia.deficitComida;
        pais.deficitEnergia = economia.deficitEnergia;
        pais.deficitPetroleo = economia.deficitPetroleo;
        pais.estruturasSemEnergia = economia.estruturasSemEnergia;
        pais.exportacaoTotal = economia.exportacaoTotal;
        pais.importacaoTotal = economia.importacaoTotal;
    }

    private static float FatorImposto(int impostoPercentual)
    {
        return 1f + Mathf.Clamp(impostoPercentual, 0, 35) / 100f;
    }

    private static int NormalizarImposto(int valor)
    {
        valor = Mathf.Clamp(valor, 0, 35);
        return Mathf.RoundToInt(valor / 5f) * 5;
    }

    private static float CargaFiscalMedia(DadosPaisGoverno pais)
    {
        if (pais == null) return 0f;
        return (pais.impostoMoradia + pais.impostoIndustria + pais.impostoComercio) / 3f;
    }

    private static string GerarSimboloMoeda(string nomeMoeda)
    {
        if (string.IsNullOrWhiteSpace(nomeMoeda)) return "$";
        string limpo = new string(nomeMoeda.Trim().Where(char.IsLetterOrDigit).Take(3).ToArray()).ToUpperInvariant();
        if (string.IsNullOrEmpty(limpo)) return "$";
        return limpo + "$";
    }

    private void AtualizarReferenciasMoeda()
    {
        DadosPaisGoverno lider = PaisLiderMoeda();
        if (lider == null) return;

        float valorLider = Mathf.Max(0.01f, lider.valorMoeda);
        for (int i = 0; i < paises.Count; i++)
        {
            DadosPaisGoverno pais = paises[i];
            if (pais == null) continue;
            pais.moedaLiderReferencia = lider.nomeMoeda;
            pais.cambioComLider = Mathf.Clamp(pais.valorMoeda / valorLider, 0.01f, 99f);
        }
    }

    private void GarantirEconomiaViva()
    {
        if (encerrando) return;

        if (SistemaEconomiaImoveis.Instancia == null)
        {
            GameObject economia = new GameObject("SistemaEconomiaImoveis_Runtime");
            economia.AddComponent<SistemaEconomiaImoveis>();
            DontDestroyOnLoad(economia);
        }

        if (SistemaNoticiasEconomicas.Instancia == null)
        {
            GameObject noticiasEco = new GameObject("SistemaNoticiasEconomicas_Runtime");
            noticiasEco.AddComponent<SistemaNoticiasEconomicas>();
            DontDestroyOnLoad(noticiasEco);
        }
    }

    private void DescobrirIAsDaCena()
    {
#if UNITY_2023_1_OR_NEWER
        IA_Comandante[] comandantes = FindObjectsByType<IA_Comandante>(FindObjectsSortMode.None);
        IdentidadeIA[] identidades = FindObjectsByType<IdentidadeIA>(FindObjectsSortMode.None);
#else
        IA_Comandante[] comandantes = FindObjectsOfType<IA_Comandante>();
        IdentidadeIA[] identidades = FindObjectsOfType<IdentidadeIA>();
#endif
        foreach (IA_Comandante comandante in comandantes)
        {
            if (comandante == null || comandante.TeamID <= 1 || ObterPais(comandante.TeamID) != null) continue;
            paises.Add(new DadosPaisGoverno
            {
                teamId = comandante.TeamID,
                nomePais = string.IsNullOrEmpty(comandante.NomeComandante) ? "Pais IA " + comandante.TeamID : comandante.NomeComandante,
                nomeMoeda = "Moeda " + comandante.TeamID,
                simboloMoeda = "IA$",
                saldo = Mathf.RoundToInt(comandante.dinheiro),
                rendaPorSegundo = comandante.rendaPorSegundo,
                bloco = "IA"
            });
        }

        foreach (IdentidadeIA identidade in identidades)
        {
            if (identidade == null || identidade.teamID <= 1 || ObterPais(identidade.teamID) != null) continue;
            paises.Add(new DadosPaisGoverno
            {
                teamId = identidade.teamID,
                nomePais = string.IsNullOrEmpty(identidade.nomeComandante) ? "Pais IA " + identidade.teamID : identidade.nomeComandante,
                nomeMoeda = "Moeda " + identidade.teamID,
                simboloMoeda = "IA$",
                bloco = "IA"
            });
        }
    }

    private void GarantirMercado()
    {
        if (SistemaMercadoGlobal.Instancia != null) return;
        GameObject go = new GameObject("SistemaMercadoGlobal_Runtime");
        go.AddComponent<SistemaMercadoGlobal>();
        DontDestroyOnLoad(go);
    }
}
