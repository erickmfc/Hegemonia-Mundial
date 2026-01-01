using UnityEngine;

public class SistemaDeTiro : MonoBehaviour
{
    public GameObject prefabProjetil; // A munição
    public Transform bocaDoCano;      // O ponto de saída
    public float forcaDoTiro = 1000f; // Se usarmos física depois

    [Header("Áudio")]
    public AudioClip somTiro; // Para eu arrastar o arquivo .wav no Inspector
    private AudioSource fonteAudio;

    private ControleUnidade selecao; // Para saber se podemos atirar

    void Start()
    {
        // Procura o script de seleção no Tanque (pai ou avô deste objeto)
        selecao = GetComponentInParent<ControleUnidade>();

        // Inicialização Inteligente
        fonteAudio = GetComponent<AudioSource>();
        if (fonteAudio == null)
        {
            fonteAudio = gameObject.AddComponent<AudioSource>();
        }
        fonteAudio.spatialBlend = 1.0f; // Som 3D
    }

    void Update()
    {
        // Se o tanque está selecionado E apertou ESPAÇO
        if (selecao != null && selecao.selecionado && Input.GetKeyDown(KeyCode.Space))
        {
            Atirar();
        }
    }

    void Atirar()
    {
        // Cria a bala na posição da boca do cano e com a rotação da boca do cano
        GameObject bala = Instantiate(prefabProjetil, bocaDoCano.position, bocaDoCano.rotation);
        
        // Define quem atirou (para não se auto-atacar)
        Projetil scriptBala = bala.GetComponent<Projetil>();
        if (scriptBala != null)
        {
            scriptBala.SetDono(transform.root.gameObject);
        }

        // Execução do Som
        if (fonteAudio != null && somTiro != null)
        {
            fonteAudio.PlayOneShot(somTiro);
        }
    }
}
