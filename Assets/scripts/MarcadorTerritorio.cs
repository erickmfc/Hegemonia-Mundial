using UnityEngine;

[RequireComponent(typeof(IdentidadeUnidade))]
public class MarcadorTerritorio : MonoBehaviour
{
    [Header("Área de Domínio")]
    [Tooltip("Metade do tamanho do lado do quadrado. Ex: Raio 100 cria um domínio de 200x200 metros quadrados.")]
    public float raioDeDominio = 100f; 
    public bool ehPrefeitura = false; // Se for Prefeitura, raio = 300 e define regras de ilha

    [Header("Visualização de Fronteira no Jogo")]
    [Tooltip("Mostra linhas e uma área espelhada transparente no campo de batalha para definir de quem é a área.")]
    public bool mostrarBordasNoJogo = true;
    public float larguraLinha = 1.5f; // Grossura do risco pintado no chão

    private LineRenderer linhaFronteira;
    private GameObject areaTranslúcida;

    // Identidade cache
    public int teamID { get; private set; }

    void Start()
    {
        var id = GetComponent<IdentidadeUnidade>();
        if (id != null) teamID = id.teamID;

        // Auto-detecta Prefeitura pelo script ou nome (apreciação da montagem anterior)
        if (GetComponent<ComplexoGovernamental>() != null || gameObject.name.ToLower().Contains("prefeitura"))
        {
            ehPrefeitura = true;
            if (raioDeDominio < 300f) raioDeDominio = 300f; // Prefeituras têm raio maior p/ controle central
        }

        // Aguarda 1 frame para garantir que os Gerentes existam antes de registrar
        Invoke("RegistrarSe", 0.5f);
        
        // Inicia identificação in-game
        Invoke("CriarIdentificacaoVisual", 0.6f);
    }

    void RegistrarSe()
    {
        if (GerenteDeTerritorio.Instancia != null)
        {
            GerenteDeTerritorio.Instancia.RegistrarMarcador(this);
        }
        GerenciadorDivisaoTerritorial.GarantirInstancia();
        if (GerenciadorDivisaoTerritorial.Instancia != null)
        {
            GerenciadorDivisaoTerritorial.Instancia.RegistrarCidade(this);
        }
    }

    void OnDestroy()
    {
        if (GerenteDeTerritorio.Instancia != null)
        {
            GerenteDeTerritorio.Instancia.RemoverMarcador(this);
        }
        if (GerenciadorDivisaoTerritorial.Instancia != null)
        {
            GerenciadorDivisaoTerritorial.Instancia.RemoverCidade(this);
        }
    }

    // --- RENDERIZA O PISO "HOLOGRÁFICO" COM A COR DO PAÍS QUE VOCÊ PEDIU ---
    void CriarIdentificacaoVisual()
    {
        if (!mostrarBordasNoJogo) return;

        // --- 1. A Linha da Fronteira (Quadrada) ---
        GameObject linhaObj = new GameObject("Visual_Fronteira_" + teamID);
        linhaObj.transform.SetParent(this.transform);
        linhaObj.transform.localPosition = Vector3.zero;
        
        linhaFronteira = linhaObj.AddComponent<LineRenderer>();
        linhaFronteira.useWorldSpace = false; // Em relação ao objeto pai
        linhaFronteira.startWidth = larguraLinha;
        linhaFronteira.endWidth = larguraLinha;
        linhaFronteira.positionCount = 5; 
        linhaFronteira.loop = true;
        linhaFronteira.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linhaFronteira.receiveShadows = false;
        
        Material mat = new Material(Shader.Find("Sprites/Default")); // Material brilhante limpo
        
        // Cores Dinâmicas pelo número da equipe (TeamID)
        Color corBase;
        switch (teamID)
        {
            case 1: corBase = new Color(0f, 0.4f, 1f, 1f); break; // Azul (Player)
            case 2: corBase = new Color(1f, 0.1f, 0.1f, 1f); break; // Vermelho (Inimigo)
            case 3: corBase = new Color(0f, 0.8f, 0f, 1f); break; // Verde
            case 4: corBase = new Color(1f, 0.8f, 0f, 1f); break; // Amarelo
            case 5: corBase = new Color(0.6f, 0f, 1f, 1f); break; // Roxo
            case 6: corBase = new Color(1f, 0f, 0.8f, 1f); break; // Rosa
            case 7: corBase = new Color(0f, 0.8f, 0.8f, 1f); break; // Ciano
            default: corBase = new Color(0.5f, 0.5f, 0.5f, 1f); break; // Cinza (Neutro ou 8+)
        }

        // BEM transparente:
        Color corBorda = new Color(corBase.r, corBase.g, corBase.b, 0.15f); // Era 0.8f
        Color corFundo = new Color(corBase.r, corBase.g, corBase.b, 0.02f); // Era 0.15f

        // Prefeituras são levemente mais visíveis (mas ainda transparentes)
        if (ehPrefeitura)
        {
            corBorda = new Color(corBase.r, corBase.g, corBase.b, 0.3f); 
        }

        linhaFronteira.material = mat;
        linhaFronteira.startColor = corBorda;
        linhaFronteira.endColor = corBorda;

        float r = raioDeDominio;
        float y = 0.5f; // Altura do chão para não clipar

        // Desenha as 4 quinas do quadrado usando coordenadas locais
        linhaFronteira.SetPosition(0, new Vector3(-r, y, -r));
        linhaFronteira.SetPosition(1, new Vector3(-r, y, r));
        linhaFronteira.SetPosition(2, new Vector3(r, y, r));
        linhaFronteira.SetPosition(3, new Vector3(r, y, -r));
        linhaFronteira.SetPosition(4, new Vector3(-r, y, -r));

        // --- 2. O Chão pintado translucido (Região interna inteira) ---
        areaTranslúcida = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(areaTranslúcida.GetComponent<Collider>()); // Tira colisão mecânica
        areaTranslúcida.name = "Piso_Holografico_Territorio";
        areaTranslúcida.transform.SetParent(this.transform);
        areaTranslúcida.transform.localPosition = new Vector3(0, 0.45f, 0); // Fica debaixo da linha
        areaTranslúcida.transform.localRotation = Quaternion.Euler(90, 0, 0); // Deita o flat no chão
        areaTranslúcida.transform.localScale = new Vector3(r * 2f, r * 2f, 1f); // Quad mede 1x1, multiplica pra preencher 100%
        
        Renderer rendQuad = areaTranslúcida.GetComponent<Renderer>();
        rendQuad.material = new Material(Shader.Find("Sprites/Default"));
        rendQuad.material.color = corFundo;
        rendQuad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rendQuad.receiveShadows = false;
    }

    // Desenhar um QUADRADO no editor permanentemente para você conseguir ver o mapa organizando a fase
    void OnDrawGizmos()
    {
        Gizmos.color = ehPrefeitura ? new Color(1, 0.8f, 0, 0.2f) : new Color(0, 0.8f, 1, 0.2f);

        Vector3 size = new Vector3(raioDeDominio * 2, 0.1f, raioDeDominio * 2);

        // Preenchimento Suave do Quadrado
        Gizmos.DrawCube(transform.position, size);

        // Linha grossa nas bordas (Quadrado)
        Gizmos.color = ehPrefeitura ? Color.yellow : Color.cyan;
        Gizmos.DrawWireCube(transform.position, size);
    }
}
