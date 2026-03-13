using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Componente de UI que desenha um triângulo via Mesh customizado.
/// Usado pelo MiniMapa para desenhar o indicador do jogador e o cone de FOV.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class TrianguloUI : Graphic
{
    [HideInInspector] public Color corTriangulo = Color.red;
    [HideInInspector] public Color corBorda = Color.black;
    [HideInInspector] public bool ehCone = false; // true = cone de visão (triângulo largo)
    protected override void Start()
    {
        base.Start();
        if (Application.isPlaying)
        {
            if (transform.parent == null || transform.parent.name != "MapaImagem")
            {
                // ATENÇÃO: Se destruir o gameObject inteiro aqui, pode apagar o Gerente de Jogo ou o Minimapa junto!
                // O certo é destruir apenas ESTE COMPONENTE intruso (this).
                Destroy(this);
            }
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = rectTransform.rect;

        Vector2 topo, esq, dir;

        if (ehCone)
        {
            // Cone de FOV: ponto de origem no centro-baixo, largo no topo
            topo = new Vector2(r.width * 0.5f, r.height);
            esq  = new Vector2(-r.width * 0.5f, 0);
            dir  = new Vector2(r.width * 0.5f, 0);
        }
        else
        {
            // Triângulo do jogador: aponta para cima, pequeno
            float h = r.height * 0.5f;
            float w = r.width  * 0.45f;
            topo = new Vector2(0,  h);
            esq  = new Vector2(-w, -h);
            dir  = new Vector2( w, -h);
        }

        // Preenche o triângulo
        UIVertex v = UIVertex.simpleVert;
        v.color = corTriangulo;

        v.position = topo; vh.AddVert(v);
        v.position = esq;  vh.AddVert(v);
        v.position = dir;  vh.AddVert(v);
        vh.AddTriangle(0, 1, 2);

        // Borda (outline) — apenas para o triângulo do jogador
        if (!ehCone && corBorda.a > 0.01f)
        {
            float offset = 1.5f;
            v.color = corBorda;

            Vector2[] pts = { topo, esq, dir };
            for (int i = 0; i < 3; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[(i + 1) % 3];
                Vector2 perp = new Vector2(-(b - a).y, (b - a).x).normalized * offset;

                v.position = (Vector3)(a + perp); int i0 = vh.currentVertCount; vh.AddVert(v);
                v.position = (Vector3)(b + perp); vh.AddVert(v);
                v.position = (Vector3)(a - perp); vh.AddVert(v);
                v.position = (Vector3)(b - perp); vh.AddVert(v);
                vh.AddTriangle(i0, i0+1, i0+2);
                vh.AddTriangle(i0+1, i0+3, i0+2);
            }
        }
    }

    // Força redesenho ao mudar a cor
    public void SetCores(Color triangulo, Color borda)
    {
        corTriangulo = triangulo;
        corBorda = borda;
        SetVerticesDirty();
    }
}
