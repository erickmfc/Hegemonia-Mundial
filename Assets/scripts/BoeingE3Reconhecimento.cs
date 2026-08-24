using System;
using System.Collections.Generic;
using UnityEngine;
using Hegemonia.RTS;

/// <summary>
/// Controle do Boeing E-3 Sentry/AWACS.
///
/// O movimento continua pertencendo ao ControleAviao e ao GerenciadorAeroporto.
/// Esta classe apenas especializa a aeronave para voo alto, curvas amplas e
/// reconhecimento/retransmissao de contatos.
/// </summary>
[DisallowMultipleComponent]
public sealed class BoeingE3Reconhecimento : ControleAviao
{
    public enum TipoContatoReconhecimento
    {
        UnidadeInimiga,
        SubmarinoNaSuperficie,
        UnidadeAliada,
        LancamentoDeMissil,
        InformacaoDeSensor
    }

    [Serializable]
    public sealed class ContatoReconhecimento
    {
        public int equipeObservadora;
        public int idAlvo;
        public int equipeAlvo;
        public string nomeAlvo;
        public TipoContatoReconhecimento tipo;
        public Vector3 ultimaPosicaoConhecida;
        public float ultimaAtualizacao;
        public bool inimigo;
        public string fonte;
        public string origemAeronave;
        public Vector3 origemAeronavePosicao;
        public float alcanceComunicacao;
    }

    /// <summary>
    /// Canal de retransmissao. Navios, quartel e IA podem assinar este evento
    /// sem assumir o controle de movimento do E-3.
    /// </summary>
    public static event Action<ContatoReconhecimento> OnContatoTransmitido;

    private static readonly Dictionary<long, ContatoReconhecimento> contatosAtivos =
        new Dictionary<long, ContatoReconhecimento>(256);

    [Header("=== RECONHECIMENTO E RETRANSMISSAO ===")]
    public bool reconhecimentoAtivo = true;
    [Min(500f)] public float alcanceReconhecimento = 4500f;
    [Min(500f)] public float alcanceSubmarinoNaSuperficie = 3200f;
    [Min(0.25f)] public float intervaloVarredura = 1.5f;
    [Min(1f)] public float memoriaContato = 30f;
    public bool receberInformacoesDeOutrosSensores = true;
    public bool retransmitirParaAliados = true;
    public bool alertarQuartelDoJogador = true;

    [Header("=== VOO AWACS ===")]
    [Min(120f)] public float altitudeCruzeiroE3 = 650f;
    [Min(60f)] public float velocidadeCruzeiroE3 = 145f;
    [Min(20f)] public float velocidadeMinimaEmCurva = 82f;
    [Min(1f)] public float taxaCurvaE3 = 10f;
    [Min(1f)] public float taxaSubidaDescidaE3 = 18f;
    [Min(5f)] public float inclinacaoMaximaE3 = 12f;

    private IdentidadeUnidade identidade;
    private RTSVisibilityService servicoVisibilidade;
    private float proximaVarredura;
    private int equipe;
    private Quaternion rotacaoVisualPrefab = Quaternion.identity;
    private bool visualPrefabInicializado;
    private readonly List<IdentidadeUnidade> bufferUnidades = new List<IdentidadeUnidade>(256);
    private readonly List<MissileThreatTracker> bufferMisseis = new List<MissileThreatTracker>(64);
    private readonly HashSet<int> misseisJaAlertados = new HashSet<int>();
    private readonly Dictionary<int, float> ultimoAlertaPorAlvo = new Dictionary<int, float>(128);

    public int EquipeReconhecimento => equipe;
    public int QuantidadeContatosAtivos => contatosAtivos.Count;

