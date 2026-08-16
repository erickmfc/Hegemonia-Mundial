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
    private sealed class EntradaEspacialTatica
    {
        public ControleUnidade controle;
        public Transform transform;
        public IdentidadeUnidade identidade;
        public SistemaDeDanos danos;
        public int teamId;
    }

    private sealed class CachePercepcao
    {
        public float expiraEm;
        public readonly List<Transform> resultados = new List<Transform>(16);
    }

    private static readonly List<ControleUnidade> BufferControles = new List<ControleUnidade>(512);
    // Consultas de alvo rodam na thread principal do Unity; reutilizar este buffer evita GC por procura.
    private static readonly List<Transform> BufferInimigoMaisProximo = new List<Transform>(8);
    private static readonly Dictionary<long, CachePercepcao> CacheInimigosPorSetor = new Dictionary<long, CachePercepcao>(128);
    private static readonly Dictionary<long, List<EntradaEspacialTatica>> IndiceEspacialTatico = new Dictionary<long, List<EntradaEspacialTatica>>(256);
    private static readonly List<EntradaEspacialTatica> EntradasEspaciais = new List<EntradaEspacialTatica>(512);
    private const float TamanhoCelulaTatica = 96f;
    private static float proximaAtualizacaoIndiceEspacial;
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
        // Em jogo normal a telemetria fica desligada. Evite chamar Stopwatch
        // milhares de vezes por frame apenas para descartar a medicao depois.
        if (!DiagnosticoDesempenhoJogo.CapturaAtiva)
        {
            return 0L;
        }
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    public static void RegistrarTempoDecorrido(CategoriaBudgetGameplay categoria, long inicioTimestamp)
    {
        if (inicioTimestamp == 0L || !DiagnosticoDesempenhoJogo.CapturaAtiva)
        {
            return;
        }
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
        ObterInimigosProximos(origem, raio, meuTime, BufferInimigoMaisProximo, 8);
        Transform melhor = null;
        float menorSqr = float.MaxValue;
        for (int i = 0; i < BufferInimigoMaisProximo.Count; i++)
        {
            Transform candidato = BufferInimigoMaisProximo[i];
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

        AtualizarIndiceEspacialSeNecessario();

        int minX = Mathf.FloorToInt((origem.x - raio) / TamanhoCelulaTatica);
        int maxX = Mathf.FloorToInt((origem.x + raio) / TamanhoCelulaTatica);
        int minZ = Mathf.FloorToInt((origem.z - raio) / TamanhoCelulaTatica);
        int maxZ = Mathf.FloorToInt((origem.z + raio) / TamanhoCelulaTatica);
        float raioSqr = raio * raio;
        int limite = Mathf.Max(1, maxResultados);

        for (int x = minX; x <= maxX && destino.Count < limite; x++)
        {
            for (int z = minZ; z <= maxZ && destino.Count < limite; z++)
            {
                List<EntradaEspacialTatica> celula;
                if (!IndiceEspacialTatico.TryGetValue(ComporChaveCelula(x, z), out celula))
                {
                    continue;
                }

                for (int i = 0; i < celula.Count && destino.Count < limite; i++)
                {
                    EntradaEspacialTatica entrada = celula[i];
                    if (entrada == null || entrada.transform == null || entrada.teamId <= 0 || entrada.teamId == meuTime)
                    {
                        continue;
                    }

                    if (entrada.danos != null && entrada.danos.vidaAtual <= 0f)
                    {
                        continue;
                    }

                    Vector3 delta = entrada.transform.position - origem;
                    delta.y = 0f;
                    if (delta.sqrMagnitude <= raioSqr)
                    {
                        destino.Add(entrada.transform);
                    }
                }
            }
        }
    }

    private static void AtualizarIndiceEspacialSeNecessario()
    {
        if (Time.unscaledTime < proximaAtualizacaoIndiceEspacial)
        {
            return;
        }

        long inicio = MarcarInicioMedicao();
        IndiceEspacialTatico.Clear();
        EntradasEspaciais.Clear();
        RegistroEntidadesJogo.FillControlesUnidade(BufferControles);
        Transform camera = ObterCameraPrincipal();
        int proximas = 0;
        int medias = 0;
        int distantes = 0;

        for (int i = 0; i < BufferControles.Count; i++)
        {
            ControleUnidade controle = BufferControles[i];
            if (controle == null || !controle.gameObject.activeInHierarchy)
            {
                continue;
            }

            Transform transformControle = controle.transform;
            IdentidadeUnidade identidade = controle.GetComponent<IdentidadeUnidade>();
            if (identidade == null)
            {
                identidade = controle.GetComponentInParent<IdentidadeUnidade>();
            }

            int team = identidade != null ? identidade.teamID : ObterTime(controle.gameObject);
            if (team <= 0)
            {
                continue;
            }

            SistemaDeDanos danos = controle.GetComponent<SistemaDeDanos>();
            if (danos == null)
            {
                danos = controle.GetComponentInChildren<SistemaDeDanos>();
            }

            EntradaEspacialTatica entrada = new EntradaEspacialTatica
            {
                controle = controle,
                transform = transformControle,
                identidade = identidade,
                danos = danos,
                teamId = team
            };
            EntradasEspaciais.Add(entrada);

            if (camera != null)
            {
                float distanciaSqr = (transformControle.position - camera.position).sqrMagnitude;
                if (distanciaSqr >= 320f * 320f) distantes++;
                else if (distanciaSqr >= 140f * 140f) medias++;
                else proximas++;
            }

            int cellX = Mathf.FloorToInt(transformControle.position.x / TamanhoCelulaTatica);
            int cellZ = Mathf.FloorToInt(transformControle.position.z / TamanhoCelulaTatica);
            long key = ComporChaveCelula(cellX, cellZ);
            List<EntradaEspacialTatica> celula;
            if (!IndiceEspacialTatico.TryGetValue(key, out celula))
            {
                celula = new List<EntradaEspacialTatica>(8);
                IndiceEspacialTatico.Add(key, celula);
            }
            celula.Add(entrada);
        }

        float intervalo = DiagnosticoDesempenhoJogo.RuntimeSaturado() ? 0.55f
            : DiagnosticoDesempenhoJogo.RuntimeSobPressao() ? 0.35f : 0.20f;
        proximaAtualizacaoIndiceEspacial = Time.unscaledTime + intervalo;
        long delta = System.Diagnostics.Stopwatch.GetTimestamp() - inicio;
        if (delta > 0)
        {
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo(
                "tactical_index_ms",
                (float)(delta * 1000.0 / System.Diagnostics.Stopwatch.Frequency));
        }
        RegistrarTempoDecorrido(CategoriaBudgetGameplay.Sensor, inicio);
        DiagnosticoDesempenhoJogo.DefinirContadorMetrica("tactical_index_units", EntradasEspaciais.Count);
        DiagnosticoDesempenhoJogo.DefinirContadorMetrica("land_units_near", proximas);
        DiagnosticoDesempenhoJogo.DefinirContadorMetrica("land_units_medium", medias);
        DiagnosticoDesempenhoJogo.DefinirContadorMetrica("land_units_far", distantes);
    }

    private static long ComporChaveCelula(int x, int z)
    {
        return ((long)x << 32) ^ (uint)z;
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
