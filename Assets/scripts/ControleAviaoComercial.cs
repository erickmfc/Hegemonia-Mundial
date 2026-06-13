using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controla um avião comercial autônomo: taxi, decolagem, voo, pouso, retorno à vaga.
/// Integra com GerenciadorAeroportoComercial para gerenciamento de pistas e voos agendados.
/// </summary>
public class ControleAviaoComercial : ControleAviao
{
    public enum TipoPropulsao { Turbina, Helice }

    [Header("=== PROPULSÃO COMERCIAL ===")]
    public TipoPropulsao tipoPropulsao = TipoPropulsao.Turbina;

    [Tooltip("Hélices que devem girar (se tipo for Helice)")]
    public List<Transform> helices = new List<Transform>();
    public Vector3 eixoGiroHelice = Vector3.forward;
    public float velocidadeMaxGiroHelice = 1500f;

    [Tooltip("Turbinas/Partículas de jato (se tipo for Turbina)")]
    public List<ParticleSystem> rastroTurbinas = new List<ParticleSystem>();
    public List<Light> luzesTurbina = new List<Light>();

    [Header("=== PARÂMETROS REALISTAS COMERCIAIS ===")]
    public float inerciaAceleracao = 1.5f;
    public float altitudeCruzeiroComercial = 90f;
    public float asaBankingComercial = 25f;
    public float arfagemPitchComercial = 12f;

    [Header("=== ÁUDIO COMERCIAL ===")]
    public AudioSource somMotorComercial;

    private float velocidadeGiroHeliceAtual = 0f;

    [Header("=== INFORMAÇÕES DE VOO ===")]
    public string nomeCompanhia = "Independente";
    public string nomeDestinoIA = "";
    public int passagensVendidas = 0;

    private bool destinoCalculado = false;
    private float timerEstacionadoComercial = 0f;
    private bool decolagemSolicitada = false;
    private bool aguardandoPista = false;

    [Header("=== INFRAESTRUTURA COMERCIAL ===")]
    public GerenciadorAeroportoComercial aeroportoOrigemComercial;
    public PistaComercial pistaDesignada;

    /// <summary>Voo agendado associado a este avião (atualiza o painel Z)</summary>
    [HideInInspector] public VooAgendado vooAssociado;

    private EstadoAviao estadoAnterior;

    // ── Startup ─────────────────────────────────────────────────────────
    protected override void Start()
    {
        base.Start();

        velocidadeSolo        = Mathf.Max(velocidadeSolo, 10f);
        asaBankingMaximo      = asaBankingComercial;
        arfagemPitchMaxima    = arfagemPitchComercial;

        if (somMotorComercial == null) somMotorComercial = GetComponent<AudioSource>();
        if (somMotorComercial == null) somMotorComercial = GetComponentInChildren<AudioSource>();
        if (somMotorComercial != null) { somMotorComercial.loop = true; somMotorComercial.spatialBlend = 1f; }

        CombustivelUnidade comb = CombustivelUnidade.Garantir(gameObject, false);
        if (comb != null)
        {
            comb.mostrarIndicadorMundo = false; // Não mostra UI para avião comercial
            comb.consumoPorSegundoMovendo *= 2.5f; // Gasta bastante para durar o voo todo
        }
    }

    // ── Update ─────────────────────────────────────────────────────────
    protected override void Update()
    {
        GerenciarEstacionamento();
        GerenciarLiberacaoPista();

        base.Update();
        AtualizarEfeitosMotores();
        VerificarChegadaExterior();
    }

    // ── Estacionamento e decolagem automática ──────────────────────────
    private void GerenciarEstacionamento()
    {
        if (estadoAtual != EstadoAviao.ProntoNoPatio)
        {
            timerEstacionadoComercial = 0f;
            decolagemSolicitada = false;
            aguardandoPista = false;
            return;
        }

        // Aguarda horário do voo (gerenciado pelo aeroporto)
        // O aeroporto chama SolicitarDecolagem() no momento certo.
        // Fallback: se ficar parado mais de 3 min, SOLICITA pista ao aeroporto antes de decolar.
        timerEstacionadoComercial += Time.deltaTime;

        if (timerEstacionadoComercial >= 180f && !decolagemSolicitada)
        {
            // Se tem aeroporto, solicita pista através do sistema formal.
            // Isso garante que o avião use a pista e não decole "do ar".
            if (aeroportoOrigemComercial != null)
            {
                SolicitarDecolagem();
            }
            else if (pistaDesignada != null)
            {
                // Sem aeroporto mas com pista atribuída manualmente: decola
                SolicitarDecolagem();
            }
            // Se não tem nem aeroporto nem pista: aguarda mais 60s e tenta de novo
            else if (timerEstacionadoComercial >= 240f)
            {
                // Último recurso: decola sem pista (comportamento antigo de fallback)
                decolagemSolicitada = true;
                DefinirDestinoComercial();
                IniciarMissaoCompleta(alvoGPSVoo);
            }
        }
    }

