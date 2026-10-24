#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        FieldInfo navigationField = controllerType.GetField("navegacao", Members);
        FieldInfo tabsField = controllerType.GetField("abas", Members);
        Assert.That(sectionsField, Is.Not.Null);
        Assert.That(contentField, Is.Not.Null);
        Assert.That(actionsField, Is.Not.Null);
        Assert.That(navigationField, Is.Not.Null);
        Assert.That(tabsField, Is.Not.Null);

        IDictionary sections = sectionsField.GetValue(null) as IDictionary;
        Assert.That(sections, Is.Not.Null);
        ScrollView navigation = navigationField.GetValue(controller) as ScrollView;
        ScrollView tabsView = tabsField.GetValue(controller) as ScrollView;
        Assert.That(navigation, Is.Not.Null, "A navegação principal real precisa estar vinculada.");
        Assert.That(tabsView, Is.Not.Null, "A navegação de subtabs real precisa estar vinculada.");
        int tabCount = 0;
        foreach (DictionaryEntry section in sections)
        {
            string category = (string)section.Key;
            string[] tabs = (string[])section.Value;
            Assert.That(tabs, Is.Not.Null.And.Not.Empty, "Cada seção precisa declarar ao menos uma aba: " + category);

            Button sectionButton = navigation.Query<Button>(className: "gov-nav-button").ToList()
                .Find(button => button.name == "nav-" + category.ToLowerInvariant());
            Assert.That(sectionButton, Is.Not.Null, "Falta o botão real da seção " + category + ".");
            Click(sectionButton);
            Assert.That(sectionButton.ClassListContains("active"), Is.True,
                "O clique da seção precisa atualizar seu estado visual: " + category);
            foreach (string tab in tabs)
            {
                Button tabButton = tabsView.Query<Button>(className: "gov-tab-button").ToList()
                    .Find(button => button.name == "tab-" + tab.ToLowerInvariant());
                Assert.That(tabButton, Is.Not.Null, "Falta o botão real da subtab " + category + " / " + tab + ".");
                Click(tabButton);
                yield return null;
                Assert.That(tabButton.ClassListContains("active"), Is.True,
                    "O clique da subtab precisa atualizar seu estado visual: " + category + " / " + tab);
                VisualElement content = contentField.GetValue(controller) as VisualElement;
                VisualElement actions = actionsField.GetValue(controller) as VisualElement;
                Assert.That(content, Is.Not.Null);
                Assert.That(content.childCount, Is.GreaterThan(1), category + " / " + tab + " precisa renderizar seus dados.");
                Assert.That(actions, Is.Not.Null);
                Assert.That(actions.childCount, Is.GreaterThan(1), category + " / " + tab + " precisa exibir ações.");
                AssertControlsFitWithinCards(content, category + " / " + tab);
                AssertButtonsFitWithinCards(actions, category + " / " + tab);
                if (category == "IA03" && tab == "Situacao")
                {
                    string statusText = string.Join("\n", content.Query<Label>().ToList().Select(label => label.text));
                    Assert.That(statusText, Does.Contain("IA03 — Valdoria"),
                        "O país da instância IA03 ativa deve aparecer no painel do Governo.");
                    Assert.That(statusText, Does.Contain("Execução: ATIVA"),
                        "O painel deve indicar quando o estrategista IA03 está ativo e configurado.");
                    Assert.That(statusText, Does.Contain("Autoridade de ordens: ativa"),
                        "Uma IA03 ativa precisa ter autoridade real para enviar ordens pelo BrainMaster.");
                    Assert.That(statusText, Does.Contain("INATIVA NA PARTIDA"),
                        "Instâncias IA03 desativadas na cena devem ser identificadas como inativas.");
                }
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

        Assert.That(tabCount, Is.EqualTo(41), "A auditoria precisa visitar todas as subtabs declaradas do Governo.");

        MethodInfo showSection = controllerType.GetMethod("MostrarSecao", Members);
        MethodInfo showPage = controllerType.GetMethod("MostrarPagina", Members);
        System.Type governmentType = ResolveType("SistemaGovernoMundial");
        PropertyInfo governmentInstance = governmentType.GetProperty("Instancia", Members);
        MethodInfo registerNews = governmentType.GetMethod(
            "RegistrarNoticia", Members, null, new[] { typeof(string) }, null);
        Assert.That(showSection, Is.Not.Null);
        Assert.That(showPage, Is.Not.Null);
        Assert.That(governmentInstance, Is.Not.Null);
        Assert.That(registerNews, Is.Not.Null);
        object government = governmentInstance.GetValue(null);
        Assert.That(government, Is.Not.Null);

        showSection.Invoke(controller, new object[] { "IA03" });
        showPage.Invoke(controller, new object[] { "Noticias" });
        const string testNews = "IA03 notícia de validação do Menu Governo";
        registerNews.Invoke(government, new object[] { testNews });

        bool newsAppeared = false;
        for (int attempt = 0; attempt < 120 && !newsAppeared; attempt++)
        {
            VisualElement currentContent = contentField.GetValue(controller) as VisualElement;
            foreach (Label label in currentContent.Query<Label>().ToList())
            {
                if (label.text.Contains(testNews))
                {
                    newsAppeared = true;
                    break;
                }
            }

            if (!newsAppeared) yield return null;
        }

        Assert.That(newsAppeared, Is.True,
            "Uma notícia IA03 recebida com o menu aberto deve atualizar a página sem trocar de aba.");

        System.Type commerceType = ResolveType("SistemaComercioNacional");
        Component commerceSystem = Object.FindFirstObjectByType(commerceType, FindObjectsInactive.Include) as Component;
        if (commerceSystem != null)
            Object.Destroy(commerceSystem.gameObject);
        yield return null;

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

    private static void Click(Button button)
    {
        object clickable = typeof(Button).GetProperty("clickable", Members).GetValue(button);
        MethodInfo simulateClick = clickable.GetType().GetMethod(
            "SimulateSingleClick", Members, null, new[] { typeof(EventBase), typeof(int) }, null);
        Assert.That(simulateClick, Is.Not.Null, "O botão deve expor seu callback de clique do UI Toolkit.");
        simulateClick.Invoke(clickable, new object[] { ClickEvent.GetPooled(), 100 });
    }

    private static void AssertControlsFitWithinCards(VisualElement root, string page)
    {
        foreach (VisualElement element in root.Query<VisualElement>().ToList())
        {
            VisualElement card = FindCardAncestor(element);
            if (card == null || element.worldBound.width <= 0f)
                continue;

            Rect bounds = card.worldBound;
            Assert.That(element.worldBound.xMin, Is.GreaterThanOrEqualTo(bounds.xMin - 1f),
                page + ": " + element.GetType().Name + " precisa começar dentro do card.");
            Assert.That(element.worldBound.xMax, Is.LessThanOrEqualTo(bounds.xMax + 1f),
                page + ": " + element.GetType().Name + " precisa terminar dentro do card.");

            if (element is Label label && !string.IsNullOrWhiteSpace(label.text))
            {
                Assert.That(label.resolvedStyle.fontSize, Is.LessThanOrEqualTo(24f),
                    page + ": o texto do card não pode crescer além da escala tipográfica prevista: " + label.text);
            }
            else if (element is Button button)
            {
                Assert.That(string.IsNullOrWhiteSpace(button.text), Is.False,
                    page + ": todo botão do conteúdo precisa ter rótulo visível.");
                Assert.That(button.resolvedStyle.fontSize, Is.LessThanOrEqualTo(16f),
                    page + ": o texto do botão do conteúdo não pode ultrapassar a escala prevista: " + button.text);
            }
        }
    }

    private static void AssertButtonsFitWithinCards(VisualElement root, string page)
    {
        foreach (Button button in root.Query<Button>().ToList())
        {
            Assert.That(string.IsNullOrWhiteSpace(button.text), Is.False,
                page + ": todo botão de ação precisa ter rótulo visível.");
            Assert.That(button.resolvedStyle.fontSize, Is.LessThanOrEqualTo(16f),
                page + ": o texto do botão não pode ultrapassar a escala prevista: " + button.text);

            VisualElement card = FindCardAncestor(button);
            if (card == null || button.worldBound.width <= 0f)
                continue;

            Rect bounds = card.worldBound;
            Assert.That(button.worldBound.xMin, Is.GreaterThanOrEqualTo(bounds.xMin - 1f),
                page + ": o botão " + button.text + " precisa começar dentro do card.");
            Assert.That(button.worldBound.xMax, Is.LessThanOrEqualTo(bounds.xMax + 1f),
                page + ": o botão " + button.text + " precisa terminar dentro do card.");
        }
    }

    private static VisualElement FindCardAncestor(VisualElement element)
    {
        for (VisualElement ancestor = element; ancestor != null; ancestor = ancestor.parent)
        {
            if (ancestor.ClassListContains("gov-card")
                || ancestor.ClassListContains("gov-budget-card")
                || ancestor.ClassListContains("gov-market-card")
                || ancestor.ClassListContains("gov-culture-control")
                || ancestor.ClassListContains("gov-identity-card")
                || ancestor.ClassListContains("gov-stat-card")
                || ancestor.ClassListContains("gov-science-card")
                || ancestor.ClassListContains("gov-action-section"))
                return ancestor;
        }

        return null;
    }
}
#endif
