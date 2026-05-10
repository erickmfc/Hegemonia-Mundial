using UnityEngine;

public class BombaAerea : MonoBehaviour
{
    public float raioExplosao = 15f;
    public float dano = 100f;
    public GameObject efeitoExplosao; // Prefab de partícula
    public AudioClip somExplosao;
    private readonly Collider[] bufferExplosao = new Collider[96];
    private bool explodiu;

    private void OnEnable()
    {
        explodiu = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        Explodir();
    }
    
    void Explodir()
    {
        if (explodiu)
        {
            return;
        }

        explodiu = true;

        // 1. Dano em área
        int hits = Physics.OverlapSphereNonAlloc(transform.position, raioExplosao, bufferExplosao, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            Collider hit = bufferExplosao[i];
            if (hit == null)
            {
                continue;
            }

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
        if (efeitoExplosao != null) PoolDeObjetosCombate.SpawnTemporario(efeitoExplosao, transform.position, Quaternion.identity, 4f);
        if (somExplosao != null) AudioSource.PlayClipAtPoint(somExplosao, transform.position);
        
        Debug.Log("💥 BOOM! Bomba explodiu.");
        PoolDeObjetosCombate.Release(gameObject); // Remove a bomba
    }
}

public class CaixaCura : MonoBehaviour
{
    public float raioCura = 10f;
    public float quantidadeCura = 50f;
    public GameObject efeitoCura;
    private readonly Collider[] bufferCura = new Collider[96];
    private bool ativou;

    private void OnEnable()
    {
        ativou = false;
    }
    
    void OnCollisionEnter(Collision collision)
    {
        AtivarCura();
    }
    
    void AtivarCura()
    {
        if (ativou)
        {
            return;
        }

        ativou = true;

        // 1. Cura em área
        int hits = Physics.OverlapSphereNonAlloc(transform.position, raioCura, bufferCura, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        int curados = 0;
        
        for (int i = 0; i < hits; i++)
        {
            Collider hit = bufferCura[i];
            if (hit == null)
            {
                continue;
            }

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
        if (efeitoCura != null) PoolDeObjetosCombate.SpawnTemporario(efeitoCura, transform.position, Quaternion.identity, 3f);
        
        Debug.Log($"💊 Suprimentos entregues! {curados} unidades curadas.");
        PoolDeObjetosCombate.Release(gameObject);
    }
}
