using System.Collections.Generic;
using UnityEngine;
using Hegemonia.RTS;

/// <summary>
/// Patrulha opcional com detecção por raio, memória de contato e interceptação
/// mantendo uma distância segura. Usa ControleUnidade como executor único.
/// </summary>
[DisallowMultipleComponent]
public sealed class PatrulhaCombateSegura : MonoBehaviour
{
    public enum Estado { Patrulhando, Interceptando, RastreandoUltimaPosicao, RetornandoAPatrulha }

    [Header("Identificação e autorização")]
    [SerializeField] private int equipeOverride;
    [Tooltip("A IA/diplomacia deve ativar isto somente durante guerra ou ordem de combate.")]
    [SerializeField] private bool combateAutorizado;
    [Tooltip("Mesmo com a autorização ligada, só engaja equipes declaradas em guerra.")]
    [SerializeField] private bool exigirEstadoDeGuerra = true;
    [Tooltip("Reconhece e publica o contato, mas não persegue nem arma o combate.")]
    [SerializeField] private bool somenteReconhecimento;
    [SerializeField] private bool usarDeteccaoPorRaio = true;
    [SerializeField] private LayerMask camadasDeDeteccao = ~0;

    [Header("Área de reconhecimento")]
    [Min(1f)] [SerializeField] private float raioDeteccaoMetros = 200f;
    [Min(0.1f)] [SerializeField] private float intervaloVarreduraSegundos = 0.5f;
    [Min(0.1f)] [SerializeField] private float intervaloReconhecimentoEmPazSegundos = 2f;
    [Min(0.1f)] [SerializeField] private float tempoMemoriaContatoSegundos = 12f;

    [Header("Interceptação segura")]
    [Min(1f)] [SerializeField] private float distanciaSeguraMetros = 150f;
    [Min(0f)] [SerializeField] private float antecipacaoMovimentoSegundos = 2f;
    [Min(0.1f)] [SerializeField] private float intervaloReplanejamentoSegundos = 1.25f;
    [SerializeField] private bool manterAltitudeAtual = true;

    [Header("Rota de patrulha")]
    [SerializeField] private List<Transform> pontosDePatrulha = new List<Transform>();
    [SerializeField] private bool retornarAPatrulhaAoPerderContato = true;

    private ControleUnidade controle;
    private IdentidadeUnidade identidade;
    private readonly Collider[] bufferColisores = new Collider[128];
    private readonly HashSet<IdentidadeUnidade> candidatos = new HashSet<IdentidadeUnidade>();
    private Transform alvoAtual;
    private Vector3 ultimaPosicaoConhecida;
    private float ultimoContatoEm;
    private float proximaVarreduraEm;
    private float proximoReplanejamentoEm;
    private Estado estadoAtual = Estado.Patrulhando;
    private Transform prioridadeDefinidaPelaPatrulha;
    private string idOrdemPatrulhaAtiva;

    public Estado EstadoAtual => estadoAtual;
    public Transform AlvoAtual => alvoAtual;
    public Vector3 UltimaPosicaoConhecida => ultimaPosicaoConhecida;
    public float RaioDeteccaoMetros => Mathf.Max(1f, raioDeteccaoMetros);
    public float DistanciaSeguraMetros => Mathf.Max(1f, distanciaSeguraMetros);
    public bool CombateAutorizado => combateAutorizado;

    private void Awake()
    {
        controle = GetComponent<ControleUnidade>();
        identidade = GetComponent<IdentidadeUnidade>() ?? GetComponentInParent<IdentidadeUnidade>();
        ValidarConfiguracao();
    }

    private void OnValidate() => ValidarConfiguracao();

