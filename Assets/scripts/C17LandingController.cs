using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.Aeronaves.C17
{
    /// <summary>
    /// Valida e descreve uma pista improvisada. Nao move o aviao.
    /// </summary>
    [RequireComponent(typeof(C17FlightController))]
    public sealed class C17LandingController : MonoBehaviour
    {
        [SerializeField, Min(80f)] private float comprimentoMinimoPouso = 100f;
        [SerializeField, Min(10f)] private float larguraMinimaPouso = 18f;
        [SerializeField, Range(1f, 30f)] private float inclinacaoMaximaTerreno = 12f;
        [SerializeField, Min(5f)] private float timeoutAproximacao = 25f;
        [SerializeField] private LayerMask camadaTerreno;
        [SerializeField] private LayerMask camadaAgua;
        [SerializeField] private LayerMask camadaObstaculos;
        [SerializeField] private GameObject prefabBastaoSinalizador;

        private readonly List<SinalizadorPouso> sinalizadores = new List<SinalizadorPouso>();
        private AreaPousoSinalizada areaAtual;
        private float inicioAproximacao;

        public AreaPousoSinalizada AreaPousoAtual => areaAtual;

        public bool TentarCriarAreaPouso(Vector3 inicio, Vector3 direcao, out AreaPousoSinalizada area, out string feedback)
        {
            area = new AreaPousoSinalizada(inicio, direcao, comprimentoMinimoPouso, larguraMinimaPouso);
            bool valida = area.ValidarAreaPouso(camadaTerreno, camadaAgua, camadaObstaculos, inclinacaoMaximaTerreno);
            feedback = valida ? "Area de pouso valida." : area.MotivoInvalidez;
            return valida;
        }

        public void DefinirAreaPousoConfirmada(AreaPousoSinalizada area)
        {
            LimparSinalizadores();
            areaAtual = area;
            inicioAproximacao = Time.time;
            if (areaAtual != null && areaAtual.EhValida) InstanciarSinalizadores(areaAtual);
        }

        public bool ExecutarAproximacao(float deltaTempo, out bool arremeter)
        {
            arremeter = areaAtual == null || !areaAtual.EhValida || Time.time - inicioAproximacao > timeoutAproximacao;
            if (arremeter) return false;
            Vector3 direcao = areaAtual.DirecaoPista;
            float alinhamento = Vector3.Angle(ProjetarHorizontal(transform.forward), direcao);
            return Vector3.Distance(transform.position, areaAtual.PontoEntradaAproximacao) <= 28f && alinhamento <= 35f;
        }

        public bool ExecutarPousoEFreio(float deltaTempo, out bool finalizado)
        {
            finalizado = areaAtual != null && Vector3.Distance(transform.position, areaAtual.PontoParadaSolo) <= 5f;
            return finalizado;
        }

        public void ExecutarArremetida(float deltaTempo, out bool concluida)
        {
            concluida = transform.position.y >= 80f;
        }

        private void InstanciarSinalizadores(AreaPousoSinalizada area)
        {
            if (prefabBastaoSinalizador == null) return;
            Vector3 lateral = Vector3.Cross(Vector3.up, area.DirecaoPista).normalized * area.LarguraSeguranca * 0.5f;
            float[] pontos = { 0f, 0.5f, 1f };
            for (int i = 0; i < pontos.Length; i++)
            {
                Vector3 centro = area.PontoInicial + area.DirecaoPista * area.Comprimento * pontos[i];
                CriarSinalizador(centro - lateral, area.EhValida);
                CriarSinalizador(centro + lateral, area.EhValida);
            }
        }

        private void CriarSinalizador(Vector3 posicao, bool valido)
        {
            GameObject obj = Instantiate(prefabBastaoSinalizador, posicao + Vector3.up * 0.15f, Quaternion.identity);
            SinalizadorPouso sinal = obj != null ? obj.GetComponent<SinalizadorPouso>() : null;
            if (sinal != null)
            {
                sinal.DefinirEstadoVisual(valido);
                sinalizadores.Add(sinal);
            }
        }

        public void LimparSinalizadores()
        {
            for (int i = 0; i < sinalizadores.Count; i++)
            {
                if (sinalizadores[i] != null) Destroy(sinalizadores[i].gameObject);
            }
            sinalizadores.Clear();
        }

        private static Vector3 ProjetarHorizontal(Vector3 valor)
        {
            valor.y = 0f;
            return valor.normalized;
        }
    }
}
