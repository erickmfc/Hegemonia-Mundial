using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(ControleAviao))]
// [RequireComponent(typeof(ControleUnidade))] // Removido para permitir deletar o script antigo
[RequireComponent(typeof(SistemaDeDanos))]
public class LancadorMisselCaca : MonoBehaviour
{
    public enum GeracaoCaca
    {
        AutomaticaPorModelo = 0,
        TerceiraGeracao = 3,
        QuartaGeracao = 4
    }

    private const float DuracaoMinimaAlvoForcado = 60f;
    private const float DuracaoAlvoAposDisparo = 8f;
    private const float RaioDeteccaoTerceiraGeracao = 6000f;
    private const float RaioDeteccaoQuartaGeracao = 9000f;
    private const float RaioDeteccaoPadraoFallback = 1500f;

    [Header("Configuração de Munição e Vida")]
    public int municaoAtual = 2;    
    public int municaoMaxima = 4;
    
    [Header("Configuração de Lançamento")]
    public Transform[] pontosDeSaida; 
    public GameObject missilCacaPrefab; 
    public float tempoRecarga = 2.0f;
    public float raioDeDeteccao = 600f;
    [SerializeField] private GeracaoCaca geracaoCaca = GeracaoCaca.AutomaticaPorModelo;

    [Header("Postura de Combate e Arco Frontal")]
    [Tooltip("Meio-ângulo máximo frontal (em graus) para liberar lançamento de míssil.")]
    [Range(10f, 90f)] [SerializeField] private float anguloMaximoDisparoFrontal = 38f;
    [Tooltip("Distância mínima horizontal para manter postura de ataque sem grudar em outro caça.")]
    [Min(200f)] [SerializeField] private float distanciaMinimaStandOffAereo = 900f;
    [Tooltip("Distância mínima horizontal de afastamento contra alvos navais ou terrestres.")]
    [Min(200f)] [SerializeField] private float distanciaMinimaStandOffSuperficie = 1200f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    
    private float cronometroRecarga = 0f;
    private int indiceCano = 0;
    private ControleUnidade _unidadeBase;
    private ControleAviao _vooModerno;
    private SistemaDeDanos _sistemaDanos;
    private int _meuTime;

    // --- CACHE: Rigidbody e AudioSource (evita GetComponent repetido) ---
    private Rigidbody _rb;
    private AudioSource _audioSource;

    // Cache por ponto de saida (evita GetComponentsInChildren em cada recarga/disparo).
    private Renderer[][] _renderersPorSaida;

    // Patrulha e Comportamento
    public bool modoPassivo = false;
    private Vector3 pontoPatrulha;
    private bool voltandoParaBase = false;
    private Transform _alvoIAForcado;
    private Vector3 _ultimoPontoAlvoIA;
    private float _tempoValidadeAlvoIA = -1f;
    private MisselCaca _missilEmVoo;
    private Transform _alvoDoMissilEmVoo;
    private float _tempoExpiracaoEsperaImpacto = -1f;
    private Transform _alvoPerseguicaoAtivo;
    private bool _controlandoPerseguicaoVoo;
    private bool _emManobraAfastamento;

    // Detecção
    // Struct: vive na stack, zero alocação por scan (antes era class = heap alloc por cada alvo detectado)
    public struct AlvoDetectado 
    {
        public Transform transform;
        public string nome;
        public float distancia;
        public int prioridade;
        public bool ehAereo;
    }
    private List<AlvoDetectado> inimigosNaArea = new List<AlvoDetectado>();
    private float tempoUltimoScan = 0f;
    private bool avisouPrefabMissilAusente;

    public bool TemInimigosDetectados => inimigosNaArea != null && inimigosNaArea.Count > 0;

    // O modo definido no menu (ControleUnidade) e a autoridade final. O
    // aeroporto/IA ainda pode enviar uma missao, mas nunca pode transformar
    // uma aeronave marcada como passiva em uma unidade de ataque autonomo.
    private bool ModoPassivoOficial => _unidadeBase != null && !_unidadeBase.ModoCombateAtivo;

    public void DefinirModoPassivoPeloMenu(bool passivo)
    {
        modoPassivo = passivo;
        if (passivo)
        {
            _alvoIAForcado = null;
            _tempoValidadeAlvoIA = -1f;
            LiberarPerseguicaoVoo();
        }
    }

    // Interface
    private Vector2 scrollPosition;
    private bool radarMinimizado = false;
    private bool radarFechado = false;
    private bool ultimoEstadoRadar = false;

    // --- CACHE: Busca O(1) de unidades ---
    private static readonly List<IdentidadeUnidade> _bufferGlobais = new List<IdentidadeUnidade>(512);
    private readonly HashSet<Transform> _alvosJaVistos = new HashSet<Transform>();
    private readonly HashSet<Transform> _alvosAutorizadosHud = new HashSet<Transform>();
    private bool somenteAlvosAutorizadosHud;
    private bool ordensManuaisHud;

    public void DefinirPoliticaTaticaHud(bool manual, IList<Transform> alvosAutorizados, bool limitarAutomatico)
    {
        ordensManuaisHud = manual;
        somenteAlvosAutorizadosHud = limitarAutomatico;
        _alvosAutorizadosHud.Clear();
        if (manual)
        {
            LiberarPerseguicaoVoo();
        }
        if (alvosAutorizados == null) return;
        for (int i = 0; i < alvosAutorizados.Count; i++)
        {
            Transform alvo = alvosAutorizados[i];
            if (alvo != null) _alvosAutorizadosHud.Add(alvo);
        }
    }

