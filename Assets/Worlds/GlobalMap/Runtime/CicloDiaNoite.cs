using UnityEngine;

/// <summary>
/// Controla o ciclo visual suave de Amanhecer, Dia, Tarde e Noite Clara Tatica.
/// Totalmente independente do calendario economico (GerenciadorTempo) e otimizado para RTS.
/// </summary>
[DisallowMultipleComponent]
public class CicloDiaNoite : MonoBehaviour
{
    public enum ModoCiclo
    {
        CicloAutomatico = 0,
        FixoDia = 1,
        FixoTarde = 2,
        FixoNoite = 3
    }

    [Header("Referências")]
    [SerializeField] private Light luzPrincipal;
    [SerializeField] private GlobalHorizonAtmosphere atmosferaHorizonte;

    [Header("Tempo do Ciclo Visual (Independente do Calendário)")]
    [SerializeField] private ModoCiclo modo = ModoCiclo.CicloAutomatico;
    [Tooltip("Duracao de um ciclo completo (24h visuais) em segundos reais. Padrao: 450s (~15 dias de 30s).")]
    [SerializeField] private float duracaoCicloSegundos = 450f;
    [Range(0f, 24f)]
    [SerializeField] private float horaAtual = 10.5f;

    [Header("Horários Fixos de Referência")]
    [SerializeField] private float horaFixaDia = 12f;
    [SerializeField] private float horaFixaTarde = 17.65f;
    [SerializeField] private float horaFixaNoite = 23f;

    [Header("Direção do Sol")]
    [SerializeField] private float azimuteY = -32f;

    private float _ultimaHoraAplicada = -999f;

    // Paletas pre-alocadas (zero GC)
    private static readonly Color CorLuzDia = new Color(1.00f, 0.95f, 0.84f, 1f);
    private static readonly Color CorLuzTarde = new Color(1.00f, 0.64f, 0.34f, 1f);
    private static readonly Color CorLuzNoite = new Color(0.68f, 0.79f, 0.96f, 1f); // Lua clara tatica

    private static readonly Color AmbCeuDia = new Color(0.50f, 0.58f, 0.66f, 1f);
    private static readonly Color AmbEqDia = new Color(0.31f, 0.36f, 0.41f, 1f);
    private static readonly Color AmbChaoDia = new Color(0.16f, 0.18f, 0.20f, 1f);

    private static readonly Color AmbCeuTarde = new Color(0.54f, 0.43f, 0.40f, 1f);
    private static readonly Color AmbEqTarde = new Color(0.38f, 0.28f, 0.26f, 1f);
    private static readonly Color AmbChaoTarde = new Color(0.18f, 0.15f, 0.16f, 1f);

    private static readonly Color AmbCeuNoite = new Color(0.30f, 0.38f, 0.50f, 1f);
    private static readonly Color AmbEqNoite = new Color(0.22f, 0.29f, 0.39f, 1f);
    private static readonly Color AmbChaoNoite = new Color(0.14f, 0.18f, 0.24f, 1f);

    private static readonly Color SkyDia = new Color(0.46f, 0.64f, 0.80f, 1f);
    private static readonly Color GroundDia = new Color(0.11f, 0.22f, 0.31f, 1f);
    private static readonly Color FogDia = new Color(0.62f, 0.75f, 0.85f, 1f);

    private static readonly Color SkyTarde = new Color(0.62f, 0.50f, 0.54f, 1f);
    private static readonly Color GroundTarde = new Color(0.16f, 0.18f, 0.24f, 1f);
    private static readonly Color FogTarde = new Color(0.76f, 0.58f, 0.48f, 1f);

    private static readonly Color SkyNoite = new Color(0.18f, 0.26f, 0.40f, 1f);
    private static readonly Color GroundNoite = new Color(0.08f, 0.14f, 0.22f, 1f);
    private static readonly Color FogNoite = new Color(0.20f, 0.29f, 0.42f, 1f);

    public float HoraAtual => horaAtual;

    private void Start()
    {
        ResolverReferencias();
        AplicarIluminacao(forcar: true);
    }

    private void Update()
    {
        if (modo == ModoCiclo.CicloAutomatico)
        {
            if (!MenuPausaController.EstaPausado)
            {
                float duracao = Mathf.Max(30f, duracaoCicloSegundos);
                horaAtual += (Time.deltaTime / duracao) * 24f;
                if (horaAtual >= 24f) horaAtual -= 24f;
            }
        }
        else if (modo == ModoCiclo.FixoDia)
        {
            horaAtual = horaFixaDia;
        }
        else if (modo == ModoCiclo.FixoTarde)
        {
            horaAtual = horaFixaTarde;
        }
        else if (modo == ModoCiclo.FixoNoite)
        {
            horaAtual = horaFixaNoite;
        }

        AplicarIluminacao(forcar: false);
    }

    private void ResolverReferencias()
    {
        if (luzPrincipal == null)
        {
            luzPrincipal = RenderSettings.sun;
        }

        if (atmosferaHorizonte == null)
        {
            atmosferaHorizonte = GetComponent<GlobalHorizonAtmosphere>();
            if (atmosferaHorizonte == null)
            {
#if UNITY_2023_1_OR_NEWER
                atmosferaHorizonte = FindFirstObjectByType<GlobalHorizonAtmosphere>();
#else
                atmosferaHorizonte = FindObjectOfType<GlobalHorizonAtmosphere>();
#endif
            }
        }
    }

