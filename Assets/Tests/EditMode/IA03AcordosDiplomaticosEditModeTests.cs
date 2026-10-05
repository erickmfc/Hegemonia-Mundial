#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IA03AcordosDiplomaticosEditModeTests
{
    private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
    private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private Type governoType;
    private Type gerenteType;
    private Type clockType;
    private GameObject governoObject;
    private GameObject gerenteObject;
    private GameObject clockObject;
    private Component governo;
    private Component gerente;
    private Component clock;
    private ScriptableObject mapa;
    private object gerenteAnterior;
    private object clockAnterior;

    [SetUp]
    public void SetUp()
    {
        governoType = ResolverTipo("SistemaGovernoMundial");
        gerenteType = ResolverTipo("GerenteDeTerritorio");
        clockType = ResolverTipo("GerenciadorTempo");
        gerenteAnterior = gerenteType.GetField("Instancia", BindingFlags.Static | BindingFlags.Public).GetValue(null);
        clockAnterior = clockType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);

        governoObject = new GameObject("Teste_IA03_Governo");
        governoObject.SetActive(false);
        governo = governoObject.AddComponent(governoType);

        gerenteObject = new GameObject("Teste_IA03_GerenteTerritorial");
        gerenteObject.SetActive(false);
        gerente = gerenteObject.AddComponent(gerenteType);
        mapa = ScriptableObject.CreateInstance(ResolverTipo("DadosMapaTerritorial"));
        gerenteType.GetProperty("MapaPolitico", PublicInstance).SetValue(gerente, mapa, null);
        gerenteType.GetField("Instancia", BindingFlags.Static | BindingFlags.Public).SetValue(null, gerente);

        clockObject = new GameObject("Teste_IA03_Relogio");
        clockObject.SetActive(false);
        clock = clockObject.AddComponent(clockType);
        clockType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public)
            .GetSetMethod(true).Invoke(null, new[] { clock });
        clockType.GetField("totalDias", PublicInstance).SetValue(clock, 40);
    }

    [TearDown]
    public void TearDown()
    {
        if (governoObject != null) UnityEngine.Object.DestroyImmediate(governoObject);
        if (gerenteObject != null) UnityEngine.Object.DestroyImmediate(gerenteObject);
        if (clockObject != null) UnityEngine.Object.DestroyImmediate(clockObject);
        if (mapa != null) UnityEngine.Object.DestroyImmediate(mapa);
        gerenteType.GetField("Instancia", BindingFlags.Static | BindingFlags.Public).SetValue(null, gerenteAnterior);
        clockType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public)
            .GetSetMethod(true).Invoke(null, new[] { clockAnterior });
    }

    [Test]
    public void DesmilitarizacaoVigoraApenasParaSignatariosEExpiraNoDiaConfigurado()
    {
        AdicionarRegiao("zona", 3, capturable: false);
        object proposta = CriarProposta("Desmilitarizacao", 2, 3, 41, new[] { "zona" }, true);
        DefinirPropostas(proposta);

        Assert.That(ConsultarZona(2, "zona"), Is.True);
        Assert.That(ConsultarZona(3, "zona"), Is.True);
        Assert.That(ConsultarZona(4, "zona"), Is.False);

        clockType.GetField("totalDias", PublicInstance).SetValue(clock, 41);
        Invocar(governo, "ProcessarAcordosTemporarios");

        Assert.That(ConsultarZona(2, "zona"), Is.False);
        Assert.That(Field(proposta, "status").ToString(), Is.EqualTo("Expirada"));
    }

    [Test]
    public void CessaoTemporariaDevolveRegiaoAoProprietarioQuandoExpira()
    {
        AdicionarRegiao("concessao", 3, capturable: true);
        Invocar(gerente, "TentarCapturarTerritorio", "concessao", 2);
        object proposta = CriarProposta("CessaoTerritorialTemporaria", 2, 3, 41, new[] { "concessao" }, false);
        DefinirPropostas(proposta);

        clockType.GetField("totalDias", PublicInstance).SetValue(clock, 41);
        Invocar(governo, "ProcessarAcordosTemporarios");

        object estado = Invocar(gerente, "ObterEstadoDaRegiao", "concessao");
        Assert.That(Field(estado, "ownerCountryTeamId"), Is.EqualTo(3));
        Assert.That(Field(proposta, "status").ToString(), Is.EqualTo("Expirada"));
    }

    [Test]
    public void ExpiracaoDaConcessaoNaoSobrescreveUmaConquistaPosterior()
    {
        AdicionarRegiao("concessao", 3, capturable: true);
        Invocar(gerente, "TentarCapturarTerritorio", "concessao", 2);
        object proposta = CriarProposta("CessaoTerritorialTemporaria", 2, 3, 41, new[] { "concessao" }, false);
        DefinirPropostas(proposta);
        Invocar(gerente, "TentarCapturarTerritorio", "concessao", 4);

        clockType.GetField("totalDias", PublicInstance).SetValue(clock, 41);
        Invocar(governo, "ProcessarAcordosTemporarios");

        object estado = Invocar(gerente, "ObterEstadoDaRegiao", "concessao");
        Assert.That(Field(estado, "ownerCountryTeamId"), Is.EqualTo(4));
        Assert.That(Field(proposta, "status").ToString(), Is.EqualTo("Expirada"));
    }

    [Test]
    public void CessaoPermanenteAtualizaProprietarioEPublicaMudancaTerritorial()
    {
        AdicionarRegiao("cessao-permanente", 3, capturable: true);
        object proposta = CriarProposta("CessaoTerritorial", 2, 3, 0, new[] { "cessao-permanente" }, false);
        SetField(proposta, "id", "teste-cessao-permanente");
        SetField(proposta, "status", Enum.Parse(ResolverTipo("StatusPropostaInternacional"), "Pendente"));
        DefinirPropostas(proposta);
        bool recebeuMudanca = false;
        int donoAnterior = -1;
        int novoDono = -1;
        Action<string, int, int> aoMudarDono = (id, anterior, atual) =>
        {
            if (id != "cessao-permanente") return;
            recebeuMudanca = true;
            donoAnterior = anterior;
            novoDono = atual;
        };
        EventInfo eventoTerritorial = gerenteType.GetEvent("OnTerritoryOwnerChanged", BindingFlags.Instance | BindingFlags.Public);
        eventoTerritorial.AddEventHandler(gerente, aoMudarDono);
        bool recebeuEventoDeOcupacao = false;
        Action<string, int, int> aoCapturarPorOcupacao = (id, anterior, atual) =>
            recebeuEventoDeOcupacao |= id == "cessao-permanente";
        EventInfo eventoOcupacao = gerenteType.GetEvent("OnTerritoryCapturedByOccupation", BindingFlags.Instance | BindingFlags.Public);
        eventoOcupacao.AddEventHandler(gerente, aoCapturarPorOcupacao);
        bool recebeuNoticia = false;
        Action<string> aoReceberNoticia = mensagem => recebeuNoticia |= mensagem.Contains("cedeu permanentemente");
        EventInfo eventoNoticia = governoType.GetEvent("OnNoticia", BindingFlags.Instance | BindingFlags.Public);
        eventoNoticia.AddEventHandler(governo, aoReceberNoticia);

        try
        {
            MethodInfo resolver = governoType.GetMethod("ResolverProposta", AllInstance);
            object[] argumentos =
            {
                "teste-cessao-permanente",
                Enum.Parse(ResolverTipo("StatusPropostaInternacional"), "Aceita"),
                null
            };
            bool sucesso = (bool)resolver.Invoke(governo, argumentos);

            Assert.That(sucesso, Is.True, argumentos[2] as string);
            Assert.That(recebeuMudanca, Is.True);
            Assert.That(recebeuEventoDeOcupacao, Is.False, "Cessão diplomática deve publicar a mudança genérica, não uma captura por ocupação.");
            Assert.That(donoAnterior, Is.EqualTo(3));
            Assert.That(novoDono, Is.EqualTo(2));
            Assert.That(Field(Invocar(gerente, "ObterEstadoDaRegiao", "cessao-permanente"), "ownerCountryTeamId"), Is.EqualTo(2));
            IList capturasSalvas = (IList)Invocar(gerente, "CopiarProprietariosCapturados");
            object captura = capturasSalvas.Cast<object>().First(item => (string)Field(item, "territorioId") == "cessao-permanente");
            Assert.That(Field(captura, "ownerCountryTeamId"), Is.EqualTo(2));
            IList regioes = (IList)mapa.GetType().GetProperty("Regioes", PublicInstance).GetValue(mapa, null);
            object regiaoBase = regioes.Cast<object>().First(item => (string)Field(item, "territorioId") == "cessao-permanente");
            Assert.That(Field(regiaoBase, "ownerCountryTeamId"), Is.EqualTo(3), "O asset mantém a posse inicial; o runtime/save usa o proprietário capturado.");
            Assert.That(recebeuNoticia, Is.True);
            Assert.That(Field(proposta, "status").ToString(), Is.EqualTo("Executada"));
        }
        finally
        {
            eventoTerritorial.RemoveEventHandler(gerente, aoMudarDono);
            eventoOcupacao.RemoveEventHandler(gerente, aoCapturarPorOcupacao);
            eventoNoticia.RemoveEventHandler(governo, aoReceberNoticia);
        }
    }

    [Test]
    public void CessaoGenericaDuranteMissaoDeCapturaNaoCompletaAMissao()
    {
        const string territorioId = "missao-sem-captura-diplomatica";
        Vector3 posicao = new Vector3(4321f, 0f, 4321f);
        GameObject estrategaObject = null;
        GameObject creatyObject = null;
        GameObject unidadeMissao = new GameObject("Unidade IA03 cessão teste");
        ScriptableObject missao = null;
        try
        {
            AdicionarRegiao(territorioId, 3, capturable: true);
            ConfigurarGeometriaTerritorial(territorioId, posicao);
            Component estratega = CriarEstrategaComMissaoCaptura(
                posicao, out estrategaObject, out creatyObject, out missao);
            VincularUnidadeAMissao(estratega, unidadeMissao);

            Assert.That(Invocar(gerente, "TentarCapturarTerritorio", territorioId, 2), Is.True);
            Invocar(estratega, "ProcessarMissaoAtiva", 1f);
            Assert.That(Field(estratega, "territorioDoObjetivoCapturado"), Is.False,
                "A mudança genérica do proprietário por cessão não pode confirmar a captura da missão.");
            Assert.That(Field(estratega, "territorioDoObjetivoPerdido"), Is.False);
            Assert.That(Field(estratega, "missaoAtiva"), Is.SameAs(missao));
            Assert.That(Field(estratega, "estadoDaMissao").ToString(), Is.EqualTo("EmAndamento"));

            Assert.That(Invocar(gerente, "TentarCapturarTerritorio", territorioId, 3), Is.True);
            Invocar(estratega, "ProcessarMissaoAtiva", 2f);
            Assert.That(Field(estratega, "territorioDoObjetivoCapturado"), Is.False);
            Assert.That(Field(estratega, "territorioDoObjetivoPerdido"), Is.False,
                "A reversão diplomática também não representa perda física da missão.");
            Assert.That(Field(estratega, "missaoAtiva"), Is.SameAs(missao));
            Assert.That(Field(estratega, "estadoDaMissao").ToString(), Is.EqualTo("EmAndamento"));
        }
        finally
        {
            if (estrategaObject != null) UnityEngine.Object.DestroyImmediate(estrategaObject);
            if (creatyObject != null) UnityEngine.Object.DestroyImmediate(creatyObject);
            UnityEngine.Object.DestroyImmediate(unidadeMissao);
            if (missao != null) UnityEngine.Object.DestroyImmediate(missao);
        }
    }

    [Test]
    public void CapturaFisicaPublicaEventoEspecificoEConservaEventoGenerico()
    {
        const string territorioId = "captura-fisica";
        Vector3 posicao = new Vector3(4321f, 0f, 4321f);
        GameObject unidadeObject = new GameObject("Unidade teste captura física");
        GameObject estrategaObject = null;
        GameObject creatyObject = null;
        ScriptableObject missao = null;
        EventInfo eventoGenerico = gerenteType.GetEvent("OnTerritoryOwnerChanged", BindingFlags.Instance | BindingFlags.Public);
        EventInfo eventoOcupacao = gerenteType.GetEvent("OnTerritoryCapturedByOccupation", BindingFlags.Instance | BindingFlags.Public);
        bool recebeuGenerico = false;
        bool recebeuOcupacao = false;
        int donoAnterior = int.MinValue;
        int novoDono = int.MinValue;
        Action<string, int, int> aoMudarDono = (id, anterior, atual) =>
        {
            if (id != territorioId) return;
            recebeuGenerico = true;
            donoAnterior = anterior;
            novoDono = atual;
        };
        Action<string, int, int> aoCapturar = (id, anterior, atual) =>
        {
            if (id != territorioId) return;
            recebeuOcupacao = true;
            donoAnterior = anterior;
            novoDono = atual;
        };

        try
        {
            SetField(gerente, "usarLimitesDeTerreno", false);
            Bounds limitesTeste = new Bounds(Vector3.zero, new Vector3(10000f, 1000f, 10000f));
            SetField(gerente, "limitesMapaExplicitos", limitesTeste);
            SetField(gerente, "limitesTerritoriais", limitesTeste);
            SetField(gerente, "limitesTerritoriaisProntos", true);
            SetField(gerente, "definicaoMapaGlobal", null);
            SetField(gerente, "segundosParaCapturarTerritorio", 1f);

            AdicionarRegiao(territorioId, 0, capturable: true, neutral: true);
            ConfigurarGeometriaTerritorial(territorioId, posicao);
            object consulta = Invocar(gerente, "ObterTerritorioNaPosicao", posicao);
            Assert.That(Field(consulta, "encontrouRegiao"), Is.True, "O polígono de teste deve conter a posição da unidade.");
            Assert.That(Field(consulta, "territorioId"), Is.EqualTo(territorioId));
            Assert.That(Field(consulta, "neutral"), Is.True);
            Assert.That(Field(consulta, "capturable"), Is.True);

            unidadeObject.transform.position = posicao;
            Type identidadeType = ResolverTipo("IdentidadeUnidade");
            Component identidade = unidadeObject.AddComponent(identidadeType);
            SetField(identidade, "teamID", 2);
            SetField(identidade, "tipoUnidade", Enum.Parse(ResolverTipo("TipoUnidade"), "Infantaria"));
            MethodInfo registrarUnidade = ResolverTipo("RegistroEntidadesJogo")
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(metodo => metodo.Name == "Register"
                    && metodo.GetParameters().Length == 1
                    && metodo.GetParameters()[0].ParameterType == identidadeType);
            Assert.That(registrarUnidade, Is.Not.Null);
            // Em EditMode, MonoBehaviours comuns não recebem necessariamente OnEnable como no Play Mode.
            registrarUnidade.Invoke(null, new object[] { identidade });
            Component estratega = CriarEstrategaComMissaoCaptura(
                posicao, out estrategaObject, out creatyObject, out missao);
            eventoGenerico.AddEventHandler(gerente, aoMudarDono);
            eventoOcupacao.AddEventHandler(gerente, aoCapturar);

            Invocar(gerente, "AtualizarCapturasTerritoriais", 1f);

            IDictionary equipesPresentes = (IDictionary)Field(gerente, "equipesPresentesPorTerritorio");
            Assert.That(equipesPresentes.Contains(territorioId), Is.True, "A IdentidadeUnidade ativa deve ser lida pelo registro reutilizável.");
            Assert.That(recebeuGenerico, Is.True);
            Assert.That(recebeuOcupacao, Is.True);
            Assert.That(donoAnterior, Is.EqualTo(0));
            Assert.That(novoDono, Is.EqualTo(2));
            Assert.That(Invocar(gerente, "ObterDonoDaRegiao", territorioId), Is.EqualTo(2));
            Assert.That(Field(estratega, "territorioDoObjetivoCapturado"), Is.True,
                "O consumidor IA03 deve receber o evento físico e confirmar o objetivo da missão.");
            Assert.That(Field(estratega, "territorioDoObjetivoPerdido"), Is.False);
            VincularUnidadeAMissao(estratega, unidadeObject);
            Invocar(estratega, "ProcessarMissaoAtiva", 2f);
            Assert.That(Field(estratega, "estadoDaMissao").ToString(), Is.EqualTo("Sucesso"));
            Assert.That(Field(estratega, "missaoAtiva"), Is.Null);
            Assert.That(Field(estratega, "creatyAtivo"), Is.Null);
            Assert.That(((IList)Field(estratega, "unidadesAtivasNaMissao")).Count, Is.EqualTo(0));
            object relatorio = estratega.GetType().GetProperty("RelatorioAtual").GetValue(estratega, null);
            Assert.That(Field(relatorio, "BatalhasVencidas"), Is.EqualTo(0),
                "O sucesso territorial conclui a missão, mas não aumenta vitórias em batalha.");
        }
        finally
        {
            eventoGenerico.RemoveEventHandler(gerente, aoMudarDono);
            eventoOcupacao.RemoveEventHandler(gerente, aoCapturar);
            UnityEngine.Object.DestroyImmediate(unidadeObject);
            if (estrategaObject != null) UnityEngine.Object.DestroyImmediate(estrategaObject);
            if (creatyObject != null) UnityEngine.Object.DestroyImmediate(creatyObject);
            if (missao != null) UnityEngine.Object.DestroyImmediate(missao);
        }
    }

    private object CriarProposta(string tipo, int origem, int alvo, int terminaDia, IEnumerable<string> regioes, bool dmz)
    {
        Type propostaType = ResolverTipo("PropostaInternacional");
        object proposta = Activator.CreateInstance(propostaType);
        SetField(proposta, "tipo", Enum.Parse(ResolverTipo("TipoPropostaInternacional"), tipo));
        SetField(proposta, "status", Enum.Parse(ResolverTipo("StatusPropostaInternacional"), "Executada"));
        SetField(proposta, "origemTeamId", origem);
        SetField(proposta, "alvoTeamId", alvo);
        SetField(proposta, "duracaoDias", 1);
        SetField(proposta, "terminaEmDiaDeJogo", terminaDia);
        SetField(proposta, dmz ? "territoriosDesmilitarizados" : "territoriosConcedidos", new List<string>(regioes));
        return proposta;
    }

    private void DefinirPropostas(object proposta)
    {
        Type propostaType = ResolverTipo("PropostaInternacional");
        Type listaType = typeof(List<>).MakeGenericType(propostaType);
        IList lista = (IList)Activator.CreateInstance(listaType);
        lista.Add(proposta);
        governoType.GetField("propostas", PublicInstance).SetValue(governo, lista);
    }

    private bool ConsultarZona(int teamId, string id)
    {
        return (bool)Invocar(governo, "EstaRegiaoDesmilitarizada", teamId, id);
    }

    private Component CriarEstrategaComMissaoCaptura(
        Vector3 posicao,
        out GameObject estrategaObject,
        out GameObject creatyObject,
        out ScriptableObject missao)
    {
        Type strategaType = ResolverTipo("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolverTipo("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        estrategaObject = new GameObject("IA03 teste missão de captura");
        estrategaObject.SetActive(false);
        Component brain = estrategaObject.AddComponent(brainType);
        SetField(brain, "TeamId", 2);
        Component estratega = estrategaObject.AddComponent(strategaType);
        SetField(estratega, "brain", brain);
        SetField(estratega, "paisAlvoTeamId", 3);
        SetField(estratega, "equipeAlvoAtiva", 3);

        creatyObject = new GameObject("Creaty teste missão de captura");
        creatyObject.SetActive(false);
        creatyObject.transform.position = posicao;
        Component creaty = creatyObject.AddComponent(ResolverTipo("Hegemonia.AI.IA03.CreatyEstrategico"));
        SetField(estratega, "creatyAtivo", creaty);

        Type missionType = ResolverTipo("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type conditionType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        missao = ScriptableObject.CreateInstance(missionType);
        SetField(missao, "tipoMissao", Enum.Parse(ResolverTipo("Hegemonia.AI.IA03.IA03TipoMissao"), "AtaqueLimitado"));
        SetField(missao, "condicaoDeSucesso", Enum.Parse(conditionType, "CapturarTerritorio"));
        SetField(missao, "condicaoDeFracasso", Enum.Parse(conditionType, "SobreviverAteOPrazo"));
        SetField(estratega, "missaoAtiva", missao);
        Invocar(estratega, "GarantirAssinaturaTerritorial");
        return estratega;
    }

    private static void VincularUnidadeAMissao(Component estratega, GameObject unidade)
    {
        ((IList)Field(estratega, "unidadesAtivasNaMissao")).Add(unidade);
        SetField(estratega, "unidadesOriginaisNaMissao", 1);
        SetField(estratega, "inicioMissaoEm", 0f);
    }

    private void ConfigurarGeometriaTerritorial(string id, Vector3 posicao)
    {
        Bounds limites = new Bounds(Vector3.zero, new Vector3(10000f, 1000f, 10000f));
        SetField(gerente, "usarLimitesDeTerreno", false);
        SetField(gerente, "limitesMapaExplicitos", limites);
        SetField(gerente, "limitesTerritoriais", limites);
        SetField(gerente, "limitesTerritoriaisProntos", true);
        SetField(gerente, "definicaoMapaGlobal", null);

        Vector2 uv = new Vector2(posicao.x / 10000f + 0.5f, 0.5f - posicao.z / 10000f);
        var vertices = new List<Vector2>
        {
            uv + new Vector2(-0.01f, -0.01f),
            uv + new Vector2(0.01f, -0.01f),
            uv + new Vector2(0.01f, 0.01f),
            uv + new Vector2(-0.01f, 0.01f)
        };
        IList regioes = (IList)mapa.GetType().GetProperty("Regioes", PublicInstance).GetValue(mapa, null);
        object regiao = regioes.Cast<object>().First(item => (string)Field(item, "territorioId") == id);
        SetField(regiao, "vertices", vertices);
        Invocar(mapa, "InvalidarIndice");
    }

    private void AdicionarRegiao(string id, int owner, bool capturable, bool neutral = false)
    {
        Type regiaoType = ResolverTipo("RegiaoPolitica");
        object regiao = Activator.CreateInstance(regiaoType);
        SetField(regiao, "territorioId", id);
        SetField(regiao, "ownerCountryTeamId", owner);
        SetField(regiao, "neutral", neutral);
        SetField(regiao, "capturable", capturable);
        SetField(regiao, "tipo", Enum.Parse(ResolverTipo("TipoRegiaoPolitica"), "Terra"));
        IList regioes = (IList)mapa.GetType().GetProperty("Regioes", PublicInstance).GetValue(mapa, null);
        regioes.Add(regiao);
    }

    private static Type ResolverTipo(string nome)
    {
        Type tipo = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(nome, false))
            .FirstOrDefault(candidato => candidato != null);
        Assert.That(tipo, Is.Not.Null, "Tipo não encontrado: " + nome);
        return tipo;
    }

    private static object Invocar(object alvo, string metodo, params object[] argumentos)
    {
        MethodInfo info = alvo.GetType().GetMethod(metodo, AllInstance);
        Assert.That(info, Is.Not.Null, "Método não encontrado: " + metodo);
        return info.Invoke(alvo, argumentos);
    }

    private static void SetField(object alvo, string nome, object valor)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, AllInstance);
        Assert.That(campo, Is.Not.Null, "Campo não encontrado: " + nome);
        campo.SetValue(alvo, valor);
    }

    private static object Field(object alvo, string nome)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, AllInstance);
        Assert.That(campo, Is.Not.Null, "Campo não encontrado: " + nome);
        return campo.GetValue(alvo);
    }
}
#endif
