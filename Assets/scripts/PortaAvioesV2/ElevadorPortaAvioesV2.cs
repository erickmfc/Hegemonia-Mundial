using System.Collections;
using UnityEngine;

public sealed class ElevadorPortaAvioesV2 : MonoBehaviour
{
    public Transform plataforma;
    public Transform posicaoConves;
    public Transform posicaoBaixa;
    public float velocidade = 3f;
    public bool ocupado { get; private set; }

    public bool Configurado => plataforma != null && posicaoConves != null && posicaoBaixa != null;

    public void ConfigurarReferencias()
    {
        if (plataforma == null) plataforma = transform.Find("Plataforma");
        if (posicaoConves == null) posicaoConves = transform.Find("Posicao_Conves");
        if (posicaoBaixa == null) posicaoBaixa = transform.Find("Posicao_Baixa");
    }

    private void Awake() { ConfigurarReferencias(); }

    public IEnumerator MoverPara(bool baixo)
    {
        ConfigurarReferencias();
        if (!Configurado) yield break;
        ocupado = true; Vector3 destino = (baixo ? posicaoBaixa : posicaoConves).position;
        while ((plataforma.position - destino).sqrMagnitude > .01f)
        {
            plataforma.position = Vector3.MoveTowards(plataforma.position, destino, Mathf.Max(.01f, velocidade) * Time.deltaTime);
            yield return null;
        }
        plataforma.position = destino; ocupado = false;
    }
    private void Reset() { plataforma = transform.Find("Plataforma"); posicaoConves = transform.Find("Posicao_Conves"); posicaoBaixa = transform.Find("Posicao_Baixa"); }
    private void OnDrawGizmosSelected() { if (posicaoConves != null && posicaoBaixa != null) { Gizmos.color = new Color(.6f, 0, 1); Gizmos.DrawLine(posicaoConves.position, posicaoBaixa.position); } }
}
