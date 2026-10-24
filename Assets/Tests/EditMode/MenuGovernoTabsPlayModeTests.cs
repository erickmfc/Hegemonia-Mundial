#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

public sealed class MenuGovernoTabsPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string ScenePath = "Assets/Scenes/GlobalMapRTS.unity";
    private string previousActiveScenePath;

    [UnitySetUp]
    public IEnumerator CaptureOriginalEditorScene()
    {
        previousActiveScenePath = SceneManager.GetActiveScene().path;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator RestoreOriginalEditorScene()
    {
        if (EditorApplication.isPlaying)
            yield return new ExitPlayMode();

        Scene previous = string.IsNullOrEmpty(previousActiveScenePath)
            ? default
            : SceneManager.GetSceneByPath(previousActiveScenePath);
        if (previous.IsValid() && previous.isLoaded)
            SceneManager.SetActiveScene(previous);
    }

    [UnityTest]
    public IEnumerator TodasAsAbasDoGovernoMontamConteudoEComandosEmRuntime()
    {
        if (!EditorApplication.isPlaying)
            yield return new EnterPlayMode();

        Scene scene = EditorSceneManager.LoadSceneInPlayMode(
            ScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        if (!scene.IsValid())
            scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = SceneManager.GetSceneByName("GlobalMapRTS");

        Assert.That(scene.IsValid() && scene.isLoaded, Is.True,
            "A cena global precisa carregar para validar as abas no contexto real do Governo.");

        System.Type controllerType = ResolveType("MenuGovernoNovoController");
        Component controller = null;
        for (int attempt = 0; attempt < 120 && controller == null; attempt++)
        {
            controller = Object.FindFirstObjectByType(controllerType, FindObjectsInactive.Include) as Component;
            if (controller == null)
                yield return null;
        }
        Assert.That(controller, Is.Not.Null, "O controller UI Toolkit do Governo precisa existir na cena.");

        PropertyInfo readyProperty = controllerType.GetProperty("Pronto", Members);
        MethodInfo open = controllerType.GetMethod("Abrir", Members, null, new[] { typeof(bool) }, null);
        Assert.That(readyProperty, Is.Not.Null);
        Assert.That(open, Is.Not.Null);
        open.Invoke(controller, new object[] { true });
        for (int attempt = 0; attempt < 120 && !(bool)readyProperty.GetValue(controller); attempt++)
            yield return null;
        Assert.That((bool)readyProperty.GetValue(controller), Is.True,
            "O UIDocument precisa vincular os painéis antes de percorrer as abas.");

        FieldInfo sectionsField = controllerType.GetField("Secoes", Members);
        FieldInfo contentField = controllerType.GetField("conteudo", Members);
        FieldInfo actionsField = controllerType.GetField("acoes", Members);
        MethodInfo showSection = controllerType.GetMethod("MostrarSecao", Members);
        MethodInfo showPage = controllerType.GetMethod("MostrarPagina", Members);
        Assert.That(sectionsField, Is.Not.Null);
        Assert.That(contentField, Is.Not.Null);
        Assert.That(actionsField, Is.Not.Null);
        Assert.That(showSection, Is.Not.Null);
        Assert.That(showPage, Is.Not.Null);

        IDictionary sections = sectionsField.GetValue(null) as IDictionary;
        Assert.That(sections, Is.Not.Null);
        int tabCount = 0;
        foreach (DictionaryEntry section in sections)
        {
            string category = (string)section.Key;
            string[] tabs = (string[])section.Value;
            Assert.That(tabs, Is.Not.Null.And.Not.Empty, "Cada seção precisa declarar ao menos uma aba: " + category);

            showSection.Invoke(controller, new object[] { category });
            foreach (string tab in tabs)
            {
                showPage.Invoke(controller, new object[] { tab });
                VisualElement content = contentField.GetValue(controller) as VisualElement;
                VisualElement actions = actionsField.GetValue(controller) as VisualElement;
                Assert.That(content, Is.Not.Null);
                Assert.That(content.childCount, Is.GreaterThan(1), category + " / " + tab + " precisa renderizar seus dados.");
                Assert.That(actions, Is.Not.Null);
                Assert.That(actions.childCount, Is.GreaterThan(1), category + " / " + tab + " precisa exibir ações.");
                if (category == "Economia" && tab == "Orcamento")
                {
                    Assert.That(content.Query<SliderInt>(className: "gov-budget-slider").ToList(), Has.Count.EqualTo(9),
                        "O orçamento deve mostrar seis setores nacionais e três forças militares.");
                    Assert.That(content.Query<Label>(className: "gov-budget-summary").First(), Is.Not.Null,
                        "O orçamento deve exibir o teto diário configurado.");
                }
                if (category == "Pais" && tab == "Identidade")
                {
                    Assert.That(content.Query<DropdownField>(className: "gov-identity-color").ToList(), Has.Count.EqualTo(3),
                        "A identidade nacional deve permitir editar as três cores da bandeira.");
                    Assert.That(content.Query<DropdownField>().ToList(), Has.Count.EqualTo(4),
                        "A identidade nacional deve permitir escolher o modelo e as cores da bandeira.");
                    Assert.That(content.Query<VisualElement>(className: "gov-flag-large").First(), Is.Not.Null,
                        "A tela de identidade deve mostrar a prévia da bandeira editada.");
                }
                if (category == "Mercado" && tab == "Vender")
                {
                    ScrollView sales = content as ScrollView;
                    Assert.That(sales, Is.Not.Null, "A lista de vendas deve estar dentro do painel rolável do Mercado.");
                    VisualElement grid = sales.Query<VisualElement>(className: "gov-market-grid").First();
                    Assert.That(grid, Is.Not.Null, "A lista de itens à venda deve estar renderizada no Mercado.");
                    yield return null;
                    yield return null;
                    if (grid.childCount > 0 && sales.verticalScroller.highValue > 0f)
                    {
                        sales.scrollOffset = new Vector2(0f, sales.verticalScroller.highValue);
                        yield return null;
                        Rect ultimoItem = grid[grid.childCount - 1].worldBound;
                        Assert.That(ultimoItem.yMax, Is.LessThanOrEqualTo(sales.contentViewport.worldBound.yMax + 2f),
                            "O último item da venda precisa poder ser alcançado no fim da rolagem do Mercado.");
                    }
                }
                tabCount++;
                yield return null;
            }
        }

        Assert.That(tabCount, Is.EqualTo(37), "A auditoria precisa visitar todas as subtabs declaradas do Governo.");
        open.Invoke(controller, new object[] { false });
    }

    private static System.Type ResolveType(string typeName)
    {
        foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType(typeName, false);
            if (type != null)
                return type;
        }

        Assert.Fail("Tipo não encontrado: " + typeName);
        return null;
    }
}
#endif
