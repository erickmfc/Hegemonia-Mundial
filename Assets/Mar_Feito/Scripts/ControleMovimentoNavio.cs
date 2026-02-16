using UnityEngine;
using UnityEngine.AI; // Necessário para usar o NavMesh

public class ControleMovimentoNavio : MonoBehaviour {
    private NavMeshAgent agente;
    private Camera cam;

    void Start() {
        agente = GetComponent<NavMeshAgent>();
        cam = Camera.main;
    }

    void Update() {
        // Se clicar com o Botão Direito do Mouse
        if (Input.GetMouseButtonDown(1)) {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Se o clique atingir a água (NavMesh)
            if (Physics.Raycast(ray, out hit)) {
                // Verifica se o agente existe e está ativo antes de dar o comando
                if (agente != null && agente.isOnNavMesh) {
                    agente.SetDestination(hit.point);
                }
            }
        }
    }
}
