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
        Type balsaType = ResolveType("BalsaSalvamento");
        PropertyInfo instanciaSistema = sistemaCosteiroType.GetProperty("Instancia", StaticMembers);
        UnityEngine.Object sistemaAnterior = instanciaSistema != null
            ? instanciaSistema.GetValue(null, null) as UnityEngine.Object
            : null;
        MethodInfo capturarEstado = sistemaCosteiroType.GetMethod("CapturarEstadoSave", InstanceMembers);
        object estadoAnterior = sistemaAnterior != null && capturarEstado != null
            ? capturarEstado.Invoke(sistemaAnterior, null)
            : null;
        bool despachoAutomaticoAnterior = sistemaAnterior != null
            ? (bool)ReadMember(sistemaAnterior, "despachoAutomatico")
            : false;
        bool criacaoAutomaticaAnterior = sistemaAnterior != null
            ? (bool)ReadMember(sistemaAnterior, "criarIncidentesAutomaticamente")
            : true;

        UnityEngine.Object fichaAerea = Resources.Load("Construcoes/UH60GuardaCosteira", dadosConstrucaoType);
        UnityEngine.Object fichaNaval = Resources.Load("Construcoes/NavioGuardaCosteira", dadosConstrucaoType);

        Assert.That(fichaAerea, Is.Not.Null, "Ficha do UH-60 nao foi encontrada em Resources/Construcoes.");
        Assert.That(fichaNaval, Is.Not.Null, "Ficha do navio nao foi encontrada em Resources/Construcoes.");

        GameObject prefabAereo = ObterPrefabBasico(fichaAerea, dadosConstrucaoType);
        GameObject prefabNaval = ObterPrefabBasico(fichaNaval, dadosConstrucaoType);
        GameObject heli = UnityEngine.Object.Instantiate(prefabAereo);
        GameObject navio = UnityEngine.Object.Instantiate(prefabNaval);
        GameObject navioNormal = null;
        GameObject baseAereaTeste = null;
        GameObject vagaAereaTeste = null;
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
            MethodInfo deveAfundar = sistemaDanosType.GetMethod("DeveAfundarComoEmbarcacao", InstanceMembers);
            Assert.That(deveAfundar, Is.Not.Null);
            Assert.That((bool)deveAfundar.Invoke(heli.GetComponent(sistemaDanosType), null), Is.False,
                "A regra de afundamento classificou o UH-60 como navio.");

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
            SetMember(sistema, "despachoAutomatico", false);
            SetMember(sistema, "criarIncidentesAutomaticamente", true);

            object tipoIncidente = Enum.Parse(incidenteType, "NavioAfundado");
            MethodInfo criarIncidente = sistemaCosteiroType.GetMethod("CriarIncidente", InstanceMembers);
            Assert.That(criarIncidente, Is.Not.Null);
            object incidente = criarIncidente.Invoke(sistema, new object[]
            {
                777777, 777777, navio.transform.position, 20, tipoIncidente, "Teste Play Mode", 1, -1f
            });
            Assert.That(incidente, Is.Not.Null);
            Assert.That((int)ReadMember(incidente, "MissingPersonnel"), Is.EqualTo(20));
            IEnumerable incidentes = (IEnumerable)ReadMember(sistema, "Incidentes");
            Assert.That(incidentes.Cast<object>(), Does.Contain(incidente));

            // Um helicóptero em rota de patrulha está voando e, por isso,
            // EstaDisponivel() é falso. Ainda assim, o chamado SAR precisa
            // cancelar a patrulha cíclica e assumir a rota de resgate.
            Component identidadeHeli = heli.GetComponent(identidadeUnidadeType);
            SetMember(identidadeHeli, "teamID", 777777);
            Component combustivelHeli = heli.GetComponent(ResolveType("CombustivelUnidade"));
            PropertyInfo capacidadeCombustivelHeli = combustivelHeli.GetType().GetProperty("Capacidade", InstanceMembers);
            combustivelHeli.GetType().GetMethod("Abastecer", InstanceMembers)
                .Invoke(combustivelHeli, new[] { capacidadeCombustivelHeli.GetValue(combustivelHeli, null) });
            Component baseAerea = null;
            baseAereaTeste = new GameObject("BaseAerea_SAR_PlayModeTest");
            baseAereaTeste.SetActive(false);
            baseAerea = baseAereaTeste.AddComponent(ResolveType("GerenciadorAeroporto"));
            vagaAereaTeste = new GameObject("VagaAerea_SAR_PlayModeTest");
            MethodInfo vincularHelicoptero = helicopteroType.GetMethod("VincularAoAeroporto", InstanceMembers);
            vincularHelicoptero.Invoke(helicoptero, new object[] { baseAerea, vagaAereaTeste.transform });
            SetMember(helicoptero, "estaVoando", true);
            SetMember(helicoptero, "missaoAtualAeroporto", 3);
            Component unidadeHeliResgate = heli.GetComponent(helicopteroCosteiroType);
            MethodInfo enviarResgate = sistemaCosteiroType.GetMethod("EnviarResgate", InstanceMembers);
            Assert.That(enviarResgate, Is.Not.Null);
            Assert.That((bool)enviarResgate.Invoke(sistema, new object[]
            {
                (string)ReadMember(incidente, "IncidentId"), unidadeHeliResgate
            }), Is.True,
                "O UH-60 em patrulha deve abandonar a rota e atender o incidente SAR.");
            Assert.That((int)ReadMember(helicoptero, "missaoAtualAeroporto"), Is.Zero,
                "A missão de patrulha deve ser cancelada quando o helicóptero aceita o chamado SAR.");
            Assert.That((string)ReadMember(unidadeHeliResgate, "IncidentIdAtual"),
                Is.EqualTo((string)ReadMember(incidente, "IncidentId")));

            GameObject prefabBalsa = Resources.Load<GameObject>("Resgate/BalsaSalvamento");
            Assert.That(prefabBalsa, Is.Not.Null, "Prefab da balsa SAR nao carregou de Resources.");
            Component componenteBalsa = prefabBalsa.GetComponent(balsaType);
            Assert.That(componenteBalsa, Is.Not.Null, "Prefab da balsa SAR nao possui BalsaSalvamento.");
            Assert.That((int)ReadMember(componenteBalsa, "Capacidade"), Is.EqualTo(24),
                "A capacidade deve ser tres vezes os 8 lugares da referencia Sea Power.");

            string incidentId = (string)ReadMember(incidente, "IncidentId");
            MethodInfo existeBalsa = balsaType.GetMethod("ExisteParaIncidente", StaticMembers);
            MethodInfo contarSobreviventes = balsaType.GetMethod("ContarSobreviventes", StaticMembers);
            Assert.That(existeBalsa, Is.Not.Null);
            Assert.That(contarSobreviventes, Is.Not.Null);
            Assert.That((bool)existeBalsa.Invoke(null, new object[] { incidentId }), Is.True,
                "Incidente naval nao instanciou uma balsa visual.");
            Assert.That((int)contarSobreviventes.Invoke(null, new object[] { incidentId }), Is.EqualTo(20),
                "Os ocupantes da balsa nao correspondem aos sobreviventes do incidente.");

            Component danosNavio = navio.GetComponent(sistemaDanosType);
            Assert.That(danosNavio, Is.Not.Null);
            Component identidadeNavio = navio.GetComponent(identidadeUnidadeType);
            SetMember(identidadeNavio, "teamID", 777777);
            SetMember(identidadeNavio, "militaresConsumidos", 20);
            FieldInfo eventoMorteGlobal = sistemaDanosType.GetField("OnMorteGlobal", StaticMembers);
            Assert.That(eventoMorteGlobal, Is.Not.Null, "O evento global de morte naval nao esta disponivel.");
            Delegate listenersMorte = eventoMorteGlobal.GetValue(null) as Delegate;
            Assert.That(listenersMorte, Is.Not.Null, "Nenhum sistema recebeu o evento global de morte.");
            Delegate listenerSAR = listenersMorte.GetInvocationList().FirstOrDefault(listener =>
                listener.Target == sistema && listener.Method.Name == "AoMorrerUnidade");
            Assert.That(listenerSAR, Is.Not.Null, "O sistema SAR nao se inscreveu no evento global de morte.");
            listenerSAR.DynamicInvoke(danosNavio, null);

            IEnumerable incidentesAposMorte = (IEnumerable)ReadMember(sistema, "Incidentes");
            object incidenteAutomatico = incidentesAposMorte.Cast<object>().FirstOrDefault(item =>
                string.Equals((string)ReadMember(item, "SourceName"), navio.name, StringComparison.Ordinal));
            Assert.That(incidenteAutomatico, Is.Not.Null,
                "A destruicao de uma embarcacao nao criou automaticamente um incidente SAR.");
            string incidentIdAutomatico = (string)ReadMember(incidenteAutomatico, "IncidentId");
            Assert.That((bool)ReadMember(incidenteAutomatico, "UsaBalsasSalvamento"), Is.True);
            Assert.That((bool)existeBalsa.Invoke(null, new object[] { incidentIdAutomatico }), Is.True,
                "O incidente automatico de uma embarcacao de resgate nao criou balsa.");

            navioNormal = new GameObject("NavioNormal_SAR_PlayModeTest");
            Component identidadeNavioNormal = navioNormal.AddComponent(identidadeUnidadeType);
            SetMember(identidadeNavioNormal, "teamID", 777777);
            SetMember(identidadeNavioNormal, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Naval"));
            SetMember(identidadeNavioNormal, "militaresConsumidos", 20);
            Component danosNavioNormal = navioNormal.AddComponent(sistemaDanosType);
            Assert.That((bool)deveAfundar.Invoke(danosNavioNormal, null), Is.True,
                "Um navio comum com identidade Naval nao foi classificado para afundar.");
            listenerSAR.DynamicInvoke(danosNavioNormal, null);
            IEnumerable incidentesNavioNormal = (IEnumerable)ReadMember(sistema, "Incidentes");
            object incidenteNavioNormal = incidentesNavioNormal.Cast<object>().FirstOrDefault(item =>
                string.Equals((string)ReadMember(item, "SourceName"), navioNormal.name, StringComparison.Ordinal));
            Assert.That(incidenteNavioNormal, Is.Not.Null,
                "A destruicao de um navio comum nao criou incidente SAR automaticamente.");
            Assert.That(ReadMember(incidenteNavioNormal, "IncidentType").ToString(), Is.EqualTo("NavioAfundado"));
            string incidentIdNavioNormal = (string)ReadMember(incidenteNavioNormal, "IncidentId");
            Assert.That((bool)existeBalsa.Invoke(null, new object[] { incidentIdNavioNormal }), Is.True,
                "O navio comum nao recebeu uma balsa SAR automatica.");

            Type unidadeResgateType = ResolveType("GuardaCosteiraUnidade");
            Component unidadeResgate = navio.GetComponent(unidadeResgateType);
            Assert.That(unidadeResgate, Is.Not.Null, "O navio da Guarda Costeira nao expõe a unidade de resgate.");
            string idUnidadeResgate = (string)ReadMember(unidadeResgate, "RescueUnitId");
            IList unidadesDesignadas = (IList)ReadMember(incidente, "AssignedUnits");
            unidadesDesignadas.Add(idUnidadeResgate);

            MethodInfo reservarEPegar = sistemaCosteiroType.GetMethod("ReservarEPegar", InstanceMembers);
            Assert.That(reservarEPegar, Is.Not.Null);
            int resgatados = (int)reservarEPegar.Invoke(sistema,
                new object[] { incidentId, unidadeResgate, 7 });
            Assert.That(resgatados, Is.EqualTo(7));
            Assert.That((int)ReadMember(incidente, "MissingPersonnel"), Is.EqualTo(13));
            Assert.That((int)ReadMember(incidente, "RescuedPersonnel"), Is.EqualTo(7));
            Assert.That((int)contarSobreviventes.Invoke(null, new object[] { incidentId }), Is.EqualTo(13),
                "A ocupacao da balsa nao foi atualizada depois da coleta SAR.");

            MethodInfo desembarcar = sistemaCosteiroType.GetMethod("Desembarcar", InstanceMembers);
            Assert.That(desembarcar, Is.Not.Null);
            int entregues = (int)desembarcar.Invoke(sistema,
                new object[] { incidentId, unidadeResgate, 7 });
            Assert.That(entregues, Is.EqualTo(7));
            Assert.That((int)ReadMember(incidente, "OnboardRescueUnits"), Is.EqualTo(0));
            Assert.That((int)ReadMember(incidente, "ReturnedPersonnel"), Is.EqualTo(7));

            MethodInfo capturarEstadoAtual = sistemaCosteiroType.GetMethod("CapturarEstadoSave", InstanceMembers);
            MethodInfo restaurarEstadoAtual = sistemaCosteiroType.GetMethod("RestaurarEstadoSave", InstanceMembers);
            Assert.That(capturarEstadoAtual, Is.Not.Null);
            Assert.That(restaurarEstadoAtual, Is.Not.Null);
            object estadoComSobreviventes = capturarEstadoAtual.Invoke(sistema, null);
            restaurarEstadoAtual.Invoke(sistema, new[] { estadoComSobreviventes });
            Assert.That((bool)existeBalsa.Invoke(null, new object[] { incidentId }), Is.True,
                "As balsas nao foram recriadas ao restaurar o save SAR.");
            Assert.That((int)contarSobreviventes.Invoke(null, new object[] { incidentId }), Is.EqualTo(13));

            Bounds limitesCascoAntes = ObterLimitesCasco(navio);
            Assert.That((bool)deveAfundar.Invoke(danosNavio, null), Is.True,
                "A regra de afundamento nao reconheceu o navio da Guarda Costeira.");
            MethodInfo desativarControles = sistemaDanosType.GetMethod("DesativarUnidade", InstanceMembers);
            Assert.That(desativarControles, Is.Not.Null);
            desativarControles.Invoke(danosNavio, new object[] { true });
            SetMember(danosNavio, "duracaoAfundamento", 1.25f);
            SetMember(danosNavio, "atrasoPosAfundamento", 0f);
            MethodInfo iniciarAfundamento = sistemaDanosType.GetMethod("RotinaAfundamentoNaval", InstanceMembers);
            Assert.That(iniciarAfundamento, Is.Not.Null);
            IEnumerator rotinaAfundamento = iniciarAfundamento.Invoke(danosNavio,
                new object[] { navio.transform.position }) as IEnumerator;
            Assert.That(rotinaAfundamento, Is.Not.Null);
            yield return rotinaAfundamento;

            Assert.That(navio, Is.Not.Null, "O navio foi removido antes da animacao de afundamento.");
            Bounds limitesCascoDepois = ObterLimitesCasco(navio);
            Assert.That(limitesCascoDepois.center.y, Is.LessThan(limitesCascoAntes.center.y - 0.1f),
                "O casco nao desceu visivelmente durante a sequencia de afundamento.");
        }
        finally
        {
            UnityEngine.Object.Destroy(heli);
            UnityEngine.Object.Destroy(navio);
            UnityEngine.Object.Destroy(navioNormal);
            UnityEngine.Object.Destroy(baseAereaTeste);
            UnityEngine.Object.Destroy(vagaAereaTeste);

            UnityEngine.Object sistema = instanciaSistema != null
                ? instanciaSistema.GetValue(null, null) as UnityEngine.Object
                : null;
            if (sistemaAnterior != null && sistema == sistemaAnterior && estadoAnterior != null)
            {
                SetMember(sistemaAnterior, "despachoAutomatico", despachoAutomaticoAnterior);
                SetMember(sistemaAnterior, "criarIncidentesAutomaticamente", criacaoAutomaticaAnterior);
                MethodInfo restaurar = sistemaCosteiroType.GetMethod("RestaurarEstadoSave", InstanceMembers);
                if (restaurar != null) restaurar.Invoke(sistemaAnterior, new[] { estadoAnterior });
            }
            else if (sistema is Component componente)
            {
                MethodInfo limpar = sistemaCosteiroType.GetMethod("LimparEstadoParaNovoJogo", InstanceMembers);
                if (limpar != null) limpar.Invoke(componente, null);
                UnityEngine.Object.Destroy(componente.gameObject);
            }
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

    private static Bounds ObterLimitesCasco(GameObject navio)
    {
        Renderer[] renderizadores = navio.GetComponentsInChildren<MeshRenderer>(true)
            .Where(renderer => renderer != null && renderer.GetComponentInParent<ParticleSystem>() == null)
            .Cast<Renderer>()
            .ToArray();
        Assert.That(renderizadores.Length, Is.GreaterThan(0),
            "O navio da Guarda Costeira nao possui malha visual para afundar.");

        Bounds limites = renderizadores[0].bounds;
        for (int i = 1; i < renderizadores.Length; i++)
            limites.Encapsulate(renderizadores[i].bounds);
        return limites;
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
