using UnityEngine;

namespace Hegemonia.AI.IA03
{
    [CreateAssetMenu(fileName = "NovoPerfilPaisIA03", menuName = "Hegemonia/IA03/Perfil de País")]
    public sealed class PerfilPaisSO : ScriptableObject
    {
        [Header("Identidade")]
        [SerializeField] private string nomePais = string.Empty;
        [SerializeField] private string nomePresidente = string.Empty;
        [SerializeField] private string idPerfil = string.Empty;

        [Header("Capacidade econômica")]
        [SerializeField] private IA03NivelEconomico nivelEconomico = IA03NivelEconomico.Medio;
        [SerializeField, Min(0f)] private float reservaFinanceiraDesejada = 10000f;
        [SerializeField, Range(0f, 1f)] private float prioridadeEconomica = 0.5f;
        [SerializeField, Range(0f, 1f)] private float prioridadeMilitar = 0.5f;

        [Header("Personalidade nacional")]
        [SerializeField, Range(0f, 1f)] private float agressividade = 0.35f;
        [SerializeField, Range(0f, 1f)] private float diplomacia = 0.6f;
        [SerializeField, Range(0f, 1f)] private float preferenciaNaval = 0.33f;
        [SerializeField, Range(0f, 1f)] private float preferenciaAerea = 0.33f;
        [SerializeField, Range(0f, 1f)] private float preferenciaTerrestre = 0.34f;
        [SerializeField, Range(0f, 1f)] private float interesseEmPetroleo = 0.5f;
        [SerializeField, Range(0f, 1f)] private float interesseEmAlimentos = 0.5f;
        [SerializeField, Range(0f, 1f)] private float toleranciaABaixas = 0.35f;
        [SerializeField, Range(0f, 1f)] private float disposicaoParaNegociar = 0.65f;
        [SerializeField, Range(0f, 1f)] private float tendenciaDeExportacao = 0.5f;

        [Header("Limites de mobilização")]
        [SerializeField, Range(0f, 1f)] private float reservaDefesaNacional = 0.1f;
        [SerializeField, Range(0f, 1f)] private float contingenteMaximoDeGuerra = 0.9f;
        [SerializeField, Range(0f, 0.9f)] private float contingenteMaximoPorMissaoN1 = 0.5f;
        [SerializeField, Min(1)] private int minimoNaviosEscolta = 2;
        [SerializeField, Min(1)] private int minimoAeronavesPortaAvioes = 4;

        [Header("Conflito e negociação")]
        [SerializeField, Min(1f)] private float intervaloDecisaoSegundos = 45f;
        [SerializeField, Min(1)] private int minimoDeBatalhasParaAvaliar = 3;
        [SerializeField, Range(0f, 1f)] private float dominioMinimo = 0.67f;
        [SerializeField, Range(0f, 1f)] private float prejuizoEconomicoMinimoParaPaz = 0.3f;
        [SerializeField, Min(1f)] private float avaliacaoConflitoLimitadoSegundos = 1500f;
        [SerializeField, Min(1f)] private float avaliacaoGuerraTotalSegundos = 3600f;

        public string NomePais => nomePais;
        public string NomePresidente => nomePresidente;
        public string IdPerfil => idPerfil;
        public IA03NivelEconomico NivelEconomico => nivelEconomico;
        public float ReservaFinanceiraDesejada => reservaFinanceiraDesejada;
        public float PrioridadeEconomica => prioridadeEconomica;
        public float PrioridadeMilitar => prioridadeMilitar;
        public float Agressividade => agressividade;
        public float Diplomacia => diplomacia;
        public float PreferenciaNaval => preferenciaNaval;
        public float PreferenciaAerea => preferenciaAerea;
        public float PreferenciaTerrestre => preferenciaTerrestre;
        public float InteresseEmPetroleo => interesseEmPetroleo;
        public float InteresseEmAlimentos => interesseEmAlimentos;
        public float ToleranciaABaixas => toleranciaABaixas;
        public float DisposicaoParaNegociar => disposicaoParaNegociar;
        public float TendenciaDeExportacao => tendenciaDeExportacao;
        public float ReservaDefesaNacional => reservaDefesaNacional;
        public float ContingenteMaximoDeGuerra => contingenteMaximoDeGuerra;
        public float ContingenteMaximoPorMissaoN1 => contingenteMaximoPorMissaoN1;
        public int MinimoNaviosEscolta => minimoNaviosEscolta;
        public int MinimoAeronavesPortaAvioes => minimoAeronavesPortaAvioes;
        public float IntervaloDecisaoSegundos => intervaloDecisaoSegundos;
        public int MinimoDeBatalhasParaAvaliar => minimoDeBatalhasParaAvaliar;
        public float DominioMinimo => dominioMinimo;
        public float PrejuizoEconomicoMinimoParaPaz => prejuizoEconomicoMinimoParaPaz;
        public float AvaliacaoConflitoLimitadoSegundos => avaliacaoConflitoLimitadoSegundos;
        public float AvaliacaoGuerraTotalSegundos => avaliacaoGuerraTotalSegundos;
    }
}
