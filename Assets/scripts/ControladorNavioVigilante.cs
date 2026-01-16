using UnityEngine;
using UnityEngine.AI;

public class ControladorNavioVigilante : MonoBehaviour
{
    [Header("Configurações de Combate")]
    public float alcanceAtaque = 15f; // Alcance curto como solicitado
    public float cadenciaTiro = 0.5f; // Tiros muito rápidos
    public GameObject projetilPrefab; // Arraste sua munição neon aqui
    public Transform[] pontosDisparo; // Agora suporta múltiplos pontos de disparo (3 pontos)

    [Header("Estabilidade (Antygaviti)")]
    public float antygaviti = 5f; // Mantém o navio estável na água

    private NavMeshAgent agent;
    private float cronometroTiro;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        // Garante que o navio esteja no nível da água (Y = 0)
        transform.position = new Vector3(transform.position.x, 0, transform.position.z);
    }

    void Update()
    {
        EstabilizarNavio();
        ProcurarEAtacarInimigos();
    }

    void EstabilizarNavio()
    {
        // Lógica Antygaviti: mantém o navio travado na altura 0 da água
        if (transform.position.y != 0)
        {
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, 0, Time.deltaTime * antygaviti);
            transform.position = pos;
        }
    }

    void ProcurarEAtacarInimigos()
    {
        cronometroTiro += Time.deltaTime;

        // Procura todos os objetos com a Tag "Inimigo"
        GameObject[] inimigos = GameObject.FindGameObjectsWithTag("Inimigo");
        GameObject alvoMaisProximo = null;
        float menorDistancia = alcanceAtaque;

        foreach (GameObject inimigo in inimigos)
        {
            float distancia = Vector3.Distance(transform.position, inimigo.transform.position);
            if (distancia < menorDistancia)
            {
                menorDistancia = distancia;
                alvoMaisProximo = inimigo;
            }
        }

        // Se achou alguém perto, atira rápido!
        if (alvoMaisProximo != null && cronometroTiro >= cadenciaTiro)
        {
            Atirar(alvoMaisProximo.transform.position);
            cronometroTiro = 0;
        }
    }

    void Atirar(Vector3 posicaoAlvo)
    {
        if (projetilPrefab != null && pontosDisparo != null)
        {
            foreach (Transform ponto in pontosDisparo)
            {
                if (ponto != null)
                {
                    Vector3 direcao = posicaoAlvo - ponto.position;
                    if (direcao != Vector3.zero)
                    {
                        Instantiate(projetilPrefab, ponto.position, Quaternion.LookRotation(direcao));
                    }
                }
            }
            Debug.Log("[Marinha] Navio Vigilante disparando de múltiplos canhões!");
        }
    }
}
