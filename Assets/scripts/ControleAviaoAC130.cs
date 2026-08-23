using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controle específico do AC-130.
///
/// Sem alvo, o avião usa a patrulha retangular padrão do ControleAviao.
/// Quando encontra um inimigo, troca para uma órbita lateral contínua em
/// torno dele. Com o sentido padrão, o centro fica no lado esquerdo da
/// aeronave, onde as torretas conseguem trabalhar sem cruzar o alvo.
/// </summary>
[AddComponentMenu("Hegemonia/Aeronaves/Controle AC-130")]
public sealed class ControleAviaoAC130 : ControleAviao
{
    [Header("=== ORBITA LATERAL DO AC-130 ===")]
    [Tooltip("Permite a órbita lateral durante o engajamento. Sem alvo, usa a patrulha retangular padrão.")]
    public bool usarOrbitaLateralContinua = true;

    [Tooltip("Raio horizontal da órbita de ataque, em metros.")]
    public float raioOrbitaAtaque = 360f;

    [Tooltip("Velocidade angular da órbita, em radianos por segundo.")]
    [Range(0.05f, 0.8f)] public float velocidadeAngularAtaque = 0.24f;

    [Tooltip("Mantém o centro da órbita no lado esquerdo do avião, posição das torretas do AC-130.")]
    public bool manterAlvoNoLadoEsquerdo = true;

    [Header("=== ESTABILIDADE DE TIRO ===")]
    [Tooltip("Antecipa suavemente o deslocamento horizontal do alvo para o avião não ficar atrasado na órbita.")]
    [Range(0f, 2f)] public float antecipacaoMovimentoAlvo = 0.65f;

    [Tooltip("Suavização da posição prevista do alvo. Valores maiores acompanham melhor alvos móveis, sem gerar zigue-zague.")]
    [Range(0.5f, 8f)] public float suavizacaoCentroAtaque = 3.5f;

    [Tooltip("Força da correção radial quando o avião sai do raio ideal da órbita.")]
    [Range(0.5f, 3f)] public float pesoCorrecaoOrbitaAtaque = 1.8f;

    [Tooltip("Altura mínima acima de um alvo aéreo. Para alvos terrestres, altitudeVoo continua sendo a referência.")]
    public float alturaMinimaSobreAlvo = 80f;

    [Header("=== DEFESA DA AREA ===")]
    [Tooltip("Raio da área defendida ao redor do ponto ordenado.")]
    public float raioAreaDefendida = 650f;

    [Tooltip("Distância máxima para procurar inimigos que invadiram a área de defesa.")]
    public float alcanceDeteccaoAlvos = 1400f;

    [Tooltip("Intervalo entre buscas de novos inimigos. O alvo atual continua sendo acompanhado entre as buscas.")]
    public float intervaloBuscaAlvos = 0.35f;

    [Tooltip("Entrega o alvo encontrado diretamente às torretas ControleTorreta do prefab.")]
    public bool fixarTorretasNoAlvo = true;

    [Header("=== ARMAMENTO E PRECISÃO DO AC-130 ===")]
    [Tooltip("Alcance efetivo das armas do AC-130. As torretas são ampliadas até este valor no início da missão.")]
    public float alcanceArmasAC130 = 2400f;

    [Tooltip("Velocidade mínima de acompanhamento das torretas do AC-130.")]
    public float velocidadeMiraTorretas = 90f;

    [Tooltip("Permite que o projétil use a predição do alvo enquanto a animação visual da torreta termina de acompanhar.")]
    public bool usarPredicaoDiretaNoDisparo = true;

    private readonly Collider[] bufferBuscaAlvos = new Collider[256];
    private readonly List<Transform> bufferInimigosRegistrados = new List<Transform>(64);
    private ControleTorreta[] torretas;
    private Transform alvoDefesa;
    private Transform ultimoAlvoAplicado;
    private Vector3 ultimoCentroOrbitado;
    private Vector3 velocidadeAlvoFiltrada;
    private Vector3 ultimaPosicaoAlvoRastreada;
    private Vector3 centroAtaqueSuavizado;
    private Transform alvoRastreado;
    private bool rastreamentoAtaqueInicializado;
    private float proximaBuscaAlvos;
    private bool orbitaInicializada;
    private EstadoAviao estadoAnterior;
    private int meuTime;

