using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Equipe visual e sinal de liberação exclusivos do USS Enterprise.
/// A decisão de decolar continua pertencendo ao GerenciadorOperacoesPortaAvioesV2.
/// </summary>
public sealed class EquipeConvesEnterprise : MonoBehaviour
{
    [Header("Equipe do convés do Enterprise")]
    [SerializeField] private GameObject prefabTrabalhador;
    [SerializeField] private AnimationClip animacaoSinalLiberacao;
    [SerializeField, Min(0)] private int trabalhadoresPorCatapulta = 6;
    [SerializeField, Min(0.1f)] private float velocidadePatrulha = 1.2f;
    [Min(0f)] public float intervaloSaidaEstacionamento = 3f;
    [Min(0f)] public float intervaloEntreLiberacoes = 2f;
    [Min(0f)] public float duracaoFallbackSinal = 0.8f;

    private readonly Dictionary<string, Animator> sinalizadores = new Dictionary<string, Animator>();
    private readonly Dictionary<string, AnimatorOverrideController> controlesSinal = new Dictionary<string, AnimatorOverrideController>();
    private bool equipeCriada;

    public static float CalcularProximoInicio(float agora, float proximo, float intervalo)
    {
        return Mathf.Max(agora, proximo) + Mathf.Max(0f, intervalo);
    }

    private void Awake()
    {
        LayoutConvesPortaAvioesV2 layout = GetComponent<LayoutConvesPortaAvioesV2>();
        if (layout == null) layout = GetComponentInChildren<LayoutConvesPortaAvioesV2>(true);
        if (layout != null) GarantirRotaSegundaCatapulta(layout);
    }

    private void Start()
    {
        if (Application.isPlaying) CriarEquipe();
    }

    /// <summary>
    /// O prefab já possui marcos calibrados de alinhamento/liberação da pista 2.
    /// Completa apenas os marcos de fila, subida e saída que faltam, sem mover os existentes.
    /// </summary>
    public void GarantirRotaSegundaCatapulta(LayoutConvesPortaAvioesV2 layout)
    {
        if (layout == null || layout.decolagem == null) return;
        Transform alinhamento2 = layout.decolagem.Find("Alinhamento (1)");
        Transform liberacao2 = layout.decolagem.Find("Liberacao (1)");
        Transform alinhamento1 = layout.decolagem.Find("Alinhamento");
        Transform liberacao1 = layout.decolagem.Find("Liberacao");
        Transform fila1 = layout.decolagem.Find("Fila");
        Transform subida1 = layout.decolagem.Find("Subida_Inicial");
        Transform saida1 = layout.decolagem.Find("Saida_Voo");
        if (alinhamento2 == null || liberacao2 == null || alinhamento1 == null || liberacao1 == null) return;

        if (layout.decolagem.Find("Fila (1)") == null && fila1 != null)
            CriarMarco(layout.decolagem, "Fila (1)", alinhamento2.position + (fila1.position - alinhamento1.position), fila1.rotation);
        if (subida1 != null && layout.decolagem.Find("Subida_Inicial (1)") == null)
            CriarMarco(layout.decolagem, "Subida_Inicial (1)", liberacao2.position + (subida1.position - liberacao1.position), subida1.rotation);
        if (saida1 != null && layout.decolagem.Find("Saida_Voo (1)") == null)
            CriarMarco(layout.decolagem, "Saida_Voo (1)", liberacao2.position + (saida1.position - liberacao1.position), saida1.rotation);
    }

    /// <summary>Chamado pelo instalador de prefab do Editor; os outros navios não usam este componente.</summary>
    public void Configurar(GameObject trabalhador, AnimationClip sinal)
    {
        prefabTrabalhador = trabalhador;
        animacaoSinalLiberacao = sinal;
    }

    public IEnumerator SinalizarLiberacao(Transform catapulta)
    {
        string id = catapulta != null && catapulta.name == "Alinhamento (1)" ? "Catapulta_02" : "Catapulta_01";
        if (!sinalizadores.TryGetValue(id, out Animator animator) || animator == null || animacaoSinalLiberacao == null)
        {
            yield return new WaitForSeconds(duracaoFallbackSinal);
            yield break;
        }

        if (!controlesSinal.TryGetValue(id, out AnimatorOverrideController substituto))
        {
            RuntimeAnimatorController baseController = animator.runtimeAnimatorController;
            if (baseController != null)
            {
                substituto = new AnimatorOverrideController(baseController);
                var clipes = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                substituto.GetOverrides(clipes);
                if (clipes.Count > 0)
                {
                    for (int i = 0; i < clipes.Count; i++)
                        substituto[clipes[i].Key] = animacaoSinalLiberacao;
                    controlesSinal[id] = substituto;
                }
            }
        }

        if (!controlesSinal.TryGetValue(id, out substituto))
        {
            yield return new WaitForSeconds(duracaoFallbackSinal);
            yield break;
        }

        animator.runtimeAnimatorController = substituto;
        animator.enabled = true;
        animator.SetFloat("Velocidade", 1f);
        animator.Play("Base Layer.andando", 0, 0f);
        yield return new WaitForSeconds(Mathf.Max(0.1f, animacaoSinalLiberacao.length));
        animator.SetFloat("Velocidade", 0f);
    }

