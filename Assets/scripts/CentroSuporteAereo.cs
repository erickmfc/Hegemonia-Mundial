using UnityEngine;
using System.Collections;

public class CentroSuporteAereo : MonoBehaviour
{
    [Header("Configurações Gerais")]
    public float alturaVoo = 100f; // Altura que os aviões passam
    public float velocidadeAviao = 150f;
    
    [Header(" ✈️ Ataque Aéreo Cirúrgico")]
    public GameObject prefabAviaoAtaque; // O modelo do jato
    public GameObject prefabBomba;       // O que ele solta
    public float cooldownAtaque = 30f;
    public KeyCode teclaAtalhoAtaque = KeyCode.Alpha1; // Tecla '1'
    
    [Header(" 📦 Queda de Suprimentos")]
    public GameObject prefabAviaoCarga;   // O modelo do cargueiro
    public GameObject prefabCaixaSuprimentos; // A caixa que cura
    public float cooldownSuprimentos = 60f;
    public KeyCode teclaAtalhoSuprimentos = KeyCode.Alpha2; // Tecla '2'
    
    [Header("Interface")]
    public Texture2D cursorMira;
    
    // Variáveis Internas
    private float timerAtaque = 0f;
    private float timerSuprimentos = 0f;
    private bool modoMiraAtaque = false;
    private bool modoMiraSuprimentos = false;
    
    void Update()
    {
        AtualizarCooldowns();
        ProcessarInput();
    }
    
    void AtualizarCooldowns()
    {
        if (timerAtaque > 0) timerAtaque -= Time.deltaTime;
        if (timerSuprimentos > 0) timerSuprimentos -= Time.deltaTime;
    }
    
    void ProcessarInput()
    {
        // 1. Ativar Mira Ataque
        if (Input.GetKeyDown(teclaAtalhoAtaque) && timerAtaque <= 0)
        {
            modoMiraAtaque = true;
            modoMiraSuprimentos = false;
            Debug.Log("🎯 Mira de Ataque Aéreo Ativada! Clique no alvo.");
        }
        
        // 2. Ativar Mira Suprimentos
        if (Input.GetKeyDown(teclaAtalhoSuprimentos) && timerSuprimentos <= 0)
        {
            modoMiraSuprimentos = true;
            modoMiraAtaque = false;
            Debug.Log("📦 Mira de Suprimentos Ativada! Escolha onde soltar.");
        }
        
        // 3. Confirmar Alvo (Clique)
        if (Input.GetMouseButtonDown(0))
        {
            if (modoMiraAtaque || modoMiraSuprimentos)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                
                if (Physics.Raycast(ray, out hit))
                {
                    if (modoMiraAtaque) ChamarAtaque(hit.point);
                    if (modoMiraSuprimentos) ChamarSuprimentos(hit.point);
                    
                    // Reseta miras
                    modoMiraAtaque = false;
                    modoMiraSuprimentos = false;
                }
            }
        }
        