    void Start()
    {
        float pisoGeracao = ObterRaioMinimoGeracao();
        if (raioDeDeteccao < pisoGeracao) raioDeDeteccao = pisoGeracao;
        _unidadeBase = GetComponent<ControleUnidade>()
            ?? GetComponentInParent<ControleUnidade>()
            ?? GetComponentInChildren<ControleUnidade>(true);
        _vooModerno = GetComponent<ControleAviao>();
        _sistemaDanos = GetComponent<SistemaDeDanos>();
        _rb = GetComponent<Rigidbody>();
        _audioSource = GetComponent<AudioSource>();
        AudioRuntime.ConfigurarFonteDeMissel(_audioSource);
        PoolDeObjetosCombate.Prewarm(missilCacaPrefab, Mathf.Clamp(municaoMaxima, 2, 6));

        if (pontosDeSaida != null && pontosDeSaida.Length > 0)
        {
            _renderersPorSaida = new Renderer[pontosDeSaida.Length][];
            for (int i = 0; i < pontosDeSaida.Length; i++)
            {
                Transform saida = pontosDeSaida[i];
                _renderersPorSaida[i] = saida != null ? saida.GetComponentsInChildren<Renderer>(true) : null;
            }
        }
        
        _meuTime = ResolverTimeProprio();
        // Evita que todos os caças escaneiem e disparem no mesmo frame.
        tempoUltimoScan = Time.time + UnityEngine.Random.Range(0.05f, 0.85f);

        if (missilCacaPrefab == null)
        {
            Debug.LogError($"[LancadorMisselCaca] {name} está sem missilCacaPrefab; o caça não poderá disparar.", this);
            avisouPrefabMissilAusente = true;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        MissilePrefabAutoBinder.BindLancadorMisselCaca(this);
    }

    [ContextMenu("Auto configurar missil")]
    private void AutoConfigurarMissilEditor()
    {
        MissilePrefabAutoBinder.BindLancadorMisselCaca(this, true);
    }
#endif

    void Update()
    {
        if (cronometroRecarga > 0) cronometroRecarga -= Time.deltaTime;

        bool aviaoEstacionadoEDesligado = _vooModerno != null && _vooModerno.EstaEstacionadoEDesligado;

        bool radarAtivoVisualmente = false;
        if (!aviaoEstacionadoEDesligado)
        {
            if (_unidadeBase != null && _unidadeBase.selecionado) radarAtivoVisualmente = true;
            if (!radarAtivoVisualmente && _vooModerno != null && _vooModerno.aeroportoOrigem != null)
            {
                if (_vooModerno.aeroportoOrigem.aviaoSelecionadoParaMissao == _vooModerno) radarAtivoVisualmente = true;
            }
        }

        bool vooComOrdemAtiva = _vooModerno != null
            && (_vooModerno.estadoAtual == ControleAviao.EstadoAviao.Taxiando
                || _vooModerno.estadoAtual == ControleAviao.EstadoAviao.Decolando
                || _vooModerno.estadoAtual == ControleAviao.EstadoAviao.EmMissao);
        if (vooComOrdemAtiva && _alvoIAForcado != null && Time.time > _tempoValidadeAlvoIA)
        {
            // Um voo de ataque pode levar vários minutos até entrar no alcance.
            // O prazo inicial evita alvos esquecidos no pátio, mas não deve
            // apagar uma ordem enquanto o mesmo voo ainda está em execução.
            _tempoValidadeAlvoIA = Time.time + DuracaoAlvoAposDisparo;
        }

        bool alvoIAAtivo = _alvoIAForcado != null && Time.time <= _tempoValidadeAlvoIA;
        bool emMissao = _vooModerno != null && _vooModerno.estadoAtual == ControleAviao.EstadoAviao.EmMissao;
        bool deveEscanear = radarAtivoVisualmente || emMissao || alvoIAAtivo;

        // Uma ordem de ataque já contém um alvo confirmado pelo jogador ou
        // pela IA. Tente disparar continuamente ao entrar no alcance, sem
        // depender da próxima varredura de aquisição (que ocorre a cada 1 s).
        if (alvoIAAtivo && emMissao)
        {
            TentarDisparoContraAlvoIA();
        }

        // Detecção de Inimigos (Radar) — Apenas a cada 1 segundo pra não pesar
        if (deveEscanear && Time.time > tempoUltimoScan + 1.0f)
        {
            tempoUltimoScan = Time.time;
            EscanearArea(radarAtivoVisualmente);
            ProcessarPatrulhaAutomatica();
        }

        AtualizarPosturaPerseguicao(emMissao, alvoIAAtivo);

        // Sistema de Recarga Automática na Base
        ControleAviao.EstadoAviao estado = _vooModerno.estadoAtual;
        if (estado == ControleAviao.EstadoAviao.ProntoNoPatio || estado == ControleAviao.EstadoAviao.ReservaHangar)
        {
            if (municaoAtual < municaoMaxima)
            {
                municaoAtual = municaoMaxima;
                indiceCano = 0;
                
                // Reativa mesh dos mísseis em reserva
                if (_renderersPorSaida != null && _renderersPorSaida.Length > 0)
                {
                    for (int i = 0, count = _renderersPorSaida.Length; i < count; i++)
                    {
                        Renderer[] renderers = _renderersPorSaida[i];
                        if (renderers == null) continue;
                        for (int j = 0, jCount = renderers.Length; j < jCount; j++)
                        {
                            if (renderers[j] != null) renderers[j].enabled = true;
                        }
                    }
                }
                else if (pontosDeSaida != null)
                {
                    for (int i = 0, count = pontosDeSaida.Length; i < count; i++)
                    {
                        Transform tf = pontosDeSaida[i];
                        if (tf == null) continue;
                        Renderer[] renderers = tf.GetComponentsInChildren<Renderer>(true);
                        for (int j = 0, jCount = renderers.Length; j < jCount; j++)
                        {
                            if (renderers[j] != null) renderers[j].enabled = true;
                        }
                    }
                }
                
                if (debugLogs)
                {
                    Debug.Log($"[Base] {gameObject.name} recarregou mísseis no Hangar/Porta-Avioes.");
                }
            }

            CombustivelUnidade combustivel = CombustivelUnidade.ObterParaObjeto(gameObject);
            if (combustivel != null && combustivel.usaCombustivel && combustivel.Percentual < 0.99f)
            {
                if (_vooModerno != null)
                {
                    _vooModerno.ReabastecerSeAbaixoDe(0.99f);
                }
                else
                {
                    ServicoAbastecimento.TentarAbastecer(
                        combustivel,
                        combustivel.Capacidade - combustivel.CombustivelAtual,
                        out _);
                }
            }

            if (voltandoParaBase && municaoAtual >= municaoMaxima)
            {
                voltandoParaBase = false;
                if (_vooModerno != null && !_vooModerno.PossuiRetomadaDeMissaoPendente && _vooModerno.aeroportoOrigem != null)
                {
                    _vooModerno.IniciarMissaoCompleta(pontoPatrulha);
                    if (debugLogs)
                    {
                        Debug.Log($"[Base] {gameObject.name} voltando para a patrulha automatica.");
                    }
                }
            }
            if (_sistemaDanos.vidaAtual < _sistemaDanos.vidaMaxima)
            {
                _sistemaDanos.Reparar(_sistemaDanos.vidaMaxima);
                if (debugLogs)
                {
                    Debug.Log($"[Base] {gameObject.name} foi totalmente reparado.");
                }
            }
        }
    }

    void ProcessarPatrulhaAutomatica()
    {
        if (ModoPassivoOficial)
        {
            modoPassivo = true;
            _alvoIAForcado = null;
            _tempoValidadeAlvoIA = -1f;
            return;
        }
        if (_vooModerno == null || _vooModerno.estadoAtual != ControleAviao.EstadoAviao.EmMissao) return;
        if (voltandoParaBase) return;

        // Alvo IA forçado sempre tem prioridade, mesmo se estiver em patrulha/recon.
        if (TentarDisparoContraAlvoIA()) return;
        // A postura manual mantém sensores e ordens explícitas disponíveis,
        // mas impede que o lançador escolha sozinho um alvo detectado.
        if (ordensManuaisHud) return;

        if (modoPassivo)
        {
            if (inimigosNaArea.Count == 0) return;

            AlvoDetectado candidato = inimigosNaArea[0];
            if (candidato.transform == null) return;

            // Em modo passivo, reage defensivamente: ameaça aérea ou muito perto.
            if (!candidato.ehAereo && candidato.distancia > 450f) return;
        }

        if (municaoAtual > 0 && cronometroRecarga <= 0 && inimigosNaArea.Count > 0)
        {
            // Um contato neutro pode aparecer antes de um inimigo real na
            // ordenação de sensores. Procura o primeiro alvo autorizado para
            // que esse contato não trave a aquisição automática do caça.
            for (int i = 0; i < inimigosNaArea.Count; i++)
            {
                AlvoDetectado alvo = inimigosNaArea[i];
                if (alvo.transform == null) continue;
                if (somenteAlvosAutorizadosHud && !_alvosAutorizadosHud.Contains(alvo.transform)) continue;
                IdentidadeUnidade identidadeAlvo = ResolverIdentidadeAlvo(alvo.transform);
                if (!ContextoTerritorialDiplomatico.PodeDispararPorHostilidadeOuGuerra(_meuTime, identidadeAlvo)) continue;
                if (Disparar(alvo.transform)) break;
            }
        }

        // Volta para o porta-aviões ou base aliada mais próxima se ficar sem munição
        if (municaoAtual <= 0 && _vooModerno != null && !voltandoParaBase
            && (_vooModerno.estadoAtual == ControleAviao.EstadoAviao.EmMissao || _vooModerno.estadoAtual == ControleAviao.EstadoAviao.Decolando))
        {
            LiberarPerseguicaoVoo();
            pontoPatrulha = _vooModerno.alvoGPSVoo;
            voltandoParaBase = true;
            if (!_vooModerno.RetornarParaReabastecimentoEMissao("municao") && _vooModerno.aeroportoOrigem != null)
            {
                _vooModerno.ComandoRetornarBase();
            }
            if (debugLogs)
            {
                Debug.Log($"[Radar] {gameObject.name} sem municao. Retornando para reabastecimento no porta-avioes/base mais proxima.");
            }
        }
    }

    /// <summary>
    /// Cancela somente a retomada automática que este componente agenda após
    /// voltar à base por falta de munição. A nova ordem continua sendo
    /// executada pelo ControleAviao, que é a autoridade única do voo.
    /// </summary>
    public void CancelarRetomadaAutomaticaAposNovaOrdem()
    {
        voltandoParaBase = false;
        pontoPatrulha = Vector3.zero;
        _alvoIAForcado = null;
        _ultimoPontoAlvoIA = Vector3.zero;
        _tempoValidadeAlvoIA = -1f;
        LiberarPerseguicaoVoo();
    }

    public void DefinirAlvoIA(Transform alvo, Vector3 alvoFallback, float duracaoSegundos = 4f)
    {
        if (ModoPassivoOficial)
        {
            modoPassivo = true;
            _alvoIAForcado = null;
            _tempoValidadeAlvoIA = -1f;
            return;
        }
        _alvoIAForcado = alvo;
        _ultimoPontoAlvoIA = alvo != null ? alvo.position : alvoFallback;
        // Uma ordem de ataque sobrevive ao taxiamento, à decolagem e à
        // aproximação. A janela de 4–6 s expirava antes do caça ficar em
        // estado EmMissao ou entrar no alcance do míssil.
        _tempoValidadeAlvoIA = Time.time + Mathf.Max(DuracaoMinimaAlvoForcado, duracaoSegundos);
        modoPassivo = false;

        if (_vooModerno != null && _vooModerno.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
        {
            Vector3 foco = _ultimoPontoAlvoIA;
            if (foco.y < 60f) foco.y = 60f;
            _vooModerno.alvoPrioritarioIA = true;
            _vooModerno.centroDaPatrulha = foco;
            _vooModerno.alvoGPSVoo = foco;
            _controlandoPerseguicaoVoo = true;
        }
    }

    public bool TentarDisparoIA(Transform alvo, Vector3 alvoFallback, float duracaoSegundos = 4f)
    {
        DefinirAlvoIA(alvo, alvoFallback, duracaoSegundos);
        return TentarDisparoContraAlvoIA();
    }

    bool TentarDisparoContraAlvoIA()
    {
        if (ModoPassivoOficial) return false;
        if (_alvoIAForcado == null || Time.time > _tempoValidadeAlvoIA) return false;
        if (_vooModerno == null || _vooModerno.estadoAtual != ControleAviao.EstadoAviao.EmMissao) return false;
        if (voltandoParaBase || modoPassivo) return false;
        if (municaoAtual <= 0 || cronometroRecarga > 0f) return false;

        float alcanceMaximo = Mathf.Max(raioDeDeteccao, ObterRaioMinimoGeracao());
        if ((_alvoIAForcado.position - transform.position).sqrMagnitude > alcanceMaximo * alcanceMaximo)
        {
            return false;
        }

        if (!Disparar(_alvoIAForcado)) return false;
        _ultimoPontoAlvoIA = _alvoIAForcado.position;
        // Preserve o alvo por várias cadências para permitir mais de um
        // disparo contra o mesmo contato quando a ordem ainda está ativa.
        _tempoValidadeAlvoIA = Time.time + Mathf.Max(DuracaoAlvoAposDisparo, tempoRecarga + 1f);
        return true;
    }

    bool EhAlvoAereo(Transform alvoTransform, IdentidadeUnidade idUnidade)
    {
        if (alvoTransform == null) return false;

        // OTIMIZAÇÃO: Checagem rápida por IdentidadeUnidade antes de reflection / busca em hierarquia
        if (idUnidade != null && idUnidade.tipoUnidade == TipoUnidade.Aereo) return true;

        string tag = alvoTransform.tag;
        if (tag == "Areo" || tag == "Aereo") return true;

        string nomeAlvo = alvoTransform.name;
        if (NomeContem(nomeAlvo, "aviao") ||
            NomeContem(nomeAlvo, "caca") ||
            NomeContem(nomeAlvo, "jato") ||
            NomeContem(nomeAlvo, "heli") ||
            NomeContem(nomeAlvo, "drone") ||
            NomeContem(nomeAlvo, "vap") ||
            NomeContem(nomeAlvo, "bombard") ||
            NomeContem(nomeAlvo, "bombardeiro") ||
            NomeContem(nomeAlvo, "bomber"))
        {
            return true;
        }

        return alvoTransform.GetComponentInParent<ControleAviao>() != null ||
               alvoTransform.GetComponentInParent<ControleAviaoCaca>() != null ||
               alvoTransform.GetComponentInParent<AviaoBombardeiro>() != null ||
               alvoTransform.GetComponentInParent<Helicoptero>() != null ||
               alvoTransform.GetComponentInParent<C700TransporteAereo>() != null;
    }

    int ObterPrioridadeAlvo(Transform alvoTransform, IdentidadeUnidade idUnidade)
    {
        if (alvoTransform == null) return int.MaxValue;

        string nomeAlvo = alvoTransform.name;
        bool ehBombardeiro = (idUnidade != null && idUnidade.tipoUnidade == TipoUnidade.Aereo && NomeContem(nomeAlvo, "bomb")) ||
                             NomeContem(nomeAlvo, "bombard") ||
                             NomeContem(nomeAlvo, "bombardeiro") ||
                             NomeContem(nomeAlvo, "bomber") ||
                             alvoTransform.GetComponentInParent<AviaoBombardeiro>() != null;

        if (ehBombardeiro) return 0;
        if (NomeContem(nomeAlvo, "caca")
            || NomeContem(nomeAlvo, "jato")
            || alvoTransform.GetComponentInParent<ControleAviaoCaca>() != null
            || alvoTransform.GetComponentInParent<LancadorMisselCaca>() != null)
        {
            return 1;
        }

        bool ehCivilOuEconomico = EhAlvoCivilOuEconomico(alvoTransform, nomeAlvo);
        if (EhAlvoAereo(alvoTransform, idUnidade))
        {
            return ehCivilOuEconomico ? 4 : 2;
        }

        if (ehCivilOuEconomico) return 4;

        // Navios militares, aeroportos militares, construtores/fábricas, tendas e demais unidades combatentes
        return 3;
    }

    private static bool EhAlvoCivilOuEconomico(Transform alvoTransform, string nomeAlvo)
    {
        if (alvoTransform == null) return false;

        if (alvoTransform.GetComponentInParent<GerenciadorAeroportoComercial>() != null
            || alvoTransform.GetComponentInParent<ControleAviaoComercial>() != null
            || alvoTransform.GetComponentInParent<Hegemonia.Aeronaves.C17.C17TransporteController>() != null)
        {
            return true;
        }

        // Exceções explícitas: estruturas claramente militares nunca são rebaixadas como civis
        if (alvoTransform.GetComponentInParent<GerenciadorAeroporto>() != null
            || NomeContem(nomeAlvo, "aeroporto militar")
            || NomeContem(nomeAlvo, "tenda")
            || NomeContem(nomeAlvo, "construtor")
            || NomeContem(nomeAlvo, "quartel")
            || NomeContem(nomeAlvo, "estaleiro")
            || NomeContem(nomeAlvo, "militar")
            || NomeContem(nomeAlvo, "bunker")
            || NomeContem(nomeAlvo, "torreta"))
        {
            return false;
        }

        return NomeContem(nomeAlvo, "plataforma")
            || NomeContem(nomeAlvo, "petroleo")
            || NomeContem(nomeAlvo, "petrol")
            || NomeContem(nomeAlvo, "petroleiro")
            || NomeContem(nomeAlvo, "cargueiro")
            || NomeContem(nomeAlvo, "comercial")
            || NomeContem(nomeAlvo, "prefeitura")
            || NomeContem(nomeAlvo, "cidade")
            || NomeContem(nomeAlvo, "predio")
            || NomeContem(nomeAlvo, "torre_turistica")
            || NomeContem(nomeAlvo, "estadio")
            || NomeContem(nomeAlvo, "food")
            || NomeContem(nomeAlvo, "fazenda");
    }

    void EscanearArea(bool radarAtivoVisualmente)
    {
        inimigosNaArea.Clear();
        _alvosJaVistos.Clear();
        _meuTime = ResolverTimeProprio();

        // OTIMIZAÇÃO MAXIMA: Ao invés de usar a Física da Unity (OverlapSphere) que pesa a CPU em raios gigantes,
        // agora buscamos direto na memória do jogo apenas o que de fato é Unidade Militar.
        RegistroEntidadesJogo.FillUnidades(_bufferGlobais);
        float raioSqr = raioDeDeteccao * raioDeDeteccao;

        for (int i = 0; i < _bufferGlobais.Count; i++)
        {
            IdentidadeUnidade idAlvo = _bufferGlobais[i];
            if (idAlvo == null || idAlvo.teamID == _meuTime) continue;
            if (!ControleSubmarino.PodeSerAlvoConvencional(idAlvo.transform)) continue;

            SistemaDeDanos alvoDanos = idAlvo.GetComponent<SistemaDeDanos>()
                ?? idAlvo.GetComponentInChildren<SistemaDeDanos>(true);
            if (alvoDanos == null || alvoDanos.vidaAtual <= 0) continue;

            Transform alvoTransform = alvoDanos.transform;
            
            float distSqr = (transform.position - alvoTransform.position).sqrMagnitude;
            if (distSqr > raioSqr) continue;

            if (!_alvosJaVistos.Add(alvoTransform)) continue;

            AlvoDetectado novo = new AlvoDetectado();
            novo.transform = alvoTransform;
            // Cria string apenas se o menu visual estiver ativo para salvar GC
            if (radarAtivoVisualmente)
            {
                novo.nome = (alvoTransform.name.Contains("(Clone)")) ? alvoTransform.name.Replace("(Clone)", "") : alvoTransform.name;
            }
            else
            {
                novo.nome = string.Empty;
            }
            novo.distancia = Mathf.Sqrt(distSqr);
            novo.ehAereo = EhAlvoAereo(alvoTransform, idAlvo);
            novo.prioridade = ObterPrioridadeAlvo(alvoTransform, idAlvo);
            inimigosNaArea.Add(novo);
        }
        
        // Bombardeiros e outras aeronaves entram na frente, mantendo os mais próximos no desempate.
        inimigosNaArea.Sort((a, b) =>
        {
            int comparacaoPrioridade = a.prioridade.CompareTo(b.prioridade);
            if (comparacaoPrioridade != 0) return comparacaoPrioridade;
            return a.distancia.CompareTo(b.distancia);
        });
    }

    private int ResolverTimeProprio()
    {
        IdentidadeIA identidadeIA = GetComponentInParent<IdentidadeIA>()
            ?? GetComponentInChildren<IdentidadeIA>(true);
        if (identidadeIA != null && identidadeIA.teamID > 0) return identidadeIA.teamID;
        IdentidadeUnidade identidade = GetComponentInParent<IdentidadeUnidade>()
            ?? GetComponentInChildren<IdentidadeUnidade>(true);
        return identidade != null && identidade.teamID > 0 ? identidade.teamID : 1;
    }

    private static IdentidadeUnidade ResolverIdentidadeAlvo(Transform alvo)
    {
        if (alvo == null) return null;
        return alvo.GetComponent<IdentidadeUnidade>()
            ?? alvo.GetComponentInParent<IdentidadeUnidade>()
            ?? alvo.GetComponentInChildren<IdentidadeUnidade>(true);
    }

    bool Disparar(Transform alvo)
    {
        if (municaoAtual <= 0 || alvo == null) return false;
        if (!EstaAlvoVivo(alvo)) return false;
        if (AguardandoImpactoMissil()) return false;
        if (!EstaNoArcoFrontal(alvo)) return false;
        if (missilCacaPrefab == null)
        {
            if (!avisouPrefabMissilAusente)
            {
                Debug.LogError($"[LancadorMisselCaca] {name} tentou disparar sem missilCacaPrefab.", this);
                avisouPrefabMissilAusente = true;
            }
            return false;
        }

        Transform saida = transform;
        if (pontosDeSaida != null && pontosDeSaida.Length > 0)
        {
            int indiceAtual = Mathf.Clamp(indiceCano, 0, pontosDeSaida.Length - 1);
            saida = pontosDeSaida[indiceAtual] != null ? pontosDeSaida[indiceAtual] : transform;
            
            // Oculta mesh do míssil gasto
            Renderer[] renderers = (_renderersPorSaida != null && indiceAtual < _renderersPorSaida.Length) ? _renderersPorSaida[indiceAtual] : null;
            if (renderers == null && saida != null)
            {
                renderers = saida.GetComponentsInChildren<Renderer>(true);
            }

            if (renderers != null)
            {
                for (int i = 0, count = renderers.Length; i < count; i++)
                {
                    if (renderers[i] != null) renderers[i].enabled = false;
                }
            }

            indiceCano = (indiceCano + 1) % pontosDeSaida.Length;
        }

        GameObject missil = PoolDeObjetosCombate.Spawn(missilCacaPrefab, saida.position, saida.rotation);
        
        MisselCaca scriptVoo = missil.GetComponent<MisselCaca>();
        if (scriptVoo != null)
        {
            Vector3 velAtual = (_rb != null) ? _rb.linearVelocity : (transform.forward * 40f); 
            scriptVoo.IniciarAtaque(alvo.position, velAtual, alvo, gameObject);
            MissileThreatTracker.RegistrarLancamento(missil, this, alvo.position, alvo, Mathf.Max(velAtual.magnitude, MissileThreatTracker.EstimarVelocidade(missil)));

            _missilEmVoo = scriptVoo;
            _alvoDoMissilEmVoo = alvo;
            float distanciaTiro = Vector3.Distance(transform.position, alvo.position);
            float velocidadeCruzeiro = Mathf.Max(100f, scriptVoo.velocidadeMaxima);
            float prazoEstimado = scriptVoo.tempoQuedaLivre + (distanciaTiro / velocidadeCruzeiro) * 1.45f + 5f;
            _tempoExpiracaoEsperaImpacto = Time.time + Mathf.Clamp(prazoEstimado, 4f, 45f);
        }

        municaoAtual--;
        cronometroRecarga = tempoRecarga;
        
        if (_audioSource != null && _audioSource.enabled && _audioSource.gameObject.activeInHierarchy)
        {
            _audioSource.Play();
        }
        return true;
    }

    private bool AguardandoImpactoMissil()
    {
        if (_missilEmVoo == null) return false;

        if (!_missilEmVoo.EstaEmVooAtivo || Time.time > _tempoExpiracaoEsperaImpacto)
        {
            _missilEmVoo = null;
            _alvoDoMissilEmVoo = null;
            _tempoExpiracaoEsperaImpacto = -1f;
            return false;
        }

        if (!EstaAlvoVivo(_alvoDoMissilEmVoo))
        {
            _missilEmVoo = null;
            _alvoDoMissilEmVoo = null;
            _tempoExpiracaoEsperaImpacto = -1f;
            return false;
        }

        return true;
    }

    private bool EstaNoArcoFrontal(Transform alvo)
    {
        if (alvo == null) return false;

        Vector3 paraAlvo = alvo.position - transform.position;
        paraAlvo.y = 0f;
        Vector3 frentePlano = transform.forward;
        frentePlano.y = 0f;

        if (paraAlvo.sqrMagnitude < 1f || frentePlano.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        float cosLimite = Mathf.Cos(Mathf.Clamp(anguloMaximoDisparoFrontal, 10f, 90f) * Mathf.Deg2Rad);
        return Vector3.Dot(frentePlano.normalized, paraAlvo.normalized) >= cosLimite;
    }

    private void AtualizarPosturaPerseguicao(bool emMissao, bool alvoIAAtivo)
    {
        if (!emMissao
            || _vooModerno == null
            || _vooModerno.ordemParaRetorno
            || voltandoParaBase
            || ModoPassivoOficial
            || modoPassivo
            || municaoAtual <= 0)
        {
            LiberarPerseguicaoVoo();
            return;
        }

        Transform alvo = null;
        bool ordemExplicitamenteForcada = false;

        if (alvoIAAtivo && EstaAlvoVivo(_alvoIAForcado))
        {
            alvo = _alvoIAForcado;
            ordemExplicitamenteForcada = true;
        }
        else if (!ordensManuaisHud)
        {
            alvo = ResolverAlvoPerseguicaoAutomatica();
        }

        if (alvo == null)
        {
            LiberarPerseguicaoVoo();
            return;
        }

        if (_alvoPerseguicaoAtivo != alvo)
        {
            _alvoPerseguicaoAtivo = alvo;
            _emManobraAfastamento = false;
        }

        Vector3 paraAlvoPlano = alvo.position - transform.position;
        paraAlvoPlano.y = 0f;
        float distPlanoSqr = paraAlvoPlano.sqrMagnitude;

        float raioRadar = Mathf.Max(raioDeDeteccao, ObterRaioMinimoGeracao());
        if (!ordemExplicitamenteForcada && distPlanoSqr > (raioRadar * 1.15f) * (raioRadar * 1.15f))
        {
            LiberarPerseguicaoVoo();
            return;
        }

        IdentidadeUnidade idAlvo = ResolverIdentidadeAlvo(alvo);
        float standOffBase = EhAlvoAereo(alvo, idAlvo) ? distanciaMinimaStandOffAereo : distanciaMinimaStandOffSuperficie;
        float standOff = Mathf.Clamp(standOffBase, 250f, raioRadar * 0.5f);
        float distSaidaAfastamento = standOff * 1.8f;

        if (!_emManobraAfastamento && distPlanoSqr < standOff * standOff)
        {
            _emManobraAfastamento = true;
        }
        else if (_emManobraAfastamento && distPlanoSqr >= distSaidaAfastamento * distSaidaAfastamento)
        {
            _emManobraAfastamento = false;
        }

        Vector3 destino;
        if (_emManobraAfastamento)
        {
            Vector3 dirSaida = distPlanoSqr > 1f ? -paraAlvoPlano.normalized : -transform.forward;
            dirSaida.y = 0f;
            if (dirSaida.sqrMagnitude < 0.0001f) dirSaida = Vector3.forward;
            dirSaida.Normalize();
            Vector3 lateral = new Vector3(-dirSaida.z, 0f, dirSaida.x);
            destino = alvo.position + dirSaida * (standOff * 2.1f) + lateral * (standOff * 0.65f);
        }
        else
        {
            destino = alvo.position;
        }

        float altitudeDesejada = Mathf.Max(alvo.position.y, _vooModerno.altitudeVoo);
        if (!float.IsNaN(destino.x) && !float.IsNaN(destino.y) && !float.IsNaN(destino.z))
        {
            destino.y = altitudeDesejada;
            _vooModerno.alvoPrioritarioIA = true;
            _vooModerno.centroDaPatrulha = destino;
            _vooModerno.alvoGPSVoo = destino;
            _controlandoPerseguicaoVoo = true;
        }
    }

    private Transform ResolverAlvoPerseguicaoAutomatica()
    {
        if (AguardandoImpactoMissil() && EstaAlvoVivo(_alvoDoMissilEmVoo))
        {
            return _alvoDoMissilEmVoo;
        }

        Transform melhorDaLista = null;
        int melhorPrioridadeLista = int.MaxValue;
        for (int i = 0; i < inimigosNaArea.Count; i++)
        {
            AlvoDetectado candidato = inimigosNaArea[i];
            if (!EstaAlvoVivo(candidato.transform)) continue;
            if (somenteAlvosAutorizadosHud && !_alvosAutorizadosHud.Contains(candidato.transform)) continue;
            IdentidadeUnidade id = ResolverIdentidadeAlvo(candidato.transform);
            if (!ContextoTerritorialDiplomatico.PodeDispararPorHostilidadeOuGuerra(_meuTime, id)) continue;

            melhorDaLista = candidato.transform;
            melhorPrioridadeLista = candidato.prioridade;
            break;
        }

        if (EstaAlvoVivo(_alvoPerseguicaoAtivo))
        {
            IdentidadeUnidade idAtual = ResolverIdentidadeAlvo(_alvoPerseguicaoAtivo);
            if (ContextoTerritorialDiplomatico.PodeDispararPorHostilidadeOuGuerra(_meuTime, idAtual))
            {
                int prioAtual = ObterPrioridadeAlvo(_alvoPerseguicaoAtivo, idAtual);
                if (melhorDaLista == null || prioAtual <= melhorPrioridadeLista)
                {
                    return _alvoPerseguicaoAtivo;
                }
            }
        }

        return melhorDaLista;
    }

    private void LiberarPerseguicaoVoo()
    {
        if (_controlandoPerseguicaoVoo && _vooModerno != null)
        {
            _vooModerno.alvoPrioritarioIA = false;
        }
        _controlandoPerseguicaoVoo = false;
        _alvoPerseguicaoAtivo = null;
        _emManobraAfastamento = false;
    }

    private static bool EstaAlvoVivo(Transform alvo)
    {
        if (alvo == null || !alvo.gameObject.activeInHierarchy) return false;
        if (!ControleSubmarino.PodeSerAlvoConvencional(alvo)) return false;

        SistemaDeDanos danos = alvo.GetComponent<SistemaDeDanos>()
            ?? alvo.GetComponentInParent<SistemaDeDanos>()
            ?? alvo.GetComponentInChildren<SistemaDeDanos>(true);
        return danos == null || danos.vidaAtual > 0f;
    }

    private bool SolicitarAtaquePeloRadar(Transform alvo)
    {
        if (alvo == null || _vooModerno == null)
        {
            return false;
        }

        // O botão do radar precisa comandar a interceptação, não apenas soltar
        // um míssil enquanto o avião continua no pátio ou seguindo outra rota.
        if (_unidadeBase != null)
        {
            bool ordemAceita = _unidadeBase.EmitirMissaoAereaOfensiva(alvo.position, alvo);
            if (ordemAceita)
            {
                modoPassivo = false;
                if (_alvoIAForcado != alvo)
                {
                    DefinirAlvoIA(alvo, alvo.position, DuracaoMinimaAlvoForcado);
                }
            }
            if (!ordemAceita && debugLogs)
            {
                Debug.LogWarning(
                    $"[Radar] Ataque recusado para {name}: {_vooModerno.UltimoMotivoRecusaOrdem}",
                    this);
            }

            return ordemAceita;
        }

        // Compatibilidade com aeronaves antigas sem ControleUnidade.
        if (!_vooModerno.ReceberOrdemManual(alvo.position))
        {
            if (debugLogs)
            {
                Debug.LogWarning(
                    $"[Radar] Ataque recusado para {name}: {_vooModerno.UltimoMotivoRecusaOrdem}",
                    this);
            }

            return false;
        }

        modoPassivo = false;
        DefinirAlvoIA(alvo, alvo.position, DuracaoMinimaAlvoForcado);
        return true;
    }

    public void RecarregarCompletoNaBase()
    {
        municaoAtual = municaoMaxima;
        indiceCano = 0;
        voltandoParaBase = false;

        if (_renderersPorSaida != null && _renderersPorSaida.Length > 0)
        {
            for (int i = 0, count = _renderersPorSaida.Length; i < count; i++)
            {
                Renderer[] renderers = _renderersPorSaida[i];
                if (renderers == null) continue;
                for (int j = 0, jCount = renderers.Length; j < jCount; j++)
                {
                    if (renderers[j] != null) renderers[j].enabled = true;
                }
            }
        }
        else if (pontosDeSaida != null)
        {
            for (int i = 0, count = pontosDeSaida.Length; i < count; i++)
            {
                Transform tf = pontosDeSaida[i];
                if (tf == null) continue;
                Renderer[] renderers = tf.GetComponentsInChildren<Renderer>(true);
                for (int j = 0, jCount = renderers.Length; j < jCount; j++)
                {
                    if (renderers[j] != null) renderers[j].enabled = true;
                }
            }
        }
    }

    /// <summary>
    /// Resolve o alcance mínimo de detecção/lançamento conforme a geração do caça,
    /// preservando valores maiores configurados manualmente no Inspector.
    /// </summary>
    private float ObterRaioMinimoGeracao()
    {
        if (geracaoCaca == GeracaoCaca.QuartaGeracao) return RaioDeteccaoQuartaGeracao;
        if (geracaoCaca == GeracaoCaca.TerceiraGeracao) return RaioDeteccaoTerceiraGeracao;

        string nomeModelo = gameObject != null ? gameObject.name : string.Empty;
        if (NomeContem(nomeModelo, "F-16") || NomeContem(nomeModelo, "F16")
            || NomeContem(nomeModelo, "G18")
            || NomeContem(nomeModelo, "Su20") || NomeContem(nomeModelo, "Su-20")
            || NomeContem(nomeModelo, "Su35") || NomeContem(nomeModelo, "Su-35") || NomeContem(nomeModelo, "su-35"))
        {
            return RaioDeteccaoQuartaGeracao;
        }

        if (NomeContem(nomeModelo, "Mirage")
            || NomeContem(nomeModelo, "Su11") || NomeContem(nomeModelo, "Su-11")
            || NomeContem(nomeModelo, "G15"))
        {
            return RaioDeteccaoTerceiraGeracao;
        }

        return RaioDeteccaoPadraoFallback;
    }

    private static bool NomeContem(string texto, string termo)
    {
        return !string.IsNullOrEmpty(texto)
               && !string.IsNullOrEmpty(termo)
               && texto.IndexOf(termo, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void OnGUI()
    {
        if (IndicadorUnidadeVisibilidade.ExisteMenuOuModoDeInterfaceAberto
            || (_vooModerno != null && _vooModerno.EstaEstacionadoEDesligado))
        {
            return;
        }

        bool radarAtivoVisualmente = false;

        if (_unidadeBase != null && _unidadeBase.selecionado) radarAtivoVisualmente = true;

        if (!radarAtivoVisualmente && _vooModerno != null && _vooModerno.aeroportoOrigem != null)
        {
            if (_vooModerno.aeroportoOrigem.aviaoSelecionadoParaMissao == _vooModerno) radarAtivoVisualmente = true;
        }

        // Reseta estado fechar/minimizar se abriu de novo
        if (radarAtivoVisualmente && !ultimoEstadoRadar)
            radarFechado = false;
        ultimoEstadoRadar = radarAtivoVisualmente;

        if (!radarAtivoVisualmente || radarFechado) return;
        if (inimigosNaArea.Count == 0) return;

        float largura = 385;
        float altura = 385;
        float x = Screen.width - largura - 20; 
        float y = (Screen.height - altura) / 2; 

        if (radarMinimizado)
        {
            GUI.Box(new Rect(x, y, largura, 30), "📡 RADAR: ALVOS DETECTADOS");
            if (GUI.Button(new Rect(x + largura - 50, y + 5, 20, 20), "▼")) radarMinimizado = false;
            if (GUI.Button(new Rect(x + largura - 25, y + 5, 20, 20), "X")) radarFechado = true;
            return;
        }

        GUI.Box(new Rect(x, y, largura, altura), "📡 RADAR: ALVOS DETECTADOS");
        
        if (GUI.Button(new Rect(x + largura - 50, y + 5, 20, 20), "▲")) radarMinimizado = true;
        if (GUI.Button(new Rect(x + largura - 25, y + 5, 20, 20), "X")) radarFechado = true;

        GUI.Label(new Rect(x + 15, y + 30, 200, 20), $"<color=yellow>Mísseis Restantes: {municaoAtual} / {municaoMaxima}</color>");

        if (municaoAtual <= 0)
            GUI.Label(new Rect(x + 15, y + 50, 320, 20), "<color=red>AERONAVE SEM MÍSSEIS - RETORNE À BASE!</color>");

        GUI.Label(new Rect(x + 15, y + 75, 200, 20), $"Hostis na Área: {inimigosNaArea.Count}");

        scrollPosition = GUI.BeginScrollView(
            new Rect(x + 10, y + 100, largura - 20, altura - 110), 
            scrollPosition, 
            new Rect(0, 0, largura - 40, inimigosNaArea.Count * 45)
        );

        for (int i = 0, count = inimigosNaArea.Count; i < count; i++)
        {
            AlvoDetectado alvo = inimigosNaArea[i];
            if (alvo.transform == null) continue;

            float slotY = i * 45;
            
            GUI.Label(new Rect(5, slotY, 150, 20), $"<b>{alvo.nome}</b>");
            GUI.Label(new Rect(5, slotY + 20, 100, 20), $"{alvo.distancia:F0}m");

            if (GUI.Button(new Rect(140, slotY + 5, 80, 30), "SEGUIR"))
            {
                bool aceitouSeguimento = _unidadeBase != null
                    ? _unidadeBase.EmitirOrdemSeguir(alvo.transform)
                    : _vooModerno != null && _vooModerno.ReceberOrdemManual(alvo.transform.position);
                if (!aceitouSeguimento && debugLogs)
                {
                    Debug.LogWarning($"[Radar] Seguimento recusado para {name}: o controlador não aceitou a ordem.");
                }
            }

            GUI.enabled = (municaoAtual > 0 && cronometroRecarga <= 0);
            if (GUI.Button(new Rect(225, slotY + 5, 80, 30), "ATACAR")
                && SolicitarAtaquePeloRadar(alvo.transform))
            {
                // A ordem já foi entregue ao controlador. Ele mantém o alvo
                // durante a decolagem/aproximação e o lançador dispara no alcance.
                radarFechado = true;
            }
            GUI.enabled = true;
        }

        GUI.EndScrollView();
    }
}
