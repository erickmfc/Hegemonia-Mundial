using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class C400TransporteTerrestrePlayModeTests
{
    [UnityTest]
    public IEnumerator C400SaiDaPistaBuscaInfantariaEVeiculosEmTerraEDescarregaEmOutroPontoSeco()
    {
        GameObject terreno = CriarSuperficie("C400_PlayMode_Ground", new Vector3(0f, -1f, 0f), new Vector3(2400f, 2f, 2400f), "Chao");
        GameObject agua = CriarSuperficie("C400_PlayMode_Water", new Vector3(1000f, 5f, 700f), new Vector3(140f, 0.2f, 140f), "Agua");
        GameObject aeronaveObjeto = null;
        GameObject pistaObjeto = null;
        GameObject[] carga = new GameObject[4];

        try
        {
            ScriptableObject fichaC400 = Resources.Load<ScriptableObject>("Construcoes/C400");
            ScriptableObject fichaAeroporto = Resources.Load<ScriptableObject>("Construcoes/AeroportoTemporarioC400");
            Assert.That(fichaC400, Is.Not.Null, "A ficha C400 deve estar no caminho de Resources usado pelos menus.");
            Assert.That(fichaAeroporto, Is.Not.Null, "A ficha do aeroporto temporario deve estar no caminho de Resources usado pelos menus.");
            GameObject prefabC400 = PrefabDaFicha(fichaC400);
            GameObject prefabAeroporto = PrefabDaFicha(fichaAeroporto);
            Assert.That(prefabC400, Is.Not.Null);
            Assert.That(prefabAeroporto, Is.Not.Null);
            Assert.That(prefabAeroporto.GetComponent("MiniPistaLogistica"), Is.Not.Null);
            Assert.That(prefabAeroporto.GetComponent("AeroportoTemporarioC400"), Is.Not.Null);

            aeronaveObjeto = UnityEngine.Object.Instantiate(prefabC400, new Vector3(0f, 0.2f, 0f), Quaternion.identity);
            aeronaveObjeto.name = "C400_PlayMode_Test";
            Component transporte = aeronaveObjeto.GetComponent("C700TransporteAereo");
            Assert.That(transporte, Is.Not.Null, "O C400 deve reutilizar o controlador aereo existente.");

            pistaObjeto = new GameObject("MiniPista_C400_PlayMode");
            pistaObjeto.transform.position = new Vector3(160f, 0f, 0f);
            Component pista = pistaObjeto.AddComponent(Type.GetType("MiniPistaLogistica, Assembly-CSharp"));
            Campo(pista, "teamId", 1);
            Campo(pista, "raioAceitacaoDestino", 300f);

            float xBusca = 650f;
            float zBusca = 650f;
            carga[0] = CriarUnidade("C400_PlayMode_Infantaria", TipoUnidadeEnum("Infantaria"), 1, new Vector3(xBusca + 8f, 1f, zBusca));
            carga[1] = CriarUnidade("C400_PlayMode_Tanque", TipoUnidadeEnum("Veiculo"), 1, new Vector3(xBusca + 18f, 1f, zBusca + 5f));
            carga[2] = CriarUnidade("C400_PlayMode_Estrutura", TipoUnidadeEnum("Estrutura"), 1, new Vector3(xBusca + 5f, 1f, zBusca + 12f));
            carga[3] = CriarUnidade("C400_PlayMode_Inimigo", TipoUnidadeEnum("Infantaria"), 2, new Vector3(xBusca + 10f, 1f, zBusca + 18f));

            yield return null;
            Campo(transporte, "velocidadeCruzeiro", 280f);
            Campo(transporte, "velocidadeDecolagem", 75f);
            Campo(transporte, "giroVoo", 180f);
            Campo(transporte, "altitudeCruzeiro", 35f);
            Campo(transporte, "distanciaAproximacao", 65f);
            Campo(transporte, "raioBuscaCarga", 45f);
            Campo(transporte, "timeoutPorPontoAereo", 120f);
            Assert.That(Campo<bool>(transporte, "permitirPousoTerrestreSeco"), Is.True);
            Assert.That(Campo<bool>(transporte, "embarcarAutomaticamenteEmPouso"), Is.True);
            Assert.That(Campo<bool>(transporte, "aceitarSomenteInfantariaEVeiculos"), Is.True);
            Assert.That(Propriedade<bool>(pista, "PistaValida"), Is.True);

            Chamar(transporte, "ReceberOrdemMover", pistaObjeto.transform.position);
            yield return EsperarEstado(transporte, "Estacionado", 120f);
            Assert.That(Vector3.Distance(aeronaveObjeto.transform.position, Campo<Transform>(pista, "parkingPoint").position), Is.LessThan(6f));

            Chamar(transporte, "ReceberOrdemMover", new Vector3(xBusca, 0f, zBusca));
            yield return EsperarQuantidadeCarga(transporte, 2, 120f);
            Assert.That(carga[0].activeSelf, Is.False, "A infantaria aliada deve ser embarcada.");
            Assert.That(carga[1].activeSelf, Is.False, "O veiculo aliado deve ser embarcado.");
            Assert.That(carga[2].activeSelf, Is.True, "Estruturas nao podem entrar no manifesto fisico.");
            Assert.That(carga[3].activeSelf, Is.True, "Unidades de outro time nao podem ser embarcadas.");

            Vector3 entrega = new Vector3(900f, 0f, -500f);
            Chamar(transporte, "ReceberOrdemMover", entrega);
            yield return EsperarEstado(transporte, "Estacionado", 120f);
            yield return EsperarUnidadesAtivas(new[] { carga[0], carga[1] }, 2, 5f);
            Assert.That(Propriedade<int>(transporte, "QuantidadeCargaAtual"), Is.EqualTo(0));
            Assert.That(Vector3.Distance(aeronaveObjeto.transform.position, entrega), Is.LessThan(12f));

            Vector3 posicaoAntesDaAgua = aeronaveObjeto.transform.position;
            Component pistaDestinoAntesDaAgua = Propriedade<Component>(transporte, "PistaDestinoAtual");
            Chamar(transporte, "ReceberOrdemMover", new Vector3(1000f, 0f, 700f));
            yield return null;
            Assert.That(Estado(transporte), Is.EqualTo("Estacionado"), "O C400 deve recusar destinos marcados como agua.");
            Assert.That(Propriedade<Component>(transporte, "PistaDestinoAtual"), Is.SameAs(pistaDestinoAntesDaAgua));
            Assert.That(Vector3.Distance(aeronaveObjeto.transform.position, posicaoAntesDaAgua), Is.LessThan(0.1f));
        }
        finally
        {
            if (aeronaveObjeto != null) UnityEngine.Object.Destroy(aeronaveObjeto);
            if (pistaObjeto != null) UnityEngine.Object.Destroy(pistaObjeto);
            if (terreno != null) UnityEngine.Object.Destroy(terreno);
            if (agua != null) UnityEngine.Object.Destroy(agua);
            for (int i = 0; i < carga.Length; i++)
                if (carga[i] != null) UnityEngine.Object.Destroy(carga[i]);
        }
    }

    private static GameObject CriarSuperficie(string nome, Vector3 posicao, Vector3 escala, string tipo)
    {
        GameObject superficie = GameObject.CreatePrimitive(PrimitiveType.Cube);
        superficie.name = nome;
        superficie.transform.position = posicao;
        superficie.transform.localScale = escala;
        Type tipoMarcador = Type.GetType("MarcadorSuperficieMapa, Assembly-CSharp");
        Type enumSuperficie = Type.GetType("TipoSuperficieMapa, Assembly-CSharp");
        Component marcador = superficie.AddComponent(tipoMarcador);
        object valor = Enum.Parse(enumSuperficie, tipo);
        tipoMarcador.GetMethod("DefinirTipo", BindingFlags.Instance | BindingFlags.Public).Invoke(marcador, new[] { valor });
        tipoMarcador.GetMethod("RecalcularAgora", BindingFlags.Instance | BindingFlags.Public).Invoke(marcador, null);
        return superficie;
    }

    private static GameObject CriarUnidade(string nome, object tipo, int teamId, Vector3 posicao)
    {
        GameObject unidade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        unidade.name = nome;
        unidade.transform.position = posicao;
        Type tipoIdentidade = Type.GetType("IdentidadeUnidade, Assembly-CSharp");
        Component identidade = unidade.AddComponent(tipoIdentidade);
        Campo(identidade, "tipoUnidade", tipo);
        Campo(identidade, "teamID", teamId);
        unidade.AddComponent(Type.GetType("ControleUnidade, Assembly-CSharp"));
        return unidade;
    }

    private static object TipoUnidadeEnum(string nome)
    {
        return Enum.Parse(Type.GetType("TipoUnidade, Assembly-CSharp"), nome);
    }

    private static GameObject PrefabDaFicha(ScriptableObject ficha)
    {
        PropertyInfo propriedade = ficha != null ? ficha.GetType().GetProperty("PrefabDaUnidade", BindingFlags.Instance | BindingFlags.Public) : null;
        return propriedade != null ? propriedade.GetValue(ficha) as GameObject : null;
    }

    private static IEnumerator EsperarEstado(Component transporte, string esperado, float timeout)
    {
        float fim = Time.realtimeSinceStartup + timeout;
        while (transporte != null && Estado(transporte) != esperado)
        {
            if (Time.realtimeSinceStartup >= fim)
                Assert.Fail("C400 nao chegou ao estado " + esperado + "; estado atual=" + Estado(transporte) + "; pos=" + transporte.transform.position);
            yield return null;
        }
    }

    private static IEnumerator EsperarQuantidadeCarga(Component transporte, int quantidade, float timeout)
    {
        float fim = Time.realtimeSinceStartup + timeout;
        while (transporte != null && Propriedade<int>(transporte, "QuantidadeCargaAtual") < quantidade)
        {
            if (Time.realtimeSinceStartup >= fim)
                Assert.Fail("O C400 nao embarcou a quantidade esperada; embarcada=" + Propriedade<int>(transporte, "QuantidadeCargaAtual"));
            yield return null;
        }
    }

    private static IEnumerator EsperarUnidadesAtivas(GameObject[] unidades, int quantidade, float timeout)
    {
        float fim = Time.realtimeSinceStartup + timeout;
        while (ContarAtivas(unidades) < quantidade)
        {
            if (Time.realtimeSinceStartup >= fim)
                Assert.Fail("O C400 nao ativou todas as unidades descarregadas.");
            yield return null;
        }
    }

    private static int ContarAtivas(GameObject[] unidades)
    {
        int total = 0;
        for (int i = 0; i < unidades.Length; i++)
            if (unidades[i] != null && unidades[i].activeSelf) total++;
        return total;
    }

    private static string Estado(Component transporte)
    {
        FieldInfo campo = transporte.GetType().GetField("estadoAtual", BindingFlags.Instance | BindingFlags.Public);
        return campo == null ? string.Empty : campo.GetValue(transporte).ToString();
    }

    private static T Propriedade<T>(Component transporte, string nome)
    {
        PropertyInfo propriedade = transporte.GetType().GetProperty(nome, BindingFlags.Instance | BindingFlags.Public);
        return (T)propriedade.GetValue(transporte);
    }

    private static void Chamar(Component componente, string nome, params object[] argumentos)
    {
        MethodInfo metodo = componente.GetType().GetMethod(nome, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(metodo, Is.Not.Null, "Metodo publico ausente: " + nome);
        metodo.Invoke(componente, argumentos);
    }

    private static void Campo(Component componente, string nome, object valor)
    {
        FieldInfo campo = componente.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(campo, Is.Not.Null, "Campo publico ausente: " + nome);
        campo.SetValue(componente, valor);
    }

    private static T Campo<T>(Component componente, string nome)
    {
        FieldInfo campo = componente.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(campo, Is.Not.Null, "Campo publico ausente: " + nome);
        return (T)campo.GetValue(componente);
    }
}
