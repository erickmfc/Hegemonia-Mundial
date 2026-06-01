using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(ControleUnidade))]
public class RenderizadorOrdensUnidade : MonoBehaviour
{
    private ControleUnidade controle;
    private LineRenderer linhaPatrulha;
    private GerenteSelecao gerenteSelecao;

    private void Start()
    {
        controle = GetComponent<ControleUnidade>();
        gerenteSelecao = FindFirstObjectByType<GerenteSelecao>();

        // Configurar a linha de patrulha
        GameObject goPatrulha = new GameObject("LinhaPatrulha_" + name);
        goPatrulha.transform.SetParent(transform);
        linhaPatrulha = goPatrulha.AddComponent<LineRenderer>();
        ConfigurarLinha(linhaPatrulha, Color.green, 2f);
    }

    private void ConfigurarLinha(LineRenderer lr, Color cor, float largura)
    {
        Material materialInstancia = new Material(Shader.Find("Sprites/Default"));
        materialInstancia.color = cor;
        lr.material = materialInstancia;
        lr.startWidth = largura;
        lr.endWidth = largura;
        lr.positionCount = 0;
        lr.enabled = false;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void Update()
    {
        if (gerenteSelecao == null || controle == null) return;

        bool selecionada = gerenteSelecao.unidadesSelecionadas.Contains(controle);
        if (!selecionada)
        {
            DesligarLinhas();
            return;
        }

        AtualizarLinhas();
    }

    private void DesligarLinhas()
    {
        if (linhaPatrulha != null) linhaPatrulha.enabled = false;
    }

    private void AtualizarLinhas()
    {
        DesligarLinhas();

        if (controle.OrdemAtual == OrdemControleUnidade.Patrulhando)
        {
            var patrulha = GetComponent<ComportamentoPatrulhaUniversal>();
            if (patrulha != null)
            {
                var pontos = patrulha.ObterPontos();
                if (pontos != null && pontos.Count > 0)
                {
                    linhaPatrulha.enabled = true;
                    linhaPatrulha.positionCount = pontos.Count;
                    for (int i = 0; i < pontos.Count; i++)
                    {
                        linhaPatrulha.SetPosition(i, pontos[i]);
                    }
                }
            }
        }

        var aviao = GetComponent<ControleAviao>();
        if (aviao != null && aviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
        {
            linhaPatrulha.enabled = true;
            linhaPatrulha.positionCount = 2;
            linhaPatrulha.SetPosition(0, transform.position);
            linhaPatrulha.SetPosition(1, aviao.centroDaPatrulha);
        }
    }
}
