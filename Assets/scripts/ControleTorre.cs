using UnityEngine;

public class ControleTorre : MonoBehaviour
{
    private ControleUnidade selecaoDoTanque;
    [SerializeField] private float velocidadeGiro = 5f;

    void Start()
    {
        // Procura o script de seleção no "Pai" (o corpo do tanque ou o objeto raiz)
        selecaoDoTanque = GetComponentInParent<ControleUnidade>();
    }

    void Update()
    {
        // A torre só deve mirar se o tanque estiver SELECIONADO
        if (selecaoDoTanque != null && selecaoDoTanque.selecionado == true)
        {
            MirarNoMouse();
        }
    }

    void MirarNoMouse()
    {
        // Lança um raio do mouse para o mundo
        Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Se o raio bater em algo (no chão)
        if (Physics.Raycast(raio, out hit))
        {
            // Pega o ponto onde o mouse tocou
            Vector3 pontoDeMira = hit.point;

            // IMPORTANTE: Ignora a altura (Y) para a torre não olhar para o chão ou pro céu
            pontoDeMira.y = transform.position.y;

            // Calcula a direção
            Vector3 direcao = pontoDeMira - transform.position;

            // Cria a rotação necessária
            Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);

            // Gira suavemente a torre em direção ao alvo
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, Time.deltaTime * velocidadeGiro);
        }
    }
}