    private void AplicarIluminacao(bool forcar)
    {
        if (!forcar && Mathf.Abs(horaAtual - _ultimaHoraAplicada) < 0.01f) return;
        _ultimaHoraAplicada = horaAtual;

        // Pesos suaves das fases:
        // Amanhecer: 05:30 - 07:30 | Dia: 07:30 - 16:30 | Tarde: 16:30 - 19:00 | Noite: 19:00 - 05:30
        Color corLuz;
        float intensidadeLuz;
        Color ambCeu, ambEq, ambChao;
        Color skyTint, skyGround, fogCor;
        float exposure;
        float anguloX;

        if (horaAtual >= 7.5f && horaAtual < 16.5f)
        {
            // Dia pleno
            corLuz = CorLuzDia;
            intensidadeLuz = 1.15f;
            ambCeu = AmbCeuDia; ambEq = AmbEqDia; ambChao = AmbChaoDia;
            skyTint = SkyDia; skyGround = GroundDia; fogCor = FogDia;
            exposure = 1.15f;
            float t = (horaAtual - 7.5f) / 9f;
            anguloX = Mathf.Lerp(28f, 48f, Mathf.Sin(t * Mathf.PI));
        }
        else if (horaAtual >= 16.5f && horaAtual < 19.0f)
        {
            // Tarde / Por do sol
            float t = (horaAtual - 16.5f) / 2.5f;
            if (t < 0.55f)
            {
                float k = t / 0.55f;
                corLuz = Color.Lerp(CorLuzDia, CorLuzTarde, k);
                intensidadeLuz = Mathf.Lerp(1.15f, 0.85f, k);
                ambCeu = Color.Lerp(AmbCeuDia, AmbCeuTarde, k);
                ambEq = Color.Lerp(AmbEqDia, AmbEqTarde, k);
                ambChao = Color.Lerp(AmbChaoDia, AmbChaoTarde, k);
                skyTint = Color.Lerp(SkyDia, SkyTarde, k);
                skyGround = Color.Lerp(GroundDia, GroundTarde, k);
                fogCor = Color.Lerp(FogDia, FogTarde, k);
                exposure = Mathf.Lerp(1.15f, 1.02f, k);
            }
            else
            {
                float k = (t - 0.55f) / 0.45f;
                corLuz = Color.Lerp(CorLuzTarde, CorLuzNoite, k);
                intensidadeLuz = Mathf.Lerp(0.85f, 0.58f, k);
                ambCeu = Color.Lerp(AmbCeuTarde, AmbCeuNoite, k);
                ambEq = Color.Lerp(AmbEqTarde, AmbEqNoite, k);
                ambChao = Color.Lerp(AmbChaoTarde, AmbChaoNoite, k);
                skyTint = Color.Lerp(SkyTarde, SkyNoite, k);
                skyGround = Color.Lerp(GroundTarde, GroundNoite, k);
                fogCor = Color.Lerp(FogTarde, FogNoite, k);
                exposure = Mathf.Lerp(1.02f, 0.85f, k);
            }
            anguloX = Mathf.Lerp(28f, 12f, t);
        }
        else if (horaAtual >= 5.5f && horaAtual < 7.5f)
        {
            // Amanhecer
            float t = (horaAtual - 5.5f) / 2.0f;
            if (t < 0.45f)
            {
                float k = t / 0.45f;
                corLuz = Color.Lerp(CorLuzNoite, CorLuzTarde, k);
                intensidadeLuz = Mathf.Lerp(0.58f, 0.85f, k);
                ambCeu = Color.Lerp(AmbCeuNoite, AmbCeuTarde, k);
                ambEq = Color.Lerp(AmbEqNoite, AmbEqTarde, k);
                ambChao = Color.Lerp(AmbChaoNoite, AmbChaoTarde, k);
                skyTint = Color.Lerp(SkyNoite, SkyTarde, k);
                skyGround = Color.Lerp(GroundNoite, GroundTarde, k);
                fogCor = Color.Lerp(FogNoite, FogTarde, k);
                exposure = Mathf.Lerp(0.85f, 1.02f, k);
            }
            else
            {
                float k = (t - 0.45f) / 0.55f;
                corLuz = Color.Lerp(CorLuzTarde, CorLuzDia, k);
                intensidadeLuz = Mathf.Lerp(0.85f, 1.15f, k);
                ambCeu = Color.Lerp(AmbCeuTarde, AmbCeuDia, k);
                ambEq = Color.Lerp(AmbEqTarde, AmbEqDia, k);
                ambChao = Color.Lerp(AmbChaoTarde, AmbChaoDia, k);
                skyTint = Color.Lerp(SkyTarde, SkyDia, k);
                skyGround = Color.Lerp(GroundTarde, GroundDia, k);
                fogCor = Color.Lerp(FogTarde, FogDia, k);
                exposure = Mathf.Lerp(1.02f, 1.15f, k);
            }
            anguloX = Mathf.Lerp(14f, 28f, t);
        }
        else
        {
            // Noite clara tatica (19:00 - 05:30)
            corLuz = CorLuzNoite;
            intensidadeLuz = 0.58f;
            ambCeu = AmbCeuNoite; ambEq = AmbEqNoite; ambChao = AmbChaoNoite;
            skyTint = SkyNoite; skyGround = GroundNoite; fogCor = FogNoite;
            exposure = 0.85f;
            anguloX = 36f; // Lua alta o suficiente para iluminar relevo e navios sem breu
        }

        if (luzPrincipal != null)
        {
            luzPrincipal.color = corLuz;
            luzPrincipal.intensity = intensidadeLuz;
            luzPrincipal.transform.rotation = Quaternion.Euler(anguloX, azimuteY, 0f);
        }

        RenderSettings.ambientSkyColor = ambCeu;
        RenderSettings.ambientEquatorColor = ambEq;
        RenderSettings.ambientGroundColor = ambChao;

        if (atmosferaHorizonte != null)
        {
            atmosferaHorizonte.AplicarTemaAtmosferico(skyTint, skyGround, fogCor, exposure);
        }
    }
}