    protected override void Start()
    {
        base.Start();

        torretas = GetComponentsInChildren<ControleTorreta>(true);
        ConfigurarTorretasAC130();
        IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>();
        meuTime = identidade != null ? identidade.teamID : 0;
        estadoAnterior = estadoAtual;
        proximaBuscaAlvos = Time.time;

        // O AC-130 usa a orientação authored do modelo e não precisa de um
        // sentido aleatório: isso garante que o alvo permaneça à esquerda.
        sentidoOrbita = manterAlvoNoLadoEsquerdo ? 1 : -1;
    }

    private void ConfigurarTorretasAC130()
    {
        if (torretas == null) return;

        float alcance = Mathf.Max(400f, alcanceArmasAC130);
        float giro = Mathf.Max(30f, velocidadeMiraTorretas);

        for (int i = 0; i < torretas.Length; i++)
        {
            ControleTorreta torreta = torretas[i];
            if (torreta == null) continue;

            // Só reforça o perfil do AC-130; as demais torretas do jogo não
            // recebem assistência nem alteração de alcance.
            torreta.alcance = Mathf.Max(torreta.alcance, alcance);
            torreta.velocidadeGiro = Mathf.Max(torreta.velocidadeGiro, giro);
            torreta.dispararMesmoDesalinhado = true;
            torreta.direcionarProjetilParaPredicao = usarPredicaoDiretaNoDisparo;
            torreta.modoPassivo = false;
        }
    }

    protected override void ManobraVooRealista(float multiplicadorDanos = 1f)
    {
        if (estadoAnterior != estadoAtual)
        {
            orbitaInicializada = false;
            alvoDefesa = null;
            ultimoAlvoAplicado = null;
            ResetarRastreamentoAtaque();
            estadoAnterior = estadoAtual;
        }

        if (!usarOrbitaLateralContinua
            || estadoAtual != EstadoAviao.EmMissao
            || ordemParaRetorno)
        {
            base.ManobraVooRealista(multiplicadorDanos);
            return;
        }

        AtualizarAlvoDeDefesa();

        // Sem um inimigo válido, o AC-130 usa exatamente o circuito de
        // patrulha do ControleAviao (o retângulo/quadrado da área). A órbita
        // lateral só começa depois que um alvo foi encontrado.
        if (alvoDefesa == null)
        {
            orbitaInicializada = false;
            ResetarRastreamentoAtaque();
            base.ManobraVooRealista(multiplicadorDanos);
            return;
        }

        AtualizarPrevisaoAlvo(Time.deltaTime);

        Vector3 centro = ObterCentroDaOrbita();
        float raio = Mathf.Max(80f, raioOrbitaAtaque);
        float distanciaCentro = DistanciaHorizontal(transform.position, centro);

        // Durante a aproximação inicial, mantém o voo legado até chegar à
        // zona de órbita. Depois disso, o AC-130 nunca mais aponta para o
        // centro: segue tangente e deixa o centro no lado da arma.
        if (!orbitaInicializada && distanciaCentro > Mathf.Max(margemChegadaMissao, raio * 0.75f))
        {
            centro.y = ObterAltitudeOperacao(centro);
            alvoGPSVoo = centro;
            base.ManobraVooRealista(multiplicadorDanos);
            return;
        }

        if (!orbitaInicializada || DistanciaHorizontal(ultimoCentroOrbitado, centro) > Mathf.Max(90f, raio * 0.35f))
        {
            InicializarOrbita(centro);
        }

        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        sentidoOrbita = manterAlvoNoLadoEsquerdo ? 1 : -1;
        anguloOrbitaAtual += Mathf.Max(0.05f, velocidadeAngularAtaque) * sentidoOrbita * dt;

        float seno = Mathf.Sin(anguloOrbitaAtual);
        float cosseno = Mathf.Cos(anguloOrbitaAtual);
        Vector3 radialIdeal = new Vector3(cosseno, 0f, seno) * raio;
        Vector3 posicaoIdeal = centro + radialIdeal;
        posicaoIdeal.y = ObterAltitudeOperacao(centro);

        Vector3 erroAnel = posicaoIdeal - transform.position;
        erroAnel.y = 0f;
        float erroNormalizado = Mathf.Clamp01(erroAnel.magnitude / raio);

        // A tangente mantém o voo circular; a correção radial impede que o
        // avião abra ou feche a órbita quando o alvo se desloca.
        Vector3 tangente = new Vector3(-seno * sentidoOrbita, 0f, cosseno * sentidoOrbita);
        Vector3 direcaoPlano = tangente;
        if (erroAnel.sqrMagnitude > 0.01f)
        {
            float pesoCorrecao = Mathf.Clamp01(
                erroNormalizado * Mathf.Max(0.5f, pesoCorrecaoOrbitaAtaque));
            direcaoPlano = (tangente * (1f - pesoCorrecao) + erroAnel.normalized * pesoCorrecao).normalized;
        }

        Vector3 direcaoDesejada = direcaoPlano;
        float erroAltitude = posicaoIdeal.y - transform.position.y;
        direcaoDesejada += Vector3.up * Mathf.Clamp(erroAltitude / Mathf.Max(80f, raio), -0.45f, 0.45f);
        if (direcaoDesejada.sqrMagnitude < 0.001f)
        {
            direcaoDesejada = transform.forward;
        }
        direcaoDesejada.Normalize();

        Quaternion rotacaoDesejada = Quaternion.LookRotation(direcaoDesejada, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            rotacaoDesejada,
            Mathf.Max(25f, taxaDeGiroLeme * 1.45f) * dt);

        float velocidadeOrbita = raio * Mathf.Max(0.05f, velocidadeAngularAtaque);
        float velocidadeCruzeiro = Mathf.Clamp(
            velocidadeOrbita,
            Mathf.Max(35f, velocidadeMaximaVoo * 0.62f),
            Mathf.Max(velocidadeMaximaVoo, 60f));
        float velocidadeFinal = velocidadeCruzeiro * multiplicadorVelocidadeTurbo * multiplicadorDanos;
        float taxaVelocidade = velocidadeFinal >= velocidadeVooAtual ? aceleracaoVoo : desaceleracaoVoo;
        velocidadeVooAtual = Mathf.MoveTowards(velocidadeVooAtual, velocidadeFinal, Mathf.Max(1f, taxaVelocidade) * dt);

        Vector3 novaPosicao = transform.position + transform.forward * (velocidadeVooAtual * dt);
        novaPosicao.y = Mathf.Max(15f, novaPosicao.y);
        if (Mathf.Abs(novaPosicao.x) > 10000f || Mathf.Abs(novaPosicao.z) > 10000f)
        {
            novaPosicao = Vector3.Lerp(novaPosicao, posicaoIdeal, 0.15f);
        }
        transform.position = novaPosicao;

        AtualizarVisualDeVoo(direcaoDesejada, dt);
    }

