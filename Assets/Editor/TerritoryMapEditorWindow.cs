using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class MapaTerritorialEditorBootstrap
{
    private const string AssetPath = "Assets/Resources/MapaTerritorialInicial.asset";

    static MapaTerritorialEditorBootstrap()
    {
        EditorApplication.delayCall += GarantirAssetInicial;
    }

    private static void GarantirAssetInicial()
    {
        if (AssetDatabase.LoadAssetAtPath<DadosMapaTerritorial>(AssetPath) != null) return;
        Texture2D imagem = Resources.Load<Texture2D>("fase");
        DadosMapaTerritorial mapa = DadosMapaTerritorial.CriarModeloFase(imagem);
        AssetDatabase.CreateAsset(mapa, AssetPath);
        AssetDatabase.SaveAssets();
    }
}

/// <summary>Editor visual de polígonos políticos sobre a imagem fase.png.</summary>
public sealed class TerritoryMapEditorWindow : EditorWindow
{
    private const string AssetPath = "Assets/Resources/MapaTerritorialInicial.asset";
    private static readonly int[] Owners = { -1, 0, 1, 2, 3, 4, 5 };
    private static readonly string[] OwnerLabels =
    {
        "Não atribuído", "Neutro", "País 1", "País 2", "País 3", "País 4", "País 5"
    };

    private DadosMapaTerritorial mapa;
    private Vector2 scrollRegioes;
    private int indiceRegiaoSelecionada = -1;
    private Rect retanguloPrevia;
    private bool adicionarVertices;
    private bool testarConsulta;
    private bool arrastandoVertice;
    private int indiceVerticeSelecionado = -1;
    private Vector2 ultimoUvTestado = new Vector2(0.5f, 0.5f);
    private string resultadoTeste = "Clique no mapa para consultar uma posição.";

    [MenuItem("Hegemonia/Territórios/Editor visual de territórios")]
    public static void Abrir()
    {
        TerritoryMapEditorWindow janela = GetWindow<TerritoryMapEditorWindow>();
        janela.titleContent = new GUIContent("Territórios");
        janela.minSize = new Vector2(900f, 560f);
        janela.CarregarAssetInicial();
        janela.Show();
    }

    private void OnEnable()
    {
        CarregarAssetInicial();
    }

    private void CarregarAssetInicial()
    {
        if (mapa == null) mapa = AssetDatabase.LoadAssetAtPath<DadosMapaTerritorial>(AssetPath);
    }

    private void OnGUI()
    {
        CarregarAssetInicial();
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        mapa = (DadosMapaTerritorial)EditorGUILayout.ObjectField(mapa, typeof(DadosMapaTerritorial), false, GUILayout.Width(250f));
        if (GUILayout.Button("Carregar mapa inicial", EditorStyles.toolbarButton, GUILayout.Width(145f))) CarregarAssetInicial();
        if (GUILayout.Button("Salvar", EditorStyles.toolbarButton, GUILayout.Width(60f))) Salvar();
        GUILayout.FlexibleSpace();
        GUILayout.Label("A textura define a geografia de referência; os donos vêm das fronteiras e deste editor.", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        if (mapa == null)
        {
            EditorGUILayout.HelpBox("O asset do mapa ainda está sendo preparado. Use 'Criar mapa inicial' para gerá-lo.", MessageType.Info);
            if (GUILayout.Button("Criar mapa inicial", GUILayout.Height(32f))) CriarAssetInicial();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        DesenharListaRegioes();
        DesenharPainelMapa();
        EditorGUILayout.EndHorizontal();
    }

    private void DesenharListaRegioes()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(285f));
        GUILayout.Label("Regiões políticas", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("As áreas mantêm polígonos separados. As seis áreas terrestres de fase.png começam sem país atribuído; o contorno e a cor do bioma não escolhem o dono.", MessageType.Info);
        scrollRegioes = EditorGUILayout.BeginScrollView(scrollRegioes, GUILayout.ExpandHeight(true));
        for (int i = 0; i < mapa.Regioes.Count; i++)
        {
            RegiaoPolitica regiao = mapa.Regioes[i];
            if (regiao == null) continue;
            string label = string.IsNullOrEmpty(regiao.nome) ? regiao.territorioId : regiao.nome;
            if (GUILayout.Toggle(indiceRegiaoSelecionada == i, label + "  •  " + (regiao.tipo == TipoRegiaoPolitica.Terra ? "terra" : "mar"), "Button"))
                indiceRegiaoSelecionada = i;
        }
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("+ Região terrestre")) AdicionarRegiao(TipoRegiaoPolitica.Terra);
        if (GUILayout.Button("+ Região marítima")) AdicionarRegiao(TipoRegiaoPolitica.AguasTerritoriais);
        if (indiceRegiaoSelecionada >= 0 && indiceRegiaoSelecionada < mapa.Regioes.Count)
        {
            if (GUILayout.Button("Remover região selecionada")) RemoverRegiao();
        }
        EditorGUILayout.EndVertical();
    }