    private void Update()
    {
        if (Hegemonia.RTS.RTSSimulationClock.SimulacaoPausada) return;
        if (!combateAutorizado || controle == null)
        {
            if (estadoAtual != Estado.Patrulhando && estadoAtual != Estado.RetornandoAPatrulha)
                EncerrarContatoERetomarPatrulha();
            return;
        }

        int equipe = equipeOverride > 0 ? equipeOverride : (identidade != null ? identidade.teamID : 0);
        bool requerGuerraParaVarredura = exigirEstadoDeGuerra && !somenteReconhecimento;
        bool temGuerraAtual = RTSVisibilityService.TeamHasWarAuthorization(equipe);
        bool guerraAutorizada = !requerGuerraParaVarredura || temGuerraAtual;
        // Em paz, não há motivo para consultar a física a cada patrulha. A
        // checagem par-a-par continua em PodeEngajar depois que um alvo for
        // encontrado; esta é apenas a barreira barata para evitar a varredura
        // global quando a diplomacia não autoriza combate.
        if (requerGuerraParaVarredura && !guerraAutorizada)
        {
            if (estadoAtual != Estado.Patrulhando && estadoAtual != Estado.RetornandoAPatrulha)
                EncerrarContatoERetomarPatrulha();
            return;
        }

        if (usarDeteccaoPorRaio && Time.unscaledTime >= proximaVarreduraEm)
        {
            float intervaloEfetivo = intervaloVarreduraSegundos;
            if (somenteReconhecimento && !temGuerraAtual)
                intervaloEfetivo = Mathf.Max(intervaloEfetivo, intervaloReconhecimentoEmPazSegundos);
            proximaVarreduraEm = Time.unscaledTime + intervaloEfetivo;
            Transform detectado = EncontrarInimigoNoRaio();
            if (detectado != null) RegistrarContato(detectado);
        }

        if (Time.unscaledTime - ultimoContatoEm <= tempoMemoriaContatoSegundos &&
            alvoAtual != null && alvoAtual.gameObject.activeInHierarchy)
        {
            estadoAtual = Estado.Interceptando;
            EmitirInterceptacaoSeNecessario(alvoAtual, alvoAtual.position);
            return;
        }

        if (Time.unscaledTime - ultimoContatoEm <= tempoMemoriaContatoSegundos)
        {
            estadoAtual = Estado.RastreandoUltimaPosicao;
            EmitirInterceptacaoSeNecessario(null, ultimaPosicaoConhecida);
            return;
        }

        if (alvoAtual != null || estadoAtual == Estado.RastreandoUltimaPosicao)
            EncerrarContatoERetomarPatrulha();
    }

    public bool ReceberContatoRadar(Transform alvo)
    {
        if (!combateAutorizado || alvo == null || !EhInimigo(alvo)) return false;
        PublicarContatoReconhecido(alvo);
        if (somenteReconhecimento || !PodeEngajar(alvo)) return true;
        RegistrarContato(alvo);
        return true;
    }

    public void DefinirCombateAutorizado(bool autorizado)
    {
        combateAutorizado = autorizado;
        if (!autorizado) EncerrarContatoERetomarPatrulha();
    }

    public void ConfigurarPatrulha(IList<Vector3> pontos)
    {
        pontosDePatrulha.Clear();
        if (pontos == null) return;
        for (int i = 0; i < pontos.Count; i++)
        {
            GameObject marcador = new GameObject("PontoPatrulha_Runtime_" + i);
            marcador.transform.SetParent(transform, false);
            marcador.transform.position = pontos[i];
            marcador.hideFlags = HideFlags.HideInHierarchy;
            pontosDePatrulha.Add(marcador.transform);
        }
    }

    private void RegistrarContato(Transform alvo)
    {
        if (!EhInimigo(alvo)) return;
        PublicarContatoReconhecido(alvo);
        if (somenteReconhecimento || !PodeEngajar(alvo)) return;
        alvoAtual = alvo;
        ultimaPosicaoConhecida = alvo.position;
        ultimoContatoEm = Time.unscaledTime;
        estadoAtual = Estado.Interceptando;
        EmitirInterceptacaoSeNecessario(alvo, ultimaPosicaoConhecida);
    }