    private void AtualizarAlvoDeDefesa()
    {
        IdentidadeUnidade identidadeAtual = GetComponent<IdentidadeUnidade>();
        if (identidadeAtual != null && identidadeAtual.teamID > 0)
        {
            // O aeroporto copia a equipe para a aeronave logo depois do
            // Instantiate. Releia aqui para não perder a primeira busca caso
            // o Start do avião tenha ocorrido antes dessa cópia.
            meuTime = identidadeAtual.teamID;
        }

        if (!fixarTorretasNoAlvo && alvoDefesa == null && Time.time < proximaBuscaAlvos)
        {
            return;
        }

        if (alvoDefesa != null && !AlvoAindaValido(alvoDefesa))
        {
            AplicarAlvoNasTorretas(null);
            alvoDefesa = null;
            ultimoAlvoAplicado = null;
        }

        if (Time.time >= proximaBuscaAlvos)
        {
            proximaBuscaAlvos = Time.time + Mathf.Max(0.1f, intervaloBuscaAlvos);
            if (alvoDefesa == null)
            {
                alvoDefesa = ProcurarInimigoNaArea();
            }
        }

        if (fixarTorretasNoAlvo && alvoDefesa != ultimoAlvoAplicado)
        {
            AplicarAlvoNasTorretas(alvoDefesa);
            ultimoAlvoAplicado = alvoDefesa;
        }
    }

    private void AplicarAlvoNasTorretas(Transform alvo)
    {
        if (torretas == null) return;

        for (int i = 0; i < torretas.Length; i++)
        {
            ControleTorreta torreta = torretas[i];
            if (torreta == null) continue;
            torreta.alvoPrioritario = alvo;
            torreta.DefinirAlvo(alvo);
        }
    }

