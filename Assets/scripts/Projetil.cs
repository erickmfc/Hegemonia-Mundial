using UnityEngine;

public class Projetil : MonoBehaviour
{
    [Header("Configuração da Bala")]
    public float velocidade = 70f;
    public int dano = 20;
    public GameObject efeitoExplosao; // (Opcional) Partícula de explosão

    private Transform alvo;

    public void Perseguir(Transform _alvo)
    {
        alvo = _alvo;
    }

    void Update()
    {
        if (alvo == null)
        {
            Destroy(gameObject); // Se o alvo sumiu, a bala se autodestrói
            return;
        }

        // 1. Direção até o alvo
        Vector3 direcao = alvo.position - transform.position;
        float distanciaNesteFrame = velocidade * Time.deltaTime;

        // 2. Se chegou perto o suficiente, acerta
        if (direcao.magnitude <= distanciaNesteFrame)
        {
            AtingirAlvo();
            return;
        }

        // 3. Move a bala
        transform.Translate(direcao.normalized * distanciaNesteFrame, Space.World);
        transform.LookAt(alvo); // Bala aponta para o alvo
    }

    void AtingirAlvo()
    {
        // AQUI ENTRARIA O CÓDIGO DE TIRAR VIDA DO INIMIGO
        Debug.Log("ACERTOU O ALVO: " + alvo.name);

        if (efeitoExplosao != null)
        {
            Instantiate(efeitoExplosao, transform.position, transform.rotation);
        }

        Destroy(gameObject);
    }
}
