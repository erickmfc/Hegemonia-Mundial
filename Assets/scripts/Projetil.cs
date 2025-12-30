using UnityEngine;

public class Projetil : MonoBehaviour
{
    [Header("Configuração da Bala")]
    public float velocidade = 70f;
    public int dano = 20;
    public float tempoDeVida = 5f; // Tempo até autodestruir (evita balas eternas)
    public GameObject efeitoExplosao; // (Opcional) Partícula de explosão

    [Header("Debug")]
    public bool mostrarDebug = true;

    private Vector3 direcao; // Direção fixa ao ser disparado
    private bool inicializado = false;
    private int frameCounter = 0;

    void Start()
    {
        // Se não foi inicializado com SetDirecao, usa a direção forward do objeto
        if (!inicializado)
        {
            direcao = transform.forward;
        }
        
        // VERIFICAÇÃO E AUTO-CORREÇÃO DE COMPONENTES OBRIGATÓRIOS
        Collider col = GetComponent<Collider>();
        Rigidbody rb = GetComponent<Rigidbody>();
        
        if (mostrarDebug)
        {
            Debug.Log($"🚀 PROJÉTIL CRIADO: {gameObject.name}");
        }
        
        // AUTO-FIX: Cria Collider se não existir
        if (col == null)
        {
            Debug.LogWarning($"⚠️ {gameObject.name} não tinha Collider! Criando automaticamente...");
            SphereCollider newCol = gameObject.AddComponent<SphereCollider>();
            newCol.radius = 0.15f; // Raio padrão
            newCol.isTrigger = true;
            col = newCol;
            Debug.Log($"✅ SphereCollider adicionado automaticamente!");
        }
        else
        {
            if (mostrarDebug)
            {
                Debug.Log($"✅ Collider encontrado: {col.GetType().Name} | IsTrigger={col.isTrigger}");
            }
            
            // AUTO-FIX: Marca como trigger se não estiver
            if (!col.isTrigger)
            {
                Debug.LogWarning($"⚠️ Collider não era trigger! Corrigindo...");
                col.isTrigger = true;
                Debug.Log($"✅ Collider agora é Trigger!");
            }
        }
        
        // AUTO-FIX: Cria Rigidbody se não existir
        if (rb == null)
        {
            Debug.LogWarning($"⚠️ {gameObject.name} não tinha Rigidbody! Criando automaticamente...");
            Rigidbody newRb = gameObject.AddComponent<Rigidbody>();
            newRb.useGravity = false;
            newRb.isKinematic = true;
            rb = newRb;
            Debug.Log($"✅ Rigidbody adicionado automaticamente!");
        }
        else
        {
            if (mostrarDebug)
            {
                Debug.Log($"✅ Rigidbody: IsKinematic={rb.isKinematic} | UseGravity={rb.useGravity}");
            }
            
            // AUTO-FIX: Configura Rigidbody corretamente
            if (rb.useGravity)
            {
                rb.useGravity = false;
                Debug.Log($"✅ Gravidade desativada!");
            }
            if (!rb.isKinematic)
            {
                rb.isKinematic = true;
                Debug.Log($"✅ Rigidbody configurado como Kinematic!");
            }
        }
        
        if (mostrarDebug)
        {
            Debug.Log($"📍 Direção: {direcao} | Velocidade: {velocidade}");
            Debug.Log($"🎯 Projétil totalmente configurado e pronto!");
        }
        
        // Autodestrói após o tempo de vida
        Destroy(gameObject, tempoDeVida);
    }

    /// <summary>
    /// Define a direção fixa que o projétil vai seguir (linha reta)
    /// </summary>
    public void SetDirecao(Vector3 novaDirecao)
    {
        direcao = novaDirecao.normalized;
        transform.forward = direcao; // Faz o projétil apontar na direção
        inicializado = true;
        
        if (mostrarDebug)
        {
            Debug.Log($"🎯 Direção definida: {direcao}");
        }
    }