    private void CriarEquipe()
    {
        if (equipeCriada || prefabTrabalhador == null) return;
        LayoutConvesPortaAvioesV2 layout = GetComponent<LayoutConvesPortaAvioesV2>();
        if (layout == null) layout = GetComponentInChildren<LayoutConvesPortaAvioesV2>(true);
        if (layout == null || layout.decolagem == null) return;

        GarantirRotaSegundaCatapulta(layout);
        Transform equipe1 = layout.pontosServico != null ? layout.pontosServico.Find("Trablhadores") : null;
        Transform equipe2 = layout.pontosServico != null ? layout.pontosServico.Find("Trablhadores (1)") : null;
        if (equipe1 == null || equipe2 == null)
        {
            Debug.LogWarning("EquipeConvesEnterprise: faltam os grupos Trablhadores em PontosServico.", this);
            return;
        }

        Transform sinal1 = equipe1.childCount > 0 ? equipe1.GetChild(0) : null;
        Transform patrulha1 = equipe1.childCount > 1 ? equipe1.GetChild(1) : null;
        Transform sinal2 = equipe2.childCount > 0 ? equipe2.GetChild(0) : null;
        Transform patrulha2 = equipe2.childCount > 1 ? equipe2.GetChild(1) : null;

        NivelarPontosAoConves(equipe1, layout);
        NivelarPontosAoConves(equipe2, layout);

        if (sinal1 != null) CriarSinalizador("Catapulta_01", sinal1);
        if (sinal2 != null) CriarSinalizador("Catapulta_02", sinal2);
        CriarEquipeDeCatapulta("Equipe_01", patrulha1, layout);
        CriarEquipeDeCatapulta("Equipe_02", patrulha2, layout);

        equipeCriada = true;
    }

    private static void NivelarPontosAoConves(Transform grupo, LayoutConvesPortaAvioesV2 layout)
    {
        if (grupo == null || layout == null || layout.referenciaConves == null) return;
        for (int i = 0; i < grupo.childCount; i++)
        {
            Transform ponto = grupo.GetChild(i);
            Vector3 posicao = ponto.position;
            posicao.y = layout.referenciaConves.position.y;
            ponto.position = posicao;
        }
    }

    private void CriarEquipeDeCatapulta(string prefixo, Transform pontoPatrulha, LayoutConvesPortaAvioesV2 layout)
    {
        if (pontoPatrulha == null) return;

        int quantidade = Mathf.Max(0, trabalhadoresPorCatapulta);
        for (int i = 0; i < quantidade; i++)
        {
            string id = $"{prefixo}_Aviador_{i + 2:00}";
            if (pontoPatrulha.Find(id) != null) continue;
            CriarTripulantePatrulha(id, pontoPatrulha, layout);
        }
    }

