#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DadosConstrucaoReceitaProducaoEditModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void FichasMilitaresExistentesRecebemReceitaPendenteSemAlterarPreco()
    {
        string[] paths =
        {
            "Assets/Prefabs/Veiculos/Tank_South/Tank_South.asset",
            "Assets/Prefabs/Navios_Guerra/Leviathan/USS_Leviathan.asset",
            "Assets/Prefabs/Aeroporto/f16b/F16.asset"
        };
        Type fichaType = ResolveType("DadosConstrucao");

        foreach (string path in paths)
        {
            ScriptableObject ficha = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            Assert.That(ficha, Is.Not.Null, "Ficha militar existente não carregou: " + path);
            Assert.That(fichaType.IsInstanceOfType(ficha), Is.True, "Asset não é uma ficha DadosConstrucao: " + path);

            long precoAntes = (long)fichaType.GetMethod("ObterPrecoEfetivo", Members).Invoke(ficha, null);
            object receita = fichaType.GetProperty("ReceitaProducao", Members).GetValue(ficha, null);

            Assert.That(receita, Is.Not.Null, "A ficha deve expor sua própria receita: " + path);
            Assert.That(Read<bool>(receita, "configurada"), Is.False, "Nenhum custo deve ser presumido para " + path);
            Assert.That(((IList)Read<object>(receita, "materiaisNecessarios")).Count, Is.Zero);
            Assert.That(Read<int>(receita, "energiaNecessaria"), Is.Zero);
            Assert.That(Read<int>(receita, "diasNecessarios"), Is.Zero);
            Assert.That((long)fichaType.GetMethod("ObterPrecoEfetivo", Members).Invoke(ficha, null), Is.EqualTo(precoAntes));
            Assert.That(ValidarReceita(ficha, out string motivo), Is.False);
            Assert.That(motivo, Does.Contain("não configurada"));
        }
    }

    [Test]
    public void ReceitaMilitarSoValidaQuandoTodosOsCamposEstaoValidos()
    {
        Type fichaType = ResolveType("DadosConstrucao");
        Type materialType = ResolveType("MaterialNecessarioIndustrial");
        ScriptableObject ficha = ScriptableObject.CreateInstance(fichaType);
        try
        {
            Assert.That(ValidarReceita(ficha, out _), Is.False,
                "A receita pendente deve impedir um pedido de produção sem custos aprovados.");

            object receita = fichaType.GetProperty("ReceitaProducao", Members).GetValue(ficha, null);
            Write(receita, "configurada", true);
            Write(receita, "diasNecessarios", 0);
            Assert.That(ValidarReceita(ficha, out string motivoDias), Is.False);
            Assert.That(motivoDias, Does.Contain("duração"));

            Write(receita, "diasNecessarios", 2);
            Write(receita, "energiaNecessaria", 100);
            object material = Activator.CreateInstance(materialType);
            Write(material, "recursoId", "aco_estrutural");
            Write(material, "quantidade", 5d);
            IList materiais = (IList)Read<object>(receita, "materiaisNecessarios");
            materiais.Add(material);
            Assert.That(ValidarReceita(ficha, out _), Is.True,
                "Uma receita aprovada com duração e insumo válido deve passar pela validação de ficha.");

            Write(material, "quantidade", 0d);
            Assert.That(ValidarReceita(ficha, out string motivoMaterial), Is.False);
            Assert.That(motivoMaterial, Does.Contain("quantidade"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ficha);
        }
    }

    [Test]
    public void ReceitaMilitarNaoBloqueiaUnidadeComercial()
    {
        Type fichaType = ResolveType("DadosConstrucao");
        Type categoriaType = fichaType.GetNestedType("CategoriaItem", BindingFlags.Public);
        Type capacidadesType = ResolveType("Hegemonia.AI.BrainMaster.IA_ConstructionCapability");
        ScriptableObject ficha = ScriptableObject.CreateInstance(fichaType);
        try
        {
            fichaType.GetField("categoria", Members).SetValue(ficha, Enum.Parse(categoriaType, "Aeronautica"));
            fichaType.GetProperty("Capacidades", Members).SetValue(
                ficha,
                Enum.Parse(capacidadesType, "Unit, Commercial"),
                null);

            bool requerReceita = (bool)fichaType
                .GetMethod("RequerReceitaMilitarParaProducao", Members)
                .Invoke(ficha, null);

            Assert.That(requerReceita, Is.False,
                "Uma unidade marcada como comercial não deve ser bloqueada pela receita militar pendente.");

            fichaType.GetProperty("Capacidades", Members).SetValue(
                ficha,
                Enum.Parse(capacidadesType, "Unit, Military"),
                null);
            requerReceita = (bool)fichaType
                .GetMethod("RequerReceitaMilitarParaProducao", Members)
                .Invoke(ficha, null);
            Assert.That(requerReceita, Is.True,
                "Uma unidade militar deve depender da receita associada à sua ficha.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ficha);
        }
    }

    [Test]
    public void CatalogoUnificadoEspelhaReceitaMilitarConfiguradaDaFicha()
    {
        Type fichaType = ResolveType("DadosConstrucao");
        Type catalogoType = ResolveType("CatalogoProdutoCompartilhado");
        Type itemCatalogoType = ResolveType("CatalogoProdutoUnificadoItem");
        Type categoriaType = fichaType.GetNestedType("CategoriaItem", BindingFlags.Public);
        Type capacidadesType = ResolveType("Hegemonia.AI.BrainMaster.IA_ConstructionCapability");
        Type materialType = ResolveType("MaterialNecessarioIndustrial");
        ScriptableObject ficha = ScriptableObject.CreateInstance(fichaType);

        try
        {
            string id = "teste_receita_" + Guid.NewGuid().ToString("N");
            Write(ficha, "itemId", id);
            fichaType.GetField("categoria", Members).SetValue(ficha, Enum.Parse(categoriaType, "Exercito"));
            fichaType.GetProperty("Capacidades", Members).SetValue(ficha, Enum.Parse(capacidadesType, "Auto"), null);

            object ingredienteLegado = Activator.CreateInstance(ResolveType("IngredienteProduto"));
            Write(ingredienteLegado, "recursoId", "titanio_legado");
            Write(ingredienteLegado, "quantidade", 999d);
            object itemLegado = Activator.CreateInstance(itemCatalogoType);
            Write(itemLegado, "id", id);
            Write(itemLegado, "dinheiroNecessario", 999d);
            Write(itemLegado, "energiaNecessaria", 999d);
            Write(itemLegado, "diasProducao", 99);
            ((IList)Read<object>(itemLegado, "materiais")).Add(ingredienteLegado);
            object[] registrarLegado = { itemLegado, "fixture-legada" };
            catalogoType.GetMethod("RegistrarOuMesclar", Members | BindingFlags.Static).Invoke(null, registrarLegado);

            Array fichas = Array.CreateInstance(fichaType, 1);
            fichas.SetValue(ficha, 0);
            MethodInfo registrarConstrucoes = catalogoType.GetMethod("RegistrarConstrucoes", Members | BindingFlags.Static);
            registrarConstrucoes.Invoke(null, new object[] { fichas });

            object itemCatalogo = ObterItemCatalogo(catalogoType, id);
            Assert.That(itemCatalogo, Is.Not.Null);
            Assert.That(Read<bool>(itemCatalogo, "permiteProducaoAutomatica"), Is.False,
                "Ficha sem receita aprovada não pode liberar produção automática no catálogo.");
            string motivoPendente = Read<string>(itemCatalogo, "motivoProducaoIndisponivel");
            Assert.That(motivoPendente, Does.StartWith("PENDENTE_DE_BALANCEAMENTO:"));
            Assert.That(motivoPendente, Does.Contain("não configurada"));
            IList materiaisPendentes = (IList)Read<object>(itemCatalogo, "materiais");
            Assert.That(materiaisPendentes, Is.Empty,
                "Uma receita pendente deve remover custos legados mesclados anteriormente.");
            Assert.That(Read<double>(itemCatalogo, "dinheiroNecessario"), Is.EqualTo(100d));
            Assert.That(Read<double>(itemCatalogo, "energiaNecessaria"), Is.Zero);
            Assert.That(Read<int>(itemCatalogo, "diasProducao"), Is.Zero);

            catalogoType.GetMethod("RegistrarOuMesclar", Members | BindingFlags.Static).Invoke(null, registrarLegado);
            itemCatalogo = ObterItemCatalogo(catalogoType, id);
            Assert.That(Read<bool>(itemCatalogo, "permiteProducaoAutomatica"), Is.False,
                "Uma ficha militar pendente não pode ser reaberta por mesclagem legada.");
            Assert.That(Read<string>(itemCatalogo, "motivoProducaoIndisponivel"), Is.EqualTo(motivoPendente),
                "A mesclagem legada deve preservar o motivo da ficha autoritativa.");

            object receita = fichaType.GetProperty("ReceitaProducao", Members).GetValue(ficha, null);
            Write(receita, "configurada", true);
            Write(receita, "energiaNecessaria", 75);
            Write(receita, "diasNecessarios", 3);
            object material = Activator.CreateInstance(materialType);
            Write(material, "recursoId", "aco_estrutural");
            Write(material, "quantidade", 12.5d);
            ((IList)Read<object>(receita, "materiaisNecessarios")).Add(material);

            registrarConstrucoes.Invoke(null, new object[] { fichas });

            itemCatalogo = ObterItemCatalogo(catalogoType, id);
            Assert.That(Read<bool>(itemCatalogo, "permiteProducaoAutomatica"), Is.True);
            Assert.That(Read<string>(itemCatalogo, "motivoProducaoIndisponivel"), Is.Empty,
                "Uma receita aprovada deve limpar o motivo de indisponibilidade.");
            Assert.That(Read<double>(itemCatalogo, "dinheiroNecessario"), Is.EqualTo(100d),
                "O custo monetário deve continuar vindo da ficha, sem alterar o preço existente.");
            Assert.That(Read<double>(itemCatalogo, "energiaNecessaria"), Is.EqualTo(75d));
            Assert.That(Read<int>(itemCatalogo, "diasProducao"), Is.EqualTo(3));
            IList materiais = (IList)Read<object>(itemCatalogo, "materiais");
            Assert.That(materiais.Count, Is.EqualTo(1));
            Assert.That(Read<string>(materiais[0], "recursoId"), Is.EqualTo("aco estrutural"));
            Assert.That(Read<double>(materiais[0], "quantidade"), Is.EqualTo(12.5d));

            catalogoType.GetMethod("RegistrarOuMesclar", Members | BindingFlags.Static).Invoke(null, registrarLegado);
            itemCatalogo = ObterItemCatalogo(catalogoType, id);
            Assert.That(Read<bool>(itemCatalogo, "permiteProducaoAutomatica"), Is.True,
                "Uma origem legada não pode reabrir uma ficha militar depois de a receita autoritativa ser aplicada.");
            Assert.That(Read<double>(itemCatalogo, "dinheiroNecessario"), Is.EqualTo(100d));
            Assert.That(Read<double>(itemCatalogo, "energiaNecessaria"), Is.EqualTo(75d));
            Assert.That(Read<int>(itemCatalogo, "diasProducao"), Is.EqualTo(3));
            IList materiaisFinais = (IList)Read<object>(itemCatalogo, "materiais");
            Assert.That(materiaisFinais.Count, Is.EqualTo(1));
            Assert.That(Read<string>(materiaisFinais[0], "recursoId"), Is.EqualTo("aco estrutural"));
            Assert.That(Read<double>(materiaisFinais[0], "quantidade"), Is.EqualTo(12.5d));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ficha);
        }
    }

    [Test]
    public void ReceitaFicaAssociadaAPorFichaESerializaSemCustosPadrao()
    {
        Type fichaType = ResolveType("DadosConstrucao");
        ScriptableObject ficha = ScriptableObject.CreateInstance(fichaType);
        ScriptableObject outraFicha = ScriptableObject.CreateInstance(fichaType);
        ScriptableObject restaurada = null;

        try
        {
            object receita = fichaType.GetProperty("ReceitaProducao", Members).GetValue(ficha, null);
            object receitaDaOutraFicha = fichaType.GetProperty("ReceitaProducao", Members).GetValue(outraFicha, null);
            Assert.That(receita, Is.Not.Null);
            Assert.That(receitaDaOutraFicha, Is.Not.SameAs(receita), "Cada ficha precisa possuir seus próprios dados de receita.");
            Assert.That(Read<bool>(receita, "configurada"), Is.False,
                "Fichas legadas não devem receber custos de produção inventados.");
            Assert.That(Read<int>(receita, "energiaNecessaria"), Is.Zero);
            Assert.That(Read<int>(receita, "diasNecessarios"), Is.Zero);
            Assert.That(((IList)Read<object>(receita, "materiaisNecessarios")).Count, Is.Zero);

            // Valores de fixture: verificam serialização, não definem custos de jogo.
            Write(receita, "configurada", true);
            Write(receita, "energiaNecessaria", 75);
            Write(receita, "diasNecessarios", 3);

            Type materialType = ResolveType("MaterialNecessarioIndustrial");
            object material = Activator.CreateInstance(materialType);
            Write(material, "recursoId", "aco_estrutural");
            Write(material, "quantidade", 12.5d);
            ((IList)Read<object>(receita, "materiaisNecessarios")).Add(material);

            string json = EditorJsonUtility.ToJson(ficha);
            restaurada = ScriptableObject.CreateInstance(fichaType);
            EditorJsonUtility.FromJsonOverwrite(json, restaurada);

            object receitaRestaurada = fichaType.GetProperty("ReceitaProducao", Members).GetValue(restaurada, null);
            Assert.That(Read<bool>(receitaRestaurada, "configurada"), Is.True);
            Assert.That(Read<int>(receitaRestaurada, "energiaNecessaria"), Is.EqualTo(75));
            Assert.That(Read<int>(receitaRestaurada, "diasNecessarios"), Is.EqualTo(3));
            IList materiaisRestaurados = (IList)Read<object>(receitaRestaurada, "materiaisNecessarios");
            Assert.That(materiaisRestaurados.Count, Is.EqualTo(1));
            Assert.That(Read<string>(materiaisRestaurados[0], "recursoId"), Is.EqualTo("aco_estrutural"));
            Assert.That(Read<double>(materiaisRestaurados[0], "quantidade"), Is.EqualTo(12.5d));
        }
        finally
        {
            if (restaurada != null) UnityEngine.Object.DestroyImmediate(restaurada);
            if (outraFicha != null) UnityEngine.Object.DestroyImmediate(outraFicha);
            if (ficha != null) UnityEngine.Object.DestroyImmediate(ficha);
        }
    }

    private static Type ResolveType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(type => type != null);
    }

    private static bool ValidarReceita(object ficha, out string motivo)
    {
        object[] argumentos = { null };
        bool valido = (bool)ficha.GetType()
            .GetMethod("TryValidarReceitaProducao", Members)
            .Invoke(ficha, argumentos);
        motivo = argumentos[0] as string;
        return valido;
    }

    private static object ObterItemCatalogo(Type catalogoType, string id)
    {
        object[] argumentos = { id, null };
        bool encontrado = (bool)catalogoType
            .GetMethod("TentarObter", Members | BindingFlags.Static)
            .Invoke(null, argumentos);
        return encontrado ? argumentos[1] : null;
    }

    private static T Read<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, Members);
        Assert.That(field, Is.Not.Null, "Campo esperado ausente: " + fieldName);
        return (T)field.GetValue(target);
    }

    private static void Write(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, Members);
        Assert.That(field, Is.Not.Null, "Campo esperado ausente: " + fieldName);
        field.SetValue(target, value);
    }
}
#endif