    void Update()
    {
        // 🎯 TÉCNICA DO LASER INVISÍVEL (Raycast)
        // Calcula a distância que a bala vai percorrer neste frame
        float distanciaNesteFrame = velocidade * Time.deltaTime;
        
        // Lança um "laser invisível" para frente para detectar colisões ANTES de mover
        RaycastHit hit;
        if (Physics.Raycast(transform.position, direcao, out hit, distanciaNesteFrame))
        {
            // 💥 DETECTOU ALGO NO CAMINHO!
            if (mostrarDebug)
            {
                Debug.Log($"🔍 RAYCAST DETECTOU: {hit.collider.gameObject.name} a {hit.distance}m de distância");
            }
            
            // Verifica se é um alvo válido
            if (!hit.collider.isTrigger && !hit.collider.CompareTag("Player"))
            {
                if (hit.collider.CompareTag("Aereo") || hit.collider.CompareTag("Inimigo"))
                {
                    if (mostrarDebug)
                    {
                        Debug.Log($"🎯 Raycast confirmou ALVO VÁLIDO! Aplicando dano imediato.");
                    }
                    
                    // Move a bala até o ponto de impacto exato
                    transform.position = hit.point;
                    
                    // Aplica o dano
                    AtingirAlvo(hit.collider.gameObject);
                    return; // Sai do Update pois a bala será destruída
                }
            }
        }
        
        // Se não detectou nada, move normalmente em LINHA RETA
        transform.Translate(direcao * velocidade * Time.deltaTime, Space.World);
        
        // Debug a cada segundo
        frameCounter++;
        if (mostrarDebug && frameCounter % 60 == 0)
        {
            Debug.Log($"🔵 Projétil {gameObject.name} ainda ativo na posição {transform.position}");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (mostrarDebug)
        {
            Debug.Log($"💥 COLISÃO DETECTADA! Projétil colidiu com: {other.gameObject.name} | Tag: {other.tag} | IsTrigger: {other.isTrigger}");
        }

        // Ignora colisão com próprio atirador e triggers
        if (other.isTrigger)
        {
            if (mostrarDebug) Debug.Log($"⏭️ Ignorado (outro objeto é trigger)");
            return;
        }
        
        if (other.CompareTag("Player"))
        {
            if (mostrarDebug) Debug.Log($"⏭️ Ignorado (aliado)");
            return;
        }

        // Verifica se atingiu um inimigo (aéreo ou terrestre)
        if (other.CompareTag("Aereo") || other.CompareTag("Inimigo"))
        {
            if (mostrarDebug) Debug.Log($"🎯 ALVO VÁLIDO! Aplicando dano...");
            AtingirAlvo(other.gameObject);
        }
        else
        {
            if (mostrarDebug) Debug.Log($"🌍 Atingiu objeto não-alvo ({other.tag}), destruindo projétil");
            Destroy(gameObject);
        }
    }

    void AtingirAlvo(GameObject alvo)
    {
        if (alvo == null) return;

        Debug.Log($"💥💥💥 PROJÉTIL ATINGIU: {alvo.name}");

        // Tenta causar dano com sistema de Vida
        Vida vidaAlvo = alvo.GetComponent<Vida>();
        if (vidaAlvo != null)
        {
            vidaAlvo.ReceberDano(dano);
            Debug.Log($"✅ Dano de {dano} aplicado via sistema Vida");
        }
        else
        {
            // Tenta causar dano em prédios
            AtributosPredio predio = alvo.GetComponent<AtributosPredio>();
            if (predio != null)
            {
                predio.vidaAtual -= dano;
                Debug.Log($"✅ Dano de {dano} aplicado em prédio");
                
                if (predio.vidaAtual <= 0)
                {
                    Destroy(alvo);
                }
            }
            else
            {
                Debug.LogWarning($"⚠️ {alvo.name} não tem componente Vida nem AtributosPredio!");
            }
        }

        // Efeito de explosão
        if (efeitoExplosao != null)
        {
            Instantiate(efeitoExplosao, transform.position, transform.rotation);
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (mostrarDebug)
        {
            Debug.Log($"💀 Projétil {gameObject.name} foi destruído");
        }
    }
}


