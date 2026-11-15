using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class RacaoETripulacaoEditModeTests
{
    private readonly List<UnityEngine.Object> criados = new List<UnityEngine.Object>();
    private Component governoAnterior;
    private Type tipoGoverno;
    private PropertyInfo propInstanciaGoverno;

    [SetUp]
    public void SetUp()
    {
        tipoGoverno = Resolve("SistemaGovernoMundial");
        propInstanciaGoverno = tipoGoverno.GetProperty("Instancia", BindingFlags.Public | BindingFlags.Static);
        governoAnterior = propInstanciaGoverno?.GetValue(null) as Component;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = criados.Count - 1; i >= 0; i--)
        {
            if (criados[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(criados[i]);
            }
        }
        criados.Clear();
        if (propInstanciaGoverno != null)
        {
            propInstanciaGoverno.SetValue(null, governoAnterior);
        }
    }

    private static Type Resolve(string typeName)
    {
        Type t = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(t, Is.Not.Null, "Tipo nao encontrado: " + typeName);
        return t;
    }

    private GameObject CriarUnidade(string nome, string tipoUnidadeNome, int teamId)
    {
        GameObject go = new GameObject(nome);
        criados.Add(go);

        Type tIdentidade = Resolve("IdentidadeUnidade");
        Type tEnum = Resolve("TipoUnidade");
        Component id = go.AddComponent(tIdentidade);
        tIdentidade.GetField("teamID").SetValue(id, teamId);
        tIdentidade.GetField("tipoUnidade").SetValue(id, Enum.Parse(tEnum, tipoUnidadeNome));
        return go;
    }

    [Test]
    public void TripulacaoRespeitaTiersDefinidosPeloUsuario()
    {
        Type tTrip = Resolve("TripulacaoUnidade");
        MethodInfo resolverNominal = tTrip.GetMethod("ResolverTripulacaoNominal", BindingFlags.Public | BindingFlags.Static);
        Assert.That(resolverNominal, Is.Not.Null);

        int Resolver(GameObject obj) => (int)resolverNominal.Invoke(null, new object[] { obj });

        GameObject tanque = CriarUnidade("Tank_King", "Veiculo", 1);
        Assert.That(Resolver(tanque), Is.InRange(1, 6));

        GameObject caca = CriarUnidade("F-16D Bloco 60 Falcão", "Aereo", 1);
        Assert.That(Resolver(caca), Is.InRange(1, 2));

        GameObject bombardeiro = CriarUnidade("B260", "Aereo", 1);
        Assert.That(Resolver(bombardeiro), Is.EqualTo(2));

        GameObject drone = CriarUnidade("Drone_Hasaf", "Aereo", 1);
        Assert.That(Resolver(drone), Is.Zero);

        GameObject corveta = CriarUnidade("Corveta_01 Variant", "Naval", 1);
        Assert.That(Resolver(corveta), Is.InRange(80, 100));

        GameObject destroyer = CriarUnidade("USS Vindicator", "Naval", 1);
        Assert.That(Resolver(destroyer), Is.InRange(360, 400));

        GameObject transporte = CriarUnidade("Ministral", "Naval", 1);
        transporte.AddComponent(Resolve("NavioTransporteTropas"));
        Assert.That(Resolver(transporte), Is.InRange(2000, 3000));

        GameObject portaAvioes = CriarUnidade("Uss Enterprise", "Naval", 1);
        Assert.That(Resolver(portaAvioes), Is.InRange(3500, 6000));
    }

    [Test]
    public void RacaoOperaApenasParaUnidadesDoJogadorExcluindoIAsEDrones()
    {
        GameObject govObj = new GameObject("GovernoTesteRacao");
        criados.Add(govObj);
        Component gov = govObj.AddComponent(tipoGoverno);
        propInstanciaGoverno.SetValue(null, gov);
        tipoGoverno.GetField("teamJogador").SetValue(gov, 1);

        Type tRacao = Resolve("RacaoUnidade");
        MethodInfo garantir = tRacao.GetMethod("Garantir", BindingFlags.Public | BindingFlags.Static);
        MethodInfo configurar = tRacao.GetMethod("ConfigurarSeNecessario", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo propUsaRacao = tRacao.GetProperty("UsaRacao", BindingFlags.Public | BindingFlags.Instance);

        GameObject soldadoAliado = CriarUnidade("Soldier", "Infantaria", 1);
        object racaoAliado = garantir.Invoke(null, new object[] { soldadoAliado, true });
        Assert.That(racaoAliado, Is.Not.Null);
        Assert.That((bool)propUsaRacao.GetValue(racaoAliado), Is.True);

        GameObject soldadoIA = CriarUnidade("Soldier_IA", "Infantaria", 2);
        Component racaoIA = soldadoIA.AddComponent(tRacao);
        configurar.Invoke(racaoIA, new object[] { true });
        Assert.That((bool)propUsaRacao.GetValue(racaoIA), Is.False);

        GameObject droneJogador = CriarUnidade("UAV_01_Prefeb", "Aereo", 1);
        Assert.That(garantir.Invoke(null, new object[] { droneJogador, true }), Is.Null);
    }

    [Test]
    public void DestruicaoDePortaAvioesInimigoCreditaSeisMilAbatesEFomeNaoAtribuiGuerra()
    {
        GameObject govObj = new GameObject("GovernoTesteAbate");
        criados.Add(govObj);
        Component gov = govObj.AddComponent(tipoGoverno);
        propInstanciaGoverno.SetValue(null, gov);
        tipoGoverno.GetField("teamJogador").SetValue(gov, 1);

        Type tPais = Resolve("DadosPaisGoverno");
        IList paises = (IList)tipoGoverno.GetField("paises").GetValue(gov);
        paises.Clear();

        object paisJogador = Activator.CreateInstance(tPais);
        tPais.GetField("teamId").SetValue(paisJogador, 1);
        tPais.GetField("nomePais").SetValue(paisJogador, "Aliado");
        tPais.GetField("comida").SetValue(paisJogador, 200);
        paises.Add(paisJogador);

        object paisInimigo = Activator.CreateInstance(tPais);
        tPais.GetField("teamId").SetValue(paisInimigo, 2);
        tPais.GetField("nomePais").SetValue(paisInimigo, "Inimigo");
        tPais.GetField("comida").SetValue(paisInimigo, 200);
        paises.Add(paisInimigo);

        Type tDanos = Resolve("SistemaDeDanos");
        Type tTrip = Resolve("TripulacaoUnidade");
        Type tCarta = Resolve("CartaCombateRegistro");

        GameObject atacante = CriarUnidade("Destroyer_Iron", "Naval", 1);
        GameObject carrierInimigo = CriarUnidade("Uss Enterprise", "Naval", 2);
        tTrip.GetMethod("Garantir", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { carrierInimigo });
        Component danosInimigo = carrierInimigo.AddComponent(tDanos);
        tDanos.GetField("ehEstrutura").SetValue(danosInimigo, false);
        tDanos.GetField("vidaMaxima").SetValue(danosInimigo, 100f);
        tDanos.GetField("vidaAtual").SetValue(danosInimigo, 100f);

        FieldInfo campoAbates = tPais.GetField("pessoasInimigasAbatidas");
        int abatesAntes = (int)campoAbates.GetValue(paisJogador);
        tDanos.GetMethod("ReceberDano", BindingFlags.Public | BindingFlags.Instance).Invoke(danosInimigo, new object[] { 200f, atacante });
        Assert.That((int)campoAbates.GetValue(paisJogador) - abatesAntes, Is.EqualTo(6000));

        MethodInfo resolverAbate = tTrip.GetMethod("ResolverTripulacaoParaAbate", BindingFlags.Public | BindingFlags.Static);
        Assert.That((int)resolverAbate.Invoke(null, new object[] { carrierInimigo }), Is.Zero);

        GameObject soldadoAliado = CriarUnidade("Soldier_Faminto", "Infantaria", 1);
        Component danosAliado = soldadoAliado.AddComponent(tDanos);
        tDanos.GetField("ehEstrutura").SetValue(danosAliado, false);
        tDanos.GetField("vidaMaxima").SetValue(danosAliado, 50f);
        tDanos.GetField("vidaAtual").SetValue(danosAliado, 50f);

        PropertyInfo propEventos = tCarta.GetProperty("Eventos", BindingFlags.Public | BindingFlags.Static);
        int eventosAntes = ((IList)propEventos.GetValue(null)).Count;
        tDanos.GetMethod("ReceberDanoSemAgressor", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(danosAliado, new object[] { 100f, "FOME (SEM RAÇÃO)" });

        IList eventos = (IList)propEventos.GetValue(null);
        Assert.That(eventos.Count, Is.EqualTo(eventosAntes + 1));
        object ultimoEvento = eventos[0];
        Assert.That((int)ultimoEvento.GetType().GetField("equipeAtacante").GetValue(ultimoEvento), Is.EqualTo(-1));
        Assert.That((int)ultimoEvento.GetType().GetField("tripulacaoAbatida").GetValue(ultimoEvento), Is.Zero);
    }

    [Test]
    public void ConsumirComidaLogisticaDebitaEstoqueNacionalERespeitaEstoqueZerado()
    {
        GameObject govObj = new GameObject("GovernoTesteComida");
        criados.Add(govObj);
        Component gov = govObj.AddComponent(tipoGoverno);
        propInstanciaGoverno.SetValue(null, gov);
        tipoGoverno.GetField("teamJogador").SetValue(gov, 1);

        Type tPais = Resolve("DadosPaisGoverno");
        IList paises = (IList)tipoGoverno.GetField("paises").GetValue(gov);
        paises.Clear();
        object pais = Activator.CreateInstance(tPais);
        tPais.GetField("teamId").SetValue(pais, 1);
        tPais.GetField("nomePais").SetValue(pais, "Aliado");
        FieldInfo campoComida = tPais.GetField("comida");
        campoComida.SetValue(pais, 60);
        paises.Add(pais);

        Type tRacao = Resolve("RacaoUnidade");
        GameObject carrierAliado = CriarUnidade("USS Sovereign", "Naval", 1);
        object racao = tRacao.GetMethod("Garantir", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { carrierAliado, false });
        Assert.That(racao, Is.Not.Null);

        float capacidade = (float)tRacao.GetProperty("Capacidade", BindingFlags.Public | BindingFlags.Instance).GetValue(racao);
        MethodInfo abastecerNacional = tRacao.GetMethod("AbastecerUsandoEstoqueNacional", BindingFlags.Public | BindingFlags.Instance);
        float preenchido = (float)abastecerNacional.Invoke(racao, new object[] { capacidade, 1 });
        Assert.That(preenchido, Is.GreaterThan(0f));
        Assert.That((int)campoComida.GetValue(pais), Is.LessThan(60));

        campoComida.SetValue(pais, 0);
        tRacao.GetField("racaoAtual").SetValue(racao, 0f);
        float semComida = (float)abastecerNacional.Invoke(racao, new object[] { capacidade, 1 });
        Assert.That(semComida, Is.Zero);
    }

    [Test]
    public void NavioAbastecimentoConsomeReservaDeRacaoSemExcederSaldo()
    {
        GameObject navObj = CriarUnidade("Nav abastecimento", "Naval", 1);
        Type tAbast = Resolve("NavioAbastecimento");
        Component abastecedor = navObj.AddComponent(tAbast);
        tAbast.GetField("racaoTotal").SetValue(abastecedor, 500f);
        tAbast.GetField("capacidadeReservaRacao").SetValue(abastecedor, 1000f);

        MethodInfo consumir = tAbast.GetMethod("ConsumirRacaoParaUnidade", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo propRacao = tAbast.GetProperty("RacaoTotal", BindingFlags.Public | BindingFlags.Instance);

        float retirado1 = (float)consumir.Invoke(abastecedor, new object[] { 300f });
        Assert.That(retirado1, Is.EqualTo(300f).Within(0.01f));
        Assert.That((float)propRacao.GetValue(abastecedor), Is.EqualTo(200f).Within(0.01f));

        float retirado2 = (float)consumir.Invoke(abastecedor, new object[] { 400f });
        Assert.That(retirado2, Is.EqualTo(200f).Within(0.01f));
        Assert.That((float)propRacao.GetValue(abastecedor), Is.Zero);
    }

    [Test]
    public void EstruturasEAeronavesNaoRecebemRacaoEEstruturasNaoRecebemTripulacao()
    {
        GameObject govObj = new GameObject("GovernoTesteExclusoes");
        criados.Add(govObj);
        Component gov = govObj.AddComponent(tipoGoverno);
        propInstanciaGoverno.SetValue(null, gov);
        tipoGoverno.GetField("teamJogador").SetValue(gov, 1);

        Type tRacao = Resolve("RacaoUnidade");
        Type tTrip = Resolve("TripulacaoUnidade");
        MethodInfo garantirRacao = tRacao.GetMethod("Garantir", BindingFlags.Public | BindingFlags.Static);
        MethodInfo garantirTrip = tTrip.GetMethod("Garantir", BindingFlags.Public | BindingFlags.Static);
        MethodInfo ehEstruturaImovel = tTrip.GetMethod("EhEstruturaImovel", BindingFlags.Public | BindingFlags.Static);

        // Estaleiro mal serializado como Naval ainda é estrutura imóvel (sem ração e sem tripulação individual)
        GameObject estaleiroObj = CriarUnidade("Estaleiros navais", "Naval", 1);
        estaleiroObj.AddComponent(Resolve("Estaleiro"));
        Assert.That((bool)ehEstruturaImovel.Invoke(null, new object[] { estaleiroObj }), Is.True);
        Assert.That(garantirRacao.Invoke(null, new object[] { estaleiroObj, true }), Is.Null);
        Assert.That(garantirTrip.Invoke(null, new object[] { estaleiroObj }), Is.Null);

        // PierMarinha também não deve receber ração nem tripulação
        GameObject pierObj = CriarUnidade("Pier_Marinha", "Estrutura", 1);
        pierObj.AddComponent(Resolve("PierMarinha"));
        Assert.That(garantirRacao.Invoke(null, new object[] { pierObj, true }), Is.Null);
        Assert.That(garantirTrip.Invoke(null, new object[] { pierObj }), Is.Null);

        // Aeronaves (caças/aviões/helicópteros) usam tripulação para abates, mas não recebem barra de ração
        GameObject cacaObj = CriarUnidade("F-16D Bloco 60 Falcão", "Aereo", 1);
        Assert.That(garantirRacao.Invoke(null, new object[] { cacaObj, true }), Is.Null);
        object tripCaca = garantirTrip.Invoke(null, new object[] { cacaObj });
        Assert.That(tripCaca, Is.Not.Null);
        Assert.That((int)tTrip.GetProperty("TripulacaoMaxima").GetValue(tripCaca), Is.GreaterThanOrEqualTo(1));

        // Porta-aviões NÃO é estrutura imóvel (mesmo herdando GerenciadorAeroporto) e recebe ração + tripulação
        GameObject carrierObj = CriarUnidade("USS Sovereign Carrier", "Naval", 1);
        carrierObj.AddComponent(Resolve("GerenciadorPortaAvioes"));
        Assert.That((bool)ehEstruturaImovel.Invoke(null, new object[] { carrierObj }), Is.False);
        Assert.That(garantirRacao.Invoke(null, new object[] { carrierObj, true }), Is.Not.Null);
        Assert.That(garantirTrip.Invoke(null, new object[] { carrierObj }), Is.Not.Null);
    }

    [Test]
    public void FomeZeraTripulacaoSemDestruirUnidadeCobraPensaoEReposicaoReativa()
    {
        GameObject govObj = new GameObject("GovernoTesteFomeTripulacao");
        criados.Add(govObj);
        Component gov = govObj.AddComponent(tipoGoverno);
        propInstanciaGoverno.SetValue(null, gov);
        tipoGoverno.GetField("teamJogador").SetValue(gov, 1);

        Type tPais = Resolve("DadosPaisGoverno");
        IList paises = (IList)tipoGoverno.GetField("paises").GetValue(gov);
        paises.Clear();
        object pais = Activator.CreateInstance(tPais);
        tPais.GetField("teamId").SetValue(pais, 1);
        tPais.GetField("nomePais").SetValue(pais, "Aliado");
        tPais.GetField("saldo").SetValue(pais, 10_000_000L);
        tPais.GetField("populacaoMilitarAtiva").SetValue(pais, 500);
        tPais.GetField("alistaveis").SetValue(pais, 1000);
        tPais.GetField("mortosAcumulados").SetValue(pais, 0);
        tPais.GetField("pensionistasMilitares").SetValue(pais, 0);
        tPais.GetField("gastosIndenizacoesMilitares").SetValue(pais, 0L);
        paises.Add(pais);

        GameObject corveta = CriarUnidade("Corveta_Test", "Naval", 1);
        Type tDanos = Resolve("SistemaDeDanos");
        Component danos = corveta.AddComponent(tDanos);
        tDanos.GetField("ehEstrutura").SetValue(danos, false);
        tDanos.GetField("vidaMaxima").SetValue(danos, 200f);
        tDanos.GetField("vidaAtual").SetValue(danos, 200f);

        Type tTrip = Resolve("TripulacaoUnidade");
        object trip = tTrip.GetMethod("Garantir", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { corveta });
        Assert.That(trip, Is.Not.Null);

        int maxTrip = (int)tTrip.GetProperty("TripulacaoMaxima").GetValue(trip);
        Assert.That(maxTrip, Is.GreaterThan(0));

        MethodInfo aplicarFome = tTrip.GetMethod("AplicarBaixaPorFome", BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < 20; i++)
        {
            aplicarFome.Invoke(trip, new object[] { 2f, 0.25f });
        }

        Assert.That((int)tTrip.GetProperty("TripulacaoAtual").GetValue(trip), Is.Zero);
        Assert.That((bool)tTrip.GetProperty("EstaSemTripulacao").GetValue(trip), Is.True);
        Assert.That((bool)tTrip.GetProperty("EstaOperacional").GetValue(trip), Is.False);
        Assert.That((bool)tTrip.GetMethod("UnidadeEstaOperacional", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { corveta }), Is.False);

        // Casco permanece íntegro (sem explodir/desaparecer) e vulnerável a dano
        Assert.That((bool)tDanos.GetProperty("Morreu").GetValue(danos), Is.False);
        Assert.That((float)tDanos.GetField("vidaAtual").GetValue(danos), Is.EqualTo(200f).Within(0.01f));
        Assert.That(((MonoBehaviour)danos).enabled, Is.True);

        // Financeiro e baixas registrados corretamente
        Assert.That((int)tPais.GetField("mortosAcumulados").GetValue(pais), Is.GreaterThanOrEqualTo(maxTrip));
        Assert.That((int)tPais.GetField("pensionistasMilitares").GetValue(pais), Is.GreaterThanOrEqualTo(maxTrip));
        Assert.That((long)tPais.GetField("gastosIndenizacoesMilitares").GetValue(pais), Is.GreaterThan(0L));
        Assert.That((long)tPais.GetField("saldo").GetValue(pais), Is.LessThan(10_000_000L));

        // Resgate / reposição de tripulação reativa a unidade
        int repostos = (int)tTrip.GetMethod("TentarReporTripulacao", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(trip, new object[] { maxTrip, false });
        Assert.That(repostos, Is.EqualTo(maxTrip));
        Assert.That((int)tTrip.GetProperty("TripulacaoAtual").GetValue(trip), Is.EqualTo(maxTrip));
        Assert.That((bool)tTrip.GetProperty("EstaSemTripulacao").GetValue(trip), Is.False);
        Assert.That((bool)tTrip.GetProperty("EstaOperacional").GetValue(trip), Is.True);
    }

    [Test]
    public void MarcadorSlotCidadeNaoConstruidoEhIdentificadoEOcultavel()
    {
        GameObject slotCidade = CriarUnidade("City_Slot_Test", "Estrutura", 1);
        Type tMarcador = Resolve("MarcadorTerritorio");
        Component marcador = slotCidade.AddComponent(tMarcador);
        FieldInfo campoConstruir = tMarcador.GetField("construirCidadeAoIniciar");
        campoConstruir.SetValue(marcador, true);

        MethodInfo ehSlot = tMarcador.GetMethod("EhSlotDeCidadeNaoConstruido", BindingFlags.Public | BindingFlags.Static);
        Component idComp = slotCidade.GetComponent(Resolve("IdentidadeUnidade"));
        Assert.That((bool)ehSlot.Invoke(null, new object[] { idComp }), Is.True);

        campoConstruir.SetValue(marcador, false);
        Assert.That((bool)ehSlot.Invoke(null, new object[] { idComp }), Is.False);
    }
}
