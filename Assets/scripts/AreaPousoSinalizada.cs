using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.Aeronaves.C17
{
    /// <summary>
    /// Gerencia a validação completa de uma área de pouso sinalizada de 100m+.
    /// Amostra 5 pontos ao longo da linha (0%, 25%, 50%, 75%, 100%) e laterais.
    /// </summary>
    public class AreaPousoSinalizada
    {
        public Vector3 PontoInicial { get; private set; }
        public Vector3 PontoFinal { get; private set; }
        public Vector3 DirecaoPista { get; private set; }
        public float Comprimento { get; private set; }
        public float LarguraSeguranca { get; private set; }
        public bool EhValida { get; private set; }
        public string MotivoInvalidez { get; private set; }

        public Vector3 PontoEntradaAproximacao => PontoInicial - DirecaoPista * 180f + Vector3.up * 85f;
        public Vector3 PontoToqueSolo => PontoInicial + DirecaoPista * (Comprimento * 0.15f);
        public Vector3 PontoParadaSolo => PontoFinal - DirecaoPista * (Comprimento * 0.10f);

        public AreaPousoSinalizada(Vector3 inicio, Vector3 direcao, float comprimentoMinimo = 100f, float largura = 18f)
        {
            Comprimento = Mathf.Max(comprimentoMinimo, 100f);
            LarguraSeguranca = largura;
            DirecaoPista = direcao.normalized;
            PontoInicial = inicio;
            PontoFinal = inicio + DirecaoPista * Comprimento;
            MotivoInvalidez = string.Empty;
        }

        public bool ValidarAreaPouso(LayerMask camadaTerreno, LayerMask camadaAgua, LayerMask camadaObstaculos, float inclinacaoMaximaGraus = 12f)
        {
            EhValida = false;
            MotivoInvalidez = string.Empty;

            if (DirecaoPista == Vector3.zero)
            {
                MotivoInvalidez = "Área de pouso inválida: direção não definida.";
                return false;
            }

            Vector3 vetorEsquerda = Vector3.Cross(Vector3.up, DirecaoPista).normalized * (LarguraSeguranca * 0.5f);
            float[] porcentagens = new float[] { 0f, 0.25f, 0.50f, 0.75f, 1f };

            float alturaPrimeiraAmostra = 0f;

            for (int i = 0; i < porcentagens.Length; i++)
            {
                Vector3 centroAmostra = PontoInicial + DirecaoPista * (Comprimento * porcentagens[i]);
                Vector3 pontoEsq = centroAmostra - vetorEsquerda;
                Vector3 pontoDir = centroAmostra + vetorEsquerda;

                // Testar centro, esquerda e direita
                if (!AmostrarPonto(centroAmostra, camadaTerreno, camadaAgua, camadaObstaculos, out float alturaCentro, out string erroPonto))
                {
                    MotivoInvalidez = erroPonto;
                    return false;
                }

                if (!AmostrarPonto(pontoEsq, camadaTerreno, camadaAgua, camadaObstaculos, out float alturaEsq, out string erroEsq))
                {
                    MotivoInvalidez = erroEsq;
                    return false;
                }

                if (!AmostrarPonto(pontoDir, camadaTerreno, camadaAgua, camadaObstaculos, out float alturaDir, out string erroDir))
                {
                    MotivoInvalidez = erroDir;
                    return false;
                }

                if (i == 0)
                {
                    alturaPrimeiraAmostra = alturaCentro;
                }
                else
                {
                    float diferencaAltura = Mathf.Abs(alturaCentro - alturaPrimeiraAmostra);
                    float distanciaDoInicio = Comprimento * porcentagens[i];
                    float anguloInclinacao = Mathf.Atan2(diferencaAltura, distanciaDoInicio) * Mathf.Rad2Deg;

                    if (anguloInclinacao > inclinacaoMaximaGraus)
                    {
                        MotivoInvalidez = "Área de pouso inválida: terreno muito inclinado.";
                        return false;
                    }
                }
            }

            EhValida = true;
            return true;
        }

        private bool AmostrarPonto(Vector3 origemBase, LayerMask camadaTerreno, LayerMask camadaAgua, LayerMask camadaObstaculos, out float alturaSolo, out string mensagemErro)
        {
            alturaSolo = 0f;
            mensagemErro = string.Empty;

            if (camadaTerreno.value == 0)
            {
                camadaTerreno = Physics.DefaultRaycastLayers;
            }

            Vector3 topoRaio = origemBase + Vector3.up * 100f;

            // 1. Checar se há água
            if (Physics.Raycast(topoRaio, Vector3.down, out RaycastHit hitAgua, 200f, camadaAgua))
            {
                // Verifica se a água está acima ou muito próxima do terreno
                if (Physics.Raycast(topoRaio, Vector3.down, out RaycastHit hitTerrenoCheck, 200f, camadaTerreno))
                {
                    if (hitAgua.point.y >= hitTerrenoCheck.point.y - 0.5f)
                    {
                        mensagemErro = "Área de pouso inválida: água detectada.";
                        return false;
                    }
                }
                else
                {
                    mensagemErro = "Área de pouso inválida: água detectada.";
                    return false;
                }
            }

            // 2. Checar se atinge o terreno
            if (Physics.Raycast(topoRaio, Vector3.down, out RaycastHit hitTerreno, 200f, camadaTerreno))
            {
                alturaSolo = hitTerreno.point.y;

                // Verificar se a normal do solo é muito inclinada
                float anguloNormal = Vector3.Angle(hitTerreno.normal, Vector3.up);
                if (anguloNormal > 15f)
                {
                    mensagemErro = "Área de pouso inválida: terreno irregular ou inclinado.";
                    return false;
                }
            }
            else
            {
                mensagemErro = "Área de pouso inválida: fora do terreno navegável.";
                return false;
            }

            // 3. Checar obstáculos e edifícios
            if (Physics.CheckSphere(new Vector3(origemBase.x, alturaSolo + 1.5f, origemBase.z), 2.5f, camadaObstaculos))
            {
                mensagemErro = "Área de pouso inválida: obstáculo na aproximação ou linha.";
                return false;
            }

            return true;
        }
    }
}