    /// <summary>Chamado pelo GerenciadorAeroportoComercial quando o horário do voo chegou.</summary>
    public void SolicitarDecolagem()
    {
        if (decolagemSolicitada || estadoAtual != EstadoAviao.ProntoNoPatio) return;
        decolagemSolicitada = true;

        if (aeroportoOrigemComercial != null)
        {
            // Tenta pegar pista diretamente; se não conseguir, entra na fila
            PistaComercial pista = aeroportoOrigemComercial.SolicitarPistaParaDecolagem(this);
            if (pista != null)
                AtribuirPistaEDecolar(pista);
            else
                aeroportoOrigemComercial.EntrarNaFilaDecolagem(this);
        }
        else
        {
            // Sem aeroporto comercial — tenta decolagem normal
            DefinirDestinoComercial();
            IniciarMissaoCompleta(alvoGPSVoo);
        }
    }

    /// <summary>Chamado quando o aeroporto atribui uma pista livre para decolagem.</summary>
    public void AtribuirPistaEDecolar(PistaComercial pista)
    {
        pistaDesignada = pista;
        DefinirDestinoComercial();

        if (vooAssociado != null) vooAssociado.status = StatusVoo.EmVoo;

        // Desparenta da vaga (hangar) para poder se mover livremente
        if (transform.parent != null && transform.parent == vagaRetorno)
            transform.SetParent(null, true);

        Debug.Log($"[Comercial] {nomeCompanhia} decolando pela {pista.nomePista} → {nomeDestinoIA}.");
        IniciarMissaoCompleta(alvoGPSVoo);
    }

    /// <summary>Chamado quando o aeroporto atribui uma pista para pouso.</summary>
    public void AtribuirPistaEPousar(PistaComercial pista)
    {
        pistaDesignada = pista;
        // O pouso já estava em andamento, só confirma a pista
        Debug.Log($"[Comercial] {nomeCompanhia} pista atribuída para pouso: {pista.nomePista}.");
    }

    // ── Liberação de pista ─────────────────────────────────────────────
    private void GerenciarLiberacaoPista()
    {
        if (estadoAnterior == estadoAtual) return;

        if (estadoAnterior == EstadoAviao.Decolando && estadoAtual == EstadoAviao.EmMissao)
        {
            // Libera pista após decolar completamente
            if (aeroportoOrigemComercial != null) aeroportoOrigemComercial.LiberarPista(pistaDesignada, this);
            pistaDesignada = null;
        }
        else if (estadoAnterior == EstadoAviao.RetornandoPraVaga && estadoAtual == EstadoAviao.ProntoNoPatio)
        {
            // Libera pista após concluir taxi de chegada
            if (aeroportoOrigemComercial != null) aeroportoOrigemComercial.LiberarPista(pistaDesignada, this);
            pistaDesignada = null;

            // Notifica aeroporto da chegada
            if (aeroportoOrigemComercial != null) aeroportoOrigemComercial.AviaoChegou(this);

            // Reseta para próximo voo
            destinoCalculado   = false;
            nomeDestinoIA      = "";
            decolagemSolicitada= false;
            timerEstacionadoComercial = 0f;
            if (vooAssociado != null) vooAssociado.status = StatusVoo.Pousou;

            // Recarrega combustível (não mostra interface)
            CombustivelUnidade comb = GetComponent<CombustivelUnidade>();
            if (comb != null) comb.PreencherSemCusto();
        }

        estadoAnterior = estadoAtual;
    }

