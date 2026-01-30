using UnityEngine;
using System.Collections.Generic;

public class OtimizadorLOD : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("Distância em metros para começar a esconder os detalhes.")]
    public float distanciaParaSimplificar = 100f; // 100 metros é um bom começo
    
    [Tooltip("Distância para parar de desenhar o objeto completamente (Culling). 0 para desativar.")]
    public float distanciaSsumir = 500f;

    [Header("O que esconder?")]
    [Tooltip("Arraste aqui objetos pequenos: Antenas, Barris, Luzes, Partículas, Tripulação.")]
    public List<GameObject> detalhesPequenos;

    [Header("Otimização de Scripts")]
    [Tooltip("Arraste scripts visuais que podem parar de rodar de longe (Ex: Radares giratórios).")]
    public List<MonoBehaviour> scriptsDecorativos;

    // Estado interno
    private bool estaSimplificado = false;
    private bool estaInvisivel = false;
    private Transform camTransform;
    private Renderer[] renderersPrincipais;

    void Start()
    {
        if (Camera.main != null)
            camTransform = Camera.main.transform;

        // Pega os renderers do objeto principal para 'sumir' ele de muito longe
        renderersPrincipais = GetComponentsInChildren<Renderer>();
        
        // Executa a verificação a cada 0.5 segundos (Randomizado para não travar tudo junto)
        InvokeRepeating("VerificarDistancia", Random.Range(0f, 1f), 0.5f);
    }

    void VerificarDistancia()
    {
        if (camTransform == null)
        {
            if(Camera.main != null) camTransform = Camera.main.transform;
            return;
        }

        float distancia = Vector3.Distance(transform.position, camTransform.position);

        // NÍVEL 2: CULLING (Invisível)
        if (distanciaSsumir > 0 && distancia > distanciaSsumir)
        {
            if (!estaInvisivel) definirVisibilidadeTotal(false);
            return; // Se tá invisível, nem checa o resto
        }
        else
        {
            if (estaInvisivel) definirVisibilidadeTotal(true);
        }

        // NÍVEL 1: SIMPLIFICAÇÃO (Esconder detalhes)
        if (distancia > distanciaParaSimplificar)
        {
            if (!estaSimplificado) AlternarDetalhes(false);
        }
        else
        {
            if (estaSimplificado) AlternarDetalhes(true);
        }
    }

    void AlternarDetalhes(bool mostrar)
    {
        estaSimplificado = !mostrar;

        // Liga/Desliga objetos pequenos
        foreach (var obj in detalhesPequenos)
        {
            if (obj != null && obj.activeSelf != mostrar) 
                obj.SetActive(mostrar);
        }

        // Liga/Desliga scripts decorativos (radares girando, animações leves)
        foreach (var script in scriptsDecorativos)
        {
            if (script != null && script.enabled != mostrar) 
                script.enabled = mostrar;
        }
    }

    void definirVisibilidadeTotal(bool visivel)
    {
        estaInvisivel = !visivel;

        // Desliga/Liga os renderers principais (MeshRenderer, SkinnedMeshRenderer)
        foreach (var r in renderersPrincipais)
        {
            if (r != null) r.enabled = visivel;
        }
        
        // Garante que os detalhes sigam a visibilidade
        AlternarDetalhes(visivel ? !estaSimplificado : false);
    }
}