    protected override void Start()
    {
        if (modeloMecanicoVisual == null && transform.childCount > 0)
        {
            modeloMecanicoVisual = transform.GetChild(0);
        }

        // O GLB do E-3 tem o comprimento no eixo Y e a altura no eixo Z.
        // O prefab ja traz a conversao correta desses eixos para Unity. Nao
        // substituir essa rotacao por Euler(0, 180, 0), pois isso gira o
        // modelo de lado e faz o nariz apontar para o chao/pista.
        if (modeloMecanicoVisual != null)
        {
            rotacaoVisualPrefab = modeloMecanicoVisual.localRotation;
            visualPrefabInicializado = true;
        }

        altitudeCruzeiroE3 = Mathf.Max(120f, altitudeCruzeiroE3);
        velocidadeCruzeiroE3 = Mathf.Max(60f, velocidadeCruzeiroE3);
        velocidadeMinimaEmCurva = Mathf.Clamp(velocidadeMinimaEmCurva, 25f, velocidadeCruzeiroE3);
        altitudeVoo = Mathf.Max(altitudeVoo, altitudeCruzeiroE3);
        velocidadeMaximaVoo = velocidadeCruzeiroE3;
        taxaDeGiroLeme = taxaCurvaE3;
        aceleracaoVoo = Mathf.Min(Mathf.Max(aceleracaoVoo, 4f), 14f);
        desaceleracaoVoo = Mathf.Min(Mathf.Max(desaceleracaoVoo, 5f), 18f);

        identidade = GetComponent<IdentidadeUnidade>();
        if (identidade == null) identidade = GetComponentInParent<IdentidadeUnidade>();
        equipe = identidade != null ? identidade.teamID : 0;
        proximaVarredura = Time.unscaledTime + 0.5f;

        base.Start();
        AplicarVisualNivelado();
        AssinarServicoVisibilidade();
    }

    protected override void Update()
    {
        base.Update();
        // O ControleAviao base pode restaurar o visual usando apenas o eixo Y
        // durante taxi/pouso. O E-3 precisa manter a matriz de importacao
        // completa em qualquer estado.
        AplicarVisualNivelado();
        if (!reconhecimentoAtivo || !gameObject.activeInHierarchy)
        {
            return;
        }

        AssinarServicoVisibilidade();
        if (estadoAtual != EstadoAviao.EmMissao && estadoAtual != EstadoAviao.Decolando)
        {
            return;
        }

        if (Time.unscaledTime < proximaVarredura)
        {
            return;
        }

        proximaVarredura = Time.unscaledTime + Mathf.Max(0.25f, intervaloVarredura);
        ExecutarVarreduraReconhecimento();
        ExpirarContatosLocais();
    }

    /// <summary>
    /// Mantem a rota escolhida pelo jogador, mas garante que o E-3 opere em
    /// altitude de cruzeiro mesmo quando o clique foi feito no terreno.
    /// </summary>
    public override void RegistrarPatrulha(IList<Vector3> rota)
    {
        if (rota == null || rota.Count == 0)
        {
            base.RegistrarPatrulha(rota);
            return;
        }

        List<Vector3> rotaAlta = new List<Vector3>(rota.Count);
        for (int i = 0; i < rota.Count; i++)
        {
            Vector3 ponto = rota[i];
            ponto.y = Mathf.Max(ponto.y, altitudeCruzeiroE3);
            rotaAlta.Add(ponto);
        }

        base.RegistrarPatrulha(rotaAlta);
    }

    public override void AtualizarDestinoPatrulha(Vector3 destino)
    {
        destino.y = Mathf.Max(destino.y, altitudeCruzeiroE3);
        base.AtualizarDestinoPatrulha(destino);
    }

    /// <summary>
    /// Movimento nivelado e com limite angular. O E-3 nao vira para o ponto
    /// seguinte em um unico frame e nao perde altitude ao trocar de waypoint.
    /// </summary>
    protected override void ManobraVooRealista(float multiplicadorDano = 1f)
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 alvo = alvoGPSVoo;
        alvo.y = Mathf.Max(alvo.y, altitudeCruzeiroE3);
        Vector3 vetorHorizontal = alvo - transform.position;
        vetorHorizontal.y = 0f;
        if (vetorHorizontal.sqrMagnitude < 0.25f)
        {
            vetorHorizontal = transform.forward;
            vetorHorizontal.y = 0f;
        }

