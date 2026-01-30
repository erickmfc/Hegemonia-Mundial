using UnityEngine;
using UnityEngine.AI;

[ExecuteInEditMode] // Permite ver o resultado no Editor sem dar Play
public class CorretorAltura : MonoBehaviour
{
    [Header("Ajuste de Altura")]
    [Tooltip("Quanto mais alto, mais o veiculo sobe. Pode ser negativo.")]
    public float alturaExtra = 0.0f;

    [Tooltip("Se marcado, tenta alinhar a rotação com a inclinação do terreno (bom para rampas).")]
    public bool alinharComTerreno = false;

    private NavMeshAgent agente;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        // 1. Correção via NavMeshAgent (O jeito certo para unidades NavMesh)
        if (agente == null) agente = GetComponent<NavMeshAgent>();

        if (agente != null)
        {
            // O BaseOffset levanta o modelo visual SEM tirar o agente do NavMesh
            agente.baseOffset = alturaExtra;
        }
        else
        {
            // 2. Fallback: Se não tem NavMesh, tenta ajustar transform local (menos recomendado)
            // Mas cuidado para não brigar com física/gravidade
        }

        // 3. Alinhamento com Terreno (Opcional)
        if (alinharComTerreno)
        {
            RaycastHit hit;
            // Lança raio para baixo para ver a inclinação
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out hit, 5.0f))
            {
                Quaternion novaRotacao = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
                transform.rotation = Quaternion.Slerp(transform.rotation, novaRotacao, Time.deltaTime * 5f);
            }
        }
    }

    // Função pública para ajustar via código se precisar
    public void DefinirAltura(float novaAltura)
    {
        alturaExtra = novaAltura;
    }
}
