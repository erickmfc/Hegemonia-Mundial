using UnityEngine;

public class ControleTorre : MonoBehaviour
{
    private ControleUnidade selecaoDoTanque;
    [SerializeField] private float velocidadeGiro = 5f;
    private Vector3 eulerRepouso;

    void Start()
    {
        selecaoDoTanque = GetComponentInParent<ControleUnidade>();
        eulerRepouso = transform.localEulerAngles;
    }

    void Update()
    {
        if (selecaoDoTanque != null && selecaoDoTanque.selecionado)
            MirarNoMouse();
    }

    void MirarNoMouse()
    {
        Camera cameraPrincipal = Camera.main;
        if (cameraPrincipal == null) return;

        Ray raio = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (!Physics.Raycast(raio, out hit)) return;

        Vector3 pontoDeMira = hit.point;
        pontoDeMira.y = transform.position.y;
        Vector3 direcao = pontoDeMira - transform.position;
        direcao.y = 0f;
        if (direcao.sqrMagnitude < 0.001f) return;

        if (transform.parent != null)
        {
            Vector3 localDir = transform.parent.InverseTransformDirection(direcao);
            float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            Quaternion rotacaoAlvo = Quaternion.Euler(eulerRepouso.x, yaw, eulerRepouso.z);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, rotacaoAlvo, Time.deltaTime * velocidadeGiro);
        }
        else
        {
            float yawMundo = Mathf.Atan2(direcao.x, direcao.z) * Mathf.Rad2Deg;
            Quaternion rotacaoAlvoMundo = Quaternion.Euler(eulerRepouso.x, yawMundo, eulerRepouso.z);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvoMundo, Time.deltaTime * velocidadeGiro);
        }
    }
}
