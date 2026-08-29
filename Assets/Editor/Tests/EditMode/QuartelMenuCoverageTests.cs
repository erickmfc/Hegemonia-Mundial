#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

/// <summary>
/// Cobertura estrutural do Quartel. Estes testes não simulam cliques por
/// coordenadas de tela: eles verificam que todas as áreas, ícones, cenas de
/// campanha e a câmera oficial da Carta estão presentes e reutilizáveis.
/// </summary>
public sealed class QuartelMenuCoverageEditModeTests
{
    private const string QuartelPrefabPath = "Assets/Prefabs/Quartel_General/Quartel.prefab";

    private static string[] ObterCenasDeCampanhaHabilitadas()
    {
        return EditorBuildSettings.scenes
            .Where(cena => cena.enabled
                && !string.IsNullOrWhiteSpace(cena.path)
                && cena.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                && !cena.path.Contains("Menu", StringComparison.OrdinalIgnoreCase))
            .Select(cena => cena.path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    [Test]
    public void QuartelPrefab_ExposeMenuToolkitAndLaunchControl()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuartelPrefabPath);
        Assert.IsNotNull(prefab, "Prefab oficial do Quartel não foi encontrado.");

        GerenciadorQuartel gerenciador = prefab.GetComponent<GerenciadorQuartel>();
        Assert.IsNotNull(gerenciador, "O prefab não possui GerenciadorQuartel.");
        Assert.IsTrue(gerenciador.usarPainelQuartelUIToolkit, "O prefab oficial deve usar o painel moderno.");
        Assert.IsTrue(gerenciador.habilitarLancamentoCoordenado, "O lançamento coordenado deve continuar disponível.");
        Assert.IsFalse(gerenciador.abrirPainelAoIniciarNoPlayMode,
            "O Quartel deve abrir somente por ação do jogador (tecla B), não ao iniciar a cena.");
    }

