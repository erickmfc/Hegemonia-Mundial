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
        // Como fallback, se ficar parado mais de 3 min, tenta decolar sozinho.
        timerEstacionadoComercial += Time.deltaTime;
        if (timerEstacionadoComercial >= 180f && !decolagemSolicitada)
        {
            SolicitarDecolagem();
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
    /// Pushback realista: o avião comercial recua lentamente da vaga, girando o nariz
    /// na direção do primeiro waypoint da taxiway, antes de começar o táxi para frente.
    /// </summary>
    protected override IEnumerator ExecutarPushbackInicial()
    {
        // Só faz pushback se houver um ponto de destino definido
        List<Transform> taxiWps = ObterWaypointsTaxiEntrada();
        if (taxiWps == null || taxiWps.Count == 0) yield break;

        Transform primeiroPonto = taxiWps[0];
        if (primeiroPonto == null) yield break;

        // Duração e velocidade do pushback
        const float duracaoPushback = 6f;
        const float velocidadePushback = 3.5f;
        const float taxaGiroPushback = 18f; // graus/segundo — giro lento enquanto recua

        float tempo = 0f;
        while (tempo < duracaoPushback)
        {
            float dt = Time.deltaTime;
            tempo += dt;

            // Gira suavemente o NARIZ em direção ao primeiro waypoint (cauda afasta do gate)
            if (primeiroPonto != null)
            {
                Vector3 dirAlvo = primeiroPonto.position - transform.position;
                dirAlvo.y = 0f;
                if (dirAlvo.sqrMagnitude > 0.01f)
                {
                    Quaternion rotAlvo = Quaternion.LookRotation(dirAlvo.normalized);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, taxaGiroPushback * dt);
                }
            }

            // Move para trás (ré)
            transform.position -= transform.forward * (velocidadePushback * dt);

            // Mantém altura do solo
            if (modeloMecanicoVisual != null)
                modeloMecanicoVisual.localRotation = Quaternion.Lerp(modeloMecanicoVisual.localRotation, Quaternion.identity, dt * 3f);

            yield return null;
        }

        // Pausa breve antes do táxi para frente
        yield return new WaitForSeconds(1.5f);
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
            modeloMecanicoVisual.localRotation = Quaternion.Euler(empinadaPitch, 0f, giroLateralRoll);
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
