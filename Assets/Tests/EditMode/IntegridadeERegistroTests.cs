using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

public sealed class IntegridadeERegistroTests
{
    private readonly List<UnityEngine.Object> objetosParaDestruir = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        LimparAuditoriaSobrescrita();

        for (int i = objetosParaDestruir.Count - 1; i >= 0; i--)
        {
            UnityEngine.Object alvo = objetosParaDestruir[i];
            if (alvo != null)
            {
                UnityEngine.Object.DestroyImmediate(alvo);
            }
        }

        objetosParaDestruir.Clear();
    }

    [Test]
    public void RegistroCentral_PreencheHelicopterosEAvioesRegistrados()
    {
        Type helicopteroType = ResolverTipo("Helicoptero");
        Type controleAviaoType = ResolverTipo("ControleAviao");
        Type registroType = ResolverTipo("RegistroEntidadesJogo");

        GameObject helicopteroGo = CriarGameObjectRegistravel("Helicoptero_Teste");
        objetoSeguroAddComponent(helicopteroGo, helicopteroType);
        objetosParaDestruir.Add(helicopteroGo);

        GameObject aviaoGo = new GameObject("Aviao_Teste");
        objetoSeguroAddComponent(aviaoGo, controleAviaoType);
        objetosParaDestruir.Add(aviaoGo);

        int helicopterosRegistrados = ObterContagemDoRegistro(registroType, "Helicopteros");
        int avioesRegistrados = ObterContagemDoRegistro(registroType, "Avioes");

        Assert.That(helicopterosRegistrados, Is.GreaterThanOrEqualTo(1));
        Assert.That(avioesRegistrados, Is.GreaterThanOrEqualTo(1));

        object primeiroHelicoptero = InvocarMetodoStatico(registroType, "GetPrimeiroHelicoptero");
        Assert.That(primeiroHelicoptero, Is.Not.Null);
    }

    [Test]
    public void AuditoriaConteudo_UsaCatalogoSobrescrito_E_BloqueiaGateQuandoHaErro()
    {
        Type auditoriaType = ResolverTipo("AuditoriaConteudoJogo");
        Type dadosType = ResolverTipo("DadosConstrucao");

        object fichaInvalida = ScriptableObject.CreateInstance(dadosType);
        objetosParaDestruir.Add((UnityEngine.Object)fichaInvalida);

        DefinirCampoTexto(dadosType, fichaInvalida, "nomeItem", "Teste Invalido");
        DefinirCampoEnum(dadosType, fichaInvalida, "categoria", "Tecnologia");
        DefinirCampoInteiro(dadosType, fichaInvalida, "preco", -25);
        DefinirCampoObjeto(dadosType, fichaInvalida, "prefabDaUnidade", null);

        object lista = CriarListaTipada(dadosType, fichaInvalida);
        InvocarMetodoStatico(auditoriaType, "DefinirCatalogoSobrescritoParaTeste", lista);

        GameObject auditorGo = new GameObject("Auditoria_Testes");
        objetosParaDestruir.Add(auditorGo);
        object auditor = objetoSeguroAddComponent(auditorGo, auditoriaType);

        // A auditoria deve emitir os diagnósticos de cada erro encontrado;
        // neste caso os logs de erro são parte do comportamento esperado do
        // teste e não uma falha inesperada do runner.
        LogAssert.ignoreFailingMessages = true;
        try
        {
            InvocarMetodoInstancia(auditor, "ExecutarAuditoriaImediata");
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }

        object resultado = LerPropriedadeStatica(auditoriaType, "UltimoResultado");
        Assert.That(LerCampoOuPropriedade(resultado, "TotalFichas"), Is.EqualTo(1));
        Assert.That(LerCampoOuPropriedade(resultado, "Erros"), Is.GreaterThanOrEqualTo(1));
        Assert.That((bool)LerCampoOuPropriedade(resultado, "PassouGate"), Is.False);
    }

    [Test]
    public void AuditoriaConteudo_AprovaGateQuandoFichaBasicaEstaValida()
    {
        Type auditoriaType = ResolverTipo("AuditoriaConteudoJogo");
        Type dadosType = ResolverTipo("DadosConstrucao");

        object fichaValida = ScriptableObject.CreateInstance(dadosType);
        objetosParaDestruir.Add((UnityEngine.Object)fichaValida);

        GameObject prefabValido = new GameObject("Prefab_Valido");
        objetosParaDestruir.Add(prefabValido);

        DefinirCampoTexto(dadosType, fichaValida, "nomeItem", "Teste Valido");
        DefinirCampoEnum(dadosType, fichaValida, "categoria", "Tecnologia");
        DefinirCampoInteiro(dadosType, fichaValida, "preco", 100);
        DefinirCampoObjeto(dadosType, fichaValida, "prefabDaUnidade", prefabValido);

        object lista = CriarListaTipada(dadosType, fichaValida);
        InvocarMetodoStatico(auditoriaType, "DefinirCatalogoSobrescritoParaTeste", lista);

        GameObject auditorGo = new GameObject("Auditoria_Testes_OK");
        objetosParaDestruir.Add(auditorGo);
        object auditor = objetoSeguroAddComponent(auditorGo, auditoriaType);

        InvocarMetodoInstancia(auditor, "ExecutarAuditoriaImediata");

        object resultado = LerPropriedadeStatica(auditoriaType, "UltimoResultado");
        Assert.That(LerCampoOuPropriedade(resultado, "TotalFichas"), Is.EqualTo(1));
        Assert.That(LerCampoOuPropriedade(resultado, "Erros"), Is.EqualTo(0));
        Assert.That((bool)LerCampoOuPropriedade(resultado, "PassouGate"), Is.True);
    }

    [Test]
    public void AuditoriaConteudo_AceitaPersistenciaAnexadaPeloSaveParaFichaMilitarExistente()
    {
        Type auditoriaType = ResolverTipo("AuditoriaConteudoJogo");
        UnityEngine.Object ficha = Resources.Load("Construcoes/F16B");
        Assert.That(ficha, Is.Not.Null, "A ficha F16B precisa estar disponível no catálogo de Resources.");

        GameObject prefab = LerCampoOuPropriedade(ficha, "PrefabDaUnidade") as GameObject;
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent(ResolverTipo("SaveableEntity")), Is.Null,
            "A fixture deve representar a ficha sem SaveableEntity serializado no prefab.");
        Assert.That(prefab.GetComponent(ResolverTipo("IdentidadeUnidade")), Is.Not.Null,
            "O save captura unidades pela identidade e anexa SaveableEntity ao gravar.");

        InvocarMetodoStatico(auditoriaType, "DefinirCatalogoSobrescritoParaTeste",
            CriarListaTipada(ResolverTipo("DadosConstrucao"), ficha));

        GameObject auditorGo = new GameObject("Auditoria_Persistencia_Runtime");
        objetosParaDestruir.Add(auditorGo);
        object auditor = objetoSeguroAddComponent(auditorGo, auditoriaType);

        bool ignorarLogsAnterior = LogAssert.ignoreFailingMessages;
        LogAssert.ignoreFailingMessages = true;
        try
        {
            InvocarMetodoInstancia(auditor, "ExecutarAuditoriaImediata");
        }
        finally
        {
            LogAssert.ignoreFailingMessages = ignorarLogsAnterior;
        }

        object resultado = LerPropriedadeStatica(auditoriaType, "UltimoResultado");
        Assert.That(LerCampoOuPropriedade(resultado, "Erros"), Is.EqualTo(0));
        Assert.That(LerCampoOuPropriedade(resultado, "Avisos"), Is.EqualTo(0),
            "A auditoria deve aceitar a rota de persistência que o SistemaSaveGame já executa em runtime.");
    }

    [Test]
    public void AuditoriaConteudo_NaoTrataMinistralComoCombateQuandoFichaDeclaraTransporte()
    {
        Type auditoriaType = ResolverTipo("AuditoriaConteudoJogo");
        Type dadosType = ResolverTipo("DadosConstrucao");
        UnityEngine.Object ficha = Resources.Load("Construcoes/Ministral");
        Assert.That(ficha, Is.Not.Null, "Ficha de catálogo do Ministral não foi encontrada.");

        object lista = CriarListaTipada(dadosType, ficha);
        InvocarMetodoStatico(auditoriaType, "DefinirCatalogoSobrescritoParaTeste", lista);

        GameObject auditorGo = new GameObject("Auditoria_Ministral_Transporte");
        objetosParaDestruir.Add(auditorGo);
        object auditor = objetoSeguroAddComponent(auditorGo, auditoriaType);
        InvocarMetodoInstancia(auditor, "ExecutarAuditoriaImediata");

        object resultado = LerPropriedadeStatica(auditoriaType, "UltimoResultado");
        Assert.That(LerCampoOuPropriedade(resultado, "TotalFichas"), Is.EqualTo(1));
        Assert.That(LerCampoOuPropriedade(resultado, "Erros"), Is.EqualTo(0));
        Assert.That(LerCampoOuPropriedade(resultado, "Avisos"), Is.EqualTo(0),
            "A ficha e os aliases descrevem o Ministral como transporte, não como unidade de combate.");
    }

    private static Type ResolverTipo(string nomeTipo)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type tipo = assembly.GetType(nomeTipo);
            if (tipo != null)
            {
                return tipo;
            }
        }

        throw new InvalidOperationException("Nao foi possivel resolver o tipo " + nomeTipo + ".");
    }

    private static object objetoSeguroAddComponent(GameObject go, Type tipo)
    {
        Assert.That(go, Is.Not.Null);
        Assert.That(tipo, Is.Not.Null);
        return go.AddComponent(tipo);
    }

    private static int ObterContagemDoRegistro(Type registroType, string nomeCampo)
    {
        FieldInfo campo = registroType.GetField(nomeCampo, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(campo, Is.Not.Null, "Nao achei o campo " + nomeCampo + " no registro.");

        object valor = campo.GetValue(null);
        Assert.That(valor, Is.Not.Null, "Registro " + nomeCampo + " veio nulo.");

        return ((ICollection)valor).Count;
    }

    private static object InvocarMetodoStatico(Type tipo, string nomeMetodo, params object[] argumentos)
    {
        MethodInfo metodo = tipo.GetMethod(nomeMetodo, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(metodo, Is.Not.Null, "Nao achei o metodo statico " + nomeMetodo + " em " + tipo.Name + ".");
        return metodo.Invoke(null, argumentos);
    }

    private static object InvocarMetodoInstancia(object instancia, string nomeMetodo, params object[] argumentos)
    {
        MethodInfo metodo = instancia.GetType().GetMethod(nomeMetodo, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(metodo, Is.Not.Null, "Nao achei o metodo " + nomeMetodo + " em " + instancia.GetType().Name + ".");
        return metodo.Invoke(instancia, argumentos);
    }

    private static object LerPropriedadeStatica(Type tipo, string nomePropriedade)
    {
        PropertyInfo propriedade = tipo.GetProperty(nomePropriedade, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(propriedade, Is.Not.Null, "Nao achei a propriedade " + nomePropriedade + " em " + tipo.Name + ".");
        return propriedade.GetValue(null);
    }

    private static object LerCampoOuPropriedade(object instancia, string nome)
    {
        if (instancia == null)
        {
            return null;
        }

        Type tipo = instancia.GetType();
        FieldInfo campo = tipo.GetField(nome, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (campo != null)
        {
            return campo.GetValue(instancia);
        }

        PropertyInfo propriedade = tipo.GetProperty(nome, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(propriedade, Is.Not.Null, "Nao achei " + nome + " em " + tipo.Name + ".");
        return propriedade.GetValue(instancia);
    }

    private static void DefinirCampoTexto(Type tipo, object instancia, string nomeCampo, string valor)
    {
        FieldInfo campo = tipo.GetField(nomeCampo, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(campo, Is.Not.Null, "Nao achei o campo " + nomeCampo + ".");
        campo.SetValue(instancia, valor);
    }

    private static void DefinirCampoInteiro(Type tipo, object instancia, string nomeCampo, int valor)
    {
        FieldInfo campo = tipo.GetField(nomeCampo, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(campo, Is.Not.Null, "Nao achei o campo " + nomeCampo + ".");
        campo.SetValue(instancia, valor);
    }

    private static void DefinirCampoObjeto(Type tipo, object instancia, string nomeCampo, UnityEngine.Object valor)
    {
        FieldInfo campo = tipo.GetField(nomeCampo, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(campo, Is.Not.Null, "Nao achei o campo " + nomeCampo + ".");
        campo.SetValue(instancia, valor);
    }

    private static void DefinirCampoEnum(Type tipo, object instancia, string nomeCampo, string valorEnum)
    {
        FieldInfo campo = tipo.GetField(nomeCampo, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(campo, Is.Not.Null, "Nao achei o campo " + nomeCampo + ".");

        object valor = Enum.Parse(campo.FieldType, valorEnum);
        campo.SetValue(instancia, valor);
    }

    private static object CriarListaTipada(Type tipoElemento, object item)
    {
        Type listaType = typeof(List<>).MakeGenericType(tipoElemento);
        object lista = Activator.CreateInstance(listaType);
        MethodInfo add = listaType.GetMethod("Add");
        add.Invoke(lista, new[] { item });
        return lista;
    }

    private static void LimparAuditoriaSobrescrita()
    {
        Type auditoriaType = ResolverTipo("AuditoriaConteudoJogo");
        MethodInfo metodo = auditoriaType.GetMethod("DefinirCatalogoSobrescritoParaTeste", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (metodo != null)
        {
            metodo.Invoke(null, new object[] { null });
        }
    }

    private static GameObject CriarGameObjectRegistravel(string nome)
    {
        GameObject go = new GameObject(nome);
        go.AddComponent<Rigidbody>();
        go.AddComponent<NavMeshAgent>();
        go.AddComponent<Animator>();
        go.AddComponent<AudioSource>();
        return go;
    }
}