    // ── Waypoints comerciais ───────────────────────────────────────────
    protected override List<Transform> ObterWaypointsDecolagem()
    {
        if (pistaDesignada != null && pistaDesignada.waypointsDecolagem.Count > 0)
            return pistaDesignada.waypointsDecolagem;
        return base.ObterWaypointsDecolagem();
    }

    protected override List<Transform> ObterWaypointsDecida()
    {
        // Solicita pista para pouso se ainda não tem
        if (pistaDesignada == null && aeroportoOrigemComercial != null)
        {
            pistaDesignada = aeroportoOrigemComercial.SolicitarPistaParaPouso(this);
            if (pistaDesignada == null)
            {
                // Entra na fila de pouso
                aeroportoOrigemComercial.EntrarNaFilaPouso(this);
                // Retorna pista 1 como fallback de emergência
                pistaDesignada = aeroportoOrigemComercial.pista1;
            }
        }
        if (pistaDesignada != null && pistaDesignada.waypointsDecida.Count > 0)
            return pistaDesignada.waypointsDecida;
        return base.ObterWaypointsDecida();
    }

    protected override List<Transform> ObterWaypointsTaxi()
    {
        // Retorna taxi de saída da pista (chegada → hangar)
        if (pistaDesignada != null && pistaDesignada.waypointsTaxiSaida.Count > 0)
            return pistaDesignada.waypointsTaxiSaida;
        return base.ObterWaypointsTaxi();
    }

    protected override Transform ObterWpPreparacao()
    {
        // Ponto inicial do taxi de partida (hangar → pista)
        if (pistaDesignada != null && pistaDesignada.waypointsTaxiEntrada.Count > 0)
            return pistaDesignada.waypointsTaxiEntrada[0];
        return base.ObterWpPreparacao();
    }

    protected override Transform ObterWpPronto()
    {
        // Último ponto do taxi de partida (alinhamento na cabeça de pista)
        if (pistaDesignada != null && pistaDesignada.waypointsTaxiEntrada.Count > 1)
            return pistaDesignada.waypointsTaxiEntrada[pistaDesignada.waypointsTaxiEntrada.Count - 1];
        return base.ObterWpPronto();
    }

    /// <summary>
    /// Retorna a lista COMPLETA de waypoints de táxi de entrada (hangar → cabeceira da pista).
    /// Sobrescreve o método da classe base para garantir que o avião percorra todos os pontos
    /// intermediários da taxiway, em vez de pular direto para o início da pista.
    /// </summary>
    protected override List<Transform> ObterWaypointsTaxiEntrada()
    {
        if (pistaDesignada != null && pistaDesignada.waypointsTaxiEntrada.Count > 0)
            return new List<Transform>(pistaDesignada.waypointsTaxiEntrada);
        return base.ObterWaypointsTaxiEntrada();
    }

    /// <summary>
    /// Pushback realista: o avião comercial recua da vaga por 3 segundos de ré para a popa
    /// antes de iniciar a contagem para o táxi.
    /// </summary>
    private IEnumerator ExecutarPushbackInicial(Transform primeiroPonto)
    {
        if (primeiroPonto == null) yield break;

        const float duracaoPushback = 3f;
        const float velocidadePushback = 3.5f;

        float tempo = 0f;
        while (tempo < duracaoPushback)
        {
            float dt = Time.deltaTime;
            tempo += dt;

            // Move para trás (ré) em linha reta para a popa
            transform.position -= transform.forward * (velocidadePushback * dt);

            if (modeloMecanicoVisual != null)
                modeloMecanicoVisual.localRotation = Quaternion.Lerp(modeloMecanicoVisual.localRotation, Quaternion.Euler(0f, giroLateralYInicial, 0f), dt * 3f);

            yield return null;
        }
    }

    public void IniciarSequenciaPousoComercial()
    {
        StartCoroutine(SequenciaDeVooEPouso());
    }