    [Test]
    public void TodasAsAreasDoQuartel_PossuemConstrutorEIcone()
    {
        Type menuType = typeof(QuartelMenuUIController);
        FieldInfo abasField = menuType.GetField("nomesAbas", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo navegacaoField = menuType.GetField("nomesNavegacaoDesigner", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo iconeMethod = menuType.GetMethod("SimboloNavegacao", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(abasField);
        Assert.IsNotNull(navegacaoField);
        Assert.IsNotNull(iconeMethod);

        GameObject objeto = new GameObject("QuartelMenuCoverage");
        try
        {
            QuartelMenuUIController menu = objeto.AddComponent<QuartelMenuUIController>();
            // MonoBehaviour sem ExecuteInEditMode não recebe Awake ao ser
            // criado por AddComponent durante um EditMode test.
            MethodInfo awake = menuType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(awake);
            awake.Invoke(menu, null);
            string[] abas = (string[])abasField.GetValue(menu);
            string[] navegacao = (string[])navegacaoField.GetValue(menu);
            VisualElement root = (VisualElement)menuType.GetField("root", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
            Assert.IsNotNull(root);
            Assert.IsNotNull(root.Q<VisualElement>("quartel-navegacao-lateral"), "A barra lateral moderna não foi criada.");
            Assert.GreaterOrEqual(root.Query<Button>().ToList().Count, 10, "Os botões de navegação/fechamento não foram criados.");
            Assert.AreEqual(9, root.Query<Label>("quartel-aba-icone").ToList().Count,
                "Cada área do Quartel deve ter exatamente um ícone visível.");
            Assert.AreEqual(9, root.Query<Label>("quartel-aba-texto").ToList().Count,
                "Cada ícone deve estar acompanhado do texto da área correta.");
            Assert.AreEqual(9, root.Query<VisualElement>("quartel-aba-icone-badge").ToList().Count,
                "Os ícones devem estar dentro de badges próprias, sem depender apenas da cor do texto.");

            Assert.AreEqual(9, abas.Length, "O menu deve manter as nove áreas administrativas.");
            Assert.AreEqual(9, navegacao.Length, "A barra lateral deve representar as nove áreas.");

            string[] construtores =
            {
                "ConstruirAbaTropas",
                "ConstruirAbaEfetivo",
                "ConstruirAbaRecrutamento",
                "ConstruirAbaFolha",
                "ConstruirAbaTripulacoes",
                "ConstruirAbaResgate",
                "ConstruirAbaComunicacoes",
                "ConstruirAbaCartaNautica",
                "ConstruirAbaArsenal"
            };

            for (int i = 0; i < construtores.Length; i++)
            {
                Assert.IsNotNull(menuType.GetMethod(construtores[i], BindingFlags.Instance | BindingFlags.NonPublic),
                    "Área sem construtor: " + construtores[i]);
                string icone = (string)iconeMethod.Invoke(menu, new object[] { i });
                Assert.IsFalse(string.IsNullOrWhiteSpace(icone), "Área sem ícone: " + abas[i]);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(objeto);
        }
    }

    [Test]
    public void TodasAsAreasDoQuartel_ConstroemPaginaAoSeremSelecionadas()
    {
        GameObject objeto = new GameObject("QuartelMenuPagesCoverage");
        try
        {
            GerenciadorQuartel gerenciador = objeto.AddComponent<GerenciadorQuartel>();
            QuartelMenuUIController menu = objeto.AddComponent<QuartelMenuUIController>();
            Type menuType = typeof(QuartelMenuUIController);
            MethodInfo awake = menuType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo selecionarAba = menuType.GetMethod("SelecionarAba", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo conteudoField = menuType.GetField("conteudo", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo rootField = menuType.GetField("root", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(gerenciador);
            Assert.IsNotNull(awake);
            Assert.IsNotNull(selecionarAba);
            Assert.IsNotNull(conteudoField);
            Assert.IsNotNull(rootField);
            awake.Invoke(menu, null);

            ScrollView conteudo = (ScrollView)conteudoField.GetValue(menu);
            Assert.IsNotNull(conteudo);
            for (int indice = 0; indice < 9; indice++)
            {
                Assert.DoesNotThrow(
                    () => selecionarAba.Invoke(menu, new object[] { indice }),
                    "A área " + indice + " não conseguiu construir a própria página.");
                Assert.Greater(conteudo.contentContainer.childCount, 0,
                    "A área " + indice + " ficou sem conteúdo visual.");
                if (indice == 7)
                {
                    VisualElement root = (VisualElement)rootField.GetValue(menu);
                    Assert.IsNotNull(root.Q<VisualElement>("quartel-carta-mapa"));
                    Assert.IsNotNull(root.Q<VisualElement>("quartel-carta-interacao"));
                    Assert.IsNotNull(root.Q<VisualElement>("quartel-carta-navegacao"));
                    Assert.IsNotNull(root.Q<VisualElement>("quartel-carta-marcadores"));
                    Assert.IsNotNull(root.Q<VisualElement>("quartel-carta-trajetorias"));
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(objeto);
        }
    }

    [Test]
    public void CenasDeCampanha_ContemQuartelComPainelModerno()
    {
        string[] cenasDeCampanha = ObterCenasDeCampanhaHabilitadas();
        Assert.Greater(cenasDeCampanha.Length, 0, "Nenhuma cena de campanha habilitada foi encontrada no Build Settings.");
        for (int i = 0; i < cenasDeCampanha.Length; i++)
        {
            string caminho = cenasDeCampanha[i];
            Assert.IsTrue(System.IO.File.Exists(caminho), "Cena de campanha ausente: " + caminho);
            Scene cena = EditorSceneManager.OpenScene(caminho, OpenSceneMode.Additive);
            try
            {
                GerenciadorQuartel[] quartels = cena.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GerenciadorQuartel>(true))
                    .ToArray();
                Assert.Greater(quartels.Length, 0, "A cena não possui Quartel: " + caminho);
                for (int j = 0; j < quartels.Length; j++)
                {
                    Assert.IsTrue(quartels[j].usarPainelQuartelUIToolkit,
                        "Quartel fora do painel moderno em " + caminho + ": " + quartels[j].name);
                    Assert.IsTrue(quartels[j].habilitarLancamentoCoordenado,
                        "Lançamento coordenado desativado em " + caminho + ": " + quartels[j].name);
                    Assert.IsFalse(quartels[j].abrirPainelAoIniciarNoPlayMode,
                        "O Quartel não pode abrir automaticamente em " + caminho + ": " + quartels[j].name);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(cena, true);
            }
        }
    }

    [Test]
    public void CartaTerrenoRenderer_ReutilizaCameraRenderTextureEEPermiteNavegacao()
    {
        GameObject objeto = new GameObject("CartaRendererCoverage");
        try
        {
            CartaTerrenoRenderer renderer = objeto.AddComponent<CartaTerrenoRenderer>();
            Texture primeiraTextura = renderer.Renderizar(Vector3.zero, 2000f, 2f, false);
            Camera primeiraCamera = renderer.CameraCarta;
            Assert.IsNotNull(primeiraTextura);
            Assert.IsNotNull(primeiraCamera);
            Assert.AreSame(primeiraTextura, renderer.Renderizar(Vector3.zero, 2000f, 2f, false));
            Assert.AreSame(primeiraCamera, renderer.CameraCarta);
            Assert.AreSame(primeiraTextura, primeiraCamera.targetTexture);

            Vector3 posicaoAntes = primeiraCamera.transform.position;
            float orthoAntes = primeiraCamera.orthographicSize;
            renderer.DeslocarMapa(new Vector2(0.10f, -0.10f));
            renderer.AjustarZoom(1f);

            Assert.AreNotEqual(posicaoAntes, primeiraCamera.transform.position, "As setas da Carta não moveram a câmera.");
            Assert.AreNotEqual(orthoAntes, primeiraCamera.orthographicSize, "O zoom da Carta não alterou a escala.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(objeto);
        }
    }

    [Test]
    public void LancadorMisseis_ContinuaLigadoAoProtocoloDeLancamentoDoQuartel()
    {
        MethodInfo tentativaLancador = typeof(LancadorMisseis).GetMethod(
            "TentarLancarCoordenado",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(Vector3), typeof(bool), typeof(string).MakeByRefType() },
            null);
        Assert.IsNotNull(tentativaLancador, "LancadorMisseis perdeu a API de disparo coordenado manual.");
        Assert.IsNotNull(typeof(GerenciadorQuartel).GetMethod("AtualizarDadosLancamento", BindingFlags.Instance | BindingFlags.Public));
        Assert.IsNotNull(typeof(GerenciadorQuartel).GetMethod("TryExecutarLancamentoCoordenado", BindingFlags.Instance | BindingFlags.Public));

        Type unidadeType = typeof(GerenciadorQuartel).GetNestedType("UnidadeLancamentoCoordenadoV2", BindingFlags.Public);
        Assert.IsNotNull(unidadeType);
        Assert.IsNotNull(unidadeType.GetField("lancadorMisseis", BindingFlags.Instance | BindingFlags.NonPublic),
            "O registro do Quartel não mantém referência ao LancadorMisseis.");
    }
}
#endif
