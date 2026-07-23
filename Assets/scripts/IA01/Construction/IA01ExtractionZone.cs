using UnityEngine;

namespace Hegemonia.AI.IA01
{
    /// <summary>
    /// Create de extração/desembarque para navios Liberty e transportes da IA.
    /// O ponto fica associado ao time e não pode ser escolhido por outra nação.
    /// </summary>
    public sealed class IA01ExtractionZone : MonoBehaviour
    {
        [SerializeField] private int teamId = 2;
        [SerializeField, Min(10f)] private float raio = 80f;
        [SerializeField, Min(1)] private int vagas = 6;

        public int TeamId => teamId;
        public float Raio => Mathf.Max(10f, raio);
        public int Vagas => Mathf.Max(1, vagas);

        public void Configurar(int equipe, float raioZona, int totalVagas)
        {
            teamId = Mathf.Max(1, equipe);
            raio = Mathf.Max(10f, raioZona);
            vagas = Mathf.Max(1, totalVagas);
        }

        public Vector3 ObterPontoDesembarque(int indice)
        {
            float angulo = (indice % Vagas) * (360f / Vagas) * Mathf.Deg2Rad;
            return transform.position + new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * (Raio * 0.6f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.95f, 0.75f, 0.15f, 0.65f);
            Gizmos.DrawWireSphere(transform.position, Raio);
        }
    }
}
