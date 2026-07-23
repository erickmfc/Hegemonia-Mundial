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

    [Header("=== INFRAESTRUTURA COMERCIAL ===")]
    public GerenciadorAeroportoComercial aeroportoOrigemComercial;
    public PistaComercial pistaDesignada;

    /// <summary>Voo agendado associado a este avião (atualiza o painel Z)</summary>
    [HideInInspector] public VooAgendado vooAssociado;

    private EstadoAviao estadoAnterior;

    protected override void Start()
    {
        base.Start();

        velocidadeSolo        = Mathf.Max(velocidadeSolo, 10f);
        asaBankingMaximo      = asaBankingComercial;
        arfagemPitchMaxima    = arfagemPitchComercial;

        if (somMotorComercial == null) somMotorComercial = GetComponent<AudioSource>();
        if (somMotorComercial == null) somMotorComercial = GetComponentInChildren<AudioSource>();
        if (somMotorComercial != null) { somMotorComercial.loop = true; somMotorComercial.spatialBlend = 1f; }

        CombustivelUnidade comb = CombustivelUnidade.Garantir(gameObject, true);
        if (comb != null)
        {
            comb.mostrarIndicadorMundo = false; // Não mostra UI para avião comercial
            comb.usaCombustivel = false; // Avião comercial não deve cair por falta de combustível
        }
    }

    protected override void Update()
    {
        GerenciarEstacionamento();
        GerenciarLiberacaoPista();

        base.Update();
        AtualizarEfeitosMotores();
        VerificarChegadaExterior();
    }

    private void GerenciarEstacionamento()
    {
        if (estadoAtual != EstadoAviao.ProntoNoPatio)
        {
            timerEstacionadoComercial = 0f;
            decolagemSolicitada = false;
            return;
        }

        timerEstacionadoComercial += Time.deltaTime;

        if (timerEstacionadoComercial >= 180f && !decolagemSolicitada)
        {
            if (aeroportoOrigemComercial != null)
            {
                SolicitarDecolagem();
            }
            else if (pistaDesignada != null)
            {
                SolicitarDecolagem();
            }
            else if (timerEstacionadoComercial >= 240f)
            {
                decolagemSolicitada = true;
                DefinirDestinoComercial();
                IniciarMissaoCompleta(alvoGPSVoo);
            }
        }
    }

    public void SolicitarDecolagem()
    {
        if (decolagemSolicitada || estadoAtual != EstadoAviao.ProntoNoPatio) return;
        decolagemSolicitada = true;

        if (aeroportoOrigemComercial != null)
        {
            PistaComercial pista = aeroportoOrigemComercial.SolicitarPistaParaDecolagem(this);
            if (pista != null)
                AtribuirPistaEDecolar(pista);
            else
                aeroportoOrigemComercial.EntrarNaFilaDecolagem(this);
        }
        else
        {
            DefinirDestinoComercial();
            IniciarMissaoCompleta(alvoGPSVoo);
        }
    }

    public void AtribuirPistaEDecolar(PistaComercial pista)
    {
        pistaDesignada = pista;
        DefinirDestinoComercial();

        if (vooAssociado != null) vooAssociado.status = StatusVoo.EmVoo;

        if (transform.parent != null && transform.parent == vagaRetorno)
            transform.SetParent(null, true);

        Debug.Log($"[Comercial] {nomeCompanhia} decolando pela {pista.nomePista} → {nomeDestinoIA}.");
        IniciarMissaoCompleta(alvoGPSVoo);
    }

    public void AtribuirPistaEPousar(PistaComercial pista)
    {
        pistaDesignada = pista;
        Debug.Log($"[Comercial] {nomeCompanhia} pista atribuída para pouso: {pista.nomePista}.");
    }

    private void GerenciarLiberacaoPista()
    {
        if (estadoAnterior == estadoAtual) return;

        if (estadoAnterior == EstadoAviao.Decolando && estadoAtual == EstadoAviao.EmMissao)
        {
            if (aeroportoOrigemComercial != null) aeroportoOrigemComercial.LiberarPista(pistaDesignada, this);
            pistaDesignada = null;
        }
        else if (estadoAnterior == EstadoAviao.RetornandoPraVaga && estadoAtual == EstadoAviao.ProntoNoPatio)
        {
            if (aeroportoOrigemComercial != null) aeroportoOrigemComercial.LiberarPista(pistaDesignada, this);
            pistaDesignada = null;

            if (aeroportoOrigemComercial != null) aeroportoOrigemComercial.AviaoChegou(this);

            destinoCalculado   = false;
            nomeDestinoIA      = "";
            decolagemSolicitada= false;
            timerEstacionadoComercial = 0f;
            if (vooAssociado != null) vooAssociado.status = StatusVoo.Pousou;

            CombustivelUnidade comb = GetComponent<CombustivelUnidade>();
            if (comb != null) comb.PreencherSemCusto();
        }

        estadoAnterior = estadoAtual;
    }

    protected override List<Transform> ObterWaypointsDecolagem()
    {
        if (pistaDesignada != null && pistaDesignada.waypointsDecolagem.Count > 0)
            return pistaDesignada.waypointsDecolagem;
        return base.ObterWaypointsDecolagem();
    }

    protected override List<Transform> ObterWaypointsDecida()
    {
        if (pistaDesignada == null && aeroportoOrigemComercial != null)
        {
            pistaDesignada = aeroportoOrigemComercial.SolicitarPistaParaPouso(this);
            if (pistaDesignada == null)
            {
                aeroportoOrigemComercial.EntrarNaFilaPouso(this);
                pistaDesignada = aeroportoOrigemComercial.pista1;
            }
        }
        if (pistaDesignada != null && pistaDesignada.waypointsDecida.Count > 0)
            return pistaDesignada.waypointsDecida;
        return base.ObterWaypointsDecida();
    }

    protected override List<Transform> ObterWaypointsTaxi()
    {
        if (pistaDesignada != null && pistaDesignada.waypointsTaxiSaida.Count > 0)
            return pistaDesignada.waypointsTaxiSaida;
        return base.ObterWaypointsTaxi();
    }

    protected override Transform ObterWpPreparacao()
    {
        if (pistaDesignada != null && pistaDesignada.waypointsTaxiEntrada.Count > 0)
            return pistaDesignada.waypointsTaxiEntrada[0];
        return base.ObterWpPreparacao();
    }

    protected override Transform ObterWpPronto()
    {
        if (pistaDesignada != null && pistaDesignada.waypointsTaxiEntrada.Count > 1)
            return pistaDesignada.waypointsTaxiEntrada[pistaDesignada.waypointsTaxiEntrada.Count - 1];
        return base.ObterWpPronto();
    }

    protected override List<Transform> ObterWaypointsTaxiEntrada()
    {
        if (pistaDesignada != null && pistaDesignada.waypointsTaxiEntrada.Count > 0)
            return new List<Transform>(pistaDesignada.waypointsTaxiEntrada);
        return base.ObterWaypointsTaxiEntrada();
    }

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

    // =========================================================================
    // CORREÇÃO DO EFEITO CARANGUEJO - MOVIMENTO EXCLUSIVO COMERCIAL
    // Estas funções garantem que o nariz do avião sempre aponte para a direção
    // do movimento, ignorando a rotação defeituosa dos waypoints.
    // =========================================================================
    
    protected IEnumerator MoverInterpoladoComercial(Vector3 destino, float vel, bool noChao)
    {
        // Se estiver no chão, compensa a altura para não enterrar o pneu
        if (noChao) 
        {
            destino.y += ObterAlturaEstacionamento();
        }

        while (true)
        {
            Vector3 direcaoParaRotacao = destino - transform.position;
            
            // Se estiver taxiando no chão, força a rotação a ficar reta (Pitch 0) para o bico não empinar
            if (noChao) direcaoParaRotacao.y = 0f; 

            float dist = Vector3.Distance(transform.position, destino);
            if (dist <= 0.5f) break; // Chegou no alvo

            // Força o avião a olhar para a linha do movimento
            if (direcaoParaRotacao.sqrMagnitude > 0.01f)
            {
                Quaternion rotacaoAlvo = Quaternion.LookRotation(direcaoParaRotacao.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, 8f * Time.deltaTime);
            }

            // Move fisicamente para o ponto
            transform.position = Vector3.MoveTowards(transform.position, destino, vel * Time.deltaTime);
            yield return null;
        }
    }

    protected IEnumerator SeguirCaminhoComercial(List<Transform> waypoints, float velInicial, float velFinal, bool noChao)
    {
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            
            // Calcula aceleração (Ex: aumenta a vel. do inicio ao fim da pista)
            float t = waypoints.Count > 1 ? (float)i / (waypoints.Count - 1) : 1f;
            float velAtual = Mathf.Lerp(velInicial, velFinal, t);
            
            // Usa a nova rotina anti-caranguejo
            yield return StartCoroutine(MoverInterpoladoComercial(waypoints[i].position, velAtual, noChao));
        }
    }

    private List<Transform> ObterCorredorAproximacaoComercial()
    {
        List<Transform> corredor = new List<Transform>();
        Transform grupo = aeroportoOrigemComercial != null ? aeroportoOrigemComercial.decida : null;
        if (grupo == null) return corredor;

        // O grupo "chegando" é a aproximação aérea longa. Ele não é vaga e
        // nunca deve ser substituído pelo ponto de estacionamento.
        foreach (Transform ponto in grupo)
        {
            if (ponto != null) corredor.Add(ponto);
        }
        return corredor;
    }

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
            
            var wpsTaxiEntrada = ObterWaypointsTaxiEntrada();
            Transform primeiroPonto = null;
            if (wpsTaxiEntrada != null && wpsTaxiEntrada.Count > 0) primeiroPonto = wpsTaxiEntrada[0];

            yield return StartCoroutine(ExecutarPushbackInicial(primeiroPonto));
            yield return new WaitForSeconds(3f);

            while (pistaDesignada == null && aeroportoOrigemComercial != null)
            {
                pistaDesignada = aeroportoOrigemComercial.SolicitarPistaParaDecolagem(this);
                if (pistaDesignada == null)
                {
                    aeroportoOrigemComercial.EntrarNaFilaDecolagem(this);
                    yield return new WaitUntil(() => pistaDesignada != null);
                }
            }

            wpsTaxiEntrada = ObterWaypointsTaxiEntrada();

            if (wpsTaxiEntrada != null && wpsTaxiEntrada.Count > 0)
            {
                List<Transform> taxiCaminho = new List<Transform>();
                for (int i = 0; i < wpsTaxiEntrada.Count - 1; i++) 
                {
                    if (wpsTaxiEntrada[i] != null) taxiCaminho.Add(wpsTaxiEntrada[i]);
                }
                
                // USANDO O SISTEMA CORRIGIDO AQUI ->
                if (taxiCaminho.Count > 0)
                    yield return StartCoroutine(SeguirCaminhoComercial(taxiCaminho, velocidadeSolo * 0.5f, velocidadeSolo, true));
                
                Transform ultimoTaxi = wpsTaxiEntrada[wpsTaxiEntrada.Count - 1];
                if (ultimoTaxi != null)
                    yield return StartCoroutine(MoverInterpoladoComercial(ultimoTaxi.position, velocidadeSolo, true));
            }

            yield return new WaitForSeconds(3f);

            var wpDecolagem = ObterWaypointsDecolagem();
            if (wpDecolagem != null && wpDecolagem.Count > 0)
            {
                // Entrando na pista
                yield return StartCoroutine(MoverInterpoladoComercial(wpDecolagem[0].position, velocidadeSolo, true));

                yield return new WaitForSeconds(3f);

                List<Transform> pistaCaminho = new List<Transform>();
                for (int i = 1; i < wpDecolagem.Count; i++)
                {
                    if (wpDecolagem[i] != null) pistaCaminho.Add(wpDecolagem[i]);
                }
                
                // Acelerando corrigido ->
                if (pistaCaminho.Count > 0)
                    yield return StartCoroutine(SeguirCaminhoComercial(pistaCaminho, velocidadeSolo, velocidadeMaximaVoo, true));
            }

            transform.SetParent(null, true);
        }

        // ==========================================
        // FASE 2: VOO (Em Missão)
        // ==========================================
        // Para decolagens normais E para chegadas externas: voa até o alvo definido.
        // - Saídas: alvoGPSVoo é o destino (exterior).
        // - Chegadas: alvoGPSVoo é o ponto de entrada da pista (definido no SpawnarAviaoDeChegada).
        {
            estaEmModoVooFisico = true;
            estadoAtual = EstadoAviao.EmMissao;
            if (alvoGPSVoo.y < altitudeCruzeiroComercial) alvoGPSVoo.y = altitudeCruzeiroComercial;
            StartCoroutine(RecolherRodas(1f));

            float distAproximacao = Mathf.Max(20f, margemChegadaMissao);
            distAproximacao *= distAproximacao;

            while (true)
            {
                Vector3 diff = new Vector3(transform.position.x - alvoGPSVoo.x, 0, transform.position.z - alvoGPSVoo.z);
                if (diff.sqrMagnitude <= distAproximacao) break;
                if (ordemParaRetorno) break;
                yield return null;
            }

            // Para voos de SAÍDA que não são chegadas: terminam aqui (destruídos ou voam para destino).
            // Para voos de CHEGADA: continuam para a fase de pouso abaixo.
            if (vooAssociado != null && !vooAssociado.ehChegada)
            {
                // Avião de saída chegou ao destino exterior — destruir
                Destroy(gameObject);
                yield break;
            }
        }

        // ==========================================
        // FASE 3: POUSO E RETORNO À VAGA
        // ==========================================
        ordemParaRetorno = false;
        estadoAtual = EstadoAviao.Pousando;

        // Solicita pista de pouso (se ainda não tiver sido designada)
        while (pistaDesignada == null && aeroportoOrigemComercial != null)
        {
            pistaDesignada = aeroportoOrigemComercial.SolicitarPistaParaPouso(this);
            if (pistaDesignada == null)
            {
                aeroportoOrigemComercial.EntrarNaFilaPouso(this);
                // Mantém o avião voando devagar enquanto aguarda pista
                transform.position += transform.forward * (velocidadeSolo * Time.deltaTime);
                yield return null;
            }
        }

        // Obtém os dois trechos separados: corredor aéreo e pista. A pista é
        // percorrida do limiar até o ponto de toque; só depois começa o táxi.
        var wpDescidaPista = ObterWaypointsDecida();
        List<Transform> corredorAproximacao = ObterCorredorAproximacaoComercial();

        if (wpDescidaPista != null && wpDescidaPista.Count >= 2)
        {
            Transform primeiroCorredor = corredorAproximacao.Count > 0
                ? corredorAproximacao[0]
                : wpDescidaPista[wpDescidaPista.Count - 1];
            Vector3 wpEntrada = primeiroCorredor.position;
            wpEntrada.y = Mathf.Max(wpEntrada.y, 50f);
            alvoGPSVoo = wpEntrada;

            while (true)
            {
                Vector3 diff = new Vector3(transform.position.x - wpEntrada.x, 0, transform.position.z - wpEntrada.z);
                if (diff.sqrMagnitude <= 90000f) break; // 300m
                yield return null;
            }

            // A partir daqui o avião está alinhado com o eixo da pista. A
            // aproximação longa é aérea; o limiar e os demais pontos são solo.
            AbaixarRodas();
            estaEmModoVooFisico = false;

            if (corredorAproximacao.Count > 0)
                yield return StartCoroutine(SeguirCaminhoComercial(corredorAproximacao, velocidadeSolo * 2.4f, velocidadeSolo * 2f, false));

            // Último trecho do ar até o limiar da pista.
            yield return StartCoroutine(MoverInterpoladoComercial(wpDescidaPista[0].position, velocidadeSolo * 2f, false));

            // Toque confirmado: agora o avião só pode se deslocar no plano da
            // pista, com trem de pouso apoiado, até liberar a faixa.
            List<Transform> caminhoSolo = new List<Transform>();
            for (int i = 1; i < wpDescidaPista.Count; i++)
                if (wpDescidaPista[i] != null) caminhoSolo.Add(wpDescidaPista[i]);
            if (caminhoSolo.Count > 0)
                yield return StartCoroutine(SeguirCaminhoComercial(caminhoSolo, velocidadeSolo * 1.35f, velocidadeSolo, true));
        }
        else
        {
            Debug.LogWarning($"[Comercial] {name} sem rota de pouso válida; pouso cancelado para evitar queda em vaga.");
            AbaixarRodas();
            estaEmModoVooFisico = false;
            Destroy(gameObject);
            yield break;
        }

        estadoAtual = EstadoAviao.RetornandoPraVaga;
        transform.SetParent(aeroportoOrigem.transform, true);

        var wpsTaxiSaida = ObterWaypointsTaxi();
        
        yield return new WaitForSeconds(3f);

        if (wpsTaxiSaida != null && wpsTaxiSaida.Count > 0)
        {
            // Taxi até o portão
            yield return StartCoroutine(SeguirCaminhoComercial(wpsTaxiSaida, velocidadeSolo, velocidadeSolo, true));
        }

        if (vagaRetorno == null) vagaRetorno = aeroportoOrigem.ObterPrimeiraVagaLivre();
        
        if (vagaRetorno != null)
        {
            yield return new WaitForSeconds(3f);

            // Alinhamento final com a vaga para estacionar bonito de ré
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

            yield return StartCoroutine(MoverInterpoladoComercial(vagaRetorno.position, velocidadeSolo, true));

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

    private float timerVooExterior = 0f;

    private void VerificarChegadaExterior()
    {
        if (!destinoCalculado || nomeDestinoIA != "Exterior" || estadoAtual != EstadoAviao.EmMissao) return;
        
        timerVooExterior += Time.deltaTime;
        
        float dist = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(alvoGPSVoo.x, 0, alvoGPSVoo.z));
            
        if (timerVooExterior >= 120f && dist < 3000f) 
            Destroy(gameObject);
    }

    protected override void ManobraVooRealista(float multDano = 1f)
    {
        float dt = Time.deltaTime;
        Vector3 retaAteAlvo = alvoGPSVoo - transform.position;
        float anguloPressaoLateralY = 0f;

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

        if (novaPos.y < 25f)
        {
            novaPos.y = 25f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.Euler(0, transform.eulerAngles.y, 0), 20f * dt);
        }

        if (Mathf.Abs(novaPos.x) > 10000f || Mathf.Abs(novaPos.z) > 10000f)
        {
            Vector3 centro = new Vector3(0, novaPos.y, 0);
            alvoGPSVoo = centro;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation((centro - transform.position).normalized), 50f * dt);
            novaPos = transform.position + transform.forward * (velocidadeMaximaVoo * 0.5f * dt);
        }

        transform.position = novaPos;

        if (modeloMecanicoVisual != null)
        {
            float rollAlvo  = Mathf.Clamp(anguloPressaoLateralY * -1.8f, -asaBankingComercial, asaBankingComercial);
            float pitchAlvo = Mathf.Clamp(retaAteAlvo.y * -2.0f, -arfagemPitchComercial, arfagemPitchComercial);
            giroLateralRoll = Mathf.Lerp(giroLateralRoll, rollAlvo, dt * 2.5f);
            empinadaPitch   = Mathf.Lerp(empinadaPitch, pitchAlvo, dt * 2.5f);
            modeloMecanicoVisual.localRotation = Quaternion.Euler(empinadaPitch, giroLateralYInicial, giroLateralRoll);
        }
    }

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
