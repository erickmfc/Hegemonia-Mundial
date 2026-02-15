using UnityEngine;
using UnityEngine.AI;

public class AjusteCadaver : MonoBehaviour
{
    [Header("Configuração de Morte")]
    [Tooltip("Rotação no eixo X para deitar o corpo. Tente -90, 90 ou 180.")]
    public float rotacaoX = 180f; // 180 conforme solicitado pelo usuário

    void Awake()
    {
        // 1. Remove Animator IMEDIATAMENTE antes que ele rode um frame
        var anim = GetComponent<Animator>();
        if (anim != null) 
        {
            anim.enabled = false; // Desativa lógica
            Destroy(anim);        // Destrói componente
        }

        // 2. Força a rotação para deitado (mantendo a direção Y que o soldado olhava)
        // O instantiate copia a rotação Y do vivo, então nós preservamos ela.
        Vector3 eulerAtual = transform.eulerAngles;
        // Se 180 for "costas" e 0 for "frente", isso depende do modelo, mas vamos travar o X e Z.
        transform.eulerAngles = new Vector3(rotacaoX, eulerAtual.y, 0f);

        var agent = GetComponent<NavMeshAgent>();
        if (agent != null) Destroy(agent);

        // 3. Remove lógica de jogo (Identidade, Tiro, Controle)
        var id = GetComponent<IdentidadeUnidade>();
        if (id != null) Destroy(id);

        var tiro = GetComponent<SistemaDeTiro>();
        if (tiro != null) Destroy(tiro);

        var controle = GetComponent<ControleUnidade>();
        if (controle != null) Destroy(controle);

        // 4. Desativa colisões para não bloquear balas
        // (Deixa Trigger se quiser detectar mouse, mas bloqueia física solida)
        var cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols)
        {
            c.enabled = false;
        }

        // 5. Ajuste fino de posição (para não ficar flutuando nem enterrado)
        transform.position += Vector3.up * 0.05f;

        Debug.Log($"[AjusteCadaver] Corpo ajustado: RotX={rotacaoX}, Componentes removidos.");
    }
}