        // 4. Cancelar (Botão Direito)
        if (Input.GetMouseButtonDown(1))
        {
            modoMiraAtaque = false;
            modoMiraSuprimentos = false;
        }
    }
    
    void ChamarAtaque(Vector3 alvo)
    {
        Debug.Log($"✈️ Ataque Aéreo solicitado em: {alvo}");
        timerAtaque = cooldownAtaque;
        
        // Cria o avião fora da tela
        Vector3 inicio = alvo + (Vector3.back * 500f) + (Vector3.up * alturaVoo);
        Vector3 fim = alvo + (Vector3.forward * 500f) + (Vector3.up * alturaVoo);
        
        GameObject aviao = null;
        if (prefabAviaoAtaque != null)
            aviao = Instantiate(prefabAviaoAtaque, inicio, Quaternion.LookRotation(Vector3.forward));
        else
            aviao = CriarAviaoTemporario(inicio, Color.red); // Placeholder se não tiver prefab
            
        // Adiciona comportamento
        StartCoroutine(VoarESoltarCarga(aviao, inicio, fim, alvo, prefabBomba, true));
    }
    
    void ChamarSuprimentos(Vector3 alvo)
    {
        Debug.Log($"📦 Suprimentos solicitados em: {alvo}");
        timerSuprimentos = cooldownSuprimentos;
        
        Vector3 inicio = alvo + (Vector3.left * 500f) + (Vector3.up * alturaVoo); // Vem do lado
        Vector3 fim = alvo + (Vector3.right * 500f) + (Vector3.up * alturaVoo);
        
        GameObject aviao = null;
        if (prefabAviaoCarga != null)
            aviao = Instantiate(prefabAviaoCarga, inicio, Quaternion.LookRotation(Vector3.right));
        else
            aviao = CriarAviaoTemporario(inicio, Color.green);
            
        StartCoroutine(VoarESoltarCarga(aviao, inicio, fim, alvo, prefabCaixaSuprimentos, false));
    }
    
    // --- LÓGICA DO VOO ---
    IEnumerator VoarESoltarCarga(GameObject aviao, Vector3 inicio, Vector3 fim, Vector3 alvoLiberacao, GameObject cargaPrefab, bool ehBomba)
    {
        float distanciaTotal = Vector3.Distance(inicio, fim);
        float duracao = distanciaTotal / velocidadeAviao;
        float tempoAtual = 0f;
        
        bool cargaLiberada = false;
        
        while (tempoAtual < duracao)
        {
            if (aviao == null) yield break;
            
            tempoAtual += Time.deltaTime;
            float progresso = tempoAtual / duracao;
            
            // Move o avião
            aviao.transform.position = Vector3.Lerp(inicio, fim, progresso);
            
            // Checa se está perto do ponto de soltura (só checa X e Z)
            float distHorizontal = Vector2.Distance(new Vector2(aviao.transform.position.x, aviao.transform.position.z), 
                                                  new Vector2(alvoLiberacao.x, alvoLiberacao.z));
                                                  
            if (!cargaLiberada && distHorizontal < 5.0f)
            {
                cargaLiberada = true;
                LiberarCarga(aviao.transform.position, cargaPrefab, ehBomba);
            }
            
            yield return null;
        }
        
        Destroy(aviao); // Some quando sai da tela
    }
    
    void LiberarCarga(Vector3 posicao, GameObject prefab, bool ehBomba)
    {
        if (prefab == null)
        {
            Debug.LogWarning("⚠️ Sem prefab de carga configurado!");
            return;
        }
        
        GameObject carga = Instantiate(prefab, posicao, Quaternion.identity);
        Rigidbody rb = carga.GetComponent<Rigidbody>();
        if (rb == null) rb = carga.AddComponent<Rigidbody>();
        
        // Adiciona lógica de explosão/cura
        if (ehBomba)
        {
             // Adiciona script de bomba se não tiver PROVISÓRIO
             // O ideal é você ter um script Bomba, mas vou fazer um genérico aqui
        }
    }
    
    // Placeholder para testes (cria um cubo voado se não tiver modelo)
    GameObject CriarAviaoTemporario(Vector3 pos, Color cor)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(8, 2, 8); // Formato asa delta
        go.GetComponent<Renderer>().material.color = cor;
        return go;
    }
    
    void OnGUI()
    {
        // HUD Básico para mostrar disponibilidade
        GUILayout.BeginArea(new Rect(20, Screen.height - 150, 200, 120));
        
        string statusAtaque = timerAtaque > 0 ? $"{timerAtaque:F1}s" : "[1] PRONTO";
        string statusSupri = timerSuprimentos > 0 ? $"{timerSuprimentos:F1}s" : "[2] PRONTO";
        
        GUI.color = timerAtaque > 0 ? Color.grey : Color.red;
        GUILayout.Label($"✈️ ATAQUE AÉREO: {statusAtaque}");
        
        GUI.color = timerSuprimentos > 0 ? Color.grey : Color.green;
        GUILayout.Label($"📦 SUPRIMENTOS: {statusSupri}");
        
        GUILayout.EndArea();
    }
}