    private Transform ProcurarInimigoNaArea()
    {
        Vector3 centro = ObterCentroDaPatrulhaSeguro();
        float alcance = ResolverAlcanceBusca();
        Transform melhor = null;
        float melhorPontuacao = float.PositiveInfinity;
        float raioArea = Mathf.Max(1f, raioAreaDefendida);

        // Primeiro usa o índice tático, que encontra navios mesmo quando o
        // collider está em um filho ou foi configurado em uma camada incomum.
        // A origem principal é o avião: o alvo pode passar perto dele sem
        // estar perto do centro geométrico da patrulha.
        bufferInimigosRegistrados.Clear();
        InfraPerformanceGameplay.ObterInimigosProximos(
            transform.position,
            alcance,
            meuTime,
            bufferInimigosRegistrados,
            64);
        for (int i = 0; i < bufferInimigosRegistrados.Count; i++)
        {
            AvaliarCandidatoDefesa(
                bufferInimigosRegistrados[i],
                centro,
                raioArea,
                ref melhor,
                ref melhorPontuacao);
        }

        // Mantém um fallback físico para unidades que ainda não entraram no
        // índice tático, sem depender exclusivamente de um collider na raiz.
        AvaliarColisoresDefesa(
            transform.position,
            alcance,
            centro,
            raioArea,
            ref melhor,
            ref melhorPontuacao);

        if (DistanciaHorizontal(transform.position, centro) > 1f)
        {
            AvaliarColisoresDefesa(
                centro,
                alcance,
                centro,
                raioArea,
                ref melhor,
                ref melhorPontuacao);
        }

        return melhor;
    }

    private void AvaliarColisoresDefesa(
        Vector3 origem,
        float alcance,
        Vector3 centro,
        float raioArea,
        ref Transform melhor,
        ref float melhorPontuacao)
    {
        int encontrados = Physics.OverlapSphereNonAlloc(
            origem,
            alcance,
            bufferBuscaAlvos,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < encontrados; i++)
        {
            Collider colisor = bufferBuscaAlvos[i];
            if (colisor == null) continue;

            IdentidadeUnidade identidade = colisor.GetComponentInParent<IdentidadeUnidade>();
            AvaliarCandidatoDefesa(
                identidade != null ? identidade.transform : colisor.transform,
                centro,
                raioArea,
                ref melhor,
                ref melhorPontuacao);
        }
    }

    private void AvaliarCandidatoDefesa(
        Transform candidato,
        Vector3 centro,
        float raioArea,
        ref Transform melhor,
        ref float melhorPontuacao)
    {
        if (candidato == null) return;

        IdentidadeUnidade identidade = ObterIdentidadeAlvo(candidato);

        if (candidato.root == transform.root) return;
        if (identidade == null && !EhAlvoCombatenteSemIdentidade(candidato)) return;
        if (identidade != null)
        {
            if (!identidade.gameObject.activeInHierarchy) return;
            if (meuTime > 0 && identidade.teamID > 0 && identidade.teamID == meuTime) return;
            if (identidade.teamID <= 0) return;
        }

        Transform alvo = identidade != null ? identidade.transform : candidato.root;
        if (!AlvoAindaValido(alvo)) return;

        Vector3 pontoAlvo = ObterPontoAlvoMaisProximo(alvo, transform.position);
        float distanciaDaArea = DistanciaHorizontal(pontoAlvo, centro);
        float distanciaDoAviao = DistanciaHorizontal(pontoAlvo, transform.position);
        float penalidadeForaDaArea = distanciaDaArea > raioArea
            ? (distanciaDaArea - raioArea) * 3f
            : 0f;
        float pontuacao = distanciaDaArea + penalidadeForaDaArea + distanciaDoAviao * 0.15f;

        if (pontuacao < melhorPontuacao)
        {
            melhorPontuacao = pontuacao;
            melhor = alvo;
        }
    }

    private bool AlvoAindaValido(Transform alvo)
    {
        if (alvo == null || !alvo.gameObject.activeInHierarchy) return false;
        if (alvo.root == transform.root) return false;
        if (!ControleSubmarino.PodeSerAlvoConvencional(alvo)) return false;

        IdentidadeUnidade identidade = ObterIdentidadeAlvo(alvo);
        if (identidade == null && !EhAlvoCombatenteSemIdentidade(alvo)) return false;
        if (identidade != null)
        {
            if (identidade.teamID <= 0) return false;
            if (meuTime > 0 && identidade.teamID == meuTime) return false;
        }

        SistemaDeDanos danos = alvo.GetComponentInParent<SistemaDeDanos>();
        if (danos != null && danos.vidaMaxima > 0f && danos.vidaAtual <= 0f) return false;

        float alcance = ResolverAlcanceEngajamento();
        Vector3 pontoAlvo = ObterPontoAlvoMaisProximo(alvo, transform.position);
        return DistanciaHorizontal(pontoAlvo, transform.position) <= alcance;
    }

