using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hegemonia.Cartel;

/// <summary>
/// Configuração reversível e idempotente da demo1 para o Cartel Naval.
/// Não remove creates legados: cria uma camada paralela e deixa o
/// CartelAIController disponível como fallback, porém sem início automático.
/// </summary>
public static class CartelNavalDemoSetup
{
    private const string DemoScenePath = "Assets/_Recovery/demo1.unity";
    private const string ControllerName = "Cartel Naval - Operacoes";
    private const string CreatesName = "Pontos de Operacao Naval";
    private const string ContinuityName = "Continuidade_Mar_Demo";

    [MenuItem("Hegemonia/Cartel Naval/Configurar demo1 naval")]
    public static void ConfigurarDemo1()
    {
        Scene cena = EditorSceneManager.GetActiveScene();
        if (cena.path != DemoScenePath)
        {
            cena = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
        }

        if (!cena.IsValid() || !cena.isLoaded)
        {
            Debug.LogError("[Cartel Naval] Não foi possível abrir a cena demo1.");
            return;
        }

        Transform terrenos = EncontrarTransform("Terrenos");
        GameObject agua = EncontrarObjeto("Agua");
        GameObject agua1 = EncontrarObjeto("Agua (1)");
        GameObject pais3 = EncontrarObjeto("pais3");
        GameObject paredao = EncontrarObjeto("paredao inimigo");

        if (terrenos == null || agua == null || agua1 == null || pais3 == null)
        {
            Debug.LogError("[Cartel Naval] demo1 sem Terrenos, Agua, Agua (1) ou pais3; nada foi alterado.");
            return;
        }

        Transform continuidadeTransform = EncontrarFilho(terrenos, ContinuityName);
        GameObject continuidade = continuidadeTransform != null ? continuidadeTransform.gameObject : null;
        bool jaTrocado = continuidade != null;
        if (!jaTrocado)
        {
            TrocarPosicoesDePais3EAgua1(agua1.transform, pais3.transform);
            continuidade = CriarContinuidadeMar(terrenos, agua, agua1, pais3, paredao);
        }
        else
        {
            CorrigirEscalasDepoisDaTroca(agua1.transform, pais3.transform);
            AtualizarMarcadorAgua(continuidade);
        }

        CartelNavalController controlador = EncontrarControlador();
        if (controlador == null)
        {
            GameObject objetoControlador = new GameObject(ControllerName);
            Undo.RegisterCreatedObjectUndo(objetoControlador, "Criar controlador Cartel Naval");
            controlador = objetoControlador.AddComponent<CartelNavalController>();
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cartel/Barco cartel.prefab");
        controlador.PrefabNavio = prefab;
        controlador.CartelTeamId = 9;
        controlador.NomeCartel = "Cartel Naval";
        controlador.IniciarAutomaticamente = true;
        controlador.SistemaAntigoContinuaDisponivel = true;
        controlador.DesativarControladorAntigoNestaCena = true;
        controlador.NaviosIniciais = 2;
        controlador.MaxNavios = 12;
        controlador.NaviosPorOnda = 2;
        controlador.AtrasosRespawnDias = new[] { 2, 5, 9 };
        controlador.IntervaloVarredura = 12f;
        controlador.LimiarMovimentoRadar = 0.6f;
        controlador.DiasParaReaquisiçãoEmMovimento = 2;
        controlador.RaioDeteccao = 1400f;
        controlador.RoubarCombustivel = true;
        controlador.AtacarComDano = true;
        controlador.DrenoCombustivelPorSegundo = 500;
        controlador.DistanciaAtaque = 70f;

        CartelAIController legado = controlador.ControladorLegado;
        if (legado == null) legado = Object.FindFirstObjectByType<CartelAIController>();
        if (legado != null)
        {
            legado.StartAutomatically = false;
            legado.EnableExpansion = false;
            controlador.ControladorLegado = legado;
            EditorUtility.SetDirty(legado);
        }

        Transform grupoCrates = EncontrarFilho(controlador.transform, CreatesName);
        if (grupoCrates == null)
        {
            GameObject grupo = new GameObject(CreatesName);
            Undo.RegisterCreatedObjectUndo(grupo, "Criar pontos do Cartel Naval");
            grupo.transform.SetParent(controlador.transform, false);
            grupoCrates = grupo.transform;
        }

        CriarCrates(grupoCrates, agua, agua1, pais3, paredao);
        ValidarCratesNaAgua(grupoCrates);
        EditorUtility.SetDirty(controlador);
        EditorSceneManager.MarkSceneDirty(cena);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(cena);

        Debug.Log("[Cartel Naval] demo1 configurada: troca de pais3/Agua (1), continuidade marítima, crates em português, dois barcos iniciais e fallback legado preservado.");
    }

    private static CartelNavalController EncontrarControlador()
    {
        CartelNavalController[] controladores = Object.FindObjectsByType<CartelNavalController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return controladores.Length > 0 ? controladores[0] : null;
    }

    private static void TrocarPosicoesDePais3EAgua1(Transform agua1, Transform pais3)
    {
        Undo.RecordObject(agua1, "Trocar posição de Agua (1) e pais3");
        Undo.RecordObject(pais3, "Trocar posição de pais3 e Agua (1)");

        Vector3 posicao = agua1.position;
        Quaternion rotacao = agua1.rotation;

        agua1.position = pais3.position;
        agua1.rotation = pais3.rotation;

        pais3.position = posicao;
        pais3.rotation = rotacao;

        EditorUtility.SetDirty(agua1);
        EditorUtility.SetDirty(pais3);
    }

    private static void CorrigirEscalasDepoisDaTroca(Transform agua1, Transform pais3)
    {
        // A versão anterior da ferramenta trocava também o tamanho dos
        // objetos. Nesta correção, a água mantém o tamanho do plano de água
        // e o país mantém a escala do seu modelo.
        bool aguaTemEscalaDeModelo = agua1.localScale.x < 10f && agua1.localScale.z < 10f;
        bool paisTemEscalaDeAgua = pais3.localScale.x > 10f || pais3.localScale.z > 10f;
        if (!aguaTemEscalaDeModelo || !paisTemEscalaDeAgua)
        {
            return;
        }

        Vector3 escalaAgua = pais3.localScale;
        pais3.localScale = agua1.localScale;
        agua1.localScale = escalaAgua;
        EditorUtility.SetDirty(agua1);
        EditorUtility.SetDirty(pais3);
    }

    private static GameObject CriarContinuidadeMar(Transform terrenos, GameObject agua, GameObject agua1, GameObject pais3, GameObject paredao)
    {
        GameObject grupo = new GameObject(ContinuityName);
        Undo.RegisterCreatedObjectUndo(grupo, "Criar continuidade do mar");
        grupo.transform.SetParent(terrenos, false);

        MarcadorSuperficieMapa marcador = grupo.AddComponent<MarcadorSuperficieMapa>();
        marcador.DefinirTipo(TipoSuperficieMapa.Agua);

        Bounds boundsAgua = ObterBounds(agua);
        Bounds boundsAgua1 = ObterBounds(agua1);
        Bounds boundsPais3 = ObterBounds(pais3);
        Bounds boundsParedao = paredao != null ? ObterBounds(paredao) : new Bounds(boundsAgua.center + Vector3.back * 200f, Vector3.one * 10f);
        Material materialAgua = ObterMaterial(agua, agua1);

        CriarPonte(grupo.transform, "Agua_Conexao_Normal_Agua1", BordaVoltadaPara(boundsAgua, boundsAgua1.center), BordaVoltadaPara(boundsAgua1, boundsAgua.center), 150f, materialAgua);
        CriarPonte(grupo.transform, "Agua_Conexao_Agua1_Pais3", BordaVoltadaPara(boundsAgua1, boundsPais3.center), BordaVoltadaPara(boundsPais3, boundsAgua1.center), 130f, materialAgua);
        CriarPonte(grupo.transform, "Agua_Conexao_Paredao_Inimigo", BordaVoltadaPara(boundsAgua, boundsParedao.center), BordaVoltadaPara(boundsParedao, boundsAgua.center), 120f, materialAgua);

        AtualizarMarcadorAgua(grupo);
        return grupo;
    }

    private static void AtualizarMarcadorAgua(GameObject continuidade)
    {
        if (continuidade == null) return;
        MarcadorSuperficieMapa marcador = continuidade.GetComponent<MarcadorSuperficieMapa>();
        if (marcador == null) marcador = continuidade.AddComponent<MarcadorSuperficieMapa>();
        marcador.DefinirTipo(TipoSuperficieMapa.Agua);
        marcador.RecalcularAgora();
    }

    private static void CriarPonte(Transform pai, string nome, Vector3 inicio, Vector3 fim, float largura, Material material)
    {
        Transform antiga = EncontrarFilho(pai, nome);
        GameObject ponte = antiga != null ? antiga.gameObject : GameObject.CreatePrimitive(PrimitiveType.Plane);
        if (antiga == null) Undo.RegisterCreatedObjectUndo(ponte, "Criar corredor marítimo");
        ponte.name = nome;
        ponte.layer = 4;
        ponte.transform.SetParent(pai, true);

        Vector3 delta = fim - inicio;
        delta.y = 0f;
        if (delta.sqrMagnitude < 1f) delta = Vector3.forward;
        float comprimento = Mathf.Max(10f, delta.magnitude);
        Vector3 centro = (inicio + fim) * 0.5f;
        centro.y = Mathf.Min(inicio.y, fim.y) + 0.025f;
        ponte.transform.position = centro;
        ponte.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        ponte.transform.localScale = new Vector3(Mathf.Max(10f, largura) / 10f, 1f, comprimento / 10f);

        Renderer renderer = ponte.GetComponent<Renderer>();
        if (renderer != null && material != null) renderer.sharedMaterial = material;
        EditorUtility.SetDirty(ponte);
    }

    private static void CriarCrates(Transform pai, GameObject agua, GameObject agua1, GameObject pais3, GameObject paredao)
    {
        Bounds a = ObterBounds(agua);
        Bounds b = ObterBounds(agua1);
        Bounds p3 = ObterBounds(pais3);
        Bounds parede = paredao != null ? ObterBounds(paredao) : b;
        float y = Mathf.Min(a.center.y, b.center.y) + 0.15f;

        CriarCrate(pai, "Spawn_Navio_01", TipoCreateNavalCartel.SpawnNavio01, DentroDaAgua(a, 0.24f, 0.35f, y), 45f, 0);
        CriarCrate(pai, "Spawn_Navio_02", TipoCreateNavalCartel.SpawnNavio02, DentroDaAgua(a, 0.72f, 0.58f, y), 45f, 0);
        CriarCrate(pai, "Rota_Naval_01", TipoCreateNavalCartel.Rota01, DentroDaAgua(a, 0.28f, 0.70f, y), 45f, 1);
        CriarCrate(pai, "Rota_Naval_02", TipoCreateNavalCartel.Rota02, DentroDaAgua(a, 0.70f, 0.30f, y), 45f, 2);
        Vector3 pontoRota03 = EncontrarPontoMaritimoValido(DentroDaAgua(b, 0.28f, 0.42f, y));
        Vector3 pontoRota04 = EncontrarPontoMaritimoValido(DentroDaAgua(b, 0.72f, 0.64f, y));
        if (!NavalPlacementResolver.IsWaterAtPosition(pontoRota03))
        {
            Vector3 alternativa = EncontrarPontoMaritimoValido(pontoRota04 + Vector3.right * 60f);
            pontoRota03 = NavalPlacementResolver.IsWaterAtPosition(alternativa) ? alternativa : pontoRota04;
        }
        CriarCrate(pai, "Rota_Naval_03", TipoCreateNavalCartel.Rota03, pontoRota03, 45f, 3);
        CriarCrate(pai, "Rota_Naval_04", TipoCreateNavalCartel.Rota04, pontoRota04, 45f, 4);
        CriarCrate(pai, "Base_Naval", TipoCreateNavalCartel.BaseNaval, DentroDaAgua(a, 0.50f, 0.50f, y), 55f, 0);

        Vector3 entreAguaEPais = Vector3.Lerp(b.center, p3.center, 0.28f);
        entreAguaEPais.y = y;
        CriarCrate(pai, "Area_Emboscada_Petroleiros", TipoCreateNavalCartel.AreaEmboscadaPetroleiro, entreAguaEPais, 120f, 0);
        Vector3 areaPlataforma = Vector3.Lerp(a.center, parede.center, 0.62f);
        areaPlataforma.y = y;
        CriarCrate(pai, "Area_Emboscada_Plataformas", TipoCreateNavalCartel.AreaEmboscadaPlataforma, areaPlataforma, 120f, 0);

        Vector3 fuga01 = DentroDaAgua(a, 0.08f, 0.82f, y);
        Vector3 fuga02 = DentroDaAgua(b, 0.90f, 0.18f, y);
        CriarCrate(pai, "Fuga_Naval_01", TipoCreateNavalCartel.Fuga01, fuga01, 55f, 0);
        CriarCrate(pai, "Fuga_Naval_02", TipoCreateNavalCartel.Fuga02, fuga02, 55f, 0);
        CriarCrate(pai, "Reforco_Naval_01", TipoCreateNavalCartel.Reforco01, DentroDaAgua(a, 0.15f, 0.50f, y), 55f, 0);
        CriarCrate(pai, "Reforco_Naval_02", TipoCreateNavalCartel.Reforco02, DentroDaAgua(b, 0.50f, 0.82f, y), 55f, 0);
        CriarCrate(pai, "Reforco_Naval_03", TipoCreateNavalCartel.Reforco03, DentroDaAgua(a, 0.85f, 0.50f, y), 55f, 0);
    }

    private static CartelNavalCrate CriarCrate(Transform pai, string nome, TipoCreateNavalCartel tipo, Vector3 posicao, float raio, int sequencia)
    {
        Transform existente = EncontrarFilho(pai, nome);
        GameObject objeto = existente != null ? existente.gameObject : new GameObject(nome);
        if (existente == null) Undo.RegisterCreatedObjectUndo(objeto, "Criar crate naval");
        objeto.name = nome;
        objeto.transform.SetParent(pai, true);
        objeto.transform.position = EncontrarPontoMaritimoValido(posicao);
        objeto.transform.rotation = Quaternion.identity;

        CartelNavalCrate crate = objeto.GetComponent<CartelNavalCrate>();
        if (crate == null) crate = objeto.AddComponent<CartelNavalCrate>();
        crate.IdEstavel = "cartel-naval/" + nome.ToLowerInvariant();
        crate.Tipo = tipo;
        crate.SequenciaRota = sequencia;
        crate.Raio = raio;
        crate.ExigirAgua = true;
        crate.Disponivel = true;
        crate.DescricaoPortugues = CartelNavalCrate.ObterDescricaoPadrao(tipo);
        crate.DesenharGizmo = true;
        EditorUtility.SetDirty(crate);
        return crate;
    }

    private static Vector3 EncontrarPontoMaritimoValido(Vector3 ponto)
    {
        if (NavalPlacementResolver.IsWaterAtPosition(ponto)) return ponto;

        Vector3[] direcoes =
        {
            Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            (Vector3.right + Vector3.forward).normalized,
            (Vector3.right + Vector3.back).normalized,
            (Vector3.left + Vector3.forward).normalized,
            (Vector3.left + Vector3.back).normalized
        };

        float[] distancias = { 8f, 16f, 32f, 64f, 128f, 256f };
        for (int i = 0; i < distancias.Length; i++)
        {
            for (int j = 0; j < direcoes.Length; j++)
            {
                Vector3 candidato = ponto + direcoes[j] * distancias[i];
                candidato.y = ponto.y;
                if (NavalPlacementResolver.IsWaterAtPosition(candidato)) return candidato;
            }
        }

        return ponto;
    }

    private static void ValidarCratesNaAgua(Transform pai)
    {
        CartelNavalCrate[] crates = pai.GetComponentsInChildren<CartelNavalCrate>(true);
        int pontosNaAgua = 0;
        List<string> pontosForaDaAgua = new List<string>();
        for (int i = 0; i < crates.Length; i++)
        {
            CartelNavalCrate crate = crates[i];
            if (crate == null || !crate.ExigirAgua) continue;
            if (NavalPlacementResolver.IsWaterAtPosition(crate.Position)) pontosNaAgua++;
            else pontosForaDaAgua.Add(crate.name);
        }

        if (pontosForaDaAgua.Count == 0)
        {
            Debug.Log("[Cartel Naval] Validação marítima: " + pontosNaAgua + "/" + pontosNaAgua + " crates sobre água.");
        }
        else
        {
            Debug.LogWarning("[Cartel Naval] Validação marítima: " + pontosNaAgua + " crates sobre água; fora da água: " + string.Join(", ", pontosForaDaAgua.ToArray()));
        }
    }

    private static Vector3 DentroDaAgua(Bounds bounds, float percentualX, float percentualZ, float y)
    {
        float x = Mathf.Lerp(bounds.min.x, bounds.max.x, Mathf.Clamp01(percentualX));
        float z = Mathf.Lerp(bounds.min.z, bounds.max.z, Mathf.Clamp01(percentualZ));
        Vector3 ponto = new Vector3(x, y, z);
        return ponto;
    }

    private static Vector3 BordaVoltadaPara(Bounds bounds, Vector3 alvo)
    {
        Vector3 direcao = alvo - bounds.center;
        direcao.y = 0f;
        if (direcao.sqrMagnitude < 0.01f) direcao = Vector3.forward;
        direcao.Normalize();

        float distanciaX = Mathf.Abs(direcao.x) > 0.001f ? bounds.extents.x / Mathf.Abs(direcao.x) : float.MaxValue;
        float distanciaZ = Mathf.Abs(direcao.z) > 0.001f ? bounds.extents.z / Mathf.Abs(direcao.z) : float.MaxValue;
        float distancia = Mathf.Min(distanciaX, distanciaZ);
        Vector3 ponto = bounds.center + direcao * Mathf.Max(1f, distancia - 4f);
        ponto.y = bounds.center.y;
        return ponto;
    }

    private static Bounds ObterBounds(GameObject objeto)
    {
        Renderer[] renderers = objeto != null ? objeto.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        bool encontrou = false;
        Bounds bounds = new Bounds(objeto != null ? objeto.transform.position : Vector3.zero, Vector3.one * 20f);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            if (!encontrou) { bounds = renderer.bounds; encontrou = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!encontrou && objeto != null)
        {
            Collider[] colliders = objeto.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;
                if (!encontrou) { bounds = colliders[i].bounds; encontrou = true; }
                else bounds.Encapsulate(colliders[i].bounds);
            }
        }
        if (!encontrou && objeto != null) bounds = new Bounds(objeto.transform.position, new Vector3(200f, 2f, 200f));
        return bounds;
    }