    private void CriarSinalizador(string id, Transform posto)
    {
        string nomeTrabalhador = id + "_Aviador_01";
        if (posto.Find(nomeTrabalhador) != null)
        {
            Animator existente = posto.Find(nomeTrabalhador).GetComponentInChildren<Animator>(true);
            if (existente != null) sinalizadores[id] = existente;
            return;
        }

        GameObject trabalhador = InstanciarTrabalhador(nomeTrabalhador, PosicaoNoPlanoDoConves(posto), posto.rotation, posto);
        if (trabalhador == null) return;
        Animator animator = trabalhador.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.SetFloat("Velocidade", 0f);
            sinalizadores[id] = animator;
        }
    }

    private void CriarTripulantePatrulha(string id, Transform posto, LayoutConvesPortaAvioesV2 layout)
    {
        if (posto == null || posto.Find(id) != null) return;
        GameObject trabalhador = InstanciarTrabalhador(id, PosicaoNoPlanoDoConves(posto), posto.rotation, posto);
        if (trabalhador == null) return;

        Animator animator = trabalhador.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.enabled = true;
            animator.applyRootMotion = false;
            // O clip do prefab de convés usa animação escalada pelo parâmetro Velocidade.
            // Use o timestep de renderização, já que a posição é atualizada pelo mesmo loop.
            animator.updateMode = AnimatorUpdateMode.Normal;
            string parametroVelocidade = "Velocidade";
            bool possuiParametro = false;
            foreach (AnimatorControllerParameter parametro in animator.parameters)
            {
                if (parametro.type == AnimatorControllerParameterType.Float
                    && (parametro.name == "Velocidade" || parametro.name == "velocidade"))
                {
                    parametroVelocidade = parametro.name;
                    possuiParametro = true;
                    break;
                }
            }
            if (possuiParametro)
            {
                animator.SetFloat(parametroVelocidade, 1f);
                StartCoroutine(PatrulharConvés(trabalhador.transform, animator, parametroVelocidade, posto.position, posto.forward, layout.transform));
            }
        }
    }

    private IEnumerator PatrulharConvés(Transform trabalhador, Animator animator, string parametroVelocidade, Vector3 origem, Vector3 frente, Transform convés)
    {
        Vector3 origemLocal = convés.InverseTransformPoint(origem);
        Vector3 frenteLocal = convés.InverseTransformDirection(Vector3.ProjectOnPlane(frente, Vector3.up)).normalized;
        if (frenteLocal.sqrMagnitude < 0.01f) frenteLocal = Vector3.forward;
        int destinoAtual = 0;
        while (trabalhador != null && animator != null && convés != null)
        {
            Vector3 destinoLocal = origemLocal + frenteLocal * (destinoAtual == 0 ? 3f : -3f);
            Vector3 destino = convés.TransformPoint(destinoLocal);
            Vector3 direcao = destino - trabalhador.position;
            direcao.y = 0f;
            if (direcao.sqrMagnitude <= 0.16f)
            {
                destinoAtual = 1 - destinoAtual;
                yield return new WaitForSeconds(0.4f);
                continue;
            }
            trabalhador.rotation = Quaternion.RotateTowards(trabalhador.rotation, Quaternion.LookRotation(direcao), 180f * Time.deltaTime);
            trabalhador.position = Vector3.MoveTowards(trabalhador.position, destino, velocidadePatrulha * Time.deltaTime);
            animator.SetFloat(parametroVelocidade, 1f);
            yield return null;
        }
    }

    private GameObject InstanciarTrabalhador(string id, Vector3 posicao, Quaternion rotacao, Transform equipe)
    {
        GameObject trabalhador = Instantiate(prefabTrabalhador, posicao, rotacao, equipe);
        trabalhador.name = id;
        NavMeshAgent agente = trabalhador.GetComponentInChildren<NavMeshAgent>(true);
        if (agente != null) agente.enabled = false;
        ControleUnidade[] controles = trabalhador.GetComponentsInChildren<ControleUnidade>(true);
        for (int i = 0; i < controles.Length; i++)
        {
            if (controles[i] != null) controles[i].enabled = false;
        }
        IdentidadeUnidade[] identidades = trabalhador.GetComponentsInChildren<IdentidadeUnidade>(true);
        for (int i = 0; i < identidades.Length; i++)
        {
            // Tripulantes são NPCs visuais de convés; não entram no censo de
            // unidades táticas nem podem ser selecionados pelo Satélite.
            if (identidades[i] != null) identidades[i].enabled = false;
        }
        return trabalhador;
    }

    private Vector3 PosicaoNoPlanoDoConves(Transform posto)
    {
        Vector3 posicao = posto.position;
        LayoutConvesPortaAvioesV2 layout = GetComponent<LayoutConvesPortaAvioesV2>()
            ?? GetComponentInChildren<LayoutConvesPortaAvioesV2>(true);
        if (layout != null && layout.referenciaConves != null)
        {
            // Os marcos Trablhadores do Enterprise ficam abaixo da pista.
            // A referência calibrada alinha os pés ao plano real do convés,
            // mantendo X/Z e acompanhando o navio em movimento.
            posicao.y = layout.referenciaConves.position.y;
        }
        // Alguns pontos legados foram salvos muito abaixo do casco. Só reutiliza X/Z;
        // toda a equipe visual precisa nascer no plano de referência do convés.
        return posicao;
    }

    private static Transform CriarMarco(Transform pai, string nome, Vector3 posicao, Quaternion rotacao)
    {
        var objeto = new GameObject(nome);
        Transform marco = objeto.transform;
        marco.SetParent(pai, true);
        marco.SetPositionAndRotation(posicao, rotacao);
        return marco;
    }
}
