using System.Collections.Generic;
using UnityEngine;

public enum CategoriaBudgetGameplay
{
    Terra,
    Naval,
    Aereo,
    Sensor,
    Pathfinding,
    Arma,
    UI,
    IA,
    Formacao,
    Logistica
}

public enum NivelProcessamentoTatico
{
    Proximo,
    Medio,
    Distante
}

[System.Serializable]
public sealed class EstadoOtimizacaoTatica
{
    public NivelProcessamentoTatico nivel = NivelProcessamentoTatico.Proximo;
    public float proximoTickLogica;
    public float proximoTickSensor;
    public float proximoTickPath;
    public float proximoTickWatchdog;
    public float proximoTickUI;
    public bool estaSelecionada;
    public bool estaEngajada;
    public bool heroica;
    public bool distante;
}

public interface ITickGameplayUnit
{
    Object TickOwner { get; }
    EstadoOtimizacaoTatica EstadoOtimizacao { get; }
    void TickGameplay(float now, float deltaTime);
}

public static class InfraPerformanceGameplay
{
    private sealed class CachePercepcao
    {
        public float expiraEm;
        public readonly List<Transform> resultados = new List<Transform>(16);
    }

    private static readonly List<ControleUnidade> BufferControles = new List<ControleUnidade>(512);
    private static readonly Dictionary<long, CachePercepcao> CacheInimigosPorSetor = new Dictionary<long, CachePercepcao>(128);
    private static Transform _cameraCache;
    private static int _ultimoFrameCamera = -1;

    public static float ObterBudgetMs(CategoriaBudgetGameplay categoria)
    {
        switch (categoria)
        {
            case CategoriaBudgetGameplay.Terra: return 1.60f;
            case CategoriaBudgetGameplay.Naval: return 1.90f;
            case CategoriaBudgetGameplay.Aereo: return 1.35f;
            case CategoriaBudgetGameplay.Sensor: return 1.10f;
            case CategoriaBudgetGameplay.Pathfinding: return 1.20f;
            case CategoriaBudgetGameplay.Arma: return 0.95f;
            case CategoriaBudgetGameplay.UI: return 0.85f;
            case CategoriaBudgetGameplay.IA: return 4.50f;
            case CategoriaBudgetGameplay.Formacao: return 0.70f;
            case CategoriaBudgetGameplay.Logistica: return 0.75f;
            default: return 1f;
        }
    }

    public static string ObterChaveMetrica(CategoriaBudgetGameplay categoria)
    {
        switch (categoria)
        {
            case CategoriaBudgetGameplay.Terra: return "land_unit_update_ms";
            case CategoriaBudgetGameplay.Naval: return "naval_unit_update_ms";
            case CategoriaBudgetGameplay.Aereo: return "air_unit_update_ms";
            case CategoriaBudgetGameplay.Sensor: return "sensor_update_ms";
            case CategoriaBudgetGameplay.Pathfinding: return "pathfinding_ms";
            case CategoriaBudgetGameplay.Arma: return "weapon_update_ms";
            case CategoriaBudgetGameplay.UI: return "ui_rebuild_ms";
            case CategoriaBudgetGameplay.IA: return "world_refresh_ms";
            case CategoriaBudgetGameplay.Formacao: return "formation_update_ms";
            case CategoriaBudgetGameplay.Logistica: return "production_ms";
            default: return string.Empty;
        }
    }