    private float ResolverAlcanceBusca()
    {
        return Mathf.Max(
            Mathf.Max(400f, alcanceDeteccaoAlvos),
            Mathf.Max(400f, alcanceArmasAC130) + Mathf.Max(150f, raioOrbitaAtaque * 0.25f));
    }

    private float ResolverAlcanceEngajamento()
    {
        return ResolverAlcanceBusca() * 1.25f;
    }

    private static IdentidadeUnidade ObterIdentidadeAlvo(Transform alvo)
    {
        if (alvo == null) return null;

        IdentidadeUnidade identidade = alvo.GetComponentInParent<IdentidadeUnidade>();
        if (identidade == null)
        {
            identidade = alvo.GetComponentInChildren<IdentidadeUnidade>(true);
        }
        return identidade;
    }

    private static bool EhAlvoCombatenteSemIdentidade(Transform candidato)
    {
        if (candidato == null) return false;

        Transform raiz = candidato.root != null ? candidato.root : candidato;
        if (raiz.GetComponentInParent<ControleAviaoComercial>() != null
            || raiz.GetComponentInChildren<ControleAviaoComercial>(true) != null
            || raiz.GetComponentInParent<NavioCargaMercado>() != null
            || raiz.GetComponentInChildren<NavioCargaMercado>(true) != null)
        {
            return false;
        }

        if (raiz.GetComponentInParent<ControleNavioRealista>() != null
            || raiz.GetComponentInChildren<ControleNavioRealista>(true) != null
            || raiz.GetComponentInParent<ControleSubmarino>() != null
            || raiz.GetComponentInChildren<ControleSubmarino>(true) != null
            || raiz.GetComponentInParent<ControleAviao>() != null
            || raiz.GetComponentInChildren<ControleAviao>(true) != null
            || raiz.GetComponentInParent<Helicoptero>() != null
            || raiz.GetComponentInChildren<Helicoptero>(true) != null)
        {
            return true;
        }

        if (TagSafe.Matches(raiz, "Inimigo")
            || TagSafe.Matches(raiz, "Cartel")
            || TagSafe.Matches(raiz, "Navio")
            || TagSafe.Matches(raiz, "Aereo")
            || TagSafe.Matches(raiz, "Areo"))
        {
            return true;
        }

        string nome = raiz.name != null ? raiz.name : string.Empty;
        return nome.IndexOf("cartel", System.StringComparison.OrdinalIgnoreCase) >= 0
            || nome.IndexOf("navio", System.StringComparison.OrdinalIgnoreCase) >= 0
            || nome.IndexOf("barco", System.StringComparison.OrdinalIgnoreCase) >= 0
            || nome.IndexOf("aviao", System.StringComparison.OrdinalIgnoreCase) >= 0
            || nome.IndexOf("aereo", System.StringComparison.OrdinalIgnoreCase) >= 0
            || nome.IndexOf("drone", System.StringComparison.OrdinalIgnoreCase) >= 0
            || nome.IndexOf("heli", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Vector3 ObterPontoAlvoMaisProximo(Transform alvo, Vector3 origem)
    {
        if (alvo == null) return origem;

        Collider colisor = alvo.GetComponentInChildren<Collider>();
        return colisor != null ? colisor.ClosestPoint(origem) : alvo.position;
    }

    private Vector3 ObterCentroDaOrbita()
    {
        Vector3 centro = alvoDefesa != null
            ? (rastreamentoAtaqueInicializado ? centroAtaqueSuavizado : alvoDefesa.position)
            : ObterCentroDaPatrulhaSeguro();
        centro.y = 0f;
        return centro;
    }

    private void AtualizarPrevisaoAlvo(float dt)
    {
        if (alvoDefesa == null)
        {
            ResetarRastreamentoAtaque();
            return;
        }

        dt = Mathf.Clamp(dt, 0.001f, 0.1f);
        if (alvoRastreado != alvoDefesa)
        {
            alvoRastreado = alvoDefesa;
            velocidadeAlvoFiltrada = Vector3.zero;
            ultimaPosicaoAlvoRastreada = alvoDefesa.position;
            centroAtaqueSuavizado = alvoDefesa.position;
            rastreamentoAtaqueInicializado = true;
            return;
        }

        Vector3 velocidadeMedida = Vector3.zero;
        Rigidbody rbAlvo = alvoDefesa.GetComponentInParent<Rigidbody>();
        if (rbAlvo != null && !rbAlvo.isKinematic)
        {
            velocidadeMedida = rbAlvo.linearVelocity;
        }

        // Alguns navios e unidades movem o Transform diretamente, sem Rigidbody.
        // Nesse caso ainda obtemos uma velocidade útil pela diferença de posição.
        if (velocidadeMedida.sqrMagnitude < 0.25f)
        {
            velocidadeMedida = (alvoDefesa.position - ultimaPosicaoAlvoRastreada) / dt;
        }

        velocidadeMedida.y = 0f;
        velocidadeAlvoFiltrada = Vector3.Lerp(
            velocidadeAlvoFiltrada,
            velocidadeMedida,
            Mathf.Clamp01(dt * 6f));
        ultimaPosicaoAlvoRastreada = alvoDefesa.position;

        Vector3 centroPrevisto = alvoDefesa.position
            + Vector3.ProjectOnPlane(velocidadeAlvoFiltrada, Vector3.up)
            * Mathf.Clamp(antecipacaoMovimentoAlvo, 0f, 2f);
        centroPrevisto.y = alvoDefesa.position.y;

        float resposta = Mathf.Clamp(
            dt * Mathf.Max(0.5f, suavizacaoCentroAtaque),
            0.02f,
            1f);
        centroAtaqueSuavizado = Vector3.Lerp(centroAtaqueSuavizado, centroPrevisto, resposta);
    }

    private void ResetarRastreamentoAtaque()
    {
        alvoRastreado = null;
        velocidadeAlvoFiltrada = Vector3.zero;
        ultimaPosicaoAlvoRastreada = Vector3.zero;
        centroAtaqueSuavizado = Vector3.zero;
        rastreamentoAtaqueInicializado = false;
    }

    private Vector3 ObterCentroDaPatrulhaSeguro()
    {
        Vector3 centro = centroDaPatrulha;
        if (centro.sqrMagnitude < 0.01f && alvoEstrategico.sqrMagnitude > 0.01f)
        {
            centro = alvoEstrategico;
        }
        return centro;
    }

    private float ObterAltitudeOperacao(Vector3 centro)
    {
        float altitude = Mathf.Max(altitudeVoo, centro.y + 60f);
        if (alvoDefesa != null)
        {
            altitude = Mathf.Max(altitude, alvoDefesa.position.y + Mathf.Max(20f, alturaMinimaSobreAlvo));
        }
        return altitude;
    }

    private void InicializarOrbita(Vector3 centro)
    {
        Vector3 radialAtual = transform.position - centro;
        radialAtual.y = 0f;
        if (radialAtual.sqrMagnitude > 4f)
        {
            anguloOrbitaAtual = Mathf.Atan2(radialAtual.z, radialAtual.x);
        }
        else
        {
            Vector3 lado = transform.right;
            lado.y = 0f;
            if (lado.sqrMagnitude > 0.01f)
            {
                anguloOrbitaAtual = Mathf.Atan2(lado.normalized.z, lado.normalized.x);
            }
        }

        ultimoCentroOrbitado = centro;
        orbitaInicializada = true;
    }

    private void AtualizarVisualDeVoo(Vector3 direcaoDesejada, float dt)
    {
        if (modeloMecanicoVisual == null) return;

        float erroLateral = Vector3.SignedAngle(transform.forward, direcaoDesejada, Vector3.up);
        float inclinacaoAlvoZ = Mathf.Clamp(erroLateral * -1.5f, -asaBankingMaximo, asaBankingMaximo);
        giroLateralRoll = Mathf.Lerp(giroLateralRoll, inclinacaoAlvoZ, dt * 4f);
        modeloMecanicoVisual.localRotation = Quaternion.Euler(
            empinadaPitch,
            giroLateralYInicial,
            giroLateralRoll);
    }

    private static float DistanciaHorizontal(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
