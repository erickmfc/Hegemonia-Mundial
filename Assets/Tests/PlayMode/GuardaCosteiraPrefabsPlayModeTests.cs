using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// O assembly de Play Mode e isolado e nao referencia o Assembly-CSharp
/// (o runtime deste projeto ainda usa a assembly predefinida da Unity).
/// O teste continua validando os componentes reais por reflexao, como as
/// demais suites do projeto, sem alterar o runtime nem exigir reimportacao.
/// </summary>
public sealed class GuardaCosteiraPrefabsPlayModeTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator FichasDoMenuCarregamOsDoisPrefabsOperacionais()
    {
        Type dadosConstrucaoType = ResolveType("DadosConstrucao");
        Type helicopteroCosteiroType = ResolveType("UH60GuardaCosteira");
        Type helicopteroType = ResolveType("Helicoptero");
        Type sistemaDanosType = ResolveType("SistemaDeDanos");
        Type identidadeUnidadeType = ResolveType("IdentidadeUnidade");
        Type rotorVisualType = ResolveType("GuardaCosteiraRotorVisual");
        Type navioCosteiroType = ResolveType("NavioGuardaCosteira");
        Type controleNavioType = ResolveType("ControleNavioRealista");
        Type identidadeNavalType = ResolveType("IdentidadeNaval");
        Type sistemaCosteiroType = ResolveType("SistemaGuardaCosteira");
        Type incidenteType = ResolveType("RescueIncidentType");

        UnityEngine.Object fichaAerea = Resources.Load("Construcoes/UH60GuardaCosteira", dadosConstrucaoType);
        UnityEngine.Object fichaNaval = Resources.Load("Construcoes/NavioGuardaCosteira", dadosConstrucaoType);

        Assert.That(fichaAerea, Is.Not.Null, "Ficha do UH-60 nao foi encontrada em Resources/Construcoes.");
        Assert.That(fichaNaval, Is.Not.Null, "Ficha do navio nao foi encontrada em Resources/Construcoes.");

        GameObject prefabAereo = ObterPrefabBasico(fichaAerea, dadosConstrucaoType);
        GameObject prefabNaval = ObterPrefabBasico(fichaNaval, dadosConstrucaoType);
        GameObject heli = UnityEngine.Object.Instantiate(prefabAereo);
        GameObject navio = UnityEngine.Object.Instantiate(prefabNaval);
        yield return null;

        try
        {
            Assert.That(heli.GetComponent(helicopteroCosteiroType), Is.Not.Null);
            Component helicoptero = heli.GetComponent(helicopteroType);
            Assert.That(helicoptero, Is.Not.Null);
            Assert.That(heli.GetComponent(sistemaDanosType), Is.Not.Null);
            Assert.That(heli.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(ReadMember(heli.GetComponent(identidadeUnidadeType), "tipoUnidade").ToString(), Is.EqualTo("Aereo"));
            Assert.That(heli.CompareTag("Aereo"), Is.True);

            Component rotores = heli.GetComponent(rotorVisualType);
            Assert.That(rotores, Is.Not.Null, "O visual de rotores nao foi conectado ao UH-60.");
            Assert.That((bool)ReadMember(rotores, "RotorPrincipalEncontrado"), Is.True,
                "A helice principal nao foi encontrada no modelo importado.");
            Assert.That((bool)ReadMember(rotores, "RotorTraseiroEncontrado"), Is.True,
                "A helice traseira nao foi encontrada no modelo importado.");

            SetMember(helicoptero, "estaVoando", true);
            yield return new WaitForSeconds(0.1f);
            Assert.That((bool)ReadMember(rotores, "RotorPrincipalEncontrado"), Is.True);
            Assert.That((float)ReadMember(rotores, "RotacaoAplicadaTotal"), Is.GreaterThan(0f),
                "As helices nao receberam rotacao durante o voo.");

            Assert.That(navio.GetComponent(navioCosteiroType), Is.Not.Null);
            Assert.That(navio.GetComponent(controleNavioType), Is.Not.Null);
            Assert.That(navio.GetComponent(identidadeNavalType), Is.Not.Null);
            Assert.That(navio.GetComponent(sistemaDanosType), Is.Not.Null);
            Assert.That(navio.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(ReadMember(navio.GetComponent(identidadeUnidadeType), "tipoUnidade").ToString(), Is.EqualTo("Naval"));
            Assert.That(navio.CompareTag("Navio"), Is.True);

            PropertyInfo instancia = sistemaCosteiroType.GetProperty("Instancia", StaticMembers);
            Assert.That(instancia, Is.Not.Null);
            object sistema = instancia.GetValue(null, null);
            Assert.That(sistema, Is.Not.Null, "A unidade nao conectou o SistemaGuardaCosteira.");

            object tipoIncidente = Enum.Parse(incidenteType, "NavioAfundado");
            MethodInfo criarIncidente = sistemaCosteiroType.GetMethod("CriarIncidente", InstanceMembers);
            Assert.That(criarIncidente, Is.Not.Null);
            object incidente = criarIncidente.Invoke(sistema, new object[]
            {
                1, 1, navio.transform.position, 20, tipoIncidente, "Teste Play Mode", 1, -1f
            });
            Assert.That(incidente, Is.Not.Null);
            Assert.That((int)ReadMember(incidente, "MissingPersonnel"), Is.EqualTo(20));
            IEnumerable incidentes = (IEnumerable)ReadMember(sistema, "Incidentes");
            Assert.That(incidentes.Cast<object>(), Does.Contain(incidente));
        }
        finally
        {
            UnityEngine.Object.Destroy(heli);
            UnityEngine.Object.Destroy(navio);

            PropertyInfo instancia = sistemaCosteiroType.GetProperty("Instancia", StaticMembers);
            UnityEngine.Object sistema = instancia != null ? instancia.GetValue(null, null) as UnityEngine.Object : null;
            if (sistema is Component componente)
                UnityEngine.Object.Destroy(componente.gameObject);
            else if (sistema != null)
                UnityEngine.Object.Destroy(sistema);
        }
    }

    private static GameObject ObterPrefabBasico(UnityEngine.Object ficha, Type dadosConstrucaoType)
    {
        MethodInfo metodo = dadosConstrucaoType.GetMethod("TryGetPrefabBasico", InstanceMembers);
        Assert.That(metodo, Is.Not.Null);
        object[] argumentos = { null };
        Assert.That((bool)metodo.Invoke(ficha, argumentos), Is.True);
        GameObject prefab = argumentos[0] as GameObject;
        Assert.That(prefab, Is.Not.Null);
        return prefab;
    }

    private static Type ResolveType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(candidate => candidate != null);
        if (type == null) type = Type.GetType(name + ", Assembly-CSharp", false);
        Assert.That(type, Is.Not.Null, "Tipo nao carregado: " + name);
        return type;
    }

    private static object ReadMember(object target, string name)
    {
        Assert.That(target, Is.Not.Null, "Objeto ausente ao ler: " + name);
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(name, InstanceMembers | StaticMembers);
        if (property != null) return property.GetValue(target, null);
        FieldInfo field = type.GetField(name, InstanceMembers | StaticMembers);
        Assert.That(field, Is.Not.Null, "Membro nao encontrado: " + name);
        return field.GetValue(target);
    }

    private static void SetMember(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, InstanceMembers);
        Assert.That(field, Is.Not.Null, "Campo nao encontrado: " + name);
        field.SetValue(target, value);
    }
}