    // ── Ciclo de Voo e Pouso Comercial Completo ──
    protected override IEnumerator SequenciaDeVooEPouso()
    {
        if (aeroportoOrigem == null)
        {
            Destroy(gameObject);
            yield break;
        }

        ordemParaRetorno = false;
        vagaRetorno = transform.parent;

        if (aeroportoOrigemComercial == null)
            aeroportoOrigemComercial = aeroportoOrigem as GerenciadorAeroportoComercial;

        // ==========================================
        // FASE 1: DECOLAGEM E TÁXI
        // ==========================================
        if (!estaEmModoVooFisico)
        {
            estadoAtual = EstadoAviao.Decolando;
            
            // Avião pega seu caminho de saída (se o aeroporto designar uma rota base)
            var wpsTaxiEntrada = ObterWaypointsTaxiEntrada();
            
            Transform primeiroPonto = null;
            if (wpsTaxiEntrada != null && wpsTaxiEntrada.Count > 0) primeiroPonto = wpsTaxiEntrada[0];

            // 1. Pushback com 3 segundos de ré
            yield return StartCoroutine(ExecutarPushbackInicial(primeiroPonto));
            
            // 2. Pausa 3 segundos APÓS o pushback
            yield return new WaitForSeconds(3f);

            // Pede a pista ANTES de taxiar para poder usar o caminho de táxi específico daquela pista
            while (pistaDesignada == null && aeroportoOrigemComercial != null)
            {
                pistaDesignada = aeroportoOrigemComercial.SolicitarPistaParaDecolagem(this);
                if (pistaDesignada == null)
                {
                    aeroportoOrigemComercial.EntrarNaFilaDecolagem(this);
                    yield return new WaitUntil(() => pistaDesignada != null);
                }
            }

            // Atualiza o caminho de taxi com base na pista escolhida
            wpsTaxiEntrada = ObterWaypointsTaxiEntrada();

            // 3. Táxi até a cabeceira
            if (wpsTaxiEntrada != null && wpsTaxiEntrada.Count > 0)
            {
                List<Transform> taxiCaminho = new List<Transform>();
                for (int i = 0; i < wpsTaxiEntrada.Count - 1; i++) 
                {
                    if (wpsTaxiEntrada[i] != null) taxiCaminho.Add(wpsTaxiEntrada[i]);
                }
                
                if (taxiCaminho.Count > 0)
                    yield return StartCoroutine(SeguirCaminhoDeWaypoints(taxiCaminho, velocidadeSolo * 0.5f, velocidadeSolo, false, false));
                
                // 4. Vai para o último wp de táxi (hold-short) e PARA 3 SEGUNDOS
                Transform ultimoTaxi = wpsTaxiEntrada[wpsTaxiEntrada.Count - 1];
                if (ultimoTaxi != null)
                    yield return StartCoroutine(MoverInterpolado(Vector3.zero, velocidadeSolo, true, ultimoTaxi));
            }

            yield return new WaitForSeconds(3f);

            // 5. Entra na pista e se coloca na posição de voo
            var wpDecolagem = ObterWaypointsDecolagem();
            if (wpDecolagem != null && wpDecolagem.Count > 0)
            {
                // Entra na pista e alinha
                yield return StartCoroutine(MoverInterpolado(Vector3.zero, velocidadeSolo, true, wpDecolagem[0]));

                // 6. Pausa de 3 segundos na pista (Checkups)
                yield return new WaitForSeconds(3f);

                // 7. Aceleração de Decolagem
                List<Transform> pistaCaminho = new List<Transform>();
                for (int i = 1; i < wpDecolagem.Count; i++)
                {
                    if (wpDecolagem[i] != null) pistaCaminho.Add(wpDecolagem[i]);
                }
                if (pistaCaminho.Count > 0)
                    yield return StartCoroutine(SeguirCaminhoDeWaypoints(pistaCaminho, velocidadeSolo, velocidadeMaximaVoo, true, false));
            }

            // Libera o parente para voar fisicamente
            transform.SetParent(null, true);
        }

        // ==========================================
        // FASE 2: VOO (Em Missão)
        // ==========================================
        estaEmModoVooFisico = true;
        estadoAtual = EstadoAviao.EmMissao;
        if (alvoGPSVoo.y < 60f) alvoGPSVoo.y = 60f;
        StartCoroutine(RecolherRodas(1f));

        // Voa até o destino
        float distAproximacao = Mathf.Max(20f, margemChegadaMissao);
        distAproximacao *= distAproximacao;
        
        while (true)
        {
            Vector3 diff = new Vector3(transform.position.x - alvoGPSVoo.x, 0, transform.position.z - alvoGPSVoo.z);
            if (diff.sqrMagnitude <= distAproximacao) break;
            if (ordemParaRetorno) break;
            yield return null;
        }

        // ==========================================
        // FASE 3: POUSO E RETORNO À VAGA
        // ==========================================
        ordemParaRetorno = false;
        estadoAtual = EstadoAviao.Pousando;

        // Vai para a rota de descida comum do Aeroporto ("creaty")
        List<Transform> wpsCriaty = new List<Transform>();
        if (aeroportoOrigem.decida != null)
        {
            foreach (Transform t in aeroportoOrigem.decida) wpsCriaty.Add(t);
        }

        if (wpsCriaty.Count > 0)
        {
            alvoGPSVoo = wpsCriaty[0].position;
            if (alvoGPSVoo.y < 50f) alvoGPSVoo.y = 50f;

            while (true)
            {
                Vector3 diff = new Vector3(transform.position.x - alvoGPSVoo.x, 0, transform.position.z - alvoGPSVoo.z);
                if (diff.sqrMagnitude <= 90000f) break; // 300m
                yield return null;
            }

            AbaixarRodas();
            estaEmModoVooFisico = false;

            // Desce usando os waypoints do "descida" global
            yield return StartCoroutine(SeguirCaminhoDeWaypoints(wpsCriaty, velocidadeSolo * 2.4f, velocidadeSolo * 2.4f, false, true));
        }
        else
        {
            AbaixarRodas();
            estaEmModoVooFisico = false;
        }

        // Escolhe a pista de pouso (Após passar pelo último creaty)
        while (pistaDesignada == null && aeroportoOrigemComercial != null)
        {
            pistaDesignada = aeroportoOrigemComercial.SolicitarPistaParaPouso(this);
            if (pistaDesignada == null)
            {
                aeroportoOrigemComercial.EntrarNaFilaPouso(this);
                // Pairando enquanto aguarda pista
                transform.position += transform.forward * (velocidadeSolo * Time.deltaTime);
                yield return null; 
            }
        }

        // Segue os waypoints de descida DA PISTA escolhida (Pousa normalmente)
        var wpDescidaPista = ObterWaypointsDecida();
        if (wpDescidaPista != null && wpDescidaPista.Count > 0)
        {
            yield return StartCoroutine(SeguirCaminhoDeWaypoints(wpDescidaPista, velocidadeSolo * 2.4f, velocidadeSolo, false, true));
        }

        estadoAtual = EstadoAviao.RetornandoPraVaga;
        transform.SetParent(aeroportoOrigem.transform, true);

        // Táxi de saída (Ao contrário, indo para a vaga)
        var wpsTaxiSaida = ObterWaypointsTaxi();
        
        // 1. Pausa de 3 segundos no final da pista antes de taxiar
        yield return new WaitForSeconds(3f);

        if (wpsTaxiSaida != null && wpsTaxiSaida.Count > 0)
        {
            yield return StartCoroutine(SeguirCaminhoDeWaypoints(wpsTaxiSaida, velocidadeSolo, velocidadeSolo, false, true));
        }

        // Busca Vaga
        if (vagaRetorno == null) vagaRetorno = aeroportoOrigem.ObterPrimeiraVagaLivre();
        
        if (vagaRetorno != null)
        {
            // 2. Pausa de 3 segundos antes de estacionar
            yield return new WaitForSeconds(3f);

            Vector3 dirParaVaga = vagaRetorno.position - transform.position;
            dirParaVaga.y = 0f;
            if (dirParaVaga.sqrMagnitude > 0.1f)
            {
                Quaternion rotAlvo = Quaternion.LookRotation(dirParaVaga.normalized);
                while (Quaternion.Angle(transform.rotation, rotAlvo) > 5f)
                {
                    Vector3 dir2 = vagaRetorno.position - transform.position;
                    dir2.y = 0f;
                    if (dir2.sqrMagnitude > 0.01f) rotAlvo = Quaternion.LookRotation(dir2.normalized);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, 150f * Time.deltaTime);
                    yield return null;
                }
            }

            yield return StartCoroutine(MoverInterpolado(Vector3.zero, velocidadeSolo, true, vagaRetorno));

            transform.SetParent(vagaRetorno, true);
            float alt = ObterAlturaEstacionamento();
            
            while (Quaternion.Angle(transform.localRotation, Quaternion.identity) > 1f)
            {
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.identity, 30f * Time.deltaTime);
                yield return null;
            }
            transform.localRotation = Quaternion.identity;
            transform.localPosition = new Vector3(0f, alt, 0f);

