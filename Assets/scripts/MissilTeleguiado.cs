using UnityEngine;

public class MissilTeleguiado : MonoBehaviour
{
    private Transform alvo;
    public float velocidade = 80f; // Aumentado de 50 para 80
    public int dano = 20;
    public float distanciaDeImpacto = 2f; // Distância considerada como "acertou"

    public void DefinirAlvo(Transform novoAlvo)
    {
        alvo = novoAlvo;
        Destroy(gameObject, 15f); // Aumentado de 5s para 15s (tempo suficiente)
    }

    void Update()
    {
        // Se perdeu o alvo, tenta encontrar outro
        if (alvo == null)
        {
            BuscarNovoAlvo();
            
            // Se ainda não tem alvo, destrói o míssil
            if (alvo == null)
            {
                Destroy(gameObject);
                return;
            }
        }

        // Voar até o alvo
        Vector3 direcao = alvo.position - transform.position;
        float distancia = direcao.magnitude;
        float distanciaFrame = velocidade * Time.deltaTime;

        // Verifica se chegou perto o suficiente para causar dano
        if (distancia <= distanciaDeImpacto)
        {
            // Bateu no alvo! Causa dano
            AplicarDano();
            Destroy(gameObject);
            return;
        }

        // Move o míssil em direção ao alvo
        transform.Translate(direcao.normalized * distanciaFrame, Space.World);
        transform.LookAt(alvo);
    }

    void BuscarNovoAlvo()
    {
        // Procura inimigos próximos
        GameObject[] inimigos = GameObject.FindGameObjectsWithTag("Aereo");
        
        float menorDistancia = 100f; // Busca apenas em 100 unidades de raio
        GameObject novoInimigo = null;

        foreach (GameObject inimigo in inimigos)
        {
            float dist = Vector3.Distance(transform.position, inimigo.transform.position);
            if (dist < menorDistancia)
            {
                menorDistancia = dist;
                novoInimigo = inimigo;
            }
        }

        if (novoInimigo != null)
        {
            alvo = novoInimigo.transform;
            Debug.Log($"Míssil redirecionado para {alvo.name}!");
        }
    }

    void AplicarDano()
    {
        if (alvo == null) return;

        // PRIORIDADE 1: Tenta causar dano em unidades (com SistemaDeDanos)
        SistemaDeDanos sistemaDanos = alvo.GetComponent<SistemaDeDanos>();
        if (sistemaDanos != null)
        {
            sistemaDanos.ReceberDano(dano);
            Debug.Log($"Míssil causou {dano} de dano em {alvo.name}!");
            return;
        }

        // PRIORIDADE 2: Tenta causar dano em prédios (com script "AtributosPredio")
        AtributosPredio predioAtrib = alvo.GetComponent<AtributosPredio>();
        if (predioAtrib != null)
        {
            predioAtrib.vidaAtual -= dano;
            Debug.Log($"Míssil causou {dano} de dano no prédio {alvo.name}!");
            
            if (predioAtrib.vidaAtual <= 0)
            {
                Destroy(alvo.gameObject);
            }
            return;
        }

        // FALLBACK: Destrói diretamente
        if (alvo.CompareTag("Aereo") || alvo.CompareTag("Inimigo"))
        {
            Destroy(alvo.gameObject);
            Debug.Log($"{alvo.name} foi destruído pelo míssil!");
        }
    }
}