        vetorHorizontal.Normalize();
        Quaternion rotacaoDesejada = Quaternion.LookRotation(vetorHorizontal, Vector3.up);
        float erroRumo = Vector3.Angle(transform.forward, vetorHorizontal);
        float taxaCurva = Mathf.Lerp(taxaCurvaE3, taxaCurvaE3 * 1.35f, Mathf.Clamp01(erroRumo / 120f));

        // O E-3 nao usa o pitch/roll da raiz para navegar. O deslocamento
        // vertical e controlado separadamente, portanto a raiz deve ficar
        // nivelada e alterar somente o rumo.
        float rumoAtual = transform.eulerAngles.y;
        float rumoAlvo = rotacaoDesejada.eulerAngles.y;
        float novoRumo = Mathf.MoveTowardsAngle(rumoAtual, rumoAlvo, taxaCurva * dt);
        transform.rotation = Quaternion.Euler(0f, novoRumo, 0f);

        float fatorCurva = Mathf.InverseLerp(100f, 20f, erroRumo);
        float velocidadeAlvo = Mathf.Lerp(velocidadeMinimaEmCurva, velocidadeCruzeiroE3, fatorCurva);
        velocidadeAlvo *= Mathf.Clamp(multiplicadorDano, 0.45f, 1f);
        velocidadeVooAtual = Mathf.MoveTowards(velocidadeVooAtual, velocidadeAlvo, Mathf.Max(2f, aceleracaoVoo) * dt);

        Vector3 novaPosicao = transform.position + transform.forward * (velocidadeVooAtual * dt);
        float alturaAlvo = Mathf.Max(alvo.y, altitudeCruzeiroE3);
        novaPosicao.y = Mathf.MoveTowards(transform.position.y, alturaAlvo, taxaSubidaDescidaE3 * dt);
        transform.position = novaPosicao;