    private Transform EncontrarInimigoNoRaio()
    {
        candidatos.Clear();
        int quantidade = Physics.OverlapSphereNonAlloc(transform.position, raioDeteccaoMetros,
            bufferColisores, camadasDeDeteccao, QueryTriggerInteraction.Ignore);
        IdentidadeUnidade melhor = null;
        float menorDistancia = float.MaxValue;
        for (int i = 0; i < quantidade; i++)
        {
            Collider colisor = bufferColisores[i];
            if (colisor == null) continue;
            IdentidadeUnidade candidato = colisor.GetComponentInParent<IdentidadeUnidade>();
            if (candidato == null || !candidatos.Add(candidato) || !EhInimigo(candidato.transform)) continue;
            float distancia = (candidato.transform.position - transform.position).sqrMagnitude;
            if (distancia < menorDistancia) { menorDistancia = distancia; melhor = candidato; }
        }
        return melhor != null ? melhor.transform : null;
    }

    private bool EhInimigo(Transform alvo)
    {
        if (alvo == null || alvo == transform || alvo.IsChildOf(transform)) return false;
        IdentidadeUnidade identidadeAlvo = alvo.GetComponentInParent<IdentidadeUnidade>();
        int equipe = equipeOverride > 0 ? equipeOverride : (identidade != null ? identidade.teamID : 0);
        if (identidadeAlvo == null || identidadeAlvo.teamID <= 0 ||
            (equipe > 0 && identidadeAlvo.teamID == equipe)) return false;
        return true;
    }

    private bool PodeEngajar(Transform alvo)
    {
        if (!exigirEstadoDeGuerra) return true;
        IdentidadeUnidade identidadeAlvo = alvo != null ? alvo.GetComponentInParent<IdentidadeUnidade>() : null;
        int equipe = equipeOverride > 0 ? equipeOverride : (identidade != null ? identidade.teamID : 0);
        return identidadeAlvo != null
            && ContextoTerritorialDiplomatico.PodeDispararPorHostilidadeOuGuerra(equipe, identidadeAlvo);
    }

    private void PublicarContatoReconhecido(Transform alvo)
    {
        if (RTSVisibilityService.Instancia == null || alvo == null) return;
        IdentidadeUnidade identidadeAlvo = alvo.GetComponentInParent<IdentidadeUnidade>();
        int equipe = equipeOverride > 0 ? equipeOverride : (identidade != null ? identidade.teamID : 0);
        if (identidadeAlvo != null && equipe > 0 && identidadeAlvo.teamID > 0 && identidadeAlvo.teamID != equipe)
            RTSVisibilityService.Instancia.ReportContact(equipe, identidadeAlvo, RTSDetectionSource.AirRecon, tempoMemoriaContatoSegundos);
    }

    private void EmitirInterceptacaoSeNecessario(Transform alvo, Vector3 posicaoAlvo)
    {
        if (controle == null || Time.unscaledTime < proximoReplanejamentoEm) return;

        // Uma missão explícita (por exemplo, Attack N2 da IA03) tem
        // precedência sobre a aquisição automática da patrulha. Emitir a
        // ordem de interceptação abaixo cancela comportamentos e limpa a
        // prioridade; por isso a patrulha precisa ceder quando já existe um
        // alvo propagado por uma ordem superior, inclusive quando a missão e
        // a patrulha apontam para o mesmo Transform. O ponto de aproximação
        // da missão pode ser diferente do standoff seguro da patrulha.
        Transform prioridadeExistente = controle.ObterAlvoPrioritarioAtual();
        OrdemMovimento ordemExistente = controle.OrdemMovimentoAtual;
        bool prioridadePertenceAEstaPatrulha = prioridadeExistente != null
            && ReferenceEquals(prioridadeExistente, prioridadeDefinidaPelaPatrulha)
            && ordemExistente != null
            && !string.IsNullOrEmpty(idOrdemPatrulhaAtiva)
            && string.Equals(ordemExistente.Id, idOrdemPatrulhaAtiva, System.StringComparison.Ordinal);
        if (prioridadeExistente != null && !prioridadePertenceAEstaPatrulha)
            return;

        proximoReplanejamentoEm = Time.unscaledTime + intervaloReplanejamentoSegundos;
        Vector3 previsao = posicaoAlvo;
        if (alvo != null)
        {
            Rigidbody corpo = alvo.GetComponentInParent<Rigidbody>();
            if (corpo != null) previsao += corpo.linearVelocity * antecipacaoMovimentoSegundos;
        }
        Vector3 afastamento = transform.position - previsao;
        afastamento.y = 0f;
        if (afastamento.sqrMagnitude < 0.01f) { afastamento = -transform.forward; afastamento.y = 0f; }
        Vector3 destinoSeguro = previsao + afastamento.normalized * distanciaSeguraMetros;
        if (manterAltitudeAtual) destinoSeguro.y = transform.position.y;
        if (controle.EmitirOrdemMover(destinoSeguro, true))
        {
            controle.DefinirModoCombate(true);
            controle.DefinirAlvoPrioritario(alvo);
            prioridadeDefinidaPelaPatrulha = alvo;
            OrdemMovimento ordemPatrulha = controle.OrdemMovimentoAtual;
            idOrdemPatrulhaAtiva = ordemPatrulha != null ? ordemPatrulha.Id : null;
        }
    }