            estaEmModoVooFisico = false;
            estadoAtual = EstadoAviao.ProntoNoPatio;

            CombustivelUnidade comb = GetComponent<CombustivelUnidade>();
            if (comb != null) comb.PreencherSemCusto();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ── Destino comercial ──────────────────────────────────────────────
    private void DefinirDestinoComercial()
    {
        if (destinoCalculado) return;
        destinoCalculado = true;

        if (GerenciadorDivisaoTerritorial.Instancia != null && aeroportoOrigem != null)
        {
            IdentidadeUnidade idAero = aeroportoOrigem.GetComponent<IdentidadeUnidade>();
            int meuTeam = idAero != null ? idAero.teamID : 1;

            var destinos = new List<CidadeEstado>();
            foreach (var cid in GerenciadorDivisaoTerritorial.Instancia.cidades)
                if (cid.temAeroporto && cid.teamID != meuTeam) destinos.Add(cid);

            if (destinos.Count > 0)
            {
                var cidDest = destinos[Random.Range(0, destinos.Count)];
                nomeDestinoIA = cidDest.nome;
                alvoGPSVoo    = (cidDest.marcador != null) ? cidDest.marcador.transform.position : Vector3.zero;
                alvoGPSVoo.y  = altitudeCruzeiroComercial;
                if (vooAssociado != null) vooAssociado.destino = nomeDestinoIA;
                return;
            }
        }

        nomeDestinoIA = "Exterior";
        Vector2 dir = Random.insideUnitCircle.normalized * 5000f;
        alvoGPSVoo    = new Vector3(transform.position.x + dir.x, altitudeCruzeiroComercial, transform.position.z + dir.y);
        if (vooAssociado != null) vooAssociado.destino = "Exterior";
    }

