using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.Cartel
{
    public enum TipoCreateNavalCartel
    {
        BaseNaval,
        SpawnNavio01,
        SpawnNavio02,
        Rota01,
        Rota02,
        Rota03,
        Rota04,
        AreaEmboscadaPetroleiro,
        AreaEmboscadaPlataforma,
        Fuga01,
        Fuga02,
        Reforco01,
        Reforco02,
        Reforco03
    }

    /// <summary>
    /// Ponto configurável do Cartel Naval. É apenas uma referência de
    /// operação: não possui lógica de movimento própria e não esconde
    /// renderers em runtime.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Hegemonia/Cartel Naval/Create Naval")]
    public sealed class CartelNavalCrate : MonoBehaviour
    {
        private static readonly List<CartelNavalCrate> Registro = new List<CartelNavalCrate>(32);

        [Header("Identificação estável")]
        public string IdEstavel = string.Empty;

        [Header("Função")]
        public TipoCreateNavalCartel Tipo = TipoCreateNavalCartel.Rota01;
        [Min(0)] public int SequenciaRota;
        [Min(1f)] public float Raio = 35f;
        public bool ExigirAgua = true;
        public bool Disponivel = true;

        [Header("Descrição em português")]
        [TextArea(1, 3)] public string DescricaoPortugues = string.Empty;
        public Color CorGizmo = new Color(0.95f, 0.25f, 0.15f, 0.9f);
        public bool DesenharGizmo = true;

        public Vector3 Position { get { return transform.position; } }

        private void OnEnable()
        {
            Registrar(this);
        }

        private void OnDisable()
        {
            Registro.Remove(this);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(IdEstavel))
            {
                IdEstavel = GerarIdPadrao();
            }

            if (string.IsNullOrWhiteSpace(DescricaoPortugues))
            {
                DescricaoPortugues = ObterDescricaoPadrao(Tipo);
            }

            Raio = Mathf.Max(1f, Raio);
            Registrar(this);
        }

        public bool IsUsable()
        {
            return Disponivel && isActiveAndEnabled && gameObject.activeInHierarchy;
        }

        public bool Contains(Vector3 point)
        {
            Vector3 delta = point - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= Raio * Raio;
        }

        public static List<CartelNavalCrate> GetAll(bool includeInactive)
        {
            LimparRegistro();

            CartelNavalCrate[] encontrados = Object.FindObjectsByType<CartelNavalCrate>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < encontrados.Length; i++)
            {
                Registrar(encontrados[i]);
            }

            List<CartelNavalCrate> resultado = new List<CartelNavalCrate>(Registro.Count);
            for (int i = 0; i < Registro.Count; i++)
            {
                CartelNavalCrate crate = Registro[i];
                if (crate == null) continue;
                if (includeInactive || crate.gameObject.activeInHierarchy) resultado.Add(crate);
            }
            return resultado;
        }

        private static void Registrar(CartelNavalCrate crate)
        {
            if (crate != null && !Registro.Contains(crate)) Registro.Add(crate);
        }

        private static void LimparRegistro()
        {
            for (int i = Registro.Count - 1; i >= 0; i--)
            {
                if (Registro[i] == null) Registro.RemoveAt(i);
            }
        }

        private string GerarIdPadrao()
        {
            return "cartel-naval/" + gameObject.name.ToLowerInvariant().Replace(' ', '_');
        }

        public static string ObterDescricaoPadrao(TipoCreateNavalCartel tipo)
        {
            switch (tipo)
            {
                case TipoCreateNavalCartel.BaseNaval: return "Base naval e retorno da patrulha";
                case TipoCreateNavalCartel.SpawnNavio01: return "Ponto de surgimento do navio naval 01";
                case TipoCreateNavalCartel.SpawnNavio02: return "Ponto de surgimento do navio naval 02";
                case TipoCreateNavalCartel.Rota01: return "Rota de patrulha naval 01";
                case TipoCreateNavalCartel.Rota02: return "Rota de patrulha naval 02";
                case TipoCreateNavalCartel.Rota03: return "Rota de patrulha naval 03";
                case TipoCreateNavalCartel.Rota04: return "Rota de patrulha naval 04";
                case TipoCreateNavalCartel.AreaEmboscadaPetroleiro: return "Área de emboscada de petroleiros";
                case TipoCreateNavalCartel.AreaEmboscadaPlataforma: return "Área de emboscada de plataformas";
                case TipoCreateNavalCartel.Fuga01: return "Rota de fuga naval 01";
                case TipoCreateNavalCartel.Fuga02: return "Rota de fuga naval 02";
                case TipoCreateNavalCartel.Reforco01: return "Ponto de reforço naval 01";
                case TipoCreateNavalCartel.Reforco02: return "Ponto de reforço naval 02";
                case TipoCreateNavalCartel.Reforco03: return "Ponto de reforço naval 03";
                default: return "Ponto de operação naval";
            }
        }

        private void OnDrawGizmos()
        {
            if (!DesenharGizmo) return;

            Gizmos.color = CorGizmo.a <= 0f ? CorPorTipo(Tipo) : CorGizmo;
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(1f, Raio));
            Gizmos.DrawSphere(transform.position, Mathf.Clamp(Raio * 0.06f, 0.8f, 5f));

            Vector3 origem = transform.position;
            Vector3 direcao = transform.forward;
            if (direcao.sqrMagnitude < 0.01f) direcao = Vector3.forward;
            direcao.Normalize();
            Vector3 ponta = origem + direcao * Mathf.Clamp(Raio * 0.75f, 6f, 35f);
            Gizmos.DrawLine(origem, ponta);
            Gizmos.DrawLine(ponta, ponta - direcao * 4f + transform.right * 2f);
            Gizmos.DrawLine(ponta, ponta - direcao * 4f - transform.right * 2f);
        }

        private static Color CorPorTipo(TipoCreateNavalCartel tipo)
        {
            if (tipo == TipoCreateNavalCartel.AreaEmboscadaPetroleiro || tipo == TipoCreateNavalCartel.AreaEmboscadaPlataforma)
                return new Color(1f, 0.35f, 0.05f, 0.9f);
            if (tipo == TipoCreateNavalCartel.Fuga01 || tipo == TipoCreateNavalCartel.Fuga02)
                return new Color(0.7f, 0.2f, 1f, 0.9f);
            if (tipo == TipoCreateNavalCartel.Reforco01 || tipo == TipoCreateNavalCartel.Reforco02 || tipo == TipoCreateNavalCartel.Reforco03)
                return new Color(0.1f, 0.9f, 0.9f, 0.9f);
            return new Color(1f, 0.2f, 0.15f, 0.9f);
        }
    }
}