    private void EncerrarContatoERetomarPatrulha()
    {
        // Uma missão explícita pode continuar apontando para outro alvo
        // enquanto o contato automático desta patrulha expira. Nesse caso,
        // encerre somente o estado local da patrulha; limpar a prioridade
        // global cancelaria a missão superior e faria a unidade retornar à
        // rota de patrulha.
        Transform prioridadeExistente = controle != null
            ? controle.ObterAlvoPrioritarioAtual()
            : null;
        OrdemMovimento ordemExistente = controle != null ? controle.OrdemMovimentoAtual : null;
        bool prioridadePertenceAEstaPatrulha = prioridadeExistente != null
            && ReferenceEquals(prioridadeExistente, prioridadeDefinidaPelaPatrulha)
            && ordemExistente != null
            && !string.IsNullOrEmpty(idOrdemPatrulhaAtiva)
            && string.Equals(ordemExistente.Id, idOrdemPatrulhaAtiva, System.StringComparison.Ordinal);
        bool preservarPrioridadeSuperior = prioridadeExistente != null && !prioridadePertenceAEstaPatrulha;

        alvoAtual = null;
        prioridadeDefinidaPelaPatrulha = null;
        idOrdemPatrulhaAtiva = null;
        ultimaPosicaoConhecida = Vector3.zero;
        ultimoContatoEm = 0f;
        proximoReplanejamentoEm = 0f;
        estadoAtual = preservarPrioridadeSuperior
            ? Estado.Patrulhando
            : Estado.RetornandoAPatrulha;
        if (preservarPrioridadeSuperior)
            return;

        if (controle == null) return;
        controle.DefinirAlvoPrioritario(null);
        controle.DefinirModoCombate(false);
        if (retornarAPatrulhaAoPerderContato)
        {
            List<Vector3> rota = new List<Vector3>();
            for (int i = 0; i < pontosDePatrulha.Count; i++)
                if (pontosDePatrulha[i] != null) rota.Add(pontosDePatrulha[i].position);
            if (rota.Count > 0 && controle.EmitirOrdemPatrulha(rota)) { estadoAtual = Estado.Patrulhando; return; }
        }
        estadoAtual = Estado.Patrulhando;
    }

    private void ValidarConfiguracao()
    {
        raioDeteccaoMetros = Mathf.Max(1f, raioDeteccaoMetros);
        intervaloVarreduraSegundos = Mathf.Max(0.1f, intervaloVarreduraSegundos);
        tempoMemoriaContatoSegundos = Mathf.Max(0.1f, tempoMemoriaContatoSegundos);
        distanciaSeguraMetros = Mathf.Max(1f, distanciaSeguraMetros);
        antecipacaoMovimentoSegundos = Mathf.Max(0f, antecipacaoMovimentoSegundos);
        intervaloReplanejamentoSegundos = Mathf.Max(0.1f, intervaloReplanejamentoSegundos);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = combateAutorizado ? new Color(1f, .35f, .1f, .45f) : new Color(.2f, .7f, 1f, .35f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(1f, raioDeteccaoMetros));
        if (alvoAtual != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, alvoAtual.position);
            Gizmos.DrawWireSphere(alvoAtual.position, Mathf.Max(1f, distanciaSeguraMetros));
        }
    }
}
