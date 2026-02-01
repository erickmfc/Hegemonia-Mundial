using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class GerenteSelecao : MonoBehaviour
{
    [Header("Configurações Visuais")]
    public RectTransform caixaSelecaoVisual; // Sua imagem verde
    public RectTransform canvasRect;         // O Pai de todos (Interface/Canvas)

    [Header("Controle")]
    public float espacamento = 2.5f; // Distância entre soldados na formação
    public List<ControleUnidade> unidadesSelecionadas = new List<ControleUnidade>();

    private Vector2 inicioMouseScreen; // Posição pura do mouse na tela
    private bool arrastando = false;

    void Start()
    {
        // Começa desligado e zerado
        if (caixaSelecaoVisual != null)
        {
            caixaSelecaoVisual.gameObject.SetActive(false);
            caixaSelecaoVisual.sizeDelta = Vector2.zero;
        }
    }

    void Update()
    {
        // Se clicar em cima de botões da UI, não faz nada
        if (EventSystem.current.IsPointerOverGameObject()) return;

        // 1. CLICOU (Marca onde começou)
        if (Input.GetMouseButtonDown(0))
        {
            arrastando = true;
            inicioMouseScreen = Input.mousePosition; 
            DeselecionarTudo();
        }

        // 2. ARRASTANDO (Desenha a caixa)
        if (Input.GetMouseButton(0) && arrastando)
        {
            // Só mostra o verde se moveu um pouco o mouse (evita piscar)
            if(Vector2.Distance(inicioMouseScreen, Input.mousePosition) > 10)
            {
                caixaSelecaoVisual.gameObject.SetActive(true);
            }
            
            AtualizarDesenhoCaixa();
        }

        // 3. SOLTOU (Calcula quem pegou)
        if (Input.GetMouseButtonUp(0))
        {
            if (arrastando && caixaSelecaoVisual.gameObject.activeSelf)
            {
                SelecionarUnidadesMatematica();
            }
            else
            {
                // Clique Simples (Sem arrastar)
                // Se acertar uma unidade -> Seleciona ela.
                // Se acertar chão/vazio -> Mantém tudo deselecionado (já deselecionou no MouseDown).
                CliqueSimples();
            }

            // Limpeza
            arrastando = false;
            if(caixaSelecaoVisual != null)
                caixaSelecaoVisual.gameObject.SetActive(false);
        }

        // 4. MOVIMENTO EM GRUPO (Botão Direito)
        if (Input.GetMouseButtonDown(1) && unidadesSelecionadas.Count > 0)
        {
            Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(raio, out hit))
            {
                MoverUnidadesEmGrupo(hit.point);
            }
        }
    }

    // --- NOVA LÓGICA DE FORMAÇÃO ---
    void MoverUnidadesEmGrupo(Vector3 destinoCentral)
    {
        // 1. Calcula o tamanho do quadrado (raiz quadrada da quantidade)
        int total = unidadesSelecionadas.Count;
        int colunas = Mathf.CeilToInt(Mathf.Sqrt(total));
        // float espacamento = 2.5f; // REMOVIDO: Agora usa a variável pública lá de cima

        // 2. Calcula o offset para centralizar a formação no clique do mouse
        float larguraTotal = (colunas - 1) * espacamento;
        Vector3 inicio = destinoCentral - new Vector3(larguraTotal / 2, 0, larguraTotal / 2);

        for (int i = 0; i < total; i++)
        {
            if (unidadesSelecionadas[i] == null) continue;

            // Matemática da Grade
            int x = i % colunas;
            int z = i / colunas;

            Vector3 posAlvo = inicio + new Vector3(x * espacamento, 0, z * espacamento);

            // CORREÇÃO: Verifica se tem HelicopterController (sistema especial de voo)
            Helicoptero heli = unidadesSelecionadas[i].GetComponent<Helicoptero>();
            if (heli != null)
            {
                // Usa o sistema de voo avançado do helicóptero
                heli.Decolar(posAlvo);
            }
            else
            {
                // Manda a unidade terrestre/genérica andar
                unidadesSelecionadas[i].MoverParaPonto(posAlvo);
            }
        }
    }

    void AtualizarDesenhoCaixa()
    {
        if (canvasRect == null || caixaSelecaoVisual == null) return;

        Vector2 mouseAtualScreen = Input.mousePosition;

        // --- TRADUÇÃO MOUSE -> CANVAS ---
        Vector2 localInicio;
        Vector2 localAtual;

        // Converte o ponto inicial e o atual para dentro do Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, inicioMouseScreen, null, out localInicio);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouseAtualScreen, null, out localAtual);

        // Calcula o tamanho e posição no Canvas
        Vector2 tamanho = localAtual - localInicio;
        
        caixaSelecaoVisual.sizeDelta = new Vector2(Mathf.Abs(tamanho.x), Mathf.Abs(tamanho.y));
        
        // Ajusta a posição para que a caixa cresça para qualquer lado (cima/baixo/esq/dir)
        float posX = (tamanho.x < 0) ? localAtual.x : localInicio.x;
        float posY = (tamanho.y < 0) ? localAtual.y : localInicio.y;

        caixaSelecaoVisual.anchoredPosition = new Vector2(posX, posY);
    }

    void SelecionarUnidadesMatematica()
    {
        // Aqui usamos a posição REAL da tela, ignorando o desenho da caixa
        Vector2 mouseFinal = Input.mousePosition;

        float minX = Mathf.Min(inicioMouseScreen.x, mouseFinal.x);
        float maxX = Mathf.Max(inicioMouseScreen.x, mouseFinal.x);
        float minY = Mathf.Min(inicioMouseScreen.y, mouseFinal.y);
        float maxY = Mathf.Max(inicioMouseScreen.y, mouseFinal.y);

        var todasUnidades = FindObjectsByType<ControleUnidade>(FindObjectsSortMode.None);

        foreach (var unidade in todasUnidades)
        {
            // Onde o tanque está na tela?
            Vector3 posTela = Camera.main.WorldToScreenPoint(unidade.transform.position);

            if (posTela.x > minX && posTela.x < maxX && 
                posTela.y > minY && posTela.y < maxY)
            {
                AdicionarSelecao(unidade);
            }
        }
    }

    void CliqueSimples()
    {
        Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit toque;
        if (Physics.Raycast(raio, out toque))
        {
            var unidade = toque.transform.GetComponentInParent<ControleUnidade>();
            if (unidade != null) AdicionarSelecao(unidade);
        }
    }

    void AdicionarSelecao(ControleUnidade unidade)
    {
        // VERIFICA SE É DO MEU TIME
        IdentidadeUnidade id = unidade.GetComponent<IdentidadeUnidade>();
        
        if (id != null)
        {
            // Se tem identidade, respeita o time.
            // Team ID 1 = Jogador. Se for diferente, ignora.
            if (id.teamID != 1) return; 
        }
        else
        {
            // --- CORREÇÃO AUTOMÁTICA ---
            // Se a unidade não tem identidade (ex: Hamer recém colocado),
            // assumimos que é do jogador e colocamos o RG nela agora.
            id = unidade.gameObject.AddComponent<IdentidadeUnidade>();
            id.teamID = 1; // Registra como Aliado
            id.nomeDoPais = "Minha Nação";
            Debug.Log($"[Sistema] Identidade criada automaticamente para: {unidade.name}");
        }

        unidadesSelecionadas.Add(unidade);
        unidade.DefinirSelecao(true);
    }

    public void DeselecionarTudo()
    {
        foreach (var u in unidadesSelecionadas)
        {
            if (u)
            {
                u.DefinirSelecao(false);
            }
        }
        unidadesSelecionadas.Clear();
    }
}
