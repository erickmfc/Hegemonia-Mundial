using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(ControleAviao))]
// [RequireComponent(typeof(ControleUnidade))] // Removido para permitir deletar o script antigo
[RequireComponent(typeof(SistemaDeDanos))]
public class LancadorMisselCaca : MonoBehaviour
{
    [Header("Configuração de Munição e Vida")]
    public int municaoAtual = 2;    
    public int municaoMaxima = 4;
    
    [Header("Configuração de Lançamento")]
    public Transform[] pontosDeSaida; 
    public GameObject missilCacaPrefab; 
    public float tempoRecarga = 2.0f;
    public float raioDeDeteccao = 600f;

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

    // Detecção
    public class AlvoDetectado 
    {
        public Transform transform;
        public string nome;
        public float distancia;
        public int prioridade;
        public bool ehAereo;
    }
    private List<AlvoDetectado> inimigosNaArea = new List<AlvoDetectado>();
    private float tempoUltimoScan = 0f;

    // Interface
    private Vector2 scrollPosition;
    private bool radarMinimizado = false;
    private bool radarFechado = false;
    private bool ultimoEstadoRadar = false;

    // --- CACHE: Busca O(1) de unidades ---
    private static readonly List<IdentidadeUnidade> _bufferGlobais = new List<IdentidadeUnidade>(512);
    private readonly HashSet<Transform> _alvosJaVistos = new HashSet<Transform>();

    void Start()
    {
        if (raioDeDeteccao < 1500f) raioDeDeteccao = 1500f; // Garante que alcance bombardeiros muito altos
        _unidadeBase = GetComponent<ControleUnidade>();
        _vooModerno = GetComponent<ControleAviao>();
        _sistemaDanos = GetComponent<SistemaDeDanos>();
        _rb = GetComponent<Rigidbody>();
        _audioSource = GetComponent<AudioSource>();
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
        
        _meuTime = GetComponent<IdentidadeIA>()?.teamID ?? GetComponent<IdentidadeUnidade>()?.teamID ?? 1;
        // Evita que todos os caças escaneiem e disparem no mesmo frame.
        tempoUltimoScan = Time.time + UnityEngine.Random.Range(0.05f, 0.85f);
    }

