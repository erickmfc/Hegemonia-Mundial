using UnityEngine;

namespace Hegemonia.AI.IA02
{
    /// <summary>
    /// Create editável que define a área de reconhecimento aéreo da IA02.
    /// A ordem é entregue pelo mesmo ControleUnidade usado pelo jogador.
    /// </summary>
    public sealed class IA02AirPatrolZone : MonoBehaviour
    {
        [SerializeField, Min(80f)] private float raio = 260f;
        [Tooltip("Largura da area retangular de patrulha. Mantem raio como fallback para assets antigos.")]
        [SerializeField, Min(120f)] private float largura = 520f;
        [Tooltip("Profundidade da area retangular de patrulha. Mantem raio como fallback para assets antigos.")]
        [SerializeField, Min(100f)] private float profundidade = 360f;
        [SerializeField, Min(40f)] private float altitude = 120f;
        [SerializeField, Min(1)] private int intervaloDias = 1;

        public int IntervaloDias => Mathf.Max(1, intervaloDias);

        public Vector3[] CriarRota(int indice)
        {
            // A patrulha aerea usa uma caixa retangular orientada pelo create.
            // O triangulo/circulo anterior mantinha o aviao sobre o aeroporto
            // e fazia a aeronave repetir uma voltinha curta.
            float larguraEfetiva = largura > 0f ? largura : Mathf.Max(120f, raio * 2f);
            float profundidadeEfetiva = profundidade > 0f ? profundidade : Mathf.Max(100f, raio * 1.4f);
            float variacao = (indice % 2) * 0.12f;
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

        private Vector3 Ajustar(Vector3 ponto)
        {
            ponto.y = Mathf.Max(60f, altitude);
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
