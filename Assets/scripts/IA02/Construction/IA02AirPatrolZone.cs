using UnityEngine;

namespace Hegemonia.AI.IA02
{
    /// <summary>
    /// Create editável que define a área de reconhecimento aéreo da IA02.
    /// A ordem é entregue pelo mesmo ControleUnidade usado pelo jogador.
    /// </summary>
    public sealed class IA02AirPatrolZone : MonoBehaviour
    {
        public enum TipoSetor { Interior, Fronteira, CostaMarTerritorial, Estrategico }

        [SerializeField, Min(80f)] private float raio = 260f;
        [Tooltip("Largura da area retangular de patrulha. Mantem raio como fallback para assets antigos.")]
        [SerializeField, Min(120f)] private float largura = 520f;
        [Tooltip("Profundidade da area retangular de patrulha. Mantem raio como fallback para assets antigos.")]
        [SerializeField, Min(100f)] private float profundidade = 360f;
        [SerializeField, Min(40f)] private float altitude = 120f;
        [SerializeField, Min(1)] private int intervaloDias = 1;
        [SerializeField] private TipoSetor tipoSetor = TipoSetor.Interior;
        [SerializeField, Min(0)] private int aeronavesDesejadas = 2;

        public int IntervaloDias => Mathf.Max(1, intervaloDias);
        public TipoSetor Setor => tipoSetor;
        public int AeronavesDesejadas => Mathf.Max(0, aeronavesDesejadas);
        public float Raio => Mathf.Max(80f, raio);

        private void Awake()
        {
            if (tipoSetor == TipoSetor.Interior)
            {
                if (name.EndsWith("02")) tipoSetor = TipoSetor.Fronteira;
                else if (name.EndsWith("03")) tipoSetor = TipoSetor.CostaMarTerritorial;
                else if (name.EndsWith("04")) tipoSetor = TipoSetor.Estrategico;
            }
        }

        /// <summary>
        /// Retorna o waypoint aéreo deste Create. O Transform continua sendo
        /// filho do aeroporto militar; somente a altura de voo é acrescentada
        /// para que o avião atravesse a região real sem tentar pousar no ponto.
        /// </summary>
        public Vector3 ObterPontoPatrulha()
        {
            Vector3 ponto = transform.position;
            ponto.y += Mathf.Max(60f, altitude);
            return ponto;
        }

        public Vector3[] CriarRota(int indice)
        {
            // Cada aeronave recebe uma rota diferente dentro do setor. O Create
            // representa responsabilidade territorial, nao um waypoint fixo.
            float larguraEfetiva = largura > 0f ? largura : Mathf.Max(120f, Raio * 2f);
            float profundidadeEfetiva = profundidade > 0f ? profundidade : Mathf.Max(100f, Raio * 1.4f);
            float variacao = ((indice % 5) - 2) * 0.11f;
            float escala = Mathf.Clamp(1f + variacao, 0.62f, 1.28f);
            larguraEfetiva *= escala;
            profundidadeEfetiva *= Mathf.Clamp(1f - variacao * 0.5f, 0.72f, 1.2f);
            Vector3 eixoLateral = transform.right * (larguraEfetiva * (0.5f - variacao));
            Vector3 eixoFrontal = transform.forward * (profundidadeEfetiva * (0.5f - variacao));
            return new[]
            {
                Ajustar(transform.position - eixoLateral - eixoFrontal),
                Ajustar(transform.position + eixoLateral - eixoFrontal),
                Ajustar(transform.position + eixoLateral + eixoFrontal),
                Ajustar(transform.position - eixoLateral + eixoFrontal)
            };
        }

        public Vector3[] CriarRotaResposta(Vector3 ultimaPosicaoConhecida, float raioInvestigacao, int indice)
        {
            float raioResposta = Mathf.Clamp(Mathf.Max(80f, raioInvestigacao * 0.18f), 100f, Mathf.Max(140f, Raio * 0.75f));
            float angulo = (indice % 8) * 45f * Mathf.Deg2Rad;
            Vector3 centro = ultimaPosicaoConhecida;
            return new[]
            {
                Ajustar(centro + new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * raioResposta),
                Ajustar(centro + new Vector3(Mathf.Cos(angulo + 1.57f), 0f, Mathf.Sin(angulo + 1.57f)) * raioResposta),
                Ajustar(centro - new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * raioResposta),
                Ajustar(centro - new Vector3(Mathf.Cos(angulo + 1.57f), 0f, Mathf.Sin(angulo + 1.57f)) * raioResposta)
            };
        }

        private Vector3 Ajustar(Vector3 ponto)
        {
            ponto.y = transform.position.y + Mathf.Max(60f, altitude);
            return ponto;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.8f, 1f, 0.45f);
            float larguraEfetiva = largura > 0f ? largura : Mathf.Max(120f, raio * 2f);
            float profundidadeEfetiva = profundidade > 0f ? profundidade : Mathf.Max(100f, raio * 1.4f);
            Vector3 a = transform.position - transform.right * larguraEfetiva * 0.5f - transform.forward * profundidadeEfetiva * 0.5f;
            Vector3 b = transform.position + transform.right * larguraEfetiva * 0.5f - transform.forward * profundidadeEfetiva * 0.5f;
            Vector3 c = transform.position + transform.right * larguraEfetiva * 0.5f + transform.forward * profundidadeEfetiva * 0.5f;
            Vector3 d = transform.position - transform.right * larguraEfetiva * 0.5f + transform.forward * profundidadeEfetiva * 0.5f;
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
            Gizmos.DrawLine(transform.position + Vector3.up * altitude, transform.position + Vector3.up * (altitude + 30f));
        }
    }
}
