using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// OTIMIZADOR DE PERFORMANCE PARA UNIDADES DA IA
/// Análogo ao OtimizadorLOD.cs do jogador, mas aplicado a scripts de comportamento.
/// Reduz a frequência de Update de sensores, NavMesh e IA quando a unidade está distante.
/// Adicionado automaticamente pelo IA_General_Pro ao registrar qualquer unidade.
/// </summary>
[DefaultExecutionOrder(100)]
public class IA_OtimizadorUnidade : MonoBehaviour
{
    [Header("Distâncias de Throttling")]
    [Tooltip("Acima desta distância, a IA opera em modo 'Médio' (frequência reduzida).")]
    public float distanciaMedio = 150f;

    [Tooltip("Acima desta distância, a IA opera em modo 'Distante' (frequência mínima). Scripts visuais pausados.")]
    public float distanciaDistante = 350f;

    [Tooltip("Acima desta distância, renderers são desligados completamente (Culling). 0 = desabilitado.")]
    public float distanciaCulling = 600f;

    [Header("Modo Atual (Debug)")]
    [SerializeField] private string _modoAtual = "Proximo";

    // --- Cache de Componentes ---
    private NavMeshAgent _agente;
    private ControleUnidade _controle;
    private SistemaDeDanos _danos;
    private SistemaDeTiro _tiro;
    private Renderer[] _renderers;
    private Animator[] _animators;
    private List<MonoBehaviour> _scriptsHeavy = new List<MonoBehaviour>();

    // --- Estado Interno ---
    private NivelProcessamentoTatico _nivelAtual = NivelProcessamentoTatico.Proximo;
    private bool _estaCulled = false;
    private bool _inicializado = false;
    private float _originalNavSpeed = -1f;
    private float _originalUpdateInterval = -1f;
    private Transform _camTransform;

    // Taxas de atualização do NavMesh por nível (em segundos entre atualizações)
    private const float NAV_INTERVAL_PROXIMO  = 0.10f;
    private const float NAV_INTERVAL_MEDIO    = 0.35f;
    private const float NAV_INTERVAL_DISTANTE = 0.80f;

    void Start()
    {
        _agente  = GetComponent<NavMeshAgent>();
        _controle = GetComponent<ControleUnidade>();
        _danos   = GetComponent<SistemaDeDanos>();
        _tiro    = GetComponent<SistemaDeTiro>();
        _renderers = GetComponentsInChildren<Renderer>();
        _animators = GetComponentsInChildren<Animator>();

        if (_agente != null)
        {
            _originalNavSpeed = _agente.speed;
        }

        // Coleta scripts pesados (sensores, tiro) para reduzir frequência
        var sensor = GetComponent<SistemaDeTiro>();
        if (sensor != null) _scriptsHeavy.Add(sensor);

        // Busca câmera com delay para garantir que a cena carregou
        if (Camera.main != null) _camTransform = Camera.main.transform;

        _inicializado = true;

        // Randomiza o início para distribuir o custo entre frames
        InvokeRepeating(nameof(AtualizarNivel), Random.Range(0f, 1.5f), 0.5f);
    }

    void AtualizarNivel()
    {
        if (!_inicializado) return;

        // Tenta obter câmera se ainda não tem
        if (_camTransform == null)
        {
            if (Camera.main != null) _camTransform = Camera.main.transform;
            else return;
        }

        float distSqr = (transform.position - _camTransform.position).sqrMagnitude;

        // --- CULLING total (mais longe que distanciaCulling) ---
        if (distanciaCulling > 0f && distSqr > distanciaCulling * distanciaCulling)
        {
            if (!_estaCulled) AplicarCulling(true);
            return;
        }
        else if (_estaCulled)
        {
            AplicarCulling(false);
        }

        // --- Determinar nível de LOD ---
        NivelProcessamentoTatico novoNivel;

        if (distSqr >= distanciaDistante * distanciaDistante)
            novoNivel = NivelProcessamentoTatico.Distante;
        else if (distSqr >= distanciaMedio * distanciaMedio)
            novoNivel = NivelProcessamentoTatico.Medio;
        else
            novoNivel = NivelProcessamentoTatico.Proximo;

        if (novoNivel != _nivelAtual)
        {
            _nivelAtual = novoNivel;
            AplicarNivel(novoNivel);
        }
    }

    void AplicarNivel(NivelProcessamentoTatico nivel)
    {
        _modoAtual = nivel.ToString();

        switch (nivel)
        {
            case NivelProcessamentoTatico.Proximo:
                // Plena performance — tudo ligado
                AjustarNavMesh(NAV_INTERVAL_PROXIMO, 1.0f);
                AjustarAnimators(true, 1f);
                AjustarScriptsPesados(true);
                break;

            case NivelProcessamentoTatico.Medio:
                // Reduz frequência de pathfinding e animações
                AjustarNavMesh(NAV_INTERVAL_MEDIO, 1.0f);
                AjustarAnimators(true, 0.5f); // Animação em câmera lenta (metade da taxa)
                AjustarScriptsPesados(true);
                break;

            case NivelProcessamentoTatico.Distante:
                // Modo econômico: NavMesh raro, animadores pausados
                AjustarNavMesh(NAV_INTERVAL_DISTANTE, 0.5f);
                AjustarAnimators(false, 0f); // Animações pausadas
                AjustarScriptsPesados(false);
                break;
        }
    }

    void AplicarCulling(bool culling)
    {
        _estaCulled = culling;

        // Liga/Desliga renderers
        foreach (var r in _renderers)
        {
            if (r != null) r.enabled = !culling;
        }

        if (culling)
        {
            // No culling: pausar tudo, mas manter NavMesh mínimo para não "pular"
            AjustarNavMesh(1.5f, 0.25f);
            AjustarAnimators(false, 0f);
            AjustarScriptsPesados(false);
            _modoAtual = "Culled";
        }
        else
        {
            // Saindo do culling: volta ao modo distante primeiro
            AplicarNivel(_nivelAtual);
        }
    }

    void AjustarNavMesh(float intervaloAtualizacao, float fatorVelocidade)
    {
        if (_agente == null || !_agente.isActiveAndEnabled) return;

        // Ajusta a velocidade de aceleração (menor = menos cálculo de steering)
        if (_originalNavSpeed > 0f)
            _agente.speed = _originalNavSpeed * fatorVelocidade;

        // AutoBraking e obstáculo avoidance — reduz custo quando distante
        _agente.obstacleAvoidanceType = (fatorVelocidade < 0.6f)
            ? ObstacleAvoidanceType.NoObstacleAvoidance
            : ObstacleAvoidanceType.LowQualityObstacleAvoidance;
    }

    void AjustarAnimators(bool ligado, float velocidade)
    {
        foreach (var anim in _animators)
        {
            if (anim == null) continue;
            anim.enabled = ligado;
            if (ligado && velocidade >= 0f)
                anim.speed = velocidade;
        }
    }

    void AjustarScriptsPesados(bool ligado)
    {
        foreach (var script in _scriptsHeavy)
        {
            if (script != null && script.enabled != ligado)
                script.enabled = ligado;
        }
    }

    void OnDestroy()
    {
        // Garante que os animadores voltam ao normal antes de destruir
        AjustarAnimators(true, 1f);
    }
}
