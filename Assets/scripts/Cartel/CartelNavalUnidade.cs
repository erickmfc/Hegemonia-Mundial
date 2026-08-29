using UnityEngine;

namespace Hegemonia.Cartel
{
    /// <summary>
    /// Estado visual/radar de um barco pertencente ao Cartel Naval. O barco
    /// continua existindo e navegando enquanto não é detectável pelo radar.
    /// </summary>
    public sealed class CartelNavalUnidade : MonoBehaviour
    {
        [Header("Identidade operacional")]
        public CartelNavalController Controlador;
        public int IndicePatrulha;
        public CartelNavalCrate RotaAtual;
        public string EstadoOperacional = "Patrulhando";

        [Header("Estado do radar")]
        public Vector3 UltimaPosicaoRadar;
        public Vector3 PosicaoConhecidaRadar;
        public Vector3 DirecaoRadar;
        public float VelocidadeRadar;
        public bool RadarVisivel = true;
        public bool EstaEmMovimento;
        public int ProximoDiaDeteccao;
        public bool AlvoAdquirido;

        private Vector3 ultimaAmostraReal;
        private float tempoUltimaAmostra = -1f;
        private bool primeiraAmostra = true;

        private void Awake()
        {
            UltimaPosicaoRadar = transform.position;
            PosicaoConhecidaRadar = transform.position;
        }

        public void AtualizarRadar(int diaAtual, float limiarMovimento, int diasParaReaquisição, float velocidadeAtual)
        {
            Vector3 posicaoAtual = transform.position;
            float intervalo = tempoUltimaAmostra < 0f ? 0f : Mathf.Max(0.01f, Time.unscaledTime - tempoUltimaAmostra);
            Vector3 deslocamento = posicaoAtual - ultimaAmostraReal;
            deslocamento.y = 0f;
            EstaEmMovimento = !primeiraAmostra && deslocamento.magnitude >= Mathf.Max(0.01f, limiarMovimento);
            VelocidadeRadar = velocidadeAtual > 0.01f
                ? velocidadeAtual
                : (intervalo > 0.01f ? deslocamento.magnitude / intervalo : 0f);

            if (deslocamento.sqrMagnitude > 0.0025f)
            {
                DirecaoRadar = deslocamento.normalized;
            }

            if (primeiraAmostra)
            {
                RadarVisivel = true;
                PosicaoConhecidaRadar = posicaoAtual;
                UltimaPosicaoRadar = posicaoAtual;
                primeiraAmostra = false;
            }
            else if (EstaEmMovimento)
            {
                RadarVisivel = false;
                UltimaPosicaoRadar = PosicaoConhecidaRadar;
                ProximoDiaDeteccao = Mathf.Max(ProximoDiaDeteccao, diaAtual + Mathf.Max(1, diasParaReaquisição));
            }
            else if (!RadarVisivel && diaAtual >= ProximoDiaDeteccao)
            {
                RadarVisivel = true;
                PosicaoConhecidaRadar = posicaoAtual;
                UltimaPosicaoRadar = posicaoAtual;
            }
            else if (RadarVisivel)
            {
                PosicaoConhecidaRadar = posicaoAtual;
                UltimaPosicaoRadar = posicaoAtual;
            }

            ultimaAmostraReal = posicaoAtual;
            tempoUltimaAmostra = Time.unscaledTime;
        }
    }
}
