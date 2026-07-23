using UnityEngine;

namespace Hegemonia.AI.IA01
{
    /// <summary>
    /// Create manual de avanço em guerra. Coloque este objeto na água, no ar
    /// ou em terra e atribua o TeamId da IA. As ordens de guerra podem usar os
    /// pontos gerados sem procurar posições aleatórias no mapa.
    /// </summary>
    public sealed class IA01WarAdvanceZone : MonoBehaviour
    {
        public enum Dominio { Naval, Aereo, Terrestre }

        [SerializeField] private int teamId = 2;
        [SerializeField] private Dominio dominio = Dominio.Naval;
        [SerializeField, Min(20f)] private float raio = 180f;
        [SerializeField, Min(1)] private int pontos = 3;

        public int TeamId => teamId;
        public Dominio Tipo => dominio;
        public float Raio => Mathf.Max(20f, raio);

        public void Configurar(int equipe, Dominio tipo, float raioZona, int totalPontos)
        {
            teamId = Mathf.Max(1, equipe);
            dominio = tipo;
            raio = Mathf.Max(20f, raioZona);
            pontos = Mathf.Max(1, totalPontos);
        }

        public Vector3 ObterPonto(int indice)
        {
            int total = Mathf.Max(1, pontos);
            float angulo = (indice % total) * (360f / total) * Mathf.Deg2Rad;
            Vector3 p = transform.position + new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * (Raio * 0.72f);
            if (dominio == Dominio.Aereo) p.y = Mathf.Max(transform.position.y + 100f, 100f);
            else if (dominio == Dominio.Naval)
            {
                p.y = NavalPlacementResolver.ResolveSeaLevel();
                if (!NavalPlacementResolver.IsWaterAtPosition(p)
                    && NavalPlacementResolver.TryResolveWaterSpawn(p, p - transform.position, 0f, Raio, out Vector3 agua, out _, out _))
                    p = agua;
            }
            return p;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, Raio);
            Gizmos.DrawLine(transform.position, ObterPonto(0));
        }
    }
}