    void Update()
    {
        if (cronometroRecarga > 0) cronometroRecarga -= Time.deltaTime;

        bool radarAtivoVisualmente = false;
        if (_unidadeBase != null && _unidadeBase.selecionado) radarAtivoVisualmente = true;
        if (!radarAtivoVisualmente && _vooModerno != null && _vooModerno.aeroportoOrigem != null)
        {
            if (_vooModerno.aeroportoOrigem.aviaoSelecionadoParaMissao == _vooModerno) radarAtivoVisualmente = true;
        }

        bool alvoIAAtivo = _alvoIAForcado != null && Time.time <= _tempoValidadeAlvoIA;
        bool emMissao = _vooModerno != null && _vooModerno.estadoAtual == ControleAviao.EstadoAviao.EmMissao;
        bool deveEscanear = radarAtivoVisualmente || emMissao || alvoIAAtivo;

        // Detecção de Inimigos (Radar) — Apenas a cada 1 segundo pra não pesar
        if (deveEscanear && Time.time > tempoUltimoScan + 1.0f)
        {
            tempoUltimoScan = Time.time;
            EscanearArea(radarAtivoVisualmente);
            ProcessarPatrulhaAutomatica();
        }

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

                if (voltandoParaBase)
                {
                    voltandoParaBase = false;
                    if (_vooModerno.aeroportoOrigem != null)
                    {
                        _vooModerno.IniciarMissaoCompleta(pontoPatrulha);
                        if (debugLogs)
                        {
                            Debug.Log($"[Base] {gameObject.name} voltando para a patrulha automatica.");
                        }
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
        if (_vooModerno == null || _vooModerno.estadoAtual != ControleAviao.EstadoAviao.EmMissao) return;
        if (voltandoParaBase) return;

        // Alvo IA forçado sempre tem prioridade, mesmo se estiver em patrulha/recon.
        if (TentarDisparoContraAlvoIA()) return;

        if (modoPassivo)
        {
            if (inimigosNaArea.Count == 0) return;

            AlvoDetectado candidato = inimigosNaArea[0];
            if (candidato == null || candidato.transform == null) return;

            // Em modo passivo, reage defensivamente: ameaça aérea ou muito perto.
            if (!candidato.ehAereo && candidato.distancia > 450f) return;
        }

        if (municaoAtual > 0 && cronometroRecarga <= 0 && inimigosNaArea.Count > 0)
        {
            AlvoDetectado alvo = inimigosNaArea[0];
            if (alvo != null && alvo.transform != null)
                Disparar(alvo.transform);
        }

        // Volta para a base se ficar sem munição
        if (municaoAtual <= 0 && _vooModerno.aeroportoOrigem != null)
        {
            pontoPatrulha = _vooModerno.alvoGPSVoo;
            voltandoParaBase = true;
            _vooModerno.ComandoRetornarBase();
            if (debugLogs)
            {
                Debug.Log($"[Radar] {gameObject.name} sem municao. Retornando para a base via Aeroporto.");
            }
        }
    }

    public void DefinirAlvoIA(Transform alvo, Vector3 alvoFallback, float duracaoSegundos = 4f)
    {
        _alvoIAForcado = alvo;
        _ultimoPontoAlvoIA = alvo != null ? alvo.position : alvoFallback;
        _tempoValidadeAlvoIA = Time.time + Mathf.Max(1.5f, duracaoSegundos);
        modoPassivo = false;

        if (_vooModerno != null && _vooModerno.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
        {
            Vector3 foco = _ultimoPontoAlvoIA;
            if (foco.y < 60f) foco.y = 60f;
            _vooModerno.alvoPrioritarioIA = true;
            _vooModerno.centroDaPatrulha = foco;
            _vooModerno.alvoGPSVoo = foco;
        }
    }

    public bool TentarDisparoIA(Transform alvo, Vector3 alvoFallback, float duracaoSegundos = 4f)
    {
        DefinirAlvoIA(alvo, alvoFallback, duracaoSegundos);
        return TentarDisparoContraAlvoIA();
    }

    bool TentarDisparoContraAlvoIA()
    {
        if (_alvoIAForcado == null || Time.time > _tempoValidadeAlvoIA) return false;
        if (_vooModerno == null || _vooModerno.estadoAtual != ControleAviao.EstadoAviao.EmMissao) return false;
        if (voltandoParaBase || modoPassivo) return false;
        if (municaoAtual <= 0 || cronometroRecarga > 0f) return false;

        float alcanceMaximo = Mathf.Max(raioDeDeteccao, 1500f) * 1.2f;
        if ((_alvoIAForcado.position - transform.position).sqrMagnitude > alcanceMaximo * alcanceMaximo)
        {
            return false;
        }

        Disparar(_alvoIAForcado);
        _ultimoPontoAlvoIA = _alvoIAForcado.position;
        _tempoValidadeAlvoIA = Time.time + 1.5f;
        return true;
    }

    bool EhAlvoAereo(Transform alvoTransform, IdentidadeUnidade idUnidade)
    {
        if (alvoTransform == null) return false;

        string nomeAlvo = alvoTransform.name;

        return alvoTransform.position.y > 15f ||
               alvoTransform.GetComponentInParent<ControleAviao>() != null ||
               alvoTransform.GetComponentInParent<ControleAviaoCaca>() != null ||
               alvoTransform.GetComponentInParent<AviaoBombardeiro>() != null ||
               alvoTransform.GetComponentInParent<Helicoptero>() != null ||
               (idUnidade != null && idUnidade.tipoUnidade == TipoUnidade.Aereo) ||
               NomeContem(nomeAlvo, "aviao") ||
               NomeContem(nomeAlvo, "caca") ||
               NomeContem(nomeAlvo, "jato") ||
               NomeContem(nomeAlvo, "heli") ||
               NomeContem(nomeAlvo, "drone") ||
               NomeContem(nomeAlvo, "vap") ||
               NomeContem(nomeAlvo, "bombard") ||
               NomeContem(nomeAlvo, "bombardeiro") ||
               NomeContem(nomeAlvo, "bomber") ||
               alvoTransform.tag == "Areo" ||
               alvoTransform.tag == "Aereo";
    }

    int ObterPrioridadeAlvo(Transform alvoTransform, IdentidadeUnidade idUnidade)
    {
        if (alvoTransform == null) return int.MaxValue;

        string nomeAlvo = alvoTransform.name;
        bool ehBombardeiro = alvoTransform.GetComponentInParent<AviaoBombardeiro>() != null ||
                             NomeContem(nomeAlvo, "bombard") ||
                             NomeContem(nomeAlvo, "bombardeiro") ||
                             NomeContem(nomeAlvo, "bomber");

        if (ehBombardeiro) return 0;
        if (alvoTransform.GetComponentInParent<ControleAviaoCaca>() != null || NomeContem(nomeAlvo, "caca") || NomeContem(nomeAlvo, "jato")) return 1;
        if (EhAlvoAereo(alvoTransform, idUnidade)) return 2;
        return 3;
    }

    void EscanearArea(bool radarAtivoVisualmente)
    {
        inimigosNaArea.Clear();
        _alvosJaVistos.Clear();

        // OTIMIZAÇÃO MAXIMA: Ao invés de usar a Física da Unity (OverlapSphere) que pesa a CPU em raios gigantes,
        // agora buscamos direto na memória do jogo apenas o que de fato é Unidade Militar.
        RegistroEntidadesJogo.FillUnidades(_bufferGlobais);
        float raioSqr = raioDeDeteccao * raioDeDeteccao;

        for (int i = 0; i < _bufferGlobais.Count; i++)
        {
            IdentidadeUnidade idAlvo = _bufferGlobais[i];
            if (idAlvo == null || idAlvo.teamID == _meuTime) continue;
            if (!ControleSubmarino.PodeSerAlvoConvencional(idAlvo.transform)) continue;

            SistemaDeDanos alvoDanos = idAlvo.GetComponent<SistemaDeDanos>();
            if (alvoDanos == null || alvoDanos.vidaAtual <= 0) continue;

            Transform alvoTransform = alvoDanos.transform;
            
            float distSqr = (transform.position - alvoTransform.position).sqrMagnitude;
            if (distSqr > raioSqr) continue;

            if (!_alvosJaVistos.Add(alvoTransform)) continue;

            AlvoDetectado novo = new AlvoDetectado();
            novo.transform = alvoTransform;
            // Cria string apenas se o menu visual estiver ativo para salvar GC
            if (radarAtivoVisualmente) novo.nome = (alvoTransform.name.Contains("(Clone)")) ? alvoTransform.name.Replace("(Clone)", "") : alvoTransform.name;
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

    void Disparar(Transform alvo)
    {
        if (municaoAtual <= 0 || missilCacaPrefab == null || alvo == null) return;

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
            scriptVoo.IniciarAtaque(alvo.position, velAtual, alvo);
            MissileThreatTracker.RegistrarLancamento(missil, this, alvo.position, alvo, Mathf.Max(velAtual.magnitude, MissileThreatTracker.EstimarVelocidade(missil)));
        }

        municaoAtual--;
        cronometroRecarga = tempoRecarga;
        
        if (_audioSource != null) _audioSource.Play();
    }

    private static bool NomeContem(string texto, string termo)
    {
        return !string.IsNullOrEmpty(texto)
               && !string.IsNullOrEmpty(termo)
               && texto.IndexOf(termo, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void OnGUI()
    {
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
                if (_vooModerno != null) _vooModerno.alvoGPSVoo = alvo.transform.position;
            }

            GUI.enabled = (municaoAtual > 0 && cronometroRecarga <= 0);
            if (GUI.Button(new Rect(225, slotY + 5, 80, 30), "ATACAR"))
                Disparar(alvo.transform);
            GUI.enabled = true;
        }

        GUI.EndScrollView();
    }
}
