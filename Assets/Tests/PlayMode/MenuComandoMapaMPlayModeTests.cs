using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

public sealed class MenuComandoMapaMPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void SateliteConverteCliquesComExtensoesIndependentesDoMundoGlobal()
    {
        GameObject objeto = new GameObject("SateliteProjecaoRetangularTeste");
        objeto.SetActive(false);
        try
        {
            Component menu = objeto.AddComponent(TypeOf("MenuComandoController"));
            Set(menu, "metadesMapaDetectadas", new Vector2(256000f, 144000f));
            Set(menu, "mapaCentro", Vector2.zero);
            Set(menu, "mapaZoom", 1f);
            MethodInfo converter = menu.GetType().GetMethod("ConverterLocalParaMundo", Members);
            Assert.That(converter, Is.Not.Null);

            Vector3 cantoSuperior = (Vector3)converter.Invoke(menu, new object[] { new Vector2(500f, 0f) });
            Vector3 cantoInferior = (Vector3)converter.Invoke(menu, new object[] { new Vector2(0f, 500f) });
            Assert.That(cantoSuperior.x, Is.EqualTo(256000f).Within(1f));
            Assert.That(cantoSuperior.z, Is.EqualTo(144000f).Within(1f));
            Assert.That(cantoInferior.x, Is.EqualTo(-256000f).Within(1f));
            Assert.That(cantoInferior.z, Is.EqualTo(-144000f).Within(1f));
        }
        finally
        {
            Object.Destroy(objeto);
        }
    }

    [Test]
    public void CacaGlobalPlanejaConsumoComTaxaDeCruzeiroSemAlterarTanque()
    {
        GameObject aeronave = new GameObject("CacaCruzeiroGlobalTeste");
        aeronave.SetActive(false);
        GameObject objetoStreamer = new GameObject("StreamerCombustivelGlobalTeste");
        objetoStreamer.SetActive(false);
        Object mundoGlobal = ScriptableObject.CreateInstance(TypeOf("GlobalWorldDefinition"));
        try
        {
            aeronave.AddComponent(TypeOf("ControleAviaoCaca"));
            Component combustivel = aeronave.AddComponent(TypeOf("CombustivelUnidade"));
            Set(combustivel, "classe", System.Enum.Parse(TypeOf("ClasseCombustivelUnidade"), "Aerea"));
            Set(combustivel, "usaCombustivel", true);
            Set(combustivel, "capacidade", 2070f);
            Set(combustivel, "combustivelAtual", 1676.7f);
            Set(combustivel, "consumoPorSegundoMovendo", 1.45f);

            Component streamer = objetoStreamer.AddComponent(TypeOf("GlobalTerrainStreamer"));
            Set(streamer, "world", mundoGlobal);
            System.Type tipoCombustivel = TypeOf("CombustivelUnidade");
            SetStatic(tipoCombustivel, "streamerGlobalCache", streamer);
            SetStatic(tipoCombustivel, "buscaStreamerGlobalFeita", true);
            SetStatic(tipoCombustivel, "proximaBuscaStreamerGlobal", float.MaxValue);

            MethodInfo estimar = combustivel.GetType().GetMethod("EstimarConsumoParaDistancia", Members);
            float previsto = (float)estimar.Invoke(combustivel, new object[] { 180000f, 180f });
            Assert.That(previsto, Is.EqualTo(181.25f).Within(0.01f));
            Assert.That((float)Get(combustivel, "combustivelAtual"), Is.EqualTo(1676.7f).Within(0.01f),
                "Planejar a rota não pode adicionar ou gastar combustível no tanque.");
        }
        finally
        {
            System.Type tipoCombustivel = TypeOf("CombustivelUnidade");
            SetStatic(tipoCombustivel, "streamerGlobalCache", null);
            SetStatic(tipoCombustivel, "buscaStreamerGlobalFeita", false);
            SetStatic(tipoCombustivel, "proximaBuscaStreamerGlobal", 0f);
            Object.Destroy(aeronave);
            Object.Destroy(objetoStreamer);
            Object.Destroy(mundoGlobal);
        }
    }

    [UnityTest]
    public IEnumerator CliqueDoMapaMComMoverArmadoEntregaOPontoExatoAoExecutorSelecionado()
    {
        Vector3 centro = new Vector3(100000f, 0f, 100000f);
        GameObject terreno = GameObject.CreatePrimitive(PrimitiveType.Plane);
        terreno.name = "TerrenoMapaMComandoTeste";
        terreno.transform.position = centro;
        terreno.transform.localScale = Vector3.one * 100f;

        NavMeshData navMeshData = null;
        NavMeshDataInstance navMeshInstancia = default;
        GameObject menuObjeto = new GameObject("MenuComandoMapaMTeste");
        menuObjeto.SetActive(false);
        System.Type menuType = TypeOf("MenuComandoController");
        Component menu = menuObjeto.AddComponent(menuType);

        GameObject unidade = new GameObject("UnidadeMapaMComandoTeste");
        unidade.SetActive(false);
        try
        {
            var fontes = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Mesh,
                    sourceObject = terreno.GetComponent<MeshFilter>().sharedMesh,
                    transform = terreno.transform.localToWorldMatrix,
                    area = 0
                }
            };
            navMeshData = NavMeshBuilder.BuildNavMeshData(
                NavMesh.GetSettingsByID(0),
                fontes,
                new Bounds(centro, new Vector3(1200f, 100f, 1200f)),
                Vector3.zero,
                Quaternion.identity);
            Assert.That(navMeshData, Is.Not.Null, "O cenário NavMesh isolado do teste precisa ser criado.");
            navMeshInstancia = NavMesh.AddNavMeshData(navMeshData);

            unidade.transform.position = centro + new Vector3(-180f, 0f, 0f);
            NavMeshAgent agente = unidade.AddComponent<NavMeshAgent>();
            agente.speed = 10f;
            agente.acceleration = 100f;
            agente.angularSpeed = 360f;
            agente.radius = 0.3f;
            Component controle = unidade.AddComponent(TypeOf("ControleUnidade"));
            unidade.SetActive(true);
            yield return null;

            Assert.That(agente.isOnNavMesh, Is.True, "A unidade de teste precisa estar em uma rota NavMesh válida.");
            Set(menu, "menuAberto", true);
            IList selecaoMenu = (IList)Get(menu, "unidadesSelecionadasMenu");
            selecaoMenu.Add(controle);
            ExecutarOrdem(menu, "MOVER");

            Vector3 pontoClicado = centro + new Vector3(180f, 0f, 35f);
            MethodInfo processarClique = menuType.GetMethod("TentarProcessarCliqueMapaM", Members);
            Assert.That(processarClique, Is.Not.Null, "O método de comando do Mapa M não existe.");
            bool cliqueConsumido = (bool)processarClique.Invoke(menu, new object[] { pontoClicado, 0 });
            Assert.That(cliqueConsumido, Is.True,
                "O menu precisa consumir o clique quando MOVER está armado.");
            Vector3 destinoRecebido = agente.destination;
            Assert.That(destinoRecebido.x, Is.EqualTo(pontoClicado.x).Within(0.001f),
                "A coordenada X do executor precisa corresponder ao ponto informado pela câmera do Mapa M.");
            Assert.That(destinoRecebido.z, Is.EqualTo(pontoClicado.z).Within(0.001f),
                "A coordenada Z do executor precisa corresponder ao ponto informado pela câmera do Mapa M.");
            Assert.That(destinoRecebido.y, Is.EqualTo(pontoClicado.y).Within(0.1f),
                "A coordenada Y pode receber apenas o ajuste de encaixe do NavMesh.");
            Assert.That(agente.hasPath || agente.pathPending, Is.True,
                "A unidade recebeu o ponto, mas nenhum caminho foi iniciado.");
        }
        finally
        {
            if (navMeshInstancia.valid) navMeshInstancia.Remove();
            Object.Destroy(unidade);
            Object.Destroy(menuObjeto);
            Object.Destroy(terreno);
            if (navMeshData != null) Object.Destroy(navMeshData);
        }
    }

    [UnityTest]
    public IEnumerator ProjecaoDoCliqueUsaACameraDoMapaMEmVezDaCameraPrincipal()
    {
        Vector3 centro = new Vector3(100000f, 10000f, 100000f);
        GameObject controladorObjeto = new GameObject("MapaGeralControllerProjecaoTeste");
        controladorObjeto.SetActive(false);
        Component controlador = controladorObjeto.AddComponent(TypeOf("MapaGeralController"));
        GameObject cameraMapaObjeto = new GameObject("CameraMapaMProjecaoTeste");
        Camera cameraMapa = cameraMapaObjeto.AddComponent<Camera>();
        cameraMapa.orthographic = true;
        cameraMapa.orthographicSize = 500f;
        cameraMapa.transform.SetPositionAndRotation(centro + Vector3.up * 1000f, Quaternion.Euler(90f, 0f, 0f));
        GameObject cameraPrincipalObjeto = new GameObject("CameraPrincipalProjecaoTeste");
        Camera cameraPrincipal = cameraPrincipalObjeto.AddComponent<Camera>();
        cameraPrincipal.tag = "MainCamera";
        cameraPrincipal.orthographic = true;
        cameraPrincipal.orthographicSize = 500f;
        cameraPrincipal.transform.SetPositionAndRotation(
            centro + new Vector3(10000f, 1000f, 0f), Quaternion.Euler(90f, 0f, 0f));

        try
        {
            Set(controlador, "cameraMapa", cameraMapa);
            Set(controlador, "nivelDoMar", 0f);
            yield return null;

            Ray raioMapa = cameraMapa.ScreenPointToRay(Input.mousePosition);
            Plane planoMar = new Plane(Vector3.up, Vector3.zero);
            Assert.That(planoMar.Raycast(raioMapa, out float distanciaMapa), Is.True,
                "O raio da câmera do Mapa M precisa intersectar o nível do mar.");
            Vector3 pontoEsperado = raioMapa.GetPoint(distanciaMapa);

            Ray raioPrincipal = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
            Assert.That(planoMar.Raycast(raioPrincipal, out float distanciaPrincipal), Is.True);
            Vector3 pontoDaCameraPrincipal = raioPrincipal.GetPoint(distanciaPrincipal);
            Assert.That(Vector3.Distance(pontoEsperado, pontoDaCameraPrincipal), Is.GreaterThan(1000f),
                "O cenário deve distinguir claramente a projeção do Mapa M da câmera principal.");

            MethodInfo obterDestino = controlador.GetType().GetMethod("TryObterDestinoNoMapa", Members);
            Assert.That(obterDestino, Is.Not.Null, "O controlador do Mapa M precisa expor a projeção usada pelos cliques.");
            object[] argumentos = { Vector3.zero };
            bool encontrouDestino = (bool)obterDestino.Invoke(controlador, argumentos);
            Assert.That(encontrouDestino, Is.True,
                "O clique do Mapa M não encontrou um ponto no mar.");
            Assert.That((Vector3)argumentos[0], Is.EqualTo(pontoEsperado),
                "A coordenada enviada ao menu precisa vir da câmera do Mapa M, não da câmera principal.");
        }
        finally
        {
            UnityEngine.Object.Destroy(controladorObjeto);
            UnityEngine.Object.Destroy(cameraMapaObjeto);
            UnityEngine.Object.Destroy(cameraPrincipalObjeto);
        }
    }

    [UnityTest]
    public IEnumerator CliqueDaTelaDoMapaMEncaminhaPontoProjetadoAOrdemDeMover()
    {
        Vector3 centro = new Vector3(100000f, 0f, 100000f);
        GameObject terreno = CriarTerreno(centro);
        GameObject mapaObjeto = new GameObject("MapaGeralControllerCliqueIntegradoTeste");
        mapaObjeto.SetActive(false);
        Component mapa = mapaObjeto.AddComponent(TypeOf("MapaGeralController"));
        GameObject cameraMapaObjeto = new GameObject("CameraMapaMClickIntegradoTeste");
        Camera cameraMapa = cameraMapaObjeto.AddComponent<Camera>();
        cameraMapa.orthographic = true;
        cameraMapa.orthographicSize = 500f;
        cameraMapa.transform.SetPositionAndRotation(centro + Vector3.up * 1000f, Quaternion.Euler(90f, 0f, 0f));

        GameObject menuObjeto = new GameObject("MenuComandoMapaMClickIntegradoTeste");
        menuObjeto.SetActive(false);
        Component menu = menuObjeto.AddComponent(TypeOf("MenuComandoController"));
        GameObject unidade = new GameObject("UnidadeMapaMClickIntegradoTeste");
        unidade.SetActive(false);
        NavMeshData navMeshData = null;
        NavMeshDataInstance navMeshInstancia = default;

        try
        {
            navMeshInstancia = CriarNavMesh(terreno, centro, out navMeshData);
            unidade.transform.position = centro + new Vector3(-180f, 0f, 0f);
            NavMeshAgent agente = unidade.AddComponent<NavMeshAgent>();
            agente.speed = 10f;
            agente.acceleration = 100f;
            agente.radius = 0.3f;
            Component controle = unidade.AddComponent(TypeOf("ControleUnidade"));
            unidade.SetActive(true);
            yield return null;

            Assert.That(agente.isOnNavMesh, Is.True);
            Set(mapa, "cameraMapa", cameraMapa);
            Set(mapa, "nivelDoMar", 0f);
            Set(menu, "menuAberto", true);
            ((IList)Get(menu, "unidadesSelecionadasMenu")).Add(controle);
            ExecutarOrdem(menu, "MOVER");

            Vector3 posicaoTela = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            MethodInfo encaminharClique = mapa.GetType().GetMethod("TentarEncaminharCliqueMenuSatelite", Members);
            Assert.That(encaminharClique, Is.Not.Null,
                "O controlador do Mapa M precisa encaminhar cliques para o menu satélite.");
            bool cliqueConsumido = (bool)encaminharClique.Invoke(
                mapa,
                new object[] { menu, posicaoTela, 0 });
            Assert.That(cliqueConsumido, Is.True,
                "O clique de tela precisa atravessar a câmera M e alcançar o comando armado.");
            Vector3 destinoDoAgente = agente.destination;
            Assert.That(destinoDoAgente.x, Is.EqualTo(centro.x).Within(0.05f),
                "O executor da unidade precisa receber o ponto projetado pela câmera M, com a tolerância do snap NavMesh.");
            Assert.That(destinoDoAgente.z, Is.EqualTo(centro.z).Within(0.05f),
                "O executor da unidade precisa receber o ponto projetado pela câmera M, com a tolerância do snap NavMesh.");
            Assert.That(agente.hasPath || agente.pathPending, Is.True,
                "A ordem chegou ao executor, mas nenhum caminho começou.");
        }
        finally
        {
            if (navMeshInstancia.valid) navMeshInstancia.Remove();
            UnityEngine.Object.Destroy(unidade);
            UnityEngine.Object.Destroy(menuObjeto);
            UnityEngine.Object.Destroy(mapaObjeto);
            UnityEngine.Object.Destroy(cameraMapaObjeto);
            UnityEngine.Object.Destroy(terreno);
            if (navMeshData != null) UnityEngine.Object.Destroy(navMeshData);
        }
    }

    [Test]
    public void BotaoMoverDoMenuArmaAOrdemPeloCallbackDoUIElements()
    {
        GameObject menuObjeto = new GameObject("MenuComandoBotaoMoverTeste");
        menuObjeto.SetActive(false);
        Component menu = menuObjeto.AddComponent(TypeOf("MenuComandoController"));
        GameObject unidade = new GameObject("UnidadeMenuBotaoMoverTeste");
        Component controle = unidade.AddComponent(TypeOf("ControleUnidade"));
        Button botaoMover = new Button();

        try
        {
            Set(menu, "menuAberto", true);
            ((IList)Get(menu, "unidadesSelecionadasMenu")).Add(controle);
            MethodInfo vincularBotao = menu.GetType().GetMethod("VincularBotaoOrdem", Members);
            Assert.That(vincularBotao, Is.Not.Null, "O menu precisa vincular os botões de ordem do UI Toolkit.");
            vincularBotao.Invoke(menu, new object[] { botaoMover, "MOVER" });

            MethodInfo simularClique = botaoMover.clickable.GetType().GetMethod(
                "SimulateSingleClick",
                Members,
                null,
                new[] { typeof(EventBase), typeof(int) },
                null);
            Assert.That(simularClique, Is.Not.Null, "O UI Toolkit precisa disponibilizar a simulação de clique neste teste.");
            simularClique.Invoke(botaoMover.clickable, new object[] { ClickEvent.GetPooled(), 100 });

            Assert.That((bool)Get(menu, "modoMoverMapaAtivo"), Is.True,
                "O callback do botão MOVER precisa armar a ordem usada pelo clique no Mapa M.");
        }
        finally
        {
            UnityEngine.Object.Destroy(unidade);
            UnityEngine.Object.Destroy(menuObjeto);
        }
    }

    [UnityTest]
    public IEnumerator CliqueDoMapaMComAtaqueDeAreaEntregaOPontoAoExecutor()
    {
        Vector3 centro = new Vector3(100000f, 0f, 100000f);
        GameObject terreno = CriarTerreno(centro);
        GameObject menuObjeto = new GameObject("MenuComandoMapaMAtaqueTeste");
        menuObjeto.SetActive(false);
        Component menu = menuObjeto.AddComponent(TypeOf("MenuComandoController"));
        GameObject ordensObjeto = new GameObject("OrdensMapaMAtaqueTeste");
        Component ordens = ordensObjeto.AddComponent(TypeOf("DesenharLinhasOrdem"));
        GameObject unidade = new GameObject("UnidadeMapaMAtaqueTeste");
        unidade.SetActive(false);
        NavMeshData navMeshData = null;
        NavMeshDataInstance navMeshInstancia = default;

        try
        {
            navMeshInstancia = CriarNavMesh(terreno, centro, out navMeshData);
            unidade.transform.position = centro + new Vector3(-180f, 0f, 0f);
            NavMeshAgent agente = unidade.AddComponent<NavMeshAgent>();
            agente.speed = 10f;
            agente.acceleration = 100f;
            agente.radius = 0.3f;
            Component controle = unidade.AddComponent(TypeOf("ControleUnidade"));
            unidade.SetActive(true);
            yield return null;

            Assert.That(agente.isOnNavMesh, Is.True);
            Set(menu, "menuAberto", true);
            Set(menu, "desenhadorOrdens", ordens);
            ((IList)Get(menu, "unidadesSelecionadasMenu")).Add(controle);
            ExecutarOrdem(menu, "ATACAR");

            Vector3 pontoClicado = centro + new Vector3(180f, 0f, 35f);
            Assert.That(ClicarNoMapa(menu, pontoClicado, 0), Is.True,
                "O clique do Mapa M precisa ser consumido pelo modo ATAQUE.");
            AssertDestinoHorizontal(agente, pontoClicado,
                "O ataque de área precisa enviar ao executor o ponto clicado no Mapa M.");
            Assert.That(agente.hasPath || agente.pathPending, Is.True,
                "O executor aceitou o ponto, mas não iniciou o caminho do ataque de área.");
        }
        finally
        {
            ordens.SendMessage("CancelarModo", SendMessageOptions.DontRequireReceiver);
            if (navMeshInstancia.valid) navMeshInstancia.Remove();
            Object.Destroy(unidade);
            Object.Destroy(menuObjeto);
            Object.Destroy(ordensObjeto);
            Object.Destroy(terreno);
            if (navMeshData != null) Object.Destroy(navMeshData);
        }
    }

    [UnityTest]
    public IEnumerator CliqueDoMapaMComPatrulhaConfirmaERotaParaOPontoClicado()
    {
        Vector3 centro = new Vector3(100000f, 0f, 100000f);
        GameObject terreno = CriarTerreno(centro);
        GameObject menuObjeto = new GameObject("MenuComandoMapaMPatrulhaTeste");
        menuObjeto.SetActive(false);
        Component menu = menuObjeto.AddComponent(TypeOf("MenuComandoController"));
        GameObject ordensObjeto = new GameObject("OrdensMapaMPatrulhaTeste");
        Component ordens = ordensObjeto.AddComponent(TypeOf("DesenharLinhasOrdem"));
        GameObject unidade = new GameObject("UnidadeMapaMPatrulhaTeste");
        unidade.SetActive(false);
        NavMeshData navMeshData = null;
        NavMeshDataInstance navMeshInstancia = default;

        try
        {
            navMeshInstancia = CriarNavMesh(terreno, centro, out navMeshData);
            unidade.transform.position = centro + new Vector3(-180f, 0f, 0f);
            NavMeshAgent agente = unidade.AddComponent<NavMeshAgent>();
            agente.speed = 10f;
            agente.acceleration = 100f;
            agente.radius = 0.3f;
            Component controle = unidade.AddComponent(TypeOf("ControleUnidade"));
            unidade.SetActive(true);
            yield return null;

            Assert.That(agente.isOnNavMesh, Is.True);
            Set(menu, "menuAberto", true);
            Set(menu, "desenhadorOrdens", ordens);
            ((IList)Get(menu, "unidadesSelecionadasMenu")).Add(controle);
            ExecutarOrdem(menu, "PATRULHAR");

            Vector3 pontoClicado = centro + new Vector3(180f, 0f, 35f);
            Assert.That(ClicarNoMapa(menu, pontoClicado, 0), Is.True,
                "O clique esquerdo do Mapa M precisa registrar o ponto de patrulha.");
            IList pontos = (IList)Get(ordens, "pontosPatrulha");
            Assert.That(pontos.Count, Is.EqualTo(1));
            Assert.That(pontos[0], Is.EqualTo(pontoClicado));

            Assert.That(ClicarNoMapa(menu, pontoClicado, 1), Is.True,
                "O clique direito do Mapa M precisa confirmar a patrulha.");
            Component patrulha = unidade.GetComponent("ComportamentoPatrulhaUniversal");
            Assert.That(patrulha, Is.Not.Null, "O executor universal de patrulha precisa receber a rota.");
            MethodInfo obterPontos = patrulha.GetType().GetMethod("ObterPontos", Members);
            Assert.That(obterPontos, Is.Not.Null);
            IList rota = obterPontos.Invoke(patrulha, null) as IList;
            Assert.That(rota, Is.Not.Null);
            Assert.That(rota.Count, Is.EqualTo(1));
            Assert.That(rota[0], Is.EqualTo(pontoClicado));

            float limiteExecucao = Time.realtimeSinceStartup + 2f;
            while (!agente.hasPath && !agente.pathPending && Time.realtimeSinceStartup < limiteExecucao)
            {
                yield return null;
            }

            PropertyInfo ordemAtual = controle.GetType().GetProperty("OrdemAtual", Members);
            Assert.That(ordemAtual, Is.Not.Null);
            Assert.That(ordemAtual.GetValue(controle).ToString(), Is.EqualTo("Patrulhando"),
                "O controlador da unidade precisa receber a ordem de patrulha.");
            AssertDestinoHorizontal(agente, pontoClicado,
                "O executor da patrulha precisa usar o ponto clicado no Mapa M.");
        }
        finally
        {
            ordens.SendMessage("CancelarModo", SendMessageOptions.DontRequireReceiver);
            if (navMeshInstancia.valid) navMeshInstancia.Remove();
            Object.Destroy(unidade);
            Object.Destroy(menuObjeto);
            Object.Destroy(ordensObjeto);
            Object.Destroy(terreno);
            if (navMeshData != null) Object.Destroy(navMeshData);
        }
    }

    [UnityTest]
    public IEnumerator CliqueDoMapaMComSeguirSelecionaOAlvoEEntregaAOrdem()
    {
        Vector3 centro = new Vector3(100000f, 0f, 100000f);
        GameObject menuObjeto = new GameObject("MenuComandoMapaMSeguirTeste");
        menuObjeto.SetActive(false);
        Component menu = menuObjeto.AddComponent(TypeOf("MenuComandoController"));
        GameObject ordensObjeto = new GameObject("OrdensMapaMSeguirTeste");
        Component ordens = ordensObjeto.AddComponent(TypeOf("DesenharLinhasOrdem"));
        GameObject seguidor = new GameObject("UnidadeMapaMSeguirTeste");
        GameObject alvo = new GameObject("AlvoMapaMSeguirTeste");
        seguidor.SetActive(false);
        alvo.SetActive(false);

        try
        {
            seguidor.transform.position = centro + new Vector3(-300f, 0f, 0f);
            Component controle = seguidor.AddComponent(TypeOf("ControleUnidade"));
            seguidor.SetActive(true);
            Component identidadeAlvo = alvo.AddComponent(TypeOf("IdentidadeUnidade"));
            FieldInfo timeAlvo = identidadeAlvo.GetType().GetField("teamID", Members);
            Assert.That(timeAlvo, Is.Not.Null);
            timeAlvo.SetValue(identidadeAlvo, 1);
            alvo.transform.position = centro;
            alvo.SetActive(true);
            yield return null;

            Set(menu, "menuAberto", true);
            Set(menu, "desenhadorOrdens", ordens);
            ((IList)Get(menu, "unidadesSelecionadasMenu")).Add(controle);
            ExecutarOrdem(menu, "SEGUIR");

            Assert.That(ClicarNoMapa(menu, alvo.transform.position, 0), Is.True,
                "O clique do Mapa M precisa ser consumido pelo modo SEGUIR.");
            Component seguir = seguidor.GetComponent("ComportamentoSeguirUniversal");
            Assert.That(seguir, Is.Not.Null, "O seguidor precisa receber o comportamento de seguir.");
            PropertyInfo alvoSeguido = seguir.GetType().GetProperty("AlvoSeguido", Members);
            Assert.That(alvoSeguido, Is.Not.Null);
            Assert.That(alvoSeguido.GetValue(seguir), Is.EqualTo(alvo.transform),
                "A ordem precisa apontar para a unidade selecionada no Mapa M.");
            PropertyInfo ordemAtual = controle.GetType().GetProperty("OrdemAtual", Members);
            Assert.That(ordemAtual.GetValue(controle).ToString(), Is.EqualTo("Seguindo"));
        }
        finally
        {
            ordens.SendMessage("CancelarModo", SendMessageOptions.DontRequireReceiver);
            Object.Destroy(seguidor);
            Object.Destroy(alvo);
            Object.Destroy(menuObjeto);
            Object.Destroy(ordensObjeto);
        }
    }

    [UnityTest]
    public IEnumerator CliqueDoMapaMComBotaoLancamentoEntregaOAlvoExatoAoMissil()
    {
        Vector3 centro = new Vector3(100000f, 0f, 100000f);
        GameObject menuObjeto = new GameObject("MenuComandoMapaMMissilTeste");
        menuObjeto.SetActive(false);
        Component menu = menuObjeto.AddComponent(TypeOf("MenuComandoController"));
        GameObject unidade = new GameObject("UnidadeMapaMMissilTeste");
        unidade.SetActive(false);
        GameObject prefabMissil = new GameObject("PrefabMissilMapaMTeste");
        prefabMissil.transform.position = centro + Vector3.left * 5000f;
        prefabMissil.SetActive(false);
        Behaviour missilPrefabComponente = prefabMissil.AddComponent(TypeOf("MisselEstrategicoLongoAlcance")) as Behaviour;
        Assert.That(missilPrefabComponente, Is.Not.Null);
        missilPrefabComponente.enabled = false;
        GameObject missilLancadoTeste = null;

        try
        {
            unidade.transform.position = centro;
            Component controle = unidade.AddComponent(TypeOf("ControleUnidade"));
            Component silo = unidade.AddComponent(TypeOf("SiloLancadorEstrategico"));
            Set(silo, "prefabMisselEstrategico", prefabMissil);
            Set(silo, "alcanceMaximo", 5000f);
            Set(silo, "tempoPreparacao", 0f);
            Set(silo, "tempoRecarga", 1f);
            Set(silo, "misseisDisponiveis", 1);
            unidade.SetActive(true);
            yield return null;

            Set(menu, "menuAberto", true);
            ((IList)Get(menu, "unidadesSelecionadasMenu")).Add(controle);
            ExecutarOrdem(menu, "LANCAR_MISSIL");
            Assert.That((bool)Get(menu, "modoLancamentoMissilMapaAtivo"), Is.True,
                "O botão de lançamento precisa armar uma unidade que tenha silo compatível.");
            Assert.That((bool)Get(silo, "modoMarcacao"), Is.True,
                "O silo precisa entrar em marcação pelo comando do menu.");

            Vector3 pontoClicado = centro + new Vector3(800f, 0f, 1200f);
            Assert.That(ClicarNoMapa(menu, pontoClicado, 0), Is.True,
                "O clique do Mapa M precisa ser consumido pelo lançamento estratégico armado.");
            Assert.That((int)Get(silo, "misseisDisponiveis"), Is.EqualTo(0),
                "O clique válido precisa iniciar o lançamento e consumir um míssil.");

            float limiteLancamento = Time.realtimeSinceStartup + 2f;
            while (missilLancadoTeste == null && Time.realtimeSinceStartup < limiteLancamento)
            {
                UnityEngine.Object[] encontrados = Resources.FindObjectsOfTypeAll(TypeOf("MisselEstrategicoLongoAlcance"));
                for (int i = 0; i < encontrados.Length; i++)
                {
                    Component componenteMissil = encontrados[i] as Component;
                    PropertyInfo propriedadeAlvo = componenteMissil != null
                        ? componenteMissil.GetType().GetProperty("Alvo", Members)
                        : null;
                    if (componenteMissil != null
                        && componenteMissil.gameObject.scene.IsValid()
                        && propriedadeAlvo != null
                        && pontoClicado.Equals(propriedadeAlvo.GetValue(componenteMissil)))
                    {
                        missilLancadoTeste = componenteMissil.gameObject;
                        break;
                    }
                }
                if (missilLancadoTeste == null) yield return null;
            }

            Assert.That(missilLancadoTeste, Is.Not.Null,
                "O míssil disparado precisa receber o ponto clicado no Mapa M.");
            PropertyInfo alvoDoMissil = missilLancadoTeste.GetComponent(TypeOf("MisselEstrategicoLongoAlcance"))
                .GetType().GetProperty("Alvo", Members);
            Assert.That(alvoDoMissil.GetValue(missilLancadoTeste.GetComponent(TypeOf("MisselEstrategicoLongoAlcance"))),
                Is.EqualTo(pontoClicado),
                "A coordenada de impacto deve ser a do mapa M, não a do mouse na câmera principal.");
        }
        finally
        {
            if (missilLancadoTeste != null) Object.Destroy(missilLancadoTeste);
            Object.Destroy(unidade);
            Object.Destroy(menuObjeto);
            Object.Destroy(prefabMissil);
        }
    }

    [UnityTest]
    public IEnumerator CliqueDireitoDoMapaMComLancadorNavalManualEntregaPontoExatoAoMissil()
    {
#if UNITY_EDITOR
        Vector3 centro = new Vector3(100000f, 10000f, 100000f);
        GameObject menuObjeto = new GameObject("MenuComandoMapaMDisparoNavalManualTeste");
        menuObjeto.SetActive(false);
        Component menu = menuObjeto.AddComponent(TypeOf("MenuComandoController"));
        GameObject unidade = new GameObject("UnidadeMapaMDisparoNavalManualTeste");
        unidade.SetActive(false);
        GameObject missilLancado = null;
        GameObject prefabMissil = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Navios_Guerra/Efeitos/N-02 Tridente.prefab");
        Assert.That(prefabMissil, Is.Not.Null, "O prefab naval real N-02 Tridente não foi encontrado.");

        try
        {
            unidade.transform.position = centro;
            Component identidade = unidade.AddComponent(TypeOf("IdentidadeUnidade"));
            Set(identidade, "teamID", 1);
            Component controle = unidade.AddComponent(TypeOf("ControleUnidade"));
            Component lancador = unidade.AddComponent(TypeOf("LancadorNaval"));
            Set(lancador, "prefabMissel", prefabMissil);
            Set(lancador, "prefabTorpedo", null);
            Set(lancador, "municaoTotal", 1);
            Set(lancador, "torpedosTotal", 0);
            Set(lancador, "tirosPorSalva", 1);
            Set(lancador, "tempoRecargaSalva", 0f);
            Set(lancador, "tempoUltimoDisparo", Time.time - 10f);
            Set(lancador, "tempoUltimoDisparoConfirmado", -1f);
            Set(lancador, "salvaEmAndamento", false);
            FieldInfo modoAtual = lancador.GetType().GetField("modoAtual", Members);
            Assert.That(modoAtual, Is.Not.Null, "O estado operacional do lançador naval precisa estar acessível.");
            Set(lancador, "modoAtual", System.Enum.Parse(modoAtual.FieldType, "Manual"));
            unidade.SetActive(true);
            yield return null;

            MethodInfo ativarCombate = controle.GetType().GetMethod("DefinirModoCombate", Members);
            Assert.That(ativarCombate, Is.Not.Null, "O controle da unidade precisa expor o modo de combate atual.");
            ativarCombate.Invoke(controle, new object[] { true });

            Set(menu, "menuAberto", true);
            ((IList)Get(menu, "unidadesSelecionadasMenu")).Add(controle);
            Vector3 pontoClicado = centro + new Vector3(800f, 0f, 1200f);
            Assert.That(ClicarNoMapa(menu, pontoClicado, 1), Is.True,
                "O botão direito no Mapa M precisa ser consumido pelo disparo manual naval.");
            Assert.That((int)Get(lancador, "municaoTotal"), Is.EqualTo(0),
                "O lançador aceitou o disparo, mas não consumiu o míssil configurado.");

            UnityEngine.Object[] encontrados = Resources.FindObjectsOfTypeAll(TypeOf("MisselNaval"));
            for (int i = 0; i < encontrados.Length; i++)
            {
                Component componente = encontrados[i] as Component;
                if (componente != null && componente.gameObject.scene.IsValid()
                    && componente.gameObject != prefabMissil)
                {
                    missilLancado = componente.gameObject;
                    break;
                }
            }

            Assert.That(missilLancado, Is.Not.Null,
                "O lançador naval não criou um míssil ativo ao receber o ponto do Mapa M.");
            FieldInfo alvoMissil = missilLancado.GetComponent(TypeOf("MisselNaval"))
                .GetType().GetField("pontoAlvo", Members);
            Assert.That(alvoMissil, Is.Not.Null, "O míssil naval precisa manter o ponto recebido para guiagem.");
            Vector3 destinoRecebido = (Vector3)alvoMissil.GetValue(missilLancado.GetComponent(TypeOf("MisselNaval")));
            MethodInfo obterNivelMar = TypeOf("NavalPlacementResolver").GetMethod("ResolveSeaLevel", Members);
            Assert.That(obterNivelMar, Is.Not.Null, "O nível do mar usado no disparo precisa estar disponível.");
            pontoClicado.y = System.Convert.ToSingle(obterNivelMar.Invoke(null, null));
            Assert.That(destinoRecebido, Is.EqualTo(pontoClicado),
                "O míssil naval manual precisa guardar a coordenada do Mapa M, sem usar a câmera principal.");
        }
        finally
        {
            if (missilLancado != null) UnityEngine.Object.Destroy(missilLancado);
            UnityEngine.Object.Destroy(unidade);
            UnityEngine.Object.Destroy(menuObjeto);
        }
#else
        yield return null;
        Assert.Fail("O teste naval precisa executar no Editor para carregar o prefab real.");
#endif
    }

    private static GameObject CriarTerreno(Vector3 centro)
    {
        GameObject terreno = GameObject.CreatePrimitive(PrimitiveType.Plane);
        terreno.name = "TerrenoMapaMOrdemTeste";
        terreno.transform.position = centro;
        terreno.transform.localScale = Vector3.one * 100f;
        return terreno;
    }

    [UnityTest]
    public IEnumerator MoverMostraMotivoDaUnidadeQuandoNaoHaNavMeshValida()
    {
        GameObject menuObjeto = new GameObject("MenuComandoMotivoMoverTeste");
        menuObjeto.SetActive(false);
        Component menu = menuObjeto.AddComponent(TypeOf("MenuComandoController"));
        Label feedback = new Label();
        Set(menu, "ordemFeedback", feedback);

        GameObject unidade = new GameObject("UnidadeSemNavMeshMotivoTeste");
        unidade.SetActive(false);
        unidade.transform.position = new Vector3(10000000f, 0f, 10000000f);
        NavMeshAgent agente = unidade.AddComponent<NavMeshAgent>();
        agente.enabled = false;
        Component controle = unidade.AddComponent(TypeOf("ControleUnidade"));
        unidade.SetActive(true);

        try
        {
            yield return null;
            ((IList)Get(menu, "unidadesSelecionadasMenu")).Add(controle);
            MethodInfo enviarMover = menu.GetType().GetMethod("EnviarOrdemMoverMapa", Members);
            Assert.That(enviarMover, Is.Not.Null, "O caminho MOVER do Satélite precisa existir.");

            enviarMover.Invoke(menu, new object[] { unidade.transform.position + Vector3.forward * 100f });

            string motivo = (string)controle.GetType().GetProperty("UltimoMotivoRecusaOrdem").GetValue(controle);
            Assert.That(motivo, Is.Not.Empty, "A unidade precisa expor o motivo concreto da recusa.");
            Assert.That(feedback.text, Does.Contain(unidade.name), "O resultado do MOVER deve identificar a unidade recusada.");
            Assert.That(feedback.text, Does.Contain(motivo), "O resultado do MOVER deve explicar a recusa da unidade.");
        }
        finally
        {
            Object.Destroy(unidade);
            Object.Destroy(menuObjeto);
        }
    }

    [Test]
    public void PatrulhaExplicaPorQuePetroleiroFoiExcluido()
    {
        GameObject menuObjeto = new GameObject("MenuComandoPatrulhaLogisticaTeste");
        menuObjeto.SetActive(false);
        Component menu = menuObjeto.AddComponent(TypeOf("MenuComandoController"));
        Label feedback = new Label();
        Set(menu, "ordemFeedback", feedback);

        GameObject petroleiro = new GameObject("PetroleiroSemPatrulhaMilitarTeste");
        petroleiro.SetActive(false);
        Component controle = petroleiro.AddComponent(TypeOf("NavioPetroleiro"));
        ((IList)Get(menu, "unidadesSelecionadasMenu")).Add(controle);

        try
        {
            ExecutarOrdem(menu, "PATRULHAR");
            Assert.That(feedback.text, Does.Contain(petroleiro.name), "A recusa deve identificar o navio logístico.");
            Assert.That(feedback.text, Does.Contain("petroleiro controlado pela logística de petróleo"),
                "A recusa deve explicar que petroleiros não recebem patrulha militar.");
        }
        finally
        {
            Object.DestroyImmediate(petroleiro);
            Object.DestroyImmediate(menuObjeto);
        }
    }

    [UnityTest]
    public IEnumerator PatrulhaAereaComAutonomiaInsuficienteMostraMotivoDeRetornoSeguro()
    {
        Component baseAerea;
        GameObject baseObjeto = CriarBaseAereaTeste(new Vector3(20000f, 0f, 20000f), out baseAerea);
        GameObject aeronave = new GameObject("AeronaveAutonomiaInsuficienteTeste");
        aeronave.SetActive(false);
        aeronave.transform.position = baseObjeto.transform.position;
        Component controleAviao = aeronave.AddComponent(TypeOf("ControleAviao"));
        Component combustivel = aeronave.AddComponent(TypeOf("CombustivelUnidade"));
        Component controle = aeronave.AddComponent(TypeOf("ControleUnidade"));
        aeronave.SetActive(true);

        try
        {
            yield return null;
            ConfigurarAeronaveDeTeste(controleAviao, combustivel, baseAerea, 100f, 1f, 10f);
            MethodInfo emitirPatrulha = controle.GetType().GetMethod("EmitirOrdemPatrulha", Members);
            Assert.That(emitirPatrulha, Is.Not.Null);
            bool aceita = (bool)emitirPatrulha.Invoke(controle, new object[] { new List<Vector3>
            {
                aeronave.transform.position + Vector3.right * 10000f
            } });

            Assert.That(aceita, Is.False, "A patrulha não deve decolar sem combustível suficiente para o setor e retorno.");
            string motivo = (string)controle.GetType().GetProperty("UltimoMotivoRecusaOrdem").GetValue(controle);
            Assert.That(motivo, Does.Contain("autonomia insuficiente"),
                "O Satélite deve receber o motivo real da rejeição do controlador aéreo.");
        }
        finally
        {
            Object.Destroy(aeronave);
            Object.Destroy(baseObjeto);
        }
    }

    [UnityTest]
    public IEnumerator PatrulhaAereaComReservaSuficienteAceitaOrdem()
    {
        Component baseAerea;
        GameObject baseObjeto = CriarBaseAereaTeste(new Vector3(30000f, 0f, 30000f), out baseAerea);
        GameObject aeronave = new GameObject("AeronaveAutonomiaSuficienteTeste");
        aeronave.SetActive(false);
        aeronave.transform.position = baseObjeto.transform.position;
        Component controleAviao = aeronave.AddComponent(TypeOf("ControleAviao"));
        Component combustivel = aeronave.AddComponent(TypeOf("CombustivelUnidade"));
        Component controle = aeronave.AddComponent(TypeOf("ControleUnidade"));
        aeronave.SetActive(true);

        try
        {
            yield return null;
            ConfigurarAeronaveDeTeste(controleAviao, combustivel, baseAerea, 1000f, 1000f, 0.1f);
            MethodInfo emitirPatrulha = controle.GetType().GetMethod("EmitirOrdemPatrulha", Members);
            Assert.That(emitirPatrulha, Is.Not.Null);
            bool aceita = (bool)emitirPatrulha.Invoke(controle, new object[] { new List<Vector3>
            {
                aeronave.transform.position + Vector3.right * 10f
            } });

            Assert.That(aceita, Is.True, "Aeronave com base e reserva de combustível suficientes deve aceitar a patrulha.");
            string motivo = (string)controle.GetType().GetProperty("UltimoMotivoRecusaOrdem").GetValue(controle);
            Assert.That(motivo, Is.Empty);
        }
        finally
        {
            Object.Destroy(aeronave);
            Object.Destroy(baseObjeto);
        }
    }

    [UnityTest]
    public IEnumerator PatrulhaAereaDistanteUsaBaseAliadaProximaComoRetornoSeguro()
    {
        Component baseAerea;
        GameObject baseOrigem = CriarBaseAereaTeste(Vector3.zero, out baseAerea);
        Component baseAliada;
        GameObject baseAvancada = CriarBaseAereaTeste(new Vector3(9500f, 0f, 0f), out baseAliada);
        Transform vaga = new GameObject("VagaBaseAliadaTeste").transform;
        vaga.SetParent(baseAvancada.transform, false);
        Set(baseAliada, "waypointsPatio", new List<Transform> { vaga });
        baseOrigem.SetActive(true);
        baseAvancada.SetActive(true);

        GameObject aeronave = new GameObject("AeronaveTransfronteiricaTeste");
        aeronave.SetActive(false);
        aeronave.transform.position = baseOrigem.transform.position;
        Component controleAviao = aeronave.AddComponent(TypeOf("ControleAviao"));
        Component combustivel = aeronave.AddComponent(TypeOf("CombustivelUnidade"));
        Component controle = aeronave.AddComponent(TypeOf("ControleUnidade"));
        aeronave.SetActive(true);

        try
        {
            yield return null;
            ConfigurarAeronaveDeTeste(controleAviao, combustivel, baseAerea, 1000f, 308f, 0.1f);
            MethodInfo emitirPatrulha = controle.GetType().GetMethod("EmitirOrdemPatrulha", Members);
            Assert.That(emitirPatrulha, Is.Not.Null);
            bool aceita = (bool)emitirPatrulha.Invoke(controle, new object[] { new List<Vector3>
            {
                new Vector3(10000f, 0f, 0f)
            } });

            Assert.That(aceita, Is.True,
                "A missão distante deve ser permitida quando há combustível para alcançar o setor, a base aliada próxima e manter a reserva. Recusa: "
                + controle.GetType().GetProperty("UltimoMotivoRecusaOrdem").GetValue(controle));
            Assert.That(Get(controleAviao, "baseDestinoRetornoPendente"), Is.SameAs(baseAliada),
                "A aeronave deve guardar a base aliada escolhida para o retorno após a decolagem.");

            bool aceitaMover = (bool)controleAviao.GetType().GetMethod("ReceberOrdemManual", Members)
                .Invoke(controleAviao, new object[] { new Vector3(10000f, 0f, 0f) });
            Assert.That(aceitaMover, Is.True,
                "A ordem MOVER distante também deve poder usar a base aliada próxima sem reduzir a reserva de combustível.");
            Assert.That(Get(controleAviao, "baseDestinoRetornoPendente"), Is.SameAs(baseAliada));
        }
        finally
        {
            Object.Destroy(aeronave);
            Object.Destroy(baseOrigem);
            Object.Destroy(baseAvancada);
        }
    }

    private static GameObject CriarBaseAereaTeste(Vector3 position, out Component controle)
    {
        GameObject baseObjeto = new GameObject("BaseAereaAutonomiaTeste");
        baseObjeto.SetActive(false);
        baseObjeto.transform.position = position;
        controle = baseObjeto.AddComponent(TypeOf("GerenciadorAeroporto"));

        GameObject aproximacao = new GameObject("WaypointAproximacaoTeste");
        aproximacao.transform.SetParent(baseObjeto.transform, false);
        aproximacao.transform.position = position;
        GameObject toque = new GameObject("WaypointToqueTeste");
        toque.transform.SetParent(baseObjeto.transform, false);
        toque.transform.position = position + Vector3.forward * 25f;
        Set(controle, "waypointsDecida", new List<Transform> { aproximacao.transform, toque.transform });
        return baseObjeto;
    }

    private static void ConfigurarAeronaveDeTeste(
        Component controleAviao,
        Component combustivel,
        Component baseAerea,
        float capacidade,
        float combustivelAtual,
        float consumoPorSegundo)
    {
        Set(controleAviao, "aeroportoOrigem", baseAerea);
        System.Type estadoAviao = controleAviao.GetType().GetNestedType("EstadoAviao", BindingFlags.Public);
        Assert.That(estadoAviao, Is.Not.Null);
        Set(controleAviao, "estadoAtual", System.Enum.Parse(estadoAviao, "ReservaHangar"));
        Set(combustivel, "usaCombustivel", true);
        Set(combustivel, "combustivelInfinito", false);
        Set(combustivel, "capacidade", capacidade);
        Set(combustivel, "combustivelAtual", combustivelAtual);
        Set(combustivel, "consumoPorSegundoMovendo", consumoPorSegundo);
        Set(combustivel, "preencherAoIniciar", false);
    }

    private static NavMeshDataInstance CriarNavMesh(GameObject terreno, Vector3 centro, out NavMeshData navMeshData)
    {
        var fontes = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Mesh,
                sourceObject = terreno.GetComponent<MeshFilter>().sharedMesh,
                transform = terreno.transform.localToWorldMatrix,
                area = 0
            }
        };
        navMeshData = NavMeshBuilder.BuildNavMeshData(
            NavMesh.GetSettingsByID(0),
            fontes,
            new Bounds(centro, new Vector3(1200f, 100f, 1200f)),
            Vector3.zero,
            Quaternion.identity);
        Assert.That(navMeshData, Is.Not.Null, "O cenário NavMesh isolado do teste precisa ser criado.");
        return NavMesh.AddNavMeshData(navMeshData);
    }

    private static bool ClicarNoMapa(Component menu, Vector3 destino, int botao)
    {
        MethodInfo processarClique = menu.GetType().GetMethod("TentarProcessarCliqueMapaM", Members);
        Assert.That(processarClique, Is.Not.Null, "O método de comando do Mapa M não existe.");
        return (bool)processarClique.Invoke(menu, new object[] { destino, botao });
    }

    private static void ExecutarOrdem(Component menu, string ordem)
    {
        MethodInfo executarOrdem = menu.GetType().GetMethod("ExecutarOrdem", Members);
        Assert.That(executarOrdem, Is.Not.Null, "O executor dos botões do menu satélite não existe.");
        executarOrdem.Invoke(menu, new object[] { ordem });
    }

    private static void AssertDestinoHorizontal(NavMeshAgent agente, Vector3 esperado, string mensagem)
    {
        Vector3 destino = agente.destination;
        Assert.That(destino.x, Is.EqualTo(esperado.x).Within(0.001f), mensagem);
        Assert.That(destino.z, Is.EqualTo(esperado.z).Within(0.001f), mensagem);
        Assert.That(destino.y, Is.EqualTo(esperado.y).Within(0.1f), mensagem);
    }

    private static void Set(object target, string name, object value)
    {
        FieldInfo campo = target.GetType().GetField(name, Members);
        Assert.That(campo, Is.Not.Null, "Campo de teste ausente: " + name);
        campo.SetValue(target, value);
    }

    private static void SetStatic(System.Type targetType, string name, object value)
    {
        FieldInfo campo = targetType.GetField(name, Members);
        Assert.That(campo, Is.Not.Null, "Campo estático de teste ausente: " + name);
        Assert.That(campo.IsStatic, Is.True, "O campo de teste precisa ser estático: " + name);
        campo.SetValue(null, value);
    }

    private static object Get(object target, string name)
    {
        FieldInfo campo = target.GetType().GetField(name, Members);
        Assert.That(campo, Is.Not.Null, "Campo de teste ausente: " + name);
        return campo.GetValue(target);
    }

    private static System.Type TypeOf(string name)
    {
        Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            System.Type found = assemblies[i].GetType(name, false);
            if (found != null) return found;
        }

        Assert.Fail("Tipo de runtime ausente: " + name);
        return null;
    }
}