    public static long MarcarInicioMedicao()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    public static void RegistrarTempoDecorrido(CategoriaBudgetGameplay categoria, long inicioTimestamp)
    {
        long delta = System.Diagnostics.Stopwatch.GetTimestamp() - inicioTimestamp;
        if (delta <= 0)
        {
            return;
        }

        float elapsedMs = (float)(delta * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        RegistrarTempo(categoria, elapsedMs);
    }

    public static void RegistrarTempo(CategoriaBudgetGameplay categoria, float elapsedMs)
    {
        if (elapsedMs <= 0f)
        {
            return;
        }

        string chave = ObterChaveMetrica(categoria);
        if (!string.IsNullOrEmpty(chave))
        {
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo(chave, elapsedMs);
        }
    }

    public static void AtualizarEstadoBase(
        EstadoOtimizacaoTatica estado,
        Transform unidade,
        bool selecionada,
        bool engajada,
        bool heroica = false,
        float distanciaMedio = 140f,
        float distanciaDistante = 320f)
    {
        if (estado == null || unidade == null)
        {
            return;
        }

        estado.estaSelecionada = selecionada;
        estado.estaEngajada = engajada;
        estado.heroica = heroica;

        Transform camera = ObterCameraPrincipal();
        if (camera == null)
        {
            estado.nivel = NivelProcessamentoTatico.Proximo;
            estado.distante = false;
            return;
        }

        float distSqr = (unidade.position - camera.position).sqrMagnitude;
        float medioSqr = distanciaMedio * distanciaMedio;
        float distanteSqr = distanciaDistante * distanciaDistante;

        if (selecionada || heroica)
        {
            estado.nivel = NivelProcessamentoTatico.Proximo;
            estado.distante = false;
        }
        else if (distSqr >= distanteSqr)
        {
            estado.nivel = NivelProcessamentoTatico.Distante;
            estado.distante = true;
        }
        else if (distSqr >= medioSqr)
        {
            estado.nivel = NivelProcessamentoTatico.Medio;
            estado.distante = false;
        }
        else
        {
            estado.nivel = NivelProcessamentoTatico.Proximo;
            estado.distante = false;
        }
    }

    public static float ResolverIntervalo(
        float intervaloBase,
        EstadoOtimizacaoTatica estado,
        bool tickPesado = false,
        bool preservarResposta = false)
    {
        float multiplicador = 1f;

        if (estado != null)
        {
            switch (estado.nivel)
            {
                case NivelProcessamentoTatico.Medio:
                    multiplicador *= 1.35f;
                    break;
                case NivelProcessamentoTatico.Distante:
                    multiplicador *= 2.10f;
                    break;
            }

            if (estado.estaSelecionada || estado.estaEngajada)
            {
                multiplicador *= preservarResposta ? 0.85f : 1f;
            }

            if (estado.heroica)
            {
                multiplicador *= 0.90f;
            }
        }

        if (tickPesado)
        {
            multiplicador *= 1.25f;
        }

        if (DiagnosticoDesempenhoJogo.RuntimeSobPressao())
        {
            multiplicador *= 1.35f;
        }

        if (DiagnosticoDesempenhoJogo.RuntimeSaturado())
        {
            multiplicador *= 1.75f;
        }

        return Mathf.Max(0.05f, intervaloBase * multiplicador);
    }

    public static bool DeveExecutar(Object owner, ref float proximoTick, float intervalo)
    {
        float agora = Time.unscaledTime;
        if (agora < proximoTick)
        {
            return false;
        }

        int buckets = DiagnosticoDesempenhoJogo.RuntimeSaturado() ? 8 : 5;
        int bucket = owner != null ? Mathf.Abs(owner.GetInstanceID()) % buckets : 0;
        bool slotAtual = (Time.frameCount % buckets) == bucket;
        bool atrasado = agora - proximoTick >= Mathf.Max(0.04f, intervalo * 0.5f);
        if (!slotAtual && !atrasado)
        {
            return false;
        }

        proximoTick = agora + Mathf.Max(0.05f, intervalo);
        return true;
    }

    public static bool DeveAplicarReplan(Vector3 destino, ref Vector3 ultimoDestino, ref float proximoReplan, float cooldown, float tolerancia = 4.5f, bool forcar = false)
    {
        if (forcar)
        {
            ultimoDestino = destino;
            proximoReplan = Time.unscaledTime + Mathf.Max(0.05f, cooldown);
            return true;
        }

        float toleranciaSqr = tolerancia * tolerancia;
        if ((destino - ultimoDestino).sqrMagnitude > toleranciaSqr)
        {
            ultimoDestino = destino;
            proximoReplan = Time.unscaledTime + Mathf.Max(0.05f, cooldown);
            return true;
        }

        if (Time.unscaledTime >= proximoReplan)
        {
            ultimoDestino = destino;
            proximoReplan = Time.unscaledTime + Mathf.Max(0.05f, cooldown);
            return true;
        }

        return false;
    }

    public static Transform ObterInimigoMaisProximo(Vector3 origem, float raio, int meuTime)
    {
        List<Transform> buffer = new List<Transform>(8);
        ObterInimigosProximos(origem, raio, meuTime, buffer, 8);
        Transform melhor = null;
        float menorSqr = float.MaxValue;
        for (int i = 0; i < buffer.Count; i++)
        {
            Transform candidato = buffer[i];
            if (candidato == null)
            {
                continue;
            }

            float distSqr = (candidato.position - origem).sqrMagnitude;
            if (distSqr < menorSqr)
            {
                menorSqr = distSqr;
                melhor = candidato;
            }
        }

        return melhor;
    }

    public static void ObterInimigosProximos(Vector3 origem, float raio, int meuTime, List<Transform> destino, int maxResultados = 8)
    {
        if (destino == null)
        {
            return;
        }

        destino.Clear();
        if (meuTime <= 0 || raio <= 0f)
        {
            return;
        }

        float cell = Mathf.Max(60f, Mathf.Min(180f, raio));
        int cellX = Mathf.RoundToInt(origem.x / cell);
        int cellZ = Mathf.RoundToInt(origem.z / cell);
        int radiusKey = Mathf.RoundToInt(raio / 20f);
        long key = (((long)meuTime & 0xFFFFL) << 48)
                   ^ (((long)(cellX & 0xFFFF)) << 32)
                   ^ (((long)(cellZ & 0xFFFF)) << 16)
                   ^ (uint)radiusKey;

        CachePercepcao cache;
        if (!CacheInimigosPorSetor.TryGetValue(key, out cache))
        {
            cache = new CachePercepcao();
            CacheInimigosPorSetor[key] = cache;
        }

        float ttl = DiagnosticoDesempenhoJogo.RuntimeSaturado() ? 0.60f : DiagnosticoDesempenhoJogo.RuntimeSobPressao() ? 0.40f : 0.25f;
        if (Time.unscaledTime >= cache.expiraEm)
        {
            long inicio = MarcarInicioMedicao();
            cache.resultados.Clear();
            RegistroEntidadesJogo.FillControlesUnidade(BufferControles);

            float raioSqr = raio * raio;
            for (int i = 0; i < BufferControles.Count; i++)
            {
                ControleUnidade controle = BufferControles[i];
                if (controle == null || !controle.gameObject.activeInHierarchy)
                {
                    continue;
                }

                int team = ObterTime(controle.gameObject);
                if (team <= 0 || team == meuTime)
                {
                    continue;
                }

                SistemaDeDanos danos = controle.GetComponent<SistemaDeDanos>();
                if (danos == null)
                {
                    danos = controle.GetComponentInChildren<SistemaDeDanos>();
                }

                if (danos != null && danos.vidaAtual <= 0f)
                {
                    continue;
                }

                Vector3 delta = controle.transform.position - origem;
                delta.y = 0f;
                if (delta.sqrMagnitude > raioSqr)
                {
                    continue;
                }

                cache.resultados.Add(controle.transform);
            }

            cache.expiraEm = Time.unscaledTime + ttl;
            RegistrarTempoDecorrido(CategoriaBudgetGameplay.Sensor, inicio);
        }

        float maxDistSqr = raio * raio;
        for (int i = 0; i < cache.resultados.Count && destino.Count < Mathf.Max(1, maxResultados); i++)
        {
            Transform candidato = cache.resultados[i];
            if (candidato == null)
            {
                continue;
            }

            Vector3 delta = candidato.position - origem;
            delta.y = 0f;
            if (delta.sqrMagnitude <= maxDistSqr)
            {
                destino.Add(candidato);
            }
        }
    }

    private static Transform ObterCameraPrincipal()
    {
        if (_ultimoFrameCamera == Time.frameCount)
        {
            return _cameraCache;
        }

        _ultimoFrameCamera = Time.frameCount;
        if (_cameraCache == null && Camera.main != null)
        {
            _cameraCache = Camera.main.transform;
        }
        else if (Camera.main != null)
        {
            _cameraCache = Camera.main.transform;
        }

        return _cameraCache;
    }

    private static int ObterTime(GameObject obj)
    {
        if (obj == null)
        {
            return 0;
        }

        IdentidadeUnidade identidade = obj.GetComponent<IdentidadeUnidade>();
        if (identidade == null)
        {
            identidade = obj.GetComponentInParent<IdentidadeUnidade>();
        }

        if (identidade != null)
        {
            return identidade.teamID;
        }

        IdentidadeIA identidadeIA = obj.GetComponent<IdentidadeIA>();
        if (identidadeIA == null)
        {
            identidadeIA = obj.GetComponentInParent<IdentidadeIA>();
        }

        return identidadeIA != null ? identidadeIA.teamID : 0;
    }
}