    private static Material ObterMaterial(GameObject primeiro, GameObject segundo)
    {
        Renderer[] renderers = primeiro != null ? primeiro.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null && renderers[i].sharedMaterial != null) return renderers[i].sharedMaterial;
        renderers = segundo != null ? segundo.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null && renderers[i].sharedMaterial != null) return renderers[i].sharedMaterial;
        return null;
    }

    private static GameObject EncontrarObjeto(string nome)
    {
        GameObject direto = GameObject.Find(nome);
        if (direto != null) return direto;
        Transform[] raizes = SceneManager.GetActiveScene().GetRootGameObjects().Length > 0
            ? ObterRaizes(SceneManager.GetActiveScene())
            : new Transform[0];
        for (int i = 0; i < raizes.Length; i++)
        {
            Transform encontrado = EncontrarFilhoRecursivo(raizes[i], nome);
            if (encontrado != null) return encontrado.gameObject;
        }
        return null;
    }

    private static Transform EncontrarTransform(string nome)
    {
        GameObject objeto = EncontrarObjeto(nome);
        return objeto != null ? objeto.transform : null;
    }

    private static Transform[] ObterRaizes(Scene cena)
    {
        GameObject[] objetos = cena.GetRootGameObjects();
        Transform[] raizes = new Transform[objetos.Length];
        for (int i = 0; i < objetos.Length; i++) raizes[i] = objetos[i].transform;
        return raizes;
    }

    private static Transform EncontrarFilho(Transform pai, string nome)
    {
        if (pai == null) return null;
        if (pai.name == nome) return pai;
        return EncontrarFilhoRecursivo(pai, nome);
    }

    private static Transform EncontrarFilhoRecursivo(Transform atual, string nome)
    {
        for (int i = 0; i < atual.childCount; i++)
        {
            Transform filho = atual.GetChild(i);
            if (filho.name == nome) return filho;
            Transform resultado = EncontrarFilhoRecursivo(filho, nome);
            if (resultado != null) return resultado;
        }
        return null;
    }
}
