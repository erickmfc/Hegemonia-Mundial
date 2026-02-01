using UnityEngine;

public class BombaAerea : MonoBehaviour
{
    public float raioExplosao = 15f;
    public float dano = 100f;
    public GameObject efeitoExplosao; // Prefab de partícula
    public AudioClip somExplosao;

    void OnCollisionEnter(Collision collision)
    {
        Explodir();
    }
    
    void Explodir()
    {
        // 1. Dano em área
        Collider[] hits = Physics.OverlapSphere(transform.position, raioExplosao);
        foreach (var hit in hits)
        {
            // Tenta achar sistema de danos no objeto ou nos pais
            SistemaDeDanos vida = hit.GetComponent<SistemaDeDanos>();
            if (vida == null) vida = hit.GetComponentInParent<SistemaDeDanos>();
            
            if (vida != null)
            {
                // Dano reduz com a distância (mais longe = menos dano)
                float distancia = Vector3.Distance(transform.position, hit.transform.position);
                float fatorDano = 1f - Mathf.Clamp01(distancia / raioExplosao);
                vida.ReceberDano(dano * fatorDano);
            }
        }
        
        // 2. Efeitos
        if (efeitoExplosao != null) Instantiate(efeitoExplosao, transform.position, Quaternion.identity);
        if (somExplosao != null) AudioSource.PlayClipAtPoint(somExplosao, transform.position);
        
        Debug.Log("💥 BOOM! Bomba explodiu.");
        Destroy(gameObject); // Remove a bomba
    }
}

public class CaixaCura : MonoBehaviour
{
    public float raioCura = 10f;
    public float quantidadeCura = 50f;
    public GameObject efeitoCura;
    
    void OnCollisionEnter(Collision collision)
    {
        AtivarCura();
    }
    
    void AtivarCura()
    {
        // 1. Cura em área
        Collider[] hits = Physics.OverlapSphere(transform.position, raioCura);
        int curados = 0;
        
        foreach (var hit in hits)
        {
            SistemaDeDanos vida = hit.GetComponent<SistemaDeDanos>();
            if (vida == null) vida = hit.GetComponentInParent<SistemaDeDanos>();
            
            if (vida != null)
            {
                // Verifica amizade (opcional - por enquanto cura todos na área)
                IdentidadeUnidade id = vida.GetComponent<IdentidadeUnidade>();
                if (id != null && id.teamID == 1) // Só cura Player (Team 1)
                {
                    // Lógica de cura (assumindo que SistemaDeDanos tem Curar ou mexemos na variavel)
                    vida.vidaAtual = Mathf.Min(vida.vidaAtual + quantidadeCura, vida.vidaMaxima);
                    curados++;
                }
            }
        }
        
        // 2. Feedback Visual
        if (efeitoCura != null) Instantiate(efeitoCura, transform.position, Quaternion.identity);
        
        Debug.Log($"💊 Suprimentos entregues! {curados} unidades curadas.");
        Destroy(gameObject);
    }
}
