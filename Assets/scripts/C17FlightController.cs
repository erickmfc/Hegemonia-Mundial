using UnityEngine;

namespace Hegemonia.Aeronaves.C17
{
    /// <summary>
    /// Configuracao e animacao visual do voo. A movimentacao do objeto raiz pertence
    /// exclusivamente ao C17MotionController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class C17FlightController : MonoBehaviour
    {
        [Header("Velocidades")]
        [SerializeField, Min(0.1f)] private float velocidadeTaxi = 7f;
        [SerializeField, Min(1f)] private float velocidadeDecolagem = 40f;
        [SerializeField, Min(1f)] private float velocidadeCruzeiro = 65f;
        [SerializeField, Min(1f)] private float velocidadeMaxima = 90f;
        [SerializeField, Min(1f)] private float aceleracao = 25f;
        [SerializeField, Min(1f)] private float desaceleracao = 40f;

        [Header("Altitude e controle")]
        [SerializeField, Min(10f)] private float altitudeCruzeiro = 450f;
        [SerializeField, Min(1f)] private float taxaSubida = 18f;
        [SerializeField, Min(1f)] private float taxaDescida = 15f;
        [SerializeField, Min(1f)] private float taxaCurva = 35f;
        [SerializeField, Range(0f, 45f)] private float inclinacaoMaxima = 25f;
        [SerializeField, Range(0f, 25f)] private float arfagemMaxima = 12f;

        [Header("Modelo")]
        [SerializeField] private Transform referenciaFrente;
        [SerializeField] private Transform modeloVisual;
        [SerializeField] private Vector3 offsetRotacaoModelo;

        private Quaternion rotacaoVisualInicial = Quaternion.identity;
        private float inclinacaoAtual;
        private float arfagemAtual;

        public float VelocidadeTaxi => velocidadeTaxi;
        public float VelocidadeDecolagem => velocidadeDecolagem;
        public float VelocidadeCruzeiro => velocidadeCruzeiro;
        public float VelocidadeMaxima => velocidadeMaxima;
        public float Aceleracao => aceleracao;
        public float Desaceleracao => desaceleracao;
        public float AltitudeCruzeiro => altitudeCruzeiro;
        public float TaxaSubida => taxaSubida;
        public float TaxaDescida => taxaDescida;
        public float TaxaCurva => taxaCurva;
        public Transform ReferenciaFrente => referenciaFrente != null ? referenciaFrente : transform;

        private void Awake()
        {
            if (modeloVisual == null && transform.childCount > 0) modeloVisual = transform.GetChild(0);
            if (modeloVisual != null) rotacaoVisualInicial = modeloVisual.localRotation;
        }

        public Vector3 DirecaoFrente()
        {
            return ReferenciaFrente.forward.sqrMagnitude > 0.001f
                ? ReferenciaFrente.forward.normalized
                : transform.forward;
        }

        public void AtualizarVisual(float anguloCurva, float diferencaAltitude, float deltaTempo)
        {
            if (modeloVisual == null) return;
            float alvoRoll = Mathf.Clamp(-anguloCurva * 0.7f, -inclinacaoMaxima, inclinacaoMaxima);
            float alvoPitch = Mathf.Clamp(-diferencaAltitude * 0.08f, -arfagemMaxima, arfagemMaxima);
            inclinacaoAtual = Mathf.MoveTowards(inclinacaoAtual, alvoRoll, deltaTempo * 30f);
            arfagemAtual = Mathf.MoveTowards(arfagemAtual, alvoPitch, deltaTempo * 20f);
            modeloVisual.localRotation = rotacaoVisualInicial
                * Quaternion.Euler(arfagemAtual, 0f, inclinacaoAtual)
                * Quaternion.Euler(offsetRotacaoModelo);
        }

        public void ResetarVisual()
        {
            inclinacaoAtual = 0f;
            arfagemAtual = 0f;
            if (modeloVisual != null) modeloVisual.localRotation = rotacaoVisualInicial * Quaternion.Euler(offsetRotacaoModelo);
        }
    }
}
