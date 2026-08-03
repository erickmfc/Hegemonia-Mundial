using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SistemaGovernoMundial : MonoBehaviour
{
    public const string MoedaMestreNome = "Dolar Hegemonico (DH)";
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
        SistemaGovernoMundial existente = FindFirstObjectByType<SistemaGovernoMundial>();
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
        GerenciadorTempo.GarantirInstancia();
        GarantirSistemaIndustrial();
        RegistroNacoesGoverno.GarantirInstancia();
        RegistroNacoesGoverno.Instancia?.Sincronizar(paises);
        SistemaFederacoesGlobais.GarantirInstancia();
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
        SistemaFederacoesGlobais.GarantirInstancia();
    }

    public void InicializarDadosPadrao()
    {
        if (paises == null) paises = new List<DadosPaisGoverno>();
        if (relacoes == null) relacoes = new List<RelacaoPaisGoverno>();

        if (paises.Count == 0)
        {
            paises.Add(new DadosPaisGoverno { teamId = 1, nomePais = "Republica Atlas", nomeMoeda = "Atlas", simboloMoeda = "AT$", bloco = "Ordem Atlas", saldo = 5000, comida = 500, petroleo = 500, energia = 260, aco = 300, armamentos = 500, emprego = 78f, moradia = 72f, estabilidade = 76f, producao = 78f, aliadoPrioritarioTeamId = 2, rivalTeamId = 3, perfilIA = PerfilPaisIA.Neutro, modoInicialIA = ModoInicialPaisIA.Crescimento, nivelEconomico = 62, nivelIndustrial = 58, nivelMilitar = 54, nivelDiplomatico = 65, pesoComercio = 0.58f, pesoDiplomacia = 0.62f });
            paises.Add(new DadosPaisGoverno { teamId = 2, nomePais = "Republica Boreal", nomeMoeda = "Boreal", simboloMoeda = "BO$", bloco = "Ordem Atlas", saldo = 18000, comida = 1800, petroleo = 2600, energia = 380, aco = 700, armamentos = 900, emprego = 82f, moradia = 78f, estabilidade = 84f, producao = 74f, perfilIA = PerfilPaisIA.Aliado, modoInicialIA = ModoInicialPaisIA.Comercial, nivelEconomico = 78, nivelIndustrial = 66, nivelMilitar = 55, nivelDiplomatico = 76, pesoLealdadeAliados = 0.82f, pesoComercio = 0.72f });
            paises.Add(new DadosPaisGoverno { teamId = 3, nomePais = "Uniao Carmesim", nomeMoeda = "Carmesim", simboloMoeda = "CA$", bloco = "Pacto Solaris", saldo = 22000, comida = 900, petroleo = 4800, energia = 420, aco = 1200, armamentos = 1600, emprego = 61f, moradia = 52f, estabilidade = 44f, producao = 81f, emGuerra = true, perfilIA = PerfilPaisIA.ProdutorPetroleo, modoInicialIA = ModoInicialPaisIA.GuerraFria, nivelEconomico = 70, nivelIndustrial = 62, nivelMilitar = 78, nivelDiplomatico = 36, pesoAgressividade = 0.72f, pesoOdioRivais = 0.80f });
            paises.Add(new DadosPaisGoverno { teamId = 4, nomePais = "Dominio Valerian", nomeMoeda = "Valer", simboloMoeda = "VA$", bloco = "Liga Continental", saldo = 16000, comida = 600, petroleo = 900, energia = 280, aco = 1800, armamentos = 2100, emprego = 66f, moradia = 58f, estabilidade = 48f, producao = 76f, sancionado = true, perfilIA = PerfilPaisIA.Militarista, modoInicialIA = ModoInicialPaisIA.Mobilizacao, nivelEconomico = 58, nivelIndustrial = 78, nivelMilitar = 86, nivelDiplomatico = 32, pesoMilitarismo = 0.88f, pesoControleEstoque = 0.75f });
            paises.Add(new DadosPaisGoverno { teamId = 5, nomePais = "Federacao Alvorada", nomeMoeda = "Aurora", simboloMoeda = "AU$", bloco = "Nenhum", saldo = 12500, comida = 3400, petroleo = 600, energia = 220, aco = 500, armamentos = 350, emprego = 74f, moradia = 80f, estabilidade = 69f, producao = 67f, perfilIA = PerfilPaisIA.Pequeno, modoInicialIA = ModoInicialPaisIA.Crescimento, nivelEconomico = 52, nivelIndustrial = 34, nivelMilitar = 24, nivelDiplomatico = 58, pesoDependenciaExterna = 0.80f, pesoDiplomacia = 0.70f });
        }

        if (relacoes.Count == 0)
        {
            relacoes.Add(new RelacaoPaisGoverno { teamA = 1, teamB = 2, valor = 75, tratadoComercial = true, pactoMilitar = true });
            relacoes.Add(new RelacaoPaisGoverno { teamA = 1, teamB = 3, valor = -82, tratadoComercial = false, guerraDeclarada = true, sancaoAtiva = true });
            relacoes.Add(new RelacaoPaisGoverno { teamA = 1, teamB = 4, valor = -55, tratadoComercial = false, sancaoAtiva = true });
            relacoes.Add(new RelacaoPaisGoverno { teamA = 1, teamB = 5, valor = 28, tratadoComercial = true, pedidoPendente = true });
        }

        GarantirPosturasIniciais();

        foreach (DadosPaisGoverno pais in paises)
        {
            GarantirCatalogosNacionais(pais);
        }

        IA01NationNameRegistry.SortearNomesDePartida(paises, DateTime.Now.Millisecond);
        IA01NationNameRegistry.GarantirMoedasUnicas(paises);

        SincronizarJogador();
    }

    private void GarantirPosturasIniciais()
    {
        foreach (RelacaoPaisGoverno rel in relacoes)
        {
            if (rel == null) continue;
            if (rel.posturaAParaB == PosturaRelacaoPais.Neutro && rel.valor >= 50)
                rel.posturaAParaB = PosturaRelacaoPais.Amigo;
            else if (rel.posturaAParaB == PosturaRelacaoPais.Neutro && rel.valor <= -50)
                rel.posturaAParaB = PosturaRelacaoPais.Inimigo;
            if (rel.posturaBParaA == PosturaRelacaoPais.Neutro && rel.valor >= 50)
                rel.posturaBParaA = PosturaRelacaoPais.Amigo;
            else if (rel.posturaBParaA == PosturaRelacaoPais.Neutro && rel.valor <= -50)
                rel.posturaBParaA = PosturaRelacaoPais.Inimigo;
        }
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
                saldo = 12000,
                energia = 320
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
        IA01NationNameRegistry.GarantirNomesUnicos(paises, teamId + Time.frameCount);
        IA01NationNameRegistry.GarantirMoedasUnicas(paises);
        GarantirCatalogosNacionais(pais);
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

    public PosturaRelacaoPais ObterPostura(int origemTeamId, int alvoTeamId)
    {
        RelacaoPaisGoverno rel = ObterRelacao(origemTeamId, alvoTeamId);
        return rel == null ? PosturaRelacaoPais.Neutro : rel.PosturaDe(origemTeamId);
    }

    public bool DefinirPostura(int origemTeamId, int alvoTeamId, PosturaRelacaoPais postura, out string mensagem)
    {
        mensagem = string.Empty;
        DadosPaisGoverno origem = ObterPais(origemTeamId);
        DadosPaisGoverno alvo = ObterPais(alvoTeamId);
        if (origem == null || alvo == null || origemTeamId == alvoTeamId)
        {
            mensagem = "Nacao invalida para relacionamento.";
            return false;
        }

        RelacaoPaisGoverno rel = ObterRelacao(origemTeamId, alvoTeamId);
        if (postura == PosturaRelacaoPais.Inimigo && origem.federacaoGlobal == alvo.federacaoGlobal
            && !string.IsNullOrWhiteSpace(origem.federacaoGlobal))
        {
            mensagem = "Membros da mesma federacao nao podem declarar Inimigo sem romper o tratado.";
            return false;
        }

        rel.DefinirPostura(origemTeamId, postura);
        if (postura == PosturaRelacaoPais.Amigo) rel.valor = Mathf.Clamp(rel.valor + 5, -100, 100);
        else if (postura == PosturaRelacaoPais.Inimigo) rel.valor = Mathf.Clamp(rel.valor - 8, -100, 100);
        RegistrarNoticia(origem.nomePais + " declarou " + postura + " em relacao a " + alvo.nomePais + ".");
        mensagem = "Postura definida: " + postura + ".";
        OnGovernoAtualizado?.Invoke();
        return true;
    }

    private void GerarPedidosDeAjudaAutomaticos()
    {
        if (paises == null || teamJogador <= 0) return;
        float agora = Time.unscaledTime;
        foreach (DadosPaisGoverno solicitante in paises)
        {
            if (solicitante == null || solicitante.teamId == teamJogador) continue;
            if (solicitante.estabilidade > 45f && !solicitante.emGuerra && solicitante.comida > 0) continue;

            RecursoMercado recurso;
            string motivo;
            if (solicitante.comida <= 0 || solicitante.deficitComida > 0f)
            {
                recurso = RecursoMercado.Comida;
                motivo = "Pedido de mantimento alimentar: estoque de comida critico.";
            }
            else if (solicitante.emGuerra || solicitante.nivelMilitar < 30)
            {
                recurso = RecursoMercado.Armamentos;
                motivo = "Pedido de apoio militar contra ameaca ativa.";
            }
            else
            {
                recurso = RecursoMercado.Energia;
                motivo = "Pedido de mantimento energetico para estabilizar a economia.";
            }

            bool jaExiste = propostas.Any(p => p != null && p.EstaPendente && p.tipo == TipoPropostaInternacional.PedidoAjuda
                && p.origemTeamId == solicitante.teamId && p.alvoTeamId == teamJogador && p.recurso == recurso && p.expiraEm > agora);
            if (jaExiste) continue;

            TentarCriarProposta(new PropostaInternacional
            {
                origemTeamId = solicitante.teamId,
                alvoTeamId = teamJogador,
                tipo = TipoPropostaInternacional.PedidoAjuda,
                recurso = recurso,
                quantidade = Mathf.Clamp(solicitante.populacao / 4, 50, 400),
                precoUnitario = 1,
                prioridade = 90,
                motivo = motivo,
                expiraEm = agora + 120f,
                dedupKey = "ajuda-auto:" + solicitante.teamId + ":" + recurso
            });
        }
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
        RegistroNacoesGoverno.GarantirInstancia();
        RegistroNacoesGoverno.Instancia?.Sincronizar(paises);
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        float petroleoVariacao = mercado != null ? (mercado.ObterItem("petroleo")?.variacaoPercentual ?? 0f) : 0f;
        SistemaEconomiaImoveis economiaImoveis = SistemaEconomiaImoveis.Instancia;

        foreach (DadosPaisGoverno pais in paises)
        {
            if (pais == null) continue;

            float scoreAntes = pais.PontuacaoEconomica();
            DadosEconomiaPais economia = economiaImoveis != null ? economiaImoveis.ObterEconomia(pais.teamId) : null;
            if (economia == null)
            {
                economia = new DadosEconomiaPais { teamId = pais.teamId };
            }
            
            AplicarEconomiaImoveis(pais, economia);
            SistemaPopulacao.Processar(pais, economia);
            SistemaMilitar.Processar(pais, economia);
            AtualizarSistemasNacionais(pais);

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

            // ─── FELICIDADE DINÂMICA – Sistema Ponderado (Cities Skylines) ────────
            // Pesos: Comida 25% | Energia 20% | Emprego 20% | Moradia 15% |
            //        Segurança 10% | Impostos 5% | Qualidade de Vida 5%
            // Felicidade > 80 → país próspero, crescimento acelerado
            // Felicidade < 40 → emigração e crise demográfica
            // ─────────────────────────────────────────────────────────────────────
            {
                float deltaFelicidade = 0f;

                // --- 1. ALIMENTAÇÃO (Peso 25%) ---
                float consumoDiario = (pais.populacaoCivil / 100f * 1f) + (pais.populacaoMilitarAtiva / 100f * 2f);
                if (pais.comida <= 0 && pais.deficitComida > 0f)
                    deltaFelicidade -= 3.5f; // Fome crítica – catástrofe demográfica
                else if (pais.comida < consumoDiario * 3f && pais.deficitComida > 0f)
                    deltaFelicidade -= 1.0f; // Estoque baixo + déficit
                else if (pais.deficitComida > 0f)
                    deltaFelicidade -= 0.3f; // Produção insuficiente mas tem estoque
                else if (pais.comida >= consumoDiario * 10f)
                    deltaFelicidade += 0.4f; // Abundância: população feliz
                else
                    deltaFelicidade += 0.15f; // Comida adequada

                // --- 2. ENERGIA (Peso 20%) ---
                if (pais.deficitEnergia > 3f || pais.estruturasSemEnergia > 3)
                    deltaFelicidade -= 2.0f; // Apagão grave
                else if (pais.deficitEnergia > 0f || pais.estruturasSemEnergia > 0)
                    deltaFelicidade -= 0.8f; // Energia instável
                else
                    deltaFelicidade += 0.3f; // Energia estável

                // --- 3. EMPREGO (Peso 20%) ---
                if (pais.emprego < 40f)
                    deltaFelicidade -= 1.2f; // Desemprego severo
                else if (pais.emprego < 60f)
                    deltaFelicidade -= 0.5f; // Desemprego moderado
                else if (pais.emprego >= 85f)
                    deltaFelicidade += 0.5f; // Pleno emprego: muito feliz
                else if (pais.emprego >= 70f)
                    deltaFelicidade += 0.25f; // Emprego bom

                // --- 4. MORADIA (Peso 15%) ---
                if (pais.pressaoHabitacional > 1.05f)
                    deltaFelicidade -= 0.7f * (pais.pressaoHabitacional - 1f) * 10f; // Superpopulação
                else if (pais.moradia < 50f)
                    deltaFelicidade -= 0.8f; // Falta de moradia grave
                else if (pais.moradia < 70f)
                    deltaFelicidade -= 0.2f; // Moradia apertada
                else if (pais.moradia >= 90f)
                    deltaFelicidade += 0.35f; // Ótima cobertura habitacional
                else
                    deltaFelicidade += 0.1f; // Moradia razoável

                // --- 5. SEGURANÇA E ESTABILIDADE (Peso 10%) ---
                if (pais.emGuerra)
                    deltaFelicidade -= 2.0f; // Guerra = crise de felicidade
                if (pais.sancionado)
                    deltaFelicidade -= 0.8f;
                if (pais.inflacao > 20f)
                    deltaFelicidade -= (pais.inflacao - 20f) * 0.12f;
                else if (pais.inflacao < 5f)
                    deltaFelicidade += 0.15f; // Inflação controlada = estabilidade

                // --- 6. IMPOSTOS (Peso 5%) ---
                float cargaFiscalFel = CargaFiscalMedia(pais);
                if (cargaFiscalFel > 25f)
                    deltaFelicidade -= (cargaFiscalFel - 25f) * 0.10f;
                else if (cargaFiscalFel > 18f)
                    deltaFelicidade -= (cargaFiscalFel - 18f) * 0.05f;
                else
                    // Reduzir impostos melhora perceptivelmente o poder de
                    // compra e a satisfação; o efeito ainda é gradual para
                    // não transformar um clique em 100% de felicidade.
                    deltaFelicidade += 0.25f + Mathf.Clamp01((18f - cargaFiscalFel) / 18f) * 0.20f;

                // --- 7. QUALIDADE DE VIDA / IDH (Peso 5%) ---
                if (pais.qualidadeVida > 75f)
                    deltaFelicidade += 0.5f;
                else if (pais.qualidadeVida > 55f)
                    deltaFelicidade += 0.2f;
                else if (pais.qualidadeVida < 30f)
                    deltaFelicidade -= 0.8f;
                else if (pais.qualidadeVida < 45f)
                    deltaFelicidade -= 0.3f;

                // --- BÔNUS DE PROSPERIDADE: Satisfação de Serviços ---
                // País com bons serviços recebe bônus adicional de estabilidade de felicidade
                if (pais.indiceSatisfacaoServicos > 80f)
                    deltaFelicidade += 0.3f;
                else if (pais.indiceSatisfacaoServicos < 30f)
                    deltaFelicidade -= 0.4f;

                // Aplica suavizando (quanto maior a diferença, mais lenta a mudança)
                // Isso evita oscilações bruscas de 0→100 em poucos ticks
                float pesoSuavizacao = Mathf.Lerp(1f, 0.5f, Mathf.Abs(deltaFelicidade) / 5f);
                pais.felicidade = Mathf.Clamp(pais.felicidade + deltaFelicidade * pesoSuavizacao, 0f, 100f);
            }
            // ─────────────────────────────────────────────────────────────────────

            SistemaMoeda.Processar(pais, economia);

            if (pais.teamId != teamJogador)
            {
                pais.saldo += Mathf.RoundToInt(Mathf.Max(1f, pais.rendaPorSegundo - pais.gastosPorSegundo));
                if (economia != null)
                {
                    pais.comida = Mathf.Max(0, pais.comida + Mathf.RoundToInt(economia.comidaProduzida - Mathf.Max(1f, pais.populacao * 0.01f)));
                    pais.petroleo = Mathf.Max(0, pais.petroleo + Mathf.RoundToInt(economia.petroleoProduzido - economia.industriaProduzida * 0.10f));
                    pais.energia = Mathf.Max(0, pais.energia + Mathf.RoundToInt(economia.energiaProduzida - economia.energiaConsumida * 0.85f));
                    pais.aco = Mathf.Max(0, pais.aco + Mathf.RoundToInt(economia.industriaProduzida * 0.55f));
                    pais.armamentos = Mathf.Max(0, pais.armamentos + Mathf.RoundToInt(economia.industriaProduzida * (pais.pesoMilitarismo > 0.65f ? 0.22f : 0.08f)));
                    
                    if (pais.tecnologiaExtracaoConcluida)
                    {
                        pais.petroleo += 50;
                        pais.aco += 50;
                    }
                }
            }

            float scoreDepois = pais.PontuacaoEconomica();
            if (scoreAntes > 65f && scoreDepois < 55f)
                RegistrarNoticia(pais.nomePais + " entrou em deterioracao economica.");
        }

        AtualizarReferenciasMoeda();
        GerarPedidosDeAjudaAutomaticos();

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
        jogador.energia = gr.energia;
        jogador.aco = gr.aco;

        // Se a populacao do jogador nao foi inicializada, puxa o valor inicial do GerenciadorRecursos
        if (jogador.populacao <= 0 && gr.populacaoAtual > 0)
        {
            jogador.populacao = gr.populacaoAtual;
            jogador.populacaoMaxima = gr.populacaoMaxima;
        }

        // Mesmo antes de a cena registrar as estruturas na economia, aplica-se
        // um retrato vazio. Isso impede população, renda e capacidade antigas de
        // continuarem aparecendo como se existissem casas, comida ou indústria.
        if (economia == null)
        {
            economia = new DadosEconomiaPais { teamId = teamJogador };
        }

        AplicarEconomiaImoveis(jogador, economia);

        // O HUD e o caixa usam o fluxo calculado pelas estruturas reais.
        // A renda passiva antiga nao pode sustentar um pais sem economia.
        gr.dinheiroPorSegundo = jogador.saldoOperacional;
        gr.energiaPorSegundo = Mathf.RoundToInt(economia.energiaProduzida - economia.energiaConsumida);
        gr.populacaoAtual = jogador.populacao;
        gr.populacaoMaxima = jogador.populacaoMaxima;

        // CORREÇÃO: Sincronizar a população civil do jogador também, já que a UI lê jogador.populacaoCivil
        jogador.populacaoCivil = Mathf.Max(0, jogador.populacao - jogador.populacaoMilitarAtiva - jogador.reservistas - jogador.alistaveis);
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

    public bool IniciarPesquisaNacional(int teamId, string pesquisaId, out string mensagem)
    {
        mensagem = "Pesquisa indisponivel.";
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null)
        {
            return false;
        }

        GarantirCatalogosNacionais(pais);
        PesquisaNacionalEstado pesquisa = pais.pesquisas.FirstOrDefault(p => p != null && p.id == pesquisaId);
        if (pesquisa == null)
        {
            mensagem = "Pesquisa nao encontrada.";
            return false;
        }

        if (pesquisa.concluida)
        {
            mensagem = "Pesquisa ja concluida.";
            return false;
        }

        if (pesquisa.emAndamento)
        {
            mensagem = "Pesquisa ja esta em andamento.";
            return false;
        }

        if (!DependenciasAtendidas(pais, pesquisa.dependencias))
        {
            mensagem = "Dependencias cientificas ainda nao foram concluídas.";
            return false;
        }

        if (!TentarPagar(teamId, pesquisa.custoSaldo))
        {
            mensagem = "Saldo insuficiente para iniciar a pesquisa.";
            return false;
        }

        if (!ConsumirEnergia(teamId, pesquisa.custoEnergia))
        {
            AdicionarSaldo(teamId, pesquisa.custoSaldo);
            mensagem = "Energia insuficiente para iniciar a pesquisa.";
            return false;
        }

        SistemaGastosMilitares.GarantirInstancia();
        SistemaGastosMilitares.Instancia?.RegistrarPesquisa(teamId, pesquisa.id, pesquisa.nome, pesquisa.custoSaldo, pesquisa.categoria);

        pesquisa.diaInicio = DiaAtual();
        pesquisa.emAndamento = true;
        mensagem = "Pesquisa iniciada: " + pesquisa.nome + ".";
        OnGovernoAtualizado?.Invoke();
        return true;
    }

    public bool IniciarTecnologiaNacional(int teamId, string tecnologiaId, out string mensagem)
    {
        mensagem = "Tecnologia indisponivel.";
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null)
        {
            return false;
        }

        GarantirCatalogosNacionais(pais);
        TecnologiaNacionalEstado tecnologia = pais.tecnologias.FirstOrDefault(t => t != null && t.id == tecnologiaId);
        if (tecnologia == null)
        {
            mensagem = "Tecnologia nao encontrada.";
            return false;
        }

        if (tecnologia.nivelAtual >= tecnologia.nivelMaximo)
        {
            mensagem = "Tecnologia ja esta no nivel maximo.";
            return false;
        }

        if (tecnologia.emAndamento)
        {
            mensagem = "Tecnologia ja esta em desenvolvimento.";
            return false;
        }

        if (!DependenciasAtendidas(pais, tecnologia.dependencias))
        {
            mensagem = "Dependencias tecnológicas ainda nao foram atendidas.";
            return false;
        }

        int custoSaldo = tecnologia.custoSaldo * Mathf.Max(1, tecnologia.nivelAtual + 1);
        int custoEnergia = tecnologia.custoEnergia * Mathf.Max(1, tecnologia.nivelAtual + 1);
        if (!TentarPagar(teamId, custoSaldo))
        {
            mensagem = "Saldo insuficiente para investir nessa tecnologia.";
            return false;
        }

        if (!ConsumirEnergia(teamId, custoEnergia))
        {
            AdicionarSaldo(teamId, custoSaldo);
            mensagem = "Energia insuficiente para investir nessa tecnologia.";
            return false;
        }

        SistemaGastosMilitares.GarantirInstancia();
        SistemaGastosMilitares.Instancia?.RegistrarPesquisa(teamId, tecnologia.id, tecnologia.nome, custoSaldo, tecnologia.categoria);

        tecnologia.diaInicio = DiaAtual();
        tecnologia.emAndamento = true;
        mensagem = "Tecnologia em desenvolvimento: " + tecnologia.nome + ".";
        OnGovernoAtualizado?.Invoke();
        return true;
    }

    public bool ExpandirLaboratorio(int teamId, string laboratorioId, out string mensagem)
    {
        mensagem = "Laboratorio indisponivel.";
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null)
        {
            return false;
        }

        GarantirCatalogosNacionais(pais);
        LaboratorioNacionalEstado laboratorio = pais.laboratorios.FirstOrDefault(l => l != null && l.id == laboratorioId);
        if (laboratorio == null)
        {
            mensagem = "Laboratorio nao encontrado.";
            return false;
        }

        if (laboratorio.nivelAtual >= laboratorio.nivelMaximo)
        {
            mensagem = "Laboratorio ja opera no teto atual.";
            return false;
        }

        if (laboratorio.emExpansao)
        {
            mensagem = "Laboratorio ja esta sendo expandido.";
            return false;
        }

        if (!DependenciasAtendidas(pais, laboratorio.dependencias))
        {
            mensagem = "Requisitos laboratoriais ainda nao foram atendidos.";
            return false;
        }

        int custoSaldo = laboratorio.custoSaldo * Mathf.Max(1, laboratorio.nivelAtual + 1);
        int custoEnergia = laboratorio.custoEnergia * Mathf.Max(1, laboratorio.nivelAtual + 1);
        if (!TentarPagar(teamId, custoSaldo))
        {
            mensagem = "Saldo insuficiente para o laboratorio.";
            return false;
        }

        if (!ConsumirEnergia(teamId, custoEnergia))
        {
            AdicionarSaldo(teamId, custoSaldo);
            mensagem = "Energia insuficiente para a expansão.";
            return false;
        }

        laboratorio.diaInicio = DiaAtual();
        laboratorio.emExpansao = true;
        mensagem = "Expansao iniciada: " + laboratorio.nome + ".";
        OnGovernoAtualizado?.Invoke();
        return true;
    }

    public void ConfigurarSatelite(int teamId, bool manutencaoAutomatica)
    {
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null)
        {
            return;
        }

        GarantirCatalogosNacionais(pais);
        pais.sateliteDefesa.manutencaoAutomatica = manutencaoAutomatica;
        OnGovernoAtualizado?.Invoke();
    }

    public bool InvestirNoSatelite(int teamId, int aporte, out string mensagem)
    {
        mensagem = "Programa satelital indisponivel.";
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null)
        {
            return false;
        }

        GarantirCatalogosNacionais(pais);
        if (!PesquisaConcluida(pais, "pesquisa_satelite_1"))
        {
            mensagem = "Pesquise tecnologia de satelite nivel 1 primeiro.";
            return false;
        }

        if (!TentarPagar(teamId, aporte))
        {
            mensagem = "Saldo insuficiente para o aporte orbital.";
            return false;
        }

        pais.sateliteDefesa.desbloqueado = true;
        pais.sateliteDefesa.integridade = Mathf.Clamp(pais.sateliteDefesa.integridade + aporte / 40f, 0f, 100f);
        pais.sateliteDefesa.desempenho = Mathf.Clamp(pais.sateliteDefesa.desempenho + aporte / 50f, 0f, 100f);
        mensagem = "Aporte orbital aplicado ao satelite nacional.";
        OnGovernoAtualizado?.Invoke();
        return true;
    }

    public bool InvestirCapacidadeNacional(int teamId, string foco, int custo = 900)
    {
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null || string.IsNullOrWhiteSpace(foco)) return false;

        int gasto = Mathf.Clamp(custo, 150, 5000);
        if (!TentarPagar(teamId, gasto)) return false;

        string focoNormalizado = foco.Trim().ToLowerInvariant();
        int ganhoEconomico = 0;
        int ganhoIndustrial = 0;
        int ganhoDiplomatico = 0;
        int ganhoMilitar = 0;
        float bonusEstabilidade = 0f;

        switch (focoNormalizado)
        {
            case "energia":
                ganhoEconomico = 3;
                ganhoIndustrial = 2;
                bonusEstabilidade = 0.7f;
                pais.tecnologiaExtracaoConcluida = true;
                break;
            case "industria":
                ganhoEconomico = 1;
                ganhoIndustrial = 3;
                bonusEstabilidade = 0.5f;
                break;
            case "diplomacia":
                ganhoEconomico = 1;
                ganhoDiplomatico = 3;
                bonusEstabilidade = 0.8f;
                break;
            case "defesa":
                ganhoEconomico = 1;
                ganhoMilitar = 3;
                bonusEstabilidade = 0.3f;
                break;
            case "logistica":
            case "economia":
                ganhoEconomico = 3;
                ganhoIndustrial = 1;
                ganhoDiplomatico = 1;
                bonusEstabilidade = 0.6f;
                break;
            case "ciencia":
                ganhoEconomico = 1;
                ganhoIndustrial = 1;
                ganhoDiplomatico = 1;
                ganhoMilitar = 1;
                bonusEstabilidade = 0.5f;
                break;
            default:
                ganhoEconomico = 1;
                bonusEstabilidade = 0.2f;
                break;
        }

        pais.nivelEconomico = Mathf.Clamp(pais.nivelEconomico + ganhoEconomico, 0, 100);
        pais.nivelIndustrial = Mathf.Clamp(pais.nivelIndustrial + ganhoIndustrial, 0, 100);
        pais.nivelDiplomatico = Mathf.Clamp(pais.nivelDiplomatico + ganhoDiplomatico, 0, 100);
        pais.nivelMilitar = Mathf.Clamp(pais.nivelMilitar + ganhoMilitar, 0, 100);
        pais.estabilidade = Mathf.Clamp(pais.estabilidade + bonusEstabilidade, 0f, 100f);
        pais.producao = Mathf.Clamp(pais.producao + ganhoEconomico * 0.8f + ganhoIndustrial * 0.6f, 0f, 100f);

        RegistrarNoticia(pais.nomePais + " investiu em " + focoNormalizado + ".");
        if (teamId == teamJogador)
        {
            SincronizarJogador();
        }

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

    public bool TentarPagar(int teamId, long valor)
    {
        if (valor <= 0) return true;
        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null) return false;

        if (teamId == teamJogador && GerenciadorRecursos.Instancia != null)
        {
            if (!GerenciadorRecursos.Instancia.TentarGastar(custoDinheiro: valor)) return false;
            SincronizarJogador();
            return true;
        }

        if (pais.saldo < valor) return false;

        pais.saldo -= valor;
        OnGovernoAtualizado?.Invoke();
        return true;
    }

    public void AdicionarSaldo(int teamId, long valor)
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
        string recursoId = IntegracaoMercadoIndustrial.IdInternoDoMercado(recurso);
        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial != null && IndustriaIds.EhIndustrial(recursoId))
        {
            return industrial.ObterQuantidadeInt(teamId, recurso);
        }

        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null) return 0;
        switch (recurso)
        {
            case RecursoMercado.Comida: return pais.comida;
            case RecursoMercado.Petroleo: return pais.petroleo;
            case RecursoMercado.Energia: return pais.energia;
            case RecursoMercado.Aco: return pais.aco;
            case RecursoMercado.Armamentos: return pais.armamentos;
            case RecursoMercado.Uranio: return pais.uranio;
            default: return 0;
        }
    }

    public int ObterEstoque(int teamId, string recursoId)
    {
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return 0;
        }

        RecursoMercado recursoLegado;
        if (TentarConverterRecursoMercado(recursoId, out recursoLegado))
        {
            return ObterEstoque(teamId, recursoLegado);
        }

        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial != null)
        {
            return Mathf.RoundToInt((float)industrial.Armazem.ObterDisponivel(teamId.ToString(), recursoId));
        }

        return 0;
    }

    public void AdicionarEstoque(int teamId, RecursoMercado recurso, int quantidade)
    {
        AlterarEstoque(teamId, recurso, Mathf.Abs(quantidade));
    }

    public void AdicionarEstoque(int teamId, string recursoId, int quantidade)
    {
        AlterarEstoque(teamId, recursoId, Mathf.Abs(quantidade));
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

    public void RemoverEstoque(int teamId, string recursoId, int quantidade)
    {
        AlterarEstoque(teamId, recursoId, -Mathf.Abs(quantidade));
    }

    private void GarantirCatalogosNacionais(DadosPaisGoverno pais)
    {
        if (pais == null)
        {
            return;
        }

        if (pais.pesquisas == null) pais.pesquisas = new List<PesquisaNacionalEstado>();
        if (pais.tecnologias == null) pais.tecnologias = new List<TecnologiaNacionalEstado>();
        if (pais.laboratorios == null) pais.laboratorios = new List<LaboratorioNacionalEstado>();
        if (pais.sateliteDefesa == null) pais.sateliteDefesa = new SateliteDefesaEstado();

        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_extracao_ferro", "Extracao de Minerio de Ferro", "Extracao", "Organiza a primeira cadeia nacional de ferro bruto.", "Base de mineracao", "Ordens de ferro e lotes pesados", string.Empty, 520, 60, 2));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_extracao_cobre", "Extracao de Minerio de Cobre", "Extracao", "Abre o ciclo de cobre para eletrica e industria.", "Aco leve e energia basica", "Ordens de cobre e refino industrial", string.Empty, 580, 70, 2));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_extracao_bauxita", "Extracao de Bauxita", "Extracao", "Prepara a base para materiais leves.", "Base de extracao e logistica", "Bauxita e duraluminio", string.Empty, 640, 75, 2));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_metalurgia", "Metalurgia do Aco", "Pesquisa", "Libera o refino nacional de aco estrutural.", "Base industrial", "Aco estrutural e linhas de refino", "pesquisa_extracao_ferro", 650, 90, 2));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_extracao_titanio", "Extracao de Titanio", "Extracao Estrategica", "Mapeia e habilita minerio estrategico para blindagem pesada.", "Metalurgia do Aco", "Liga de titanio e materiais pesados", "pesquisa_metalurgia", 1450, 140, 3));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_eletronica", "Eletronica Industrial", "Pesquisa", "Desenvolve componentes eletronicos para guiagem e radares.", "Metalurgia do Aco", "Componentes eletronicos", "pesquisa_metalurgia,pesquisa_extracao_cobre", 950, 120, 3));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_nuclear", "Pesquisa Nuclear", "Pesquisa", "Abre o ciclo nuclear e o laboratorio dedicado.", "Eletronica, estabilidade e energia", "Uranio enriquecido e laboratorio nuclear", "pesquisa_eletronica,pesquisa_extracao_titanio", 4200, 420, 5));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_municao_leve", "Municao Leve", "Balistica", "Padroniza lotes iniciais para infantaria e defesa terrestre.", "Metalurgia e cobre", "Pacote balistico inicial", "pesquisa_metalurgia,pesquisa_extracao_cobre", 1100, 90, 2));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_municao_30", "Municao Automatizada 30 mm", "Balistica Pesada", "Abre o ramo de municao automatica para plataformas mais pesadas.", "Municao Leve", "Munição 30 mm e cadencia industrial", "pesquisa_municao_leve", 1500, 120, 3));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_municao_naval", "Municao Naval", "Artilharia Naval", "Desenvolve lotes reforcados para plataformas navais.", "Municao Leve e titanio", "Munição naval e blindagem de projetil", "pesquisa_municao_leve,pesquisa_extracao_titanio", 1900, 150, 3));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_bombas_aereas", "Bombas Aereas Taticas", "Armamento Aereo", "Abre a trilha de armamento aereo de impacto tatico.", "Aeroespacial I", "Bombas e cargas aereas", "pesquisa_aeroespacial_1", 2100, 170, 3));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_missil_guiado", "Missil Guiado", "Misseis", "Unifica sensores, guiagem e telemetria para armas inteligentes.", "Eletronica Industrial", "Misseis guiados e componentes militares", "pesquisa_eletronica", 2600, 220, 4));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_interceptacao", "Interceptacao Integrada", "Defesa Aerea", "Prepara a defesa para neutralizar alvos em voo.", "Missil Guiado e Satelite I", "Interceptacao e radares de reacao", "pesquisa_missil_guiado,pesquisa_satelite_1", 3100, 250, 4));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_ares_ar", "Ares Ar - Defesa Antiaerea", "Defesa Aerea", "Habilita a operacao e a fabricacao dos cartuchos do sistema Ares_Ar.", "Interceptacao Integrada", "Ares_Ar e cartuchos antiaereos", "pesquisa_interceptacao", 1800, 150, 3));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_foguete_1", "Tecnologia de Foguete I", "Aeroespacial", "Primeira etapa de propulsao e combustiveis.", "Metalurgia do Aco", "Base para programa orbital", "pesquisa_metalurgia", 1600, 160, 3));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_foguete_2", "Tecnologia de Foguete II", "Aeroespacial", "Aumenta alcance, guiagem e estabilidade de voo.", "Foguete I", "Programa orbital intermediario", "pesquisa_foguete_1", 2600, 240, 4));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_foguete_3", "Tecnologia de Foguete III", "Aeroespacial", "Fecha o pacote de propulsao pesada para lancamento orbital.", "Foguete II", "Capacidade de lancamento pesada", "pesquisa_foguete_2", 4200, 360, 5));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_aeroespacial_1", "Tecnologia Aeroespacial I", "Aeroespacial", "Estruturas leves, navegacao e telemetria basica.", "Eletronica Industrial", "Programa aeroespacial", "pesquisa_eletronica,pesquisa_extracao_bauxita", 1800, 180, 3));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_aeroespacial_2", "Tecnologia Aeroespacial II", "Aeroespacial", "Integra sensores, telemetria e materiais leves.", "Aeroespacial I", "Programa aeroespacial avancado", "pesquisa_aeroespacial_1", 2700, 240, 4));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_aeroespacial_3", "Tecnologia Aeroespacial III", "Aeroespacial", "Prepara a base final para missao orbital nacional.", "Aeroespacial II", "Capacidade espacial completa", "pesquisa_aeroespacial_2", 4600, 380, 5));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_satelite_1", "Tecnologia de Satelite I", "Orbital", "Primeiros sistemas de observacao e estabilizacao orbital.", "Aeroespacial I, Foguete I", "Desbloqueia satelite nacional", "pesquisa_foguete_1,pesquisa_aeroespacial_1", 2200, 210, 3));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_satelite_2", "Tecnologia de Satelite II", "Orbital", "Melhora sensores, transmissores e cobertura orbital.", "Satelite I", "Desempenho orbital melhorado", "pesquisa_satelite_1", 3200, 280, 4));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_satelite_3", "Tecnologia de Satelite III", "Orbital", "Completa autonomia e robustez do pacote satelital.", "Satelite II", "Pronto para prefab de foguete futuro", "pesquisa_satelite_2,pesquisa_foguete_3,pesquisa_aeroespacial_3", 5200, 420, 5));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_agencia_orbital", "Agencia Atlas Orbital", "Institucional", "Equivalente nacional de uma agencia espacial para coordenar missoes.", "Satelite II e Foguete II", "Coordenacao espacial nacional", "pesquisa_satelite_2,pesquisa_foguete_2", 3800, 260, 4));
        GarantirPesquisaCatalogo(pais, CriarPesquisa("pesquisa_icnu", "Programa ICNU", "Dissuasao Estrategica", "Etapa final do pacote de dissuasao orbital e nuclear.", "Nuclear, interceptacao e satelite III", "Ciclo estrategico completo", "pesquisa_nuclear,pesquisa_interceptacao,pesquisa_satelite_3", 6800, 560, 6));

        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_linhas_industriais", "Linhas Industriais", "Industria", "Amplia a capacidade de linhas industriais nacionais.", "Mais linhas e menos gargalo", "pesquisa_metalurgia", 900, 80, 2, 3));
        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_refino_alto_rendimento", "Refino de Alto Rendimento", "Industria", "Aumenta eficiencia das ordens de refino.", "Mais rendimento e menos perdas", "pesquisa_eletronica", 1200, 100, 2, 3));
        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_automacao_linhas", "Automacao de Linhas", "Industria", "Reduz paradas entre lotes e acelera a fila.", "Resposta industrial mais rapida", "pesquisa_eletronica", 1500, 120, 3, 3));
        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_reserva_materiais", "Reserva Imediata de Materiais", "Industria", "Protege estoques ja alocados aos projetos.", "Menos gargalo de materiais", "pesquisa_metalurgia", 1350, 100, 2, 2));
        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_integracao_armazem", "Integracao com Armazem Nacional", "Industria", "Liga fila, estoque e historico logístico.", "Maior controle do armazem", "pesquisa_eletronica", 1600, 120, 3, 2));
        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_propulsao_solida", "Propulsao de Combustivel Solido", "Foguetes", "Base tecnologica para o ramo de foguetes.", "Melhora programa de foguetes", "pesquisa_foguete_1", 1800, 150, 3, 3));
        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_estabilizacao_lancamento", "Estabilizacao de Lancamento", "Foguetes", "Aumenta previsibilidade e seguranca do lancador.", "Mais confianca orbital", "pesquisa_foguete_2", 2200, 170, 3, 3));
        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_controle_orbital", "Controle Orbital", "Orbital", "Melhora estabilidade e telemetria do satelite.", "Maior integridade orbital", "pesquisa_satelite_1", 2100, 180, 3, 3));
        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_radar_satelital", "Radar Satelital", "Orbital", "Integra observacao orbital a defesa aerea.", "Bonus ao satelite nacional", "pesquisa_satelite_2", 2400, 200, 3, 3));
        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_navegacao_inercial", "Navegacao Inercial", "Orbital", "Refina rotas, giroscopios e posicao do veiculo orbital.", "Controle fino de rota", "pesquisa_aeroespacial_2,pesquisa_foguete_2", 2500, 200, 3, 3));
        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_miniaturizacao", "Miniaturizacao Industrial", "Eletronica", "Compacta sensores, chips e placas criticas.", "Componentes mais densos", "pesquisa_eletronica", 1900, 150, 3, 3));
        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_guiagem_precisao", "Guiagem de Precisao", "Misseis", "Torna a linha de misseis e interceptacao mais confiavel.", "Armas inteligentes mais estaveis", "pesquisa_missil_guiado", 2600, 210, 3, 3));
        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_ciencia_aplicada", "Ciencia Aplicada", "Pesquisa", "Acelera pesquisas, laboratorios e projetos nacionais.", "Ciclo cientifico mais forte", "pesquisa_agencia_orbital", 2600, 200, 3, 3));
        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_autorizacao_nuclear", "Autorizacao Nuclear", "Nuclear", "Padroniza seguranca e governanca do programa nuclear.", "Seguranca institucional", "pesquisa_nuclear", 2800, 260, 4, 2));
        GarantirTecnologiaCatalogo(pais, CriarTecnologia("tec_blindagem_radiologica", "Blindagem Radiologica", "Nuclear", "Fortalece seguranca de laboratorios e cargas sensiveis.", "Protecao do ciclo nuclear", "pesquisa_icnu", 3400, 280, 4, 2));

        GarantirLaboratorioCatalogo(pais, CriarLaboratorio("lab_metalurgia", "Laboratorio de Materiais Ferrosos", "Ferro, aco estrutural e projeteis.", "Base metalurgica nacional.", "pesquisa_metalurgia", 1200, 100, 2, 3));
        GarantirLaboratorioCatalogo(pais, CriarLaboratorio("lab_eletronica", "Laboratorio de Eletronica Industrial", "Sensores, chips, guiagem e radares.", "Pilar de componentes eletronicos.", "pesquisa_eletronica", 1500, 120, 3, 3));
        GarantirLaboratorioCatalogo(pais, CriarLaboratorio("lab_aeroespacial", "Laboratorio Aeroespacial", "Estruturas leves, telemetria e foguetes.", "Centro de desenvolvimento orbital.", "pesquisa_aeroespacial_1,pesquisa_foguete_1", 2600, 210, 4, 3));
        GarantirLaboratorioCatalogo(pais, CriarLaboratorio("lab_nuclear", "Laboratorio Nuclear", "Ciclo do uranio, contencao e seguranca.", "Estrutura critica para programa nuclear.", "pesquisa_nuclear", 4200, 360, 5, 2));
        GarantirLaboratorioCatalogo(pais, CriarLaboratorio("lab_orbital", "Centro Orbital Atlas", "Satelites, lancamento e controle remoto.", "Centro de comando espacial.", "pesquisa_agencia_orbital,pesquisa_satelite_2", 5200, 420, 5, 3));
        GarantirLaboratorioCatalogo(pais, CriarLaboratorio("lab_balistica", "Laboratorio Balistico", "Municao leve, 30 mm e blindados.", "Polo de balistica e cadencia de linha.", "pesquisa_municao_leve,pesquisa_municao_30", 2300, 170, 3, 3));
        GarantirLaboratorioCatalogo(pais, CriarLaboratorio("lab_armas_navais", "Laboratorio de Armas Navais", "Munição naval, casco e guiagem maritima.", "Desenvolvimento naval pesado.", "pesquisa_municao_naval,pesquisa_satelite_1", 3100, 220, 4, 3));
        GarantirLaboratorioCatalogo(pais, CriarLaboratorio("lab_misseis", "Laboratorio de Misseis Guiados", "Guiagem, sensores e interceptacao.", "Centro de resposta antiaerea e tática.", "pesquisa_missil_guiado,pesquisa_interceptacao", 3600, 260, 4, 3));
        GarantirLaboratorioCatalogo(pais, CriarLaboratorio("lab_dissuasao", "Centro Estrategico de Dissuasao", "Orbital, nuclear e controle de cargas sensiveis.", "Estrutura final do pacote estrategico nacional.", "pesquisa_icnu,pesquisa_satelite_3", 6200, 520, 6, 2));
    }

    private void AtualizarSistemasNacionais(DadosPaisGoverno pais)
    {
        GarantirCatalogosNacionais(pais);
        int diaAtual = DiaAtual();

        foreach (PesquisaNacionalEstado pesquisa in pais.pesquisas.Where(p => p != null && p.emAndamento))
        {
            if (diaAtual < pesquisa.diaInicio + Mathf.Max(1, pesquisa.duracaoDias))
            {
                continue;
            }

            pesquisa.emAndamento = false;
            pesquisa.concluida = true;
            if (pesquisa.id == "pesquisa_satelite_1")
            {
                pais.sateliteDefesa.desbloqueado = true;
            }

            if (pesquisa.id == "pesquisa_nuclear")
            {
                pais.tecnologiaExtracaoConcluida = true;
            }

            RegistrarNoticia(pais.nomePais + " concluiu " + pesquisa.nome + ".");
        }

        foreach (TecnologiaNacionalEstado tecnologia in pais.tecnologias.Where(t => t != null && t.emAndamento))
        {
            if (diaAtual < tecnologia.diaInicio + Mathf.Max(1, tecnologia.duracaoDias))
            {
                continue;
            }

            tecnologia.emAndamento = false;
            tecnologia.nivelAtual = Mathf.Clamp(tecnologia.nivelAtual + 1, 0, tecnologia.nivelMaximo);
            AplicarEfeitoTecnologia(pais, tecnologia);
            RegistrarNoticia(pais.nomePais + " elevou " + tecnologia.nome + " para o nivel " + tecnologia.nivelAtual + ".");
        }

        foreach (LaboratorioNacionalEstado laboratorio in pais.laboratorios.Where(l => l != null && l.emExpansao))
        {
            if (diaAtual < laboratorio.diaInicio + Mathf.Max(1, laboratorio.duracaoDias))
            {
                continue;
            }

            laboratorio.emExpansao = false;
            laboratorio.nivelAtual = Mathf.Clamp(laboratorio.nivelAtual + 1, 0, laboratorio.nivelMaximo);
            pais.nivelIndustrial = Mathf.Clamp(pais.nivelIndustrial + 1, 0, 100);
            RegistrarNoticia(pais.nomePais + " expandiu " + laboratorio.nome + " para o nivel " + laboratorio.nivelAtual + ".");
        }

        ProcessarSatelite(pais, diaAtual);
    }

    private void ProcessarSatelite(DadosPaisGoverno pais, int diaAtual)
    {
        if (pais == null || pais.sateliteDefesa == null || !pais.sateliteDefesa.desbloqueado)
        {
            return;
        }

        if (pais.sateliteDefesa.ultimoDiaProcessado >= diaAtual)
        {
            return;
        }

        pais.sateliteDefesa.ultimoDiaProcessado = diaAtual;
        int custoBase = pais.sateliteDefesa.manutencaoAutomatica
            ? pais.sateliteDefesa.custoOperacionalDiario + pais.sateliteDefesa.custoManutencaoDiaria
            : pais.sateliteDefesa.custoOperacionalDiario;

        bool pagou = TentarPagar(pais.teamId, custoBase);
        if (pagou)
        {
            pais.sateliteDefesa.integridade = Mathf.Clamp(pais.sateliteDefesa.integridade + (pais.sateliteDefesa.manutencaoAutomatica ? 1.8f : -0.4f), 0f, 100f);
            pais.sateliteDefesa.desempenho = Mathf.Clamp(pais.sateliteDefesa.desempenho + 0.9f, 0f, 100f);
        }
        else
        {
            pais.sateliteDefesa.integridade = Mathf.Clamp(pais.sateliteDefesa.integridade - 2.8f, 0f, 100f);
            pais.sateliteDefesa.desempenho = Mathf.Clamp(pais.sateliteDefesa.desempenho - 2.1f, 0f, 100f);
        }
    }

    private void AplicarEfeitoTecnologia(DadosPaisGoverno pais, TecnologiaNacionalEstado tecnologia)
    {
        if (pais == null || tecnologia == null)
        {
            return;
        }

        switch (tecnologia.id)
        {
            case "tec_linhas_industriais":
            case "tec_refino_alto_rendimento":
            case "tec_automacao_linhas":
                pais.nivelIndustrial = Mathf.Clamp(pais.nivelIndustrial + 2, 0, 100);
                break;
            case "tec_propulsao_solida":
            case "tec_estabilizacao_lancamento":
            case "tec_controle_orbital":
            case "tec_radar_satelital":
            case "tec_navegacao_inercial":
            case "tec_guiagem_precisao":
                pais.nivelMilitar = Mathf.Clamp(pais.nivelMilitar + 2, 0, 100);
                pais.nivelIndustrial = Mathf.Clamp(pais.nivelIndustrial + 1, 0, 100);
                break;
            case "tec_reserva_materiais":
            case "tec_integracao_armazem":
            case "tec_miniaturizacao":
                pais.nivelIndustrial = Mathf.Clamp(pais.nivelIndustrial + 2, 0, 100);
                pais.nivelEconomico = Mathf.Clamp(pais.nivelEconomico + 1, 0, 100);
                break;
            case "tec_ciencia_aplicada":
                pais.nivelEconomico = Mathf.Clamp(pais.nivelEconomico + 2, 0, 100);
                pais.nivelIndustrial = Mathf.Clamp(pais.nivelIndustrial + 1, 0, 100);
                pais.nivelDiplomatico = Mathf.Clamp(pais.nivelDiplomatico + 1, 0, 100);
                break;
            case "tec_autorizacao_nuclear":
            case "tec_blindagem_radiologica":
                pais.estabilidade = Mathf.Clamp(pais.estabilidade + 1.5f, 0f, 100f);
                pais.nivelIndustrial = Mathf.Clamp(pais.nivelIndustrial + 1, 0, 100);
                break;
        }
    }

    private bool ConsumirEnergia(int teamId, int custoEnergia)
    {
        if (custoEnergia <= 0)
        {
            return true;
        }

        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null || pais.energia < custoEnergia)
        {
            return false;
        }

        pais.energia -= custoEnergia;
        if (teamId == teamJogador && GerenciadorRecursos.Instancia != null)
        {
            GerenciadorRecursos.Instancia.energia = Mathf.Max(0, GerenciadorRecursos.Instancia.energia - custoEnergia);
            GerenciadorRecursos.Instancia.NotificarAtualizacao();
        }

        return true;
    }

    private bool DependenciasAtendidas(DadosPaisGoverno pais, string dependencias)
    {
        if (pais == null || string.IsNullOrWhiteSpace(dependencias))
        {
            return true;
        }

        string[] partes = dependencias.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < partes.Length; i++)
        {
            string dependencia = partes[i].Trim();
            if (dependencia.StartsWith("lab_", StringComparison.OrdinalIgnoreCase))
            {
                LaboratorioNacionalEstado laboratorio = pais.laboratorios.FirstOrDefault(l => l != null && l.id == dependencia);
                if (laboratorio == null || laboratorio.nivelAtual <= 0)
                {
                    return false;
                }
            }
            else if (!PesquisaConcluida(pais, dependencia))
            {
                return false;
            }
        }

        return true;
    }

    private bool PesquisaConcluida(DadosPaisGoverno pais, string pesquisaId)
    {
        if (pais == null || string.IsNullOrWhiteSpace(pesquisaId))
        {
            return true;
        }

        PesquisaNacionalEstado pesquisa = pais.pesquisas.FirstOrDefault(p => p != null && p.id == pesquisaId);
        return pesquisa != null && pesquisa.concluida;
    }

    public bool TemPesquisaConcluida(int teamId, string pesquisaId)
    {
        return PesquisaConcluida(ObterPais(teamId), pesquisaId);
    }

    private int DiaAtual()
    {
        return GerenciadorTempo.Instancia != null ? Mathf.Max(1, GerenciadorTempo.Instancia.totalDias) : 1;
    }

    private static PesquisaNacionalEstado CriarPesquisa(string id, string nome, string categoria, string descricao, string requisitos, string desbloqueia, string dependencias, int custoSaldo, int custoEnergia, int duracaoDias)
    {
        return new PesquisaNacionalEstado
        {
            id = id,
            nome = nome,
            categoria = categoria,
            descricao = descricao,
            requisitosVisuais = requisitos,
            desbloqueia = desbloqueia,
            dependencias = dependencias,
            custoSaldo = custoSaldo,
            custoEnergia = custoEnergia,
            duracaoDias = Mathf.Max(1, duracaoDias)
        };
    }

    private static TecnologiaNacionalEstado CriarTecnologia(string id, string nome, string categoria, string descricao, string efeito, string dependencias, int custoSaldo, int custoEnergia, int duracaoDias, int nivelMaximo)
    {
        return new TecnologiaNacionalEstado
        {
            id = id,
            nome = nome,
            categoria = categoria,
            descricao = descricao,
            efeito = efeito,
            dependencias = dependencias,
            custoSaldo = custoSaldo,
            custoEnergia = custoEnergia,
            duracaoDias = Mathf.Max(1, duracaoDias),
            nivelMaximo = Mathf.Max(1, nivelMaximo)
        };
    }

    private static LaboratorioNacionalEstado CriarLaboratorio(string id, string nome, string especializacao, string descricao, string dependencias, int custoSaldo, int custoEnergia, int duracaoDias, int nivelMaximo)
    {
        return new LaboratorioNacionalEstado
        {
            id = id,
            nome = nome,
            especializacao = especializacao,
            descricao = descricao,
            dependencias = dependencias,
            custoSaldo = custoSaldo,
            custoEnergia = custoEnergia,
            duracaoDias = Mathf.Max(1, duracaoDias),
            nivelMaximo = Mathf.Max(1, nivelMaximo)
        };
    }

    private void GarantirPesquisaCatalogo(DadosPaisGoverno pais, PesquisaNacionalEstado pesquisa)
    {
        if (pais == null || pesquisa == null || pais.pesquisas.Any(p => p != null && string.Equals(p.id, pesquisa.id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        pais.pesquisas.Add(pesquisa);
    }

    private void GarantirTecnologiaCatalogo(DadosPaisGoverno pais, TecnologiaNacionalEstado tecnologia)
    {
        if (pais == null || tecnologia == null || pais.tecnologias.Any(t => t != null && string.Equals(t.id, tecnologia.id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        pais.tecnologias.Add(tecnologia);
    }

    private void GarantirLaboratorioCatalogo(DadosPaisGoverno pais, LaboratorioNacionalEstado laboratorio)
    {
        if (pais == null || laboratorio == null || pais.laboratorios.Any(l => l != null && string.Equals(l.id, laboratorio.id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        pais.laboratorios.Add(laboratorio);
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

    /// <summary>Registra a agressao e grava o agressor como inimigo do alvo.</summary>
    public void RegistrarAgressao(int vitimaTeamId, int agressorTeamId)
    {
        if (vitimaTeamId <= 0 || agressorTeamId <= 0 || vitimaTeamId == agressorTeamId) return;

        DadosPaisGoverno vitima = ObterPais(vitimaTeamId);
        DadosPaisGoverno agressor = ObterPais(agressorTeamId);
        if (vitima != null)
        {
            vitima.emGuerra = true;
            vitima.rivalTeamId = agressorTeamId;
            vitima.estabilidade = Mathf.Clamp(vitima.estabilidade - 6f, 0f, 100f);
        }
        if (agressor != null)
        {
            agressor.emGuerra = true;
            agressor.rivalTeamId = vitimaTeamId;
        }

        RelacaoPaisGoverno relacao = ObterRelacao(vitimaTeamId, agressorTeamId);
        bool novaGuerra = relacao != null && !relacao.guerraDeclarada;
        if (relacao != null)
        {
            relacao.guerraDeclarada = true;
            relacao.valor = Mathf.Clamp(relacao.valor - 35, -100, 100);
            relacao.posturaAParaB = PosturaRelacaoPais.Inimigo;
            relacao.posturaBParaA = PosturaRelacaoPais.Inimigo;
        }
        if (novaGuerra)
        {
            RegistrarNoticia("Agressao confirmada: " + NomePais(vitimaTeamId) + " identificou " + NomePais(agressorTeamId) + " como inimigo.");
            UnityEngine.Debug.Log("[Diplomacia] Agressor identificado como inimigo: vitima=" + vitimaTeamId + " agressor=" + agressorTeamId);
        }
        SistemaMercadoGlobal.Instancia?.SimularMercado();
        ProcessarEconomia();
        OnGovernoAtualizado?.Invoke();
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
            case RecursoMercado.Energia: return "energia";
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
        string recursoId = IntegracaoMercadoIndustrial.IdInternoDoMercado(recurso);
        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial != null && IndustriaIds.EhIndustrial(recursoId))
        {
            if (delta >= 0)
            {
                industrial.AdicionarEstoque(teamId, recurso, delta);
            }
            else
            {
                industrial.RemoverEstoque(teamId, recurso, -delta);
            }

            OnGovernoAtualizado?.Invoke();
            return;
        }

        DadosPaisGoverno pais = ObterPais(teamId);
        if (pais == null) return;
        GerenciadorRecursos gr = teamId == teamJogador ? GerenciadorRecursos.Instancia : null;
        GerenciadorArmazens armazens = teamId == teamJogador ? GerenciadorArmazens.Instancia : null;

        switch (recurso)
        {
            case RecursoMercado.Comida:
                pais.comida = Mathf.Max(0, pais.comida + delta);
                if (gr != null)
                {
                    gr.comida = pais.comida;
                }
                if (armazens != null && armazens.armazemRecursos != null)
                {
                    int alvo = Mathf.Max(0, pais.comida);
                    armazens.armazemRecursos.alimentos = Mathf.Clamp(alvo, 0, armazens.armazemRecursos.alimentosMaximo);
                    armazens.NotificarAtualizacaoManual();
                }
                break;
            case RecursoMercado.Petroleo:
                pais.petroleo = Mathf.Max(0, pais.petroleo + delta);
                if (gr != null)
                {
                    gr.petroleo = pais.petroleo;
                }
                if (armazens != null && armazens.armazemRecursos != null)
                {
                    int alvo = Mathf.Max(0, pais.petroleo);
                    armazens.armazemRecursos.petroleo = Mathf.Clamp(alvo, 0, armazens.armazemRecursos.petroleoMaximo);
                    armazens.NotificarAtualizacaoManual();
                }
                break;
            case RecursoMercado.Energia:
                pais.energia = Mathf.Max(0, pais.energia + delta);
                if (gr != null)
                {
                    gr.energia = pais.energia;
                }
                if (armazens != null && armazens.armazemRecursos != null)
                {
                    int alvo = Mathf.Max(0, pais.energia);
                    armazens.armazemRecursos.energia = Mathf.Clamp(alvo, 0, armazens.armazemRecursos.energiaMaximo);
                    armazens.NotificarAtualizacaoManual();
                }
                break;
            case RecursoMercado.Aco:
                pais.aco = Mathf.Max(0, pais.aco + delta);
                if (gr != null)
                {
                    gr.aco = pais.aco;
                }
                if (armazens != null && armazens.armazemRecursos != null)
                {
                    int alvo = Mathf.Max(0, pais.aco);
                    armazens.armazemRecursos.metal = Mathf.Clamp(alvo, 0, armazens.armazemRecursos.metalMaximo);
                    armazens.NotificarAtualizacaoManual();
                }
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

    private void AlterarEstoque(int teamId, string recursoId, int delta)
    {
        if (string.IsNullOrWhiteSpace(recursoId) || delta == 0)
        {
            return;
        }

        RecursoMercado recursoLegado;
        if (TentarConverterRecursoMercado(recursoId, out recursoLegado))
        {
            AlterarEstoque(teamId, recursoLegado, delta);
            return;
        }

        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null)
        {
            return;
        }

        if (delta >= 0)
        {
            industrial.Armazem.Adicionar(teamId.ToString(), recursoId, delta);
        }
        else
        {
            industrial.Armazem.TentarConsumir(teamId.ToString(), recursoId, -delta);
        }

        OnGovernoAtualizado?.Invoke();
    }

    private static bool TentarConverterRecursoMercado(string recursoId, out RecursoMercado recurso)
    {
        recurso = RecursoMercado.Nenhum;
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return false;
        }

        switch (recursoId.Trim().ToLowerInvariant())
        {
            case "comida":
                recurso = RecursoMercado.Comida;
                return true;
            case "petroleo":
                recurso = RecursoMercado.Petroleo;
                return true;
            case "energia":
                recurso = RecursoMercado.Energia;
                return true;
            case "aco":
            case "aco_estrutural":
                recurso = RecursoMercado.Aco;
                return true;
            case "armamentos":
                recurso = RecursoMercado.Armamentos;
                return true;
            case "uranio":
            case "uranio_bruto":
                recurso = RecursoMercado.Uranio;
                return true;
            case "minerio_ferro":
                recurso = RecursoMercado.MinerioFerro;
                return true;
            case "minerio_cobre":
                recurso = RecursoMercado.MinerioCobre;
                return true;
            case "bauxita":
                recurso = RecursoMercado.Bauxita;
                return true;
            case "minerio_titanio":
                recurso = RecursoMercado.MinerioTitanio;
                return true;
            case "cobre_eletrolitico":
                recurso = RecursoMercado.CobreEletrolitico;
                return true;
            case "duraluminio":
                recurso = RecursoMercado.Duraluminio;
                return true;
            case "liga_titanio":
                recurso = RecursoMercado.LigaTitanio;
                return true;
            case "componentes_eletronicos":
                recurso = RecursoMercado.ComponentesEletronicos;
                return true;
            case "uranio_enriquecido":
                recurso = RecursoMercado.UranioEnriquecido;
                return true;
            default:
                return false;
        }
    }

    private void AplicarEconomiaImoveis(DadosPaisGoverno pais, DadosEconomiaPais economia)
    {
        // Capacidade vem de moradia real. A prefeitura so oferece uma lotacao
        // administrativa temporaria; ela nao representa uma cidade residencial.
        int capacidadeResidencial = Mathf.Max(0, economia.moradiaTotal);
        int capacidadeAdministrativa = PossuiSedeAdministrativa(pais.teamId) ? 200 : 0;
        pais.populacaoMaxima = Mathf.Max(capacidadeResidencial, capacidadeAdministrativa);

        int limiteTotal = Mathf.Max(0, pais.populacaoMaxima);
        pais.populacao = Mathf.Clamp(pais.populacao, 0, limiteTotal);
        int naoCivil = Mathf.Max(0, pais.populacaoMilitarAtiva + pais.reservistas + pais.alistaveis);
        pais.populacaoCivil = Mathf.Clamp(pais.populacaoCivil, 0, Mathf.Max(0, limiteTotal - naoCivil));
        pais.populacao = pais.populacaoCivil + naoCivil;

        int populacaoParaIndicadores = Mathf.Max(1, pais.populacaoCivil);
        pais.emprego = Mathf.Clamp01(economia.empregosOcupados / (float)populacaoParaIndicadores) * 100f;
        pais.moradia = capacidadeResidencial <= 0 ? 0f
            : Mathf.Clamp01(capacidadeResidencial / (float)populacaoParaIndicadores) * 100f;
        pais.pressaoHabitacional = capacidadeResidencial <= 0
            ? (pais.populacaoCivil > 0 ? 2f : 0f)
            : (float)pais.populacaoCivil / capacidadeResidencial;
        pais.qualidadeVida = economia.qualidadeVida;

#if false
        if (false && economia.estruturasContadas > 0)
        {
            int baseMax = 1;
            if (pais.teamId == teamJogador && GerenciadorRecursos.Instancia != null)
            {
                baseMax = GerenciadorRecursos.Instancia.populacaoMaxima;
            }
            pais.populacaoMaxima = Mathf.Max(baseMax, economia.moradiaTotal);
            pais.populacao = Mathf.Clamp(economia.populacaoTotal > 0 ? economia.populacaoTotal : pais.populacao, 0, pais.populacaoMaxima);
            pais.emprego = economia.populacaoTotal <= 0 ? 100f : Mathf.Clamp01(economia.empregosOcupados / (float)Mathf.Max(1, economia.populacaoTotal)) * 100f;
            pais.moradia = economia.populacaoTotal <= 0 ? 100f : Mathf.Clamp01(economia.moradiaTotal / (float)Mathf.Max(1, economia.populacaoTotal)) * 100f;
            pais.qualidadeVida = economia.qualidadeVida;
        }
        else if (false)
        {
            // Sem estruturas cadastradas: usa valores orgânicos derivados da felicidade e população
            // NÃO força emprego/moradia artificialmente a 100%, isso engana o sistema de felicidade
            if (pais.teamId == teamJogador && GerenciadorRecursos.Instancia != null)
            {
                pais.populacao = GerenciadorRecursos.Instancia.populacaoAtual;
                pais.populacaoMaxima = GerenciadorRecursos.Instancia.populacaoMaxima;
            }
            // Emprego deriva da felicidade e estabilidade do país (se nenhuma estrutura existe, é informal)
            pais.emprego = Mathf.Clamp(50f + (pais.felicidade - 50f) * 0.4f + (pais.estabilidade - 50f) * 0.2f, 20f, 85f);
            // Moradia: sem construção formal, moradia é limitada (improvisada)
            pais.moradia = Mathf.Clamp(40f + (pais.felicidade - 50f) * 0.3f, 20f, 70f);
            // Qualidade de vida cresce com felicidade e emprego orgânicos
            pais.qualidadeVida = Mathf.Clamp(
                35f + (pais.emprego - 50f) * 0.25f + (pais.moradia - 50f) * 0.20f + (pais.felicidade - 50f) * 0.20f,
                15f, 70f);
        }
#endif
        pais.producao = Mathf.Clamp(35f
            + economia.industriaProduzida * 4f
            + economia.petroleoProduzido * 2f
            + economia.comidaProduzida * 1.2f
            + pais.nivelEconomico * 0.22f
            + pais.nivelIndustrial * 0.32f
            - economia.deficitEnergia * 4f, 5f, 100f);
        pais.impostoMoradia = NormalizarImposto(pais.impostoMoradia);
        pais.impostoIndustria = NormalizarImposto(pais.impostoIndustria);
        pais.impostoComercio = NormalizarImposto(pais.impostoComercio);

        float multiplicadorEconomico = 1f + pais.nivelEconomico * 0.0020f;
        float multiplicadorIndustrial = 1f + pais.nivelIndustrial * 0.0025f;
        float multiplicadorDiplomatico = 1f + pais.nivelDiplomatico * 0.0018f;
        pais.receitaMoradia = economia.receitaMoradia * FatorImposto(pais.impostoMoradia) * multiplicadorEconomico;
        pais.receitaIndustria = economia.receitaIndustria * FatorImposto(pais.impostoIndustria) * multiplicadorIndustrial;
        pais.receitaComercio = economia.receitaComercio * FatorImposto(pais.impostoComercio) * multiplicadorDiplomatico;
        pais.receitaEnergia = economia.receitaEnergia * (1f + (pais.nivelEconomico + pais.nivelIndustrial) * 0.0010f);

        // Partida nova: sem estruturas, tropas ou compromissos ativos não há
        // fluxo econômico que justifique retirar dinheiro do governo. Assim
        // que a nação construir, mobilizar ou assumir um compromisso, o bloco
        // normal abaixo volta a calcular tributos e despesas.
        if (!PossuiAtividadeOrcamentaria(pais, economia) && capacidadeAdministrativa <= 0)
        {
            pais.receitaMoradia = 0f;
            pais.receitaIndustria = 0f;
            pais.receitaComercio = 0f;
            pais.receitaEnergia = 0f;
            pais.custoManutencao = 0f;
            pais.saldoOperacional = 0f;
            pais.rendaPorSegundo = 0f;
            pais.gastosPorSegundo = 0f;
            pais.energiaProduzida = economia.energiaProduzida;
            pais.energiaConsumida = economia.energiaConsumida;
            pais.deficitComida = economia.deficitComida;
            pais.deficitEnergia = economia.deficitEnergia;
            pais.deficitPetroleo = economia.deficitPetroleo;
            pais.estruturasSemEnergia = economia.estruturasSemEnergia;
            pais.exportacaoTotal = economia.exportacaoTotal;
            pais.importacaoTotal = economia.importacaoTotal;
            return;
        }

        float custoServicosBase = capacidadeAdministrativa > 0 ? 16f : 4f;
        float custoMoradia = 0f;
        float custoDefesa = pais.populacaoMilitarAtiva * 0.012f + pais.armamentos * 0.0015f;
        float custoInfraestrutura = 0f;
        // Publicar o detalhamento no mesmo snapshot que a UI consome. Antes
        // só o total era calculado, deixando as linhas Social/Infra/Militar
        // zeradas (e arredondadas para valores fictícios como $1).
        // Nao altere o snapshot aqui: esta funcao tambem e chamada pelo HUD.
        // Somar custos no snapshot a cada refresh fazia a despesa crescer sem limite.
        float custoNacional = economia.custoManutencao
            + custoServicosBase + custoMoradia + custoDefesa + custoInfraestrutura;
        if (pais.teamId == teamJogador)
        {
            PerfilDificuldadeJogo perfil = GameDifficultyManager.PerfilAtual;
            float multiplicadorReceita = perfil != null ? perfil.MultiplicadorReceita : 1f;
            float multiplicadorManutencao = perfil != null ? perfil.MultiplicadorManutencao : 1f;
            pais.receitaMoradia *= multiplicadorReceita;
            pais.receitaIndustria *= multiplicadorReceita;
            pais.receitaComercio *= multiplicadorReceita;
            pais.receitaEnergia *= multiplicadorReceita;
            custoNacional *= multiplicadorManutencao;
        }
        pais.custoManutencao = custoNacional;
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

    private static bool PossuiAtividadeOrcamentaria(DadosPaisGoverno pais, DadosEconomiaPais economia)
    {
        if (economia != null && economia.estruturasContadas > 0) return true;
        if (pais == null) return false;

        if (pais.populacaoMilitarAtiva > 0 || pais.reservistas > 0) return true;
        if (pais.divida > 0f) return true;
        if (pais.emprestimos != null && pais.emprestimos.Any(e => e != null && e.saldoDevedor > 0f)) return true;
        if (pais.pesquisas != null && pais.pesquisas.Any(p => p != null && p.emAndamento)) return true;
        if (pais.laboratorios != null && pais.laboratorios.Any(l => l != null && l.nivelAtual > 0)) return true;
        return pais.sateliteDefesa != null && pais.sateliteDefesa.desbloqueado;
    }

    private bool PossuiSedeAdministrativa(int teamId)
    {
#if UNITY_2023_1_OR_NEWER
        ComplexoGovernamental[] sedes = FindObjectsByType<ComplexoGovernamental>(FindObjectsSortMode.None);
#else
        ComplexoGovernamental[] sedes = FindObjectsByType<ComplexoGovernamental>(FindObjectsSortMode.None);
#endif
        for (int i = 0; i < sedes.Length; i++)
        {
            ComplexoGovernamental sede = sedes[i];
            if (sede == null || !sede.isActiveAndEnabled) continue;
            IdentidadeUnidade identidade = sede.GetComponentInParent<IdentidadeUnidade>();
            if (identidade != null && identidade.teamID == teamId) return true;
            if (identidade == null && teamId == teamJogador && sede.ehDoJogador) return true;
        }
        return false;
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
            pais.moedaLiderReferencia = MoedaMestreNome;
            // valorMoeda expressa diretamente quanto 1 unidade nacional vale em DH.
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
        IEnumerable<IdentidadeIA> identidades = IdentidadeIA.TodasIdentidades;
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

    private void GarantirSistemaIndustrial()
    {
        // Cria o SistemaIndustrial se ele ainda não existe na cena
        if (SistemaIndustrialNacional.Instancia == null)
        {
            GameObject go = new GameObject("SistemaIndustrialNacional_Runtime");
            go.AddComponent<SistemaIndustrialNacional>();
            DontDestroyOnLoad(go);
        }

        // Gera o perfil mineral de todos os países existentes (apenas se ainda não gerado)
        foreach (DadosPaisGoverno pais in paises)
        {
            if (pais != null)
                SistemaIndustrialNacional.Instancia.GarantirPerfil(pais.teamId);
        }
    }
}