        AplicarVisualNivelado();
    }

    protected override Quaternion ResolverRotacaoVisualNeutra()
    {
        return rotacaoVisualPrefab;
    }

    private void AplicarVisualNivelado()
    {
        if (!visualPrefabInicializado || modeloMecanicoVisual == null)
        {
            return;
        }

        modeloMecanicoVisual.localRotation = rotacaoVisualPrefab;
    }

    private void LateUpdate()
    {
        if (!visualPrefabInicializado || !isActiveAndEnabled)
        {
            return;
        }

        // Tambem corrige o instante final das coroutines de taxi/decolagem,
        // que podem ter deixado um pequeno roll/pitch na raiz.
        Vector3 frenteNivelada = transform.forward;
        frenteNivelada.y = 0f;
        if (frenteNivelada.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(frenteNivelada.normalized, Vector3.up);
        }

        AplicarVisualNivelado();
    }

    private void AssinarServicoVisibilidade()
    {
        if (!receberInformacoesDeOutrosSensores)
        {
            if (servicoVisibilidade != null)
            {
                servicoVisibilidade.OnContactUpdated -= AoReceberContatoDeOutroSensor;
                servicoVisibilidade = null;
            }
            return;
        }

        RTSVisibilityService atual = RTSVisibilityService.Instancia;
        if (atual == servicoVisibilidade) return;

        if (servicoVisibilidade != null)
        {
            servicoVisibilidade.OnContactUpdated -= AoReceberContatoDeOutroSensor;
        }

        servicoVisibilidade = atual;
        if (servicoVisibilidade != null)
        {
            servicoVisibilidade.OnContactUpdated += AoReceberContatoDeOutroSensor;
        }
    }

    private void AoReceberContatoDeOutroSensor(RTSVisibilityContact contato)
    {
        if (!receberInformacoesDeOutrosSensores || contato == null || contato.observerTeamId != equipe)
        {
            return;
        }

        // ReportContact dispara o evento de forma sincrona. Ignorar AirRecon
        // evita que a retransmissao do proprio E-3 entre em loop.
        if (contato.source == RTSDetectionSource.AirRecon)
        {
            return;
        }

        RegistrarContato(
            contato.targetInstanceId,
            contato.targetTeamId,
            null,
            contato.lastKnownPosition,
            TipoContatoReconhecimento.InformacaoDeSensor,
            contato.targetTeamId != equipe,
            "sensor compartilhado",
            false,
            false);
    }

    private void ExecutarVarreduraReconhecimento()
    {
        if (equipe <= 0)
        {
            return;
        }

        float alcanceSqr = alcanceReconhecimento * alcanceReconhecimento;
        float alcanceSubSqr = alcanceSubmarinoNaSuperficie * alcanceSubmarinoNaSuperficie;
        RegistroEntidadesJogo.FillUnidades(bufferUnidades);

        for (int i = 0; i < bufferUnidades.Count; i++)
        {
            IdentidadeUnidade alvo = bufferUnidades[i];
            if (alvo == null || alvo == identidade || !alvo.gameObject.activeInHierarchy || alvo.teamID <= 0)
            {
                continue;
            }

            Vector3 deslocamento = alvo.transform.position - transform.position;
            float distanciaSqr = deslocamento.sqrMagnitude;
            ControleSubmarino submarino = alvo.GetComponent<ControleSubmarino>();
            if (submarino != null && !submarino.estaSubmerso && distanciaSqr <= alcanceSubSqr)
            {
                RegistrarContato(
                    alvo.GetInstanceID(),
                    alvo.teamID,
                    alvo,
                    alvo.transform.position,
                    TipoContatoReconhecimento.SubmarinoNaSuperficie,
                    alvo.teamID != equipe,
                    "radar aereo E-3",
                    true,
                    true);
                continue;
            }

            if (distanciaSqr > alcanceSqr)
            {
                continue;
            }

            bool inimigo = alvo.teamID != equipe;
            if (!inimigo && !retransmitirParaAliados)
            {
                continue;
            }

            RegistrarContato(
                alvo.GetInstanceID(),
                alvo.teamID,
                alvo,
                alvo.transform.position,
                inimigo ? TipoContatoReconhecimento.UnidadeInimiga : TipoContatoReconhecimento.UnidadeAliada,
                inimigo,
                "radar aereo E-3",
                inimigo,
                true);
        }

        MissileThreatTracker.CopiarAmeacasAtivas(bufferMisseis);
        for (int i = 0; i < bufferMisseis.Count; i++)
        {
            MissileThreatTracker missil = bufferMisseis[i];
            if (missil == null || (missil.TeamOrigem > 0 && missil.TeamOrigem == equipe))
            {
                continue;
            }

            Transform raizMissil = missil.RaizMissil;
            if (raizMissil == null || !raizMissil.gameObject.activeInHierarchy)
            {
                continue;
            }

            if ((raizMissil.position - transform.position).sqrMagnitude > alcanceReconhecimento * alcanceReconhecimento)
            {
                continue;
            }

            int idMissil = raizMissil.GetInstanceID();
            bool novoLancamento = misseisJaAlertados.Add(idMissil);
            RegistrarContato(
                idMissil,
                missil.TeamOrigem,
                null,
                raizMissil.position,
                TipoContatoReconhecimento.LancamentoDeMissil,
                true,
                "rastreador global de lancamento",
                novoLancamento,
                false);
        }

        if (misseisJaAlertados.Count > 256)
        {
            misseisJaAlertados.Clear();
        }
    }

    private void RegistrarContato(
        int idAlvo,
        int equipeAlvo,
        IdentidadeUnidade identidadeAlvo,
        Vector3 posicao,
        TipoContatoReconhecimento tipo,
        bool inimigo,
        string fonte,
        bool gerarAlerta,
        bool publicarNoServico)
    {
        if (idAlvo == 0 || equipe <= 0) return;

        long chave = ((long)equipe << 32) ^ (long)(uint)idAlvo;
        ContatoReconhecimento contato;
        if (!contatosAtivos.TryGetValue(chave, out contato) || contato == null)
        {
            contato = new ContatoReconhecimento
            {
                equipeObservadora = equipe,
                idAlvo = idAlvo
            };
            contatosAtivos[chave] = contato;
        }

        contato.equipeAlvo = equipeAlvo;
        contato.nomeAlvo = identidadeAlvo != null ? identidadeAlvo.name : tipo.ToString();
        contato.tipo = tipo;
        contato.ultimaPosicaoConhecida = posicao;
        contato.ultimaAtualizacao = Time.unscaledTime;
        contato.inimigo = inimigo;
        contato.fonte = fonte;
        contato.origemAeronave = name;
        contato.origemAeronavePosicao = transform.position;
        contato.alcanceComunicacao = Mathf.Max(0f, alcanceReconhecimento);

        if (publicarNoServico && servicoVisibilidade != null && identidadeAlvo != null && inimigo)
        {
            servicoVisibilidade.ReportContact(equipe, identidadeAlvo, RTSDetectionSource.AirRecon, memoriaContato);
        }

        OnContatoTransmitido?.Invoke(contato);
        if (gerarAlerta && inimigo)
        {
            AlertarQuartelUmaVez(contato);
        }
    }

    private void AlertarQuartelUmaVez(ContatoReconhecimento contato)
    {
        float agora = Time.unscaledTime;
        float ultimoAlerta;
        if (ultimoAlertaPorAlvo.TryGetValue(contato.idAlvo, out ultimoAlerta)
            && agora - ultimoAlerta < 12f)
        {
            return;
        }

        ultimoAlertaPorAlvo[contato.idAlvo] = agora;
        if (!alertarQuartelDoJogador || equipe != 1)
        {
            return;
        }

        Hegemonia.UI.GerenciadorAlertasUI alertas = Hegemonia.UI.GerenciadorAlertasUI.Instancia;
        if (alertas == null) return;

        string nome = string.IsNullOrWhiteSpace(contato.nomeAlvo) ? "contato desconhecido" : contato.nomeAlvo;
        alertas.MostrarAlerta(
            "E-3: " + contato.tipo + " detectado — " + nome
            + " em " + contato.ultimaPosicaoConhecida.ToString("F0"),
            new Color(0.25f, 0.85f, 1f),
            4f);
    }

    private void ExpirarContatosLocais()
    {
        float agora = Time.unscaledTime;
        List<long> expirados = null;
        foreach (KeyValuePair<long, ContatoReconhecimento> par in contatosAtivos)
        {
            ContatoReconhecimento contato = par.Value;
            if (contato == null || agora - contato.ultimaAtualizacao > memoriaContato)
            {
                if (expirados == null) expirados = new List<long>();
                expirados.Add(par.Key);
            }
        }

        if (expirados == null) return;
        for (int i = 0; i < expirados.Count; i++) contatosAtivos.Remove(expirados[i]);
    }

    public static bool TryObterContato(int equipeObservadora, int idAlvo, out ContatoReconhecimento contato)
    {
        long chave = ((long)equipeObservadora << 32) ^ (long)(uint)idAlvo;
        if (contatosAtivos.TryGetValue(chave, out contato)
            && contato != null
            && Time.unscaledTime - contato.ultimaAtualizacao <= 30f)
        {
            return true;
        }

        contato = null;
        return false;
    }

    private void OnDestroy()
    {
        if (servicoVisibilidade != null)
        {
            servicoVisibilidade.OnContactUpdated -= AoReceberContatoDeOutroSensor;
            servicoVisibilidade = null;
        }
    }
}