    private void DesenharPainelMapa()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (indiceRegiaoSelecionada >= 0 && indiceRegiaoSelecionada < mapa.Regioes.Count)
            DesenharPropriedades(mapa.Regioes[indiceRegiaoSelecionada]);
        else GUILayout.Label("Selecione uma região para editar seus dados.", EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        adicionarVertices = GUILayout.Toggle(adicionarVertices, "Adicionar vértices", "Button", GUILayout.Width(135f));
        testarConsulta = GUILayout.Toggle(testarConsulta, "Testar consulta", "Button", GUILayout.Width(120f));
        if (GUILayout.Button("Fechar polígono", GUILayout.Width(120f)))
        {
            adicionarVertices = false;
            if (indiceRegiaoSelecionada >= 0 && mapa.Regioes[indiceRegiaoSelecionada].vertices.Count < 3)
                resultadoTeste = "Um polígono precisa de pelo menos 3 vértices.";
        }
        if (GUILayout.Button("Testar ponto", GUILayout.Width(95f)))
        {
            ResultadoConsultaTerritorio consulta = mapa.ConsultarUv(ultimoUvTestado);
            resultadoTeste = FormatarConsulta(consulta);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Label(resultadoTeste, EditorStyles.miniLabel);
        GUILayout.Space(4f);
        DesenharPreviaMapa(GUILayoutUtility.GetRect(100f, 100f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)));
        EditorGUILayout.EndVertical();
    }

