using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class AgriculturaInvestimentoRecorrentePlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private object governo;
    private object pais;
    private object tempo;
    private Component agricultura;
    private bool agriculturaCriada;
    private long saldoOriginal;
    private int investimentoFixoOriginal;
    private int teamId;
    private float percentualOriginal;
    private bool percentualAtivoOriginal;
    private bool semRecursosOriginal;
    private long ultimaCobrancaOriginal;
    private int diaOriginal;
    private float duracaoDiaOriginal;
    private float[] mixOriginal;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Type govType = TypeOf("SistemaGovernoMundial");
        govType.GetMethod("GarantirInstancia", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        Type timeType = TypeOf("GerenciadorTempo");
        timeType.GetMethod("GarantirInstancia", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        yield return null;

        governo = govType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
        tempo = timeType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
        Assert.That(governo, Is.Not.Null);
        Assert.That(tempo, Is.Not.Null);

        teamId = Convert.ToInt32(Read(governo, "teamJogador")) == 1 ? 2 : 1;
        pais = govType.GetMethod("ObterPais", Members, null, new[] { typeof(int) }, null).Invoke(governo, new object[] { teamId });
        Assert.That(pais, Is.Not.Null);
        saldoOriginal = Convert.ToInt64(Read(pais, "saldo"));
        investimentoFixoOriginal = Convert.ToInt32(Read(pais, "investimentoAgricolaSemanal"));
        percentualOriginal = Convert.ToSingle(Read(pais, "investimentoAgricolaPercentualSemanal"));
        percentualAtivoOriginal = Convert.ToBoolean(Read(pais, "investimentoAgricolaPercentualAtivo"));
        semRecursosOriginal = Convert.ToBoolean(Read(pais, "investimentoAgricolaSemRecursos"));
        ultimaCobrancaOriginal = Convert.ToInt64(Read(pais, "ultimaCobrancaAgricola"));
        diaOriginal = Convert.ToInt32(Read(tempo, "totalDias"));
        duracaoDiaOriginal = Convert.ToSingle(Read(tempo, "duracaoDiaSegundos"));
        float[] mix = Read(pais, "mixCulturaAgricola") as float[];
        mixOriginal = mix != null ? (float[])mix.Clone() : null;
        Write(pais, "saldo", 100_000_000_000L);
        Write(tempo, "duracaoDiaSegundos", 1000000f);

        Component governmentComponent = governo as Component;
        Type agricultureType = TypeOf("AgriculturaNacional");
        agricultura = governmentComponent.GetComponent(agricultureType);
        if (agricultura == null)
        {
            agricultura = governmentComponent.gameObject.AddComponent(agricultureType);
            agriculturaCriada = true;
        }
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (pais != null)
        {
            Write(pais, "saldo", saldoOriginal);
            Write(pais, "investimentoAgricolaSemanal", investimentoFixoOriginal);
            Write(pais, "investimentoAgricolaPercentualSemanal", percentualOriginal);
            Write(pais, "investimentoAgricolaPercentualAtivo", percentualAtivoOriginal);
            Write(pais, "investimentoAgricolaSemRecursos", semRecursosOriginal);
            Write(pais, "ultimaCobrancaAgricola", ultimaCobrancaOriginal);
            if (mixOriginal != null) Write(pais, "mixCulturaAgricola", mixOriginal);
        }
        if (tempo != null)
        {
            Write(tempo, "totalDias", diaOriginal);
            Write(tempo, "duracaoDiaSegundos", duracaoDiaOriginal);
        }
        if (agriculturaCriada && agricultura != null)
        {
            UnityEngine.Object.Destroy(agricultura);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator DefinirPercentualNaoCobraNaHoraEProcessaCobrancaTodaSemana()
    {
        long saldoAntes = Convert.ToInt64(Read(pais, "saldo"));
        bool configurado = Convert.ToBoolean(Invoke(agricultura, "DefinirInvestimentoPercentualSemanal", teamId, 0.1f));
        Assert.That(configurado, Is.True);
        Assert.That(Convert.ToInt64(Read(pais, "saldo")), Is.EqualTo(saldoAntes), "Escolher o percentual no menu apenas agenda a recorrencia.");

        MethodInfo cobrar = agricultura.GetType().GetMethod("ProcessarCobrancaSemanal", Members);
        Assert.That(cobrar, Is.Not.Null);
        long saldoOperacional = Convert.ToInt64(Invoke(governo, "ObterSaldoDisponivelParaOperacao", teamId));
        long primeiraCobranca = (long)Math.Floor(saldoOperacional * 0.1d / 100d);
        Assert.That(primeiraCobranca, Is.GreaterThan(0));
        cobrar.Invoke(agricultura, new[] { pais });
        Assert.That(Convert.ToInt64(Read(pais, "saldo")), Is.EqualTo(saldoAntes - primeiraCobranca));
        Assert.That(Convert.ToInt64(Read(pais, "ultimaCobrancaAgricola")), Is.EqualTo(primeiraCobranca));

        long saldoDepoisDaPrimeira = Convert.ToInt64(Read(pais, "saldo"));
        long segundaBase = Convert.ToInt64(Invoke(governo, "ObterSaldoDisponivelParaOperacao", teamId));
        long segundaCobranca = (long)Math.Floor(segundaBase * 0.1d / 100d);
        Assert.That(segundaCobranca, Is.GreaterThan(0));
        cobrar.Invoke(agricultura, new[] { pais });
        Assert.That(Convert.ToInt64(Read(pais, "saldo")), Is.EqualTo(saldoDepoisDaPrimeira - segundaCobranca), "A recorrencia deve debitar novamente na semana seguinte.");
        Assert.That(Convert.ToInt64(Read(pais, "saldo")), Is.GreaterThanOrEqualTo(0));

        Write(pais, "saldo", 0L);
        cobrar.Invoke(agricultura, new[] { pais });
        Assert.That(Convert.ToInt64(Read(pais, "saldo")), Is.Zero, "Sem saldo, a recorrencia nao pode gerar saldo negativo.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator InvestimentoFixoEmMilhoesSoDebitaNaViradaSemanal()
    {
        long saldoAntes = Convert.ToInt64(Read(pais, "saldo"));
        long valorEsperado = 250_000_000L;
        long custoCalculado = Convert.ToInt64(TypeOf("AgriculturaNacional")
            .GetMethod("CalcularCobrancaFixaSemanal", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, new object[] { 250 }));
        Assert.That(custoCalculado, Is.EqualTo(valorEsperado));

        bool configurado = Convert.ToBoolean(Invoke(agricultura, "DefinirInvestimentoSemanal", teamId, 250));
        Assert.That(configurado, Is.True);
        Assert.That(Convert.ToInt64(Read(pais, "saldo")), Is.EqualTo(saldoAntes),
            "Configurar a cobrança fixa não pode retirar dinheiro imediatamente.");

        MethodInfo cobrar = agricultura.GetType().GetMethod("ProcessarCobrancaSemanal", Members);
        Assert.That(cobrar, Is.Not.Null);
        cobrar.Invoke(agricultura, new[] { pais });
        Assert.That(Convert.ToInt64(Read(pais, "saldo")), Is.EqualTo(saldoAntes - valorEsperado));
        Assert.That(Convert.ToInt64(Read(pais, "ultimaCobrancaAgricola")), Is.EqualTo(valorEsperado));

        long saldoDepoisDaPrimeira = Convert.ToInt64(Read(pais, "saldo"));
        cobrar.Invoke(agricultura, new[] { pais });
        Assert.That(Convert.ToInt64(Read(pais, "saldo")), Is.EqualTo(saldoDepoisDaPrimeira - valorEsperado),
            "A mesma configuração deve gerar uma nova cobrança na semana seguinte.");

        Write(pais, "saldo", 0L);
        cobrar.Invoke(agricultura, new[] { pais });
        Assert.That(Convert.ToInt64(Read(pais, "saldo")), Is.Zero,
            "Uma cobrança sem saldo não pode criar dívida nem comida gratuita.");
        Assert.That(Convert.ToBoolean(Read(pais, "investimentoAgricolaSemRecursos")), Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator FocoEmArrozRespeitaParticipacaoERedistribuiAsOutrasCulturas()
    {
        bool aplicado = Convert.ToBoolean(Invoke(agricultura, "DefinirParticipacaoCultura", teamId, 4, 60));
        Assert.That(aplicado, Is.True);
        float[] mix = (float[])Read(pais, "mixCulturaAgricola");
        Assert.That(mix[4], Is.EqualTo(0.6f).Within(0.001f));
        float soma = 0f;
        for (int i = 0; i < mix.Length; i++) soma += Mathf.Max(0f, mix[i]);
        Assert.That(soma, Is.EqualTo(1f).Within(0.001f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SaveComCulturasEEstoqueNacionalZeradoReconciliaComidaAoRestaurar()
    {
        Type governmentType = TypeOf("SistemaGovernoMundial");
        MethodInfo obterEstoque = governmentType.GetMethod("ObterEstoque", Members, null, new[] { typeof(int), typeof(string) }, null);
        MethodInfo adicionarEstoque = governmentType.GetMethod("AdicionarEstoque", Members, null, new[] { typeof(int), typeof(string), typeof(int) }, null);
        MethodInfo removerEstoque = governmentType.GetMethod("RemoverEstoque", Members, null, new[] { typeof(int), typeof(string), typeof(int) }, null);
        Assert.That(obterEstoque, Is.Not.Null);
        Assert.That(adicionarEstoque, Is.Not.Null);
        Assert.That(removerEstoque, Is.Not.Null);
        string cultura = Convert.ToString(TypeOf("AgriculturaNacional")
            .GetMethod("ObterIdCultura", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { 4 }));
        int comidaOriginal = Convert.ToInt32(Read(pais, "comida"));
        int estoqueAgricolaOriginal = Convert.ToInt32(Read(pais, "estoqueAgricolaTotal"));
        int estoqueCulturaOriginal = Convert.ToInt32(obterEstoque.Invoke(governo, new object[] { teamId, cultura }));
        Type saveGameType = TypeOf("SistemaSaveGame");
        saveGameType.GetMethod("GarantirInstancia", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        object saveGame = saveGameType.GetField("Instancia", BindingFlags.Static | BindingFlags.Public).GetValue(null);
        Assert.That(saveGame, Is.Not.Null);
        object saveOriginal = Read(saveGame, "dadosAtuais");

        try
        {
            adicionarEstoque.Invoke(governo, new object[] { teamId, cultura, 25 });
            long totalCulturasEsperado = 0L;
            int quantidadeCulturas = Convert.ToInt32(TypeOf("AgriculturaNacional")
                .GetProperty("QuantidadeCulturas", BindingFlags.Public | BindingFlags.Static).GetValue(null, null));
            for (int i = 0; i < quantidadeCulturas; i++)
            {
                string id = Convert.ToString(TypeOf("AgriculturaNacional")
                    .GetMethod("ObterIdCultura", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { i }));
                totalCulturasEsperado += Convert.ToInt32(obterEstoque.Invoke(governo, new object[] { teamId, id }));
            }
            Assert.That(totalCulturasEsperado, Is.GreaterThan(0));

            object dadosTeste = Activator.CreateInstance(TypeOf("DadosDoJogo"));
            object industrialTeste = Read(dadosTeste, "industria");
            IList estoquesPais = (IList)Read(industrialTeste, "estoques");
            object estoqueSalvo = Activator.CreateInstance(TypeOf("SaveEstoqueIndustrial"));
            Write(estoqueSalvo, "paisId", teamId.ToString());
            IList recursosSalvos = (IList)Read(estoqueSalvo, "estoques");
            object culturaSalva = Activator.CreateInstance(TypeOf("QuantidadeRecursoIndustrial"));
            Write(culturaSalva, "recursoId", cultura);
            Write(culturaSalva, "quantidade", 25d);
            recursosSalvos.Add(culturaSalva);
            estoquesPais.Add(estoqueSalvo);
            Write(saveGame, "dadosAtuais", dadosTeste);

            // Reproduz o save em que os estoques por cultura existem, mas o
            // total nacional ainda não foi reconstruído após a restauração.
            Write(pais, "comida", 0);
            Write(pais, "estoqueAgricolaTotal", 0);
            MethodInfo reconciliar = saveGameType.GetMethod("ReconciliarCulturasAlimentaresRestauradas", Members);
            Assert.That(reconciliar, Is.Not.Null);
            reconciliar.Invoke(saveGame, null);

            Assert.That(Read(pais, "comida"), Is.EqualTo(totalCulturasEsperado));
            Assert.That(Read(pais, "estoqueAgricolaTotal"), Is.EqualTo(totalCulturasEsperado));
        }
        finally
        {
            int estoqueCulturaAtual = Convert.ToInt32(obterEstoque.Invoke(governo, new object[] { teamId, cultura }));
            if (estoqueCulturaAtual > estoqueCulturaOriginal)
                removerEstoque.Invoke(governo, new object[] { teamId, cultura, estoqueCulturaAtual - estoqueCulturaOriginal });
            else if (estoqueCulturaAtual < estoqueCulturaOriginal)
                adicionarEstoque.Invoke(governo, new object[] { teamId, cultura, estoqueCulturaOriginal - estoqueCulturaAtual });
            Write(pais, "comida", comidaOriginal);
            Write(pais, "estoqueAgricolaTotal", estoqueAgricolaOriginal);
            Write(saveGame, "dadosAtuais", saveOriginal);
        }

        yield return null;
    }

    [Test]
    public void MaiorCobrancaAumentaBonusDeProducaoAteOLimiteConfigurado()
    {
        MethodInfo calcular = TypeOf("AgriculturaNacional").GetMethod("CalcularBonusInvestimentoPercentual", Members);
        float baixo = Convert.ToSingle(calcular.Invoke(null, new object[] { 1000000L }));
        float alto = Convert.ToSingle(calcular.Invoke(null, new object[] { 20000000L }));
        float maximo = Convert.ToSingle(calcular.Invoke(null, new object[] { long.MaxValue }));
        Assert.That(baixo, Is.GreaterThan(0f));
        Assert.That(alto, Is.GreaterThan(baixo));
        Assert.That(maximo, Is.LessThanOrEqualTo(0.5f));
    }

    private static Type TypeOf(string name) { return Type.GetType(name + ", Assembly-CSharp", true); }
    private static object Read(object target, string field) { return target.GetType().GetField(field, Members).GetValue(target); }
    private static void Write(object target, string field, object value) { target.GetType().GetField(field, Members).SetValue(target, value); }
    private static object Invoke(object target, string method, params object[] args) { return target.GetType().GetMethod(method, Members).Invoke(target, args); }
}