    // ── Chegada ao exterior (despawn) ──────────────────────────────────
    private float timerVooExterior = 0f;

    private void VerificarChegadaExterior()
    {
        if (!destinoCalculado || nomeDestinoIA != "Exterior" || estadoAtual != EstadoAviao.EmMissao) return;
        
        timerVooExterior += Time.deltaTime;
        
        float dist = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(alvoGPSVoo.x, 0, alvoGPSVoo.z));
            
        // Só some se estiver voando há mais de 2 minutos (120s) e já estiver relativamente longe
        if (timerVooExterior >= 120f && dist < 3000f) 
            Destroy(gameObject);
    }

    // ── Voo realista (sobrescreve o da base) ───────────────────────────
    protected override void ManobraVooRealista(float multDano = 1f)
    {
        float dt = Time.deltaTime;
        Vector3 retaAteAlvo = alvoGPSVoo - transform.position;
        float anguloPressaoLateralY = 0f;

        // Mantém altitude de cruzeiro
        if (estadoAtual == EstadoAviao.EmMissao && alvoGPSVoo.y < altitudeCruzeiroComercial)
        {
            alvoGPSVoo.y = altitudeCruzeiroComercial;
            retaAteAlvo  = alvoGPSVoo - transform.position;
        }

        if (retaAteAlvo.sqrMagnitude > 0.1f)
        {
            Vector3 upRef = Mathf.Abs(Vector3.Dot(retaAteAlvo.normalized, Vector3.up)) > 0.99f ? transform.up : Vector3.up;
            Quaternion rotAlvo = Quaternion.LookRotation(retaAteAlvo, upRef);
            anguloPressaoLateralY = Vector3.SignedAngle(transform.forward, retaAteAlvo, Vector3.up);
            float taxaGiro = taxaDeGiroLeme * 0.45f;
            transform.rotation = Quaternion.Slerp(transform.rotation, rotAlvo, (taxaGiro / 15f) * dt);
        }

        float mult = 1f;
        if (estadoAtual == EstadoAviao.EmMissao) mult = 0.85f;
        else if (estadoAtual == EstadoAviao.Pousando) mult = 0.4f;

        float velFinal = velocidadeMaximaVoo * multiplicadorVelocidadeTurbo * mult * multDano;
        Vector3 novaPos = transform.position + transform.forward * (velFinal * dt);

        // Piso de segurança
        if (novaPos.y < 25f)
        {
            novaPos.y = 25f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.Euler(0, transform.eulerAngles.y, 0), 20f * dt);
        }

        // Bordas do mapa
        if (Mathf.Abs(novaPos.x) > 10000f || Mathf.Abs(novaPos.z) > 10000f)
        {
            Vector3 centro = new Vector3(0, novaPos.y, 0);
            alvoGPSVoo = centro;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation((centro - transform.position).normalized), 50f * dt);
            novaPos = transform.position + transform.forward * (velocidadeMaximaVoo * 0.5f * dt);
        }

        transform.position = novaPos;

        // Banking / Pitch visuais
        if (modeloMecanicoVisual != null)
        {
            float rollAlvo  = Mathf.Clamp(anguloPressaoLateralY * -1.8f, -asaBankingComercial, asaBankingComercial);
            float pitchAlvo = Mathf.Clamp(retaAteAlvo.y * -2.0f, -arfagemPitchComercial, arfagemPitchComercial);
            giroLateralRoll = Mathf.Lerp(giroLateralRoll, rollAlvo, dt * 2.5f);
            empinadaPitch   = Mathf.Lerp(empinadaPitch, pitchAlvo, dt * 2.5f);
            modeloMecanicoVisual.localRotation = Quaternion.Euler(empinadaPitch, giroLateralYInicial, giroLateralRoll);
        }
    }

    // ── Efeitos de motor ───────────────────────────────────────────────
    private void AtualizarEfeitosMotores()
    {
        float dt = Time.deltaTime;
        bool motoresLigados = estadoAtual != EstadoAviao.ReservaHangar;

        if (motoresLigados)
        {
            float meta = (estadoAtual == EstadoAviao.Taxiando
                        || estadoAtual == EstadoAviao.ProntoNoPatio
                        || estadoAtual == EstadoAviao.RetornandoPraVaga)
                ? velocidadeMaxGiroHelice * 0.25f
                : velocidadeMaxGiroHelice;

            velocidadeGiroHeliceAtual = Mathf.Lerp(velocidadeGiroHeliceAtual, meta, dt * inerciaAceleracao);

            if (tipoPropulsao == TipoPropulsao.Helice)
                foreach (var h in helices) if (h) h.Rotate(eixoGiroHelice * (velocidadeGiroHeliceAtual * dt), Space.Self);

            bool turbina = tipoPropulsao == TipoPropulsao.Turbina;
            foreach (var ps in rastroTurbinas) if (ps) { if (turbina && !ps.isPlaying) ps.Play(); else if (!turbina && ps.isPlaying) ps.Stop(); }
            foreach (var l  in luzesTurbina)  if (l)  l.enabled = turbina;

            if (somMotorComercial != null)
            {
                if (!somMotorComercial.isPlaying) somMotorComercial.Play();
                float pct = velocidadeGiroHeliceAtual / velocidadeMaxGiroHelice;
                somMotorComercial.volume = Mathf.Lerp(0.3f, 1f, pct);
                somMotorComercial.pitch  = Mathf.Lerp(0.7f, 1.3f, pct);
            }
        }
        else
        {
            velocidadeGiroHeliceAtual = Mathf.Lerp(velocidadeGiroHeliceAtual, 0f, dt * inerciaAceleracao * 0.5f);

            if (tipoPropulsao == TipoPropulsao.Helice)
                foreach (var h in helices) if (h && velocidadeGiroHeliceAtual > 0.05f) h.Rotate(eixoGiroHelice * (velocidadeGiroHeliceAtual * dt), Space.Self);

            foreach (var ps in rastroTurbinas) if (ps && ps.isPlaying) ps.Stop();
            foreach (var l  in luzesTurbina)  if (l)  l.enabled = false;

            if (somMotorComercial != null && somMotorComercial.isPlaying)
            {
                somMotorComercial.volume = Mathf.Lerp(somMotorComercial.volume, 0f, dt * 2f);
                if (somMotorComercial.volume <= 0.02f) somMotorComercial.Stop();
            }
        }
    }
}