    private void DesenharPropriedades(RegiaoPolitica regiao)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        List<string> errosId = ValidarIds();
        if (errosId.Count > 0)
            EditorGUILayout.HelpBox("IDs territoriais inválidos ou duplicados: " + string.Join(", ", errosId.ToArray()) + ". IDs são chaves do save e dos eventos de captura.", MessageType.Error);
        EditorGUI.BeginChangeCheck();
        regiao.territorioId = EditorGUILayout.TextField("Territory ID", regiao.territorioId);
        regiao.nome = EditorGUILayout.TextField("Nome", regiao.nome);
        regiao.tipo = (TipoRegiaoPolitica)EditorGUILayout.EnumPopup("Tipo", regiao.tipo);
        int ownerIndex = 0;
        for (int i = 0; i < Owners.Length; i++) if (Owners[i] == regiao.ownerCountryTeamId) ownerIndex = i;
        ownerIndex = EditorGUILayout.Popup("País proprietário", ownerIndex, OwnerLabels);
        regiao.ownerCountryTeamId = Owners[ownerIndex];
        regiao.neutral = EditorGUILayout.Toggle("Território neutro", regiao.neutral);
        regiao.capturable = EditorGUILayout.Toggle("Capturável", regiao.capturable);
        regiao.corMapa = EditorGUILayout.ColorField("Cor política", regiao.corMapa);
        if (regiao.neutral) regiao.ownerCountryTeamId = 0;
        else if (regiao.ownerCountryTeamId == 0) regiao.ownerCountryTeamId = -1;
        if (EditorGUI.EndChangeCheck()) MarcarAlterado();
        EditorGUILayout.EndVertical();
    }

    private void DesenharPreviaMapa(Rect retangulo)
    {
        retanguloPrevia = retangulo;
        Texture2D imagem = mapa.ImagemReferencia != null ? mapa.ImagemReferencia : Resources.Load<Texture2D>("fase");
        GUI.color = Color.white;
        if (imagem != null) GUI.DrawTexture(retangulo, imagem, ScaleMode.StretchToFill, false);
        else EditorGUI.DrawRect(retangulo, new Color(0.08f, 0.30f, 0.49f));
        EditorGUI.DrawRect(new Rect(retangulo.x, retangulo.y, retangulo.width, 1f), Color.black);
        EditorGUI.DrawRect(new Rect(retangulo.x, retangulo.yMax - 1f, retangulo.width, 1f), Color.black);

        if (Event.current.type == EventType.Repaint)
        {
            Handles.BeginGUI();
            for (int i = 0; i < mapa.Regioes.Count; i++)
            {
                RegiaoPolitica regiao = mapa.Regioes[i];
                if (regiao == null || regiao.vertices == null || regiao.vertices.Count == 0) continue;
                Vector3[] pontos = new Vector3[regiao.vertices.Count];
                for (int j = 0; j < regiao.vertices.Count; j++) pontos[j] = UvParaTela(regiao.vertices[j]);
                Handles.color = CorExibicao(regiao);
                if (pontos.Length >= 2) Handles.DrawAAPolyLine(i == indiceRegiaoSelecionada ? 3.5f : 2f, Fechar(pontos));
                for (int j = 0; j < pontos.Length; j++)
                {
                    float raio = i == indiceRegiaoSelecionada ? 5f : 3.3f;
                    Handles.DrawSolidDisc(pontos[j], Vector3.forward, raio);
                }
                if (pontos.Length >= 3)
                {
                    Vector2 centro = Centro(regiao.vertices);
                    Handles.color = Color.white;
                    GUI.Label(new Rect(UvParaTela(centro).x + 5f, UvParaTela(centro).y - 9f, 160f, 20f),
                        regiao.nome + " / " + OwnerLabels[Mathf.Clamp(ArrayIndexOwner(regiao.ownerCountryTeamId), 0, OwnerLabels.Length - 1)], EditorStyles.whiteMiniLabel);
                }
            }
            Handles.EndGUI();
        }

        TratarMousePrevia();
        GUI.color = Color.white;
    }

    private void TratarMousePrevia()
    {
        Event evt = Event.current;
        if (evt == null || !retanguloPrevia.Contains(evt.mousePosition)) return;
        Vector2 uv = TelaParaUv(evt.mousePosition);
        if (evt.type == EventType.MouseDown && evt.button == 0)
        {
            if (testarConsulta)
            {
                ultimoUvTestado = uv;
                ResultadoConsultaTerritorio consulta = mapa.ConsultarUv(uv);
                resultadoTeste = FormatarConsulta(consulta);
                Repaint();
                evt.Use();
                return;
            }

            if (indiceRegiaoSelecionada < 0 || indiceRegiaoSelecionada >= mapa.Regioes.Count) return;
            RegiaoPolitica regiao = mapa.Regioes[indiceRegiaoSelecionada];
            int vertice = EncontrarVerticeProximo(regiao, evt.mousePosition, 13f);
            if (vertice >= 0)
            {
                Undo.RecordObject(mapa, "Mover vértice territorial");
                indiceVerticeSelecionado = vertice;
                arrastandoVertice = true;
                evt.Use();
            }
            else if (adicionarVertices)
            {
                Undo.RecordObject(mapa, "Adicionar vértice territorial");
                regiao.vertices.Add(uv);
                mapa.InvalidarIndice();
                MarcarAlterado();
                indiceVerticeSelecionado = regiao.vertices.Count - 1;
                Repaint();
                evt.Use();
            }
        }
        else if (evt.type == EventType.MouseDown && evt.button == 1
            && indiceRegiaoSelecionada >= 0 && indiceRegiaoSelecionada < mapa.Regioes.Count)
        {
            RegiaoPolitica regiao = mapa.Regioes[indiceRegiaoSelecionada];
            int vertice = EncontrarVerticeProximo(regiao, evt.mousePosition, 13f);
            if (vertice >= 0)
            {
                Undo.RecordObject(mapa, "Remover vértice territorial");
                regiao.vertices.RemoveAt(vertice);
                mapa.InvalidarIndice();
                MarcarAlterado();
                Repaint();
                evt.Use();
            }
        }
        else if (evt.type == EventType.MouseDrag && arrastandoVertice && indiceRegiaoSelecionada >= 0)
        {
            RegiaoPolitica regiao = mapa.Regioes[indiceRegiaoSelecionada];
            if (indiceVerticeSelecionado >= 0 && indiceVerticeSelecionado < regiao.vertices.Count)
            {
                regiao.vertices[indiceVerticeSelecionado] = uv;
                mapa.InvalidarIndice();
                MarcarAlterado();
                Repaint();
                evt.Use();
            }
        }
        else if (evt.type == EventType.MouseUp && arrastandoVertice)
        {
            arrastandoVertice = false;
            indiceVerticeSelecionado = -1;
            evt.Use();
        }
    }

    private void AdicionarRegiao(TipoRegiaoPolitica tipo)
    {
        Undo.RecordObject(mapa, "Criar região territorial");
        int numero = mapa.Regioes.Count + 1;
        RegiaoPolitica regiao = new RegiaoPolitica
        {
            territorioId = "regiao-" + numero.ToString("00"),
            nome = "Região " + numero,
            ownerCountryTeamId = -1,
            neutral = false,
            capturable = false,
            tipo = tipo,
            corMapa = tipo == TipoRegiaoPolitica.Terra ? new Color(0.95f, 0.7f, 0.2f) : new Color(1f, 0.4f, 0.68f),
            vertices = new List<Vector2>()
        };
        mapa.Regioes.Add(regiao);
        indiceRegiaoSelecionada = mapa.Regioes.Count - 1;
        adicionarVertices = true;
        mapa.InvalidarIndice();
        MarcarAlterado();
    }

    private void RemoverRegiao()
    {
        Undo.RecordObject(mapa, "Remover região territorial");
        mapa.Regioes.RemoveAt(indiceRegiaoSelecionada);
        indiceRegiaoSelecionada = Mathf.Min(indiceRegiaoSelecionada, mapa.Regioes.Count - 1);
        mapa.InvalidarIndice();
        MarcarAlterado();
    }

    private void MarcarAlterado()
    {
        mapa.InvalidarIndice();
        EditorUtility.SetDirty(mapa);
    }

    private void Salvar()
    {
        if (mapa == null) return;
        List<string> errosId = ValidarIds();
        if (errosId.Count > 0)
        {
            EditorUtility.DisplayDialog("IDs territoriais inválidos", "Corrija IDs vazios ou duplicados antes de salvar:\n\n" + string.Join("\n", errosId.ToArray()), "OK");
            return;
        }
        mapa.InvalidarIndice();
        EditorUtility.SetDirty(mapa);
        AssetDatabase.SaveAssets();
    }

    private List<string> ValidarIds()
    {
        List<string> erros = new List<string>();
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < mapa.Regioes.Count; i++)
        {
            RegiaoPolitica regiao = mapa.Regioes[i];
            if (regiao == null) { erros.Add("região nula no índice " + i); continue; }
            string id = regiao.territorioId != null ? regiao.territorioId.Trim() : string.Empty;
            if (id.Length == 0) { erros.Add("região no índice " + i + " sem ID"); continue; }
            if (!ids.Add(id)) erros.Add(id);
        }
        return erros;
    }

    private static string FormatarConsulta(ResultadoConsultaTerritorio consulta)
    {
        string id = consulta.encontrouRegiao ? consulta.territorioId : "(sem território)";
        return id + " | estado " + consulta.estado + " | owner " + consulta.ownerCountryTeamId
            + " | tipo " + consulta.tipo + " | neutro " + consulta.neutral
            + " | capturável " + consulta.capturable + " | definido " + consulta.encontrouRegiao;
    }

    private void CriarAssetInicial()
    {
        if (AssetDatabase.LoadAssetAtPath<DadosMapaTerritorial>(AssetPath) != null)
        {
            mapa = AssetDatabase.LoadAssetAtPath<DadosMapaTerritorial>(AssetPath);
            return;
        }
        DadosMapaTerritorial novo = DadosMapaTerritorial.CriarModeloFase(Resources.Load<Texture2D>("fase"));
        AssetDatabase.CreateAsset(novo, AssetPath);
        AssetDatabase.SaveAssets();
        mapa = novo;
    }

    private Vector3 UvParaTela(Vector2 uv)
    {
        return new Vector3(retanguloPrevia.x + uv.x * retanguloPrevia.width,
            retanguloPrevia.y + uv.y * retanguloPrevia.height, 0f);
    }

    private Vector2 TelaParaUv(Vector2 posicao)
    {
        return new Vector2(Mathf.Clamp01((posicao.x - retanguloPrevia.x) / retanguloPrevia.width),
            Mathf.Clamp01((posicao.y - retanguloPrevia.y) / retanguloPrevia.height));
    }

    private int EncontrarVerticeProximo(RegiaoPolitica regiao, Vector2 posicao, float tolerancia)
    {
        int melhor = -1;
        float menorSqr = tolerancia * tolerancia;
        for (int i = 0; i < regiao.vertices.Count; i++)
        {
            float distancia = ((Vector2)UvParaTela(regiao.vertices[i]) - posicao).sqrMagnitude;
            if (distancia < menorSqr) { menorSqr = distancia; melhor = i; }
        }
        return melhor;
    }

    private static Vector3[] Fechar(Vector3[] pontos)
    {
        Vector3[] fechados = new Vector3[pontos.Length + 1];
        for (int i = 0; i < pontos.Length; i++) fechados[i] = pontos[i];
        fechados[pontos.Length] = pontos[0];
        return fechados;
    }

    private static Vector2 Centro(IList<Vector2> vertices)
    {
        Vector2 total = Vector2.zero;
        for (int i = 0; i < vertices.Count; i++) total += vertices[i];
        return total / vertices.Count;
    }

    private static Color CorExibicao(RegiaoPolitica regiao)
    {
        if (regiao.neutral) return new Color(0.83f, 0.85f, 0.88f, 0.95f);
        if (regiao.ownerCountryTeamId <= 0) return new Color(0.95f, 0.95f, 0.95f, 0.85f);
        return regiao.corMapa;
    }

    private static int ArrayIndexOwner(int owner)
    {
        for (int i = 0; i < Owners.Length; i++) if (Owners[i] == owner) return i;
        return 0;
    }
}
