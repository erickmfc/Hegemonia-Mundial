using UnityEngine;

[RequireComponent(typeof(CombustivelUnidade))]
public class CaminhaoTanqueAbastecimento : MonoBehaviour
{
    [Header("Carga")]
    public float capacidadeCarga = 600f;
    public float cargaAtual = 0f;

    [Header("Abastecimento")]
    public float raioAbastecimento = 18f;
    public float taxaTransferencia = 30f;
    public float intervaloBusca = 0.25f;
    public LayerMask camadasUnidades = ~0;
    public bool abastecerAutomaticamente = true;
    public KeyCode teclaRecarregarCarga = KeyCode.R;

    [Header("Indicador")]
    public bool mostrarIndicadorSelecionado = true;
    public Vector3 offsetIndicador = new Vector3(0f, 4.1f, 0f);

    private readonly Collider[] alvos = new Collider[64];
    private ControleUnidade controle;
    private IdentidadeUnidade identidade;
    private CombustivelUnidade combustivelProprio;
    private Camera cameraCache;
    private float proximaBusca;
    private float proximaAtualizacaoTexto;
    private float recargaManualAcumulada;
    private string textoCache = "";

    public float CargaAtual => Mathf.Max(0f, cargaAtual);
    public float CapacidadeCarga => Mathf.Max(0f, capacidadeCarga);
    public float EspacoCarga => Mathf.Max(0f, CapacidadeCarga - CargaAtual);
    public float PercentualCarga => CapacidadeCarga > 0f ? Mathf.Clamp01(CargaAtual / CapacidadeCarga) : 0f;

    private void Awake()
    {
        controle = GetComponent<ControleUnidade>();
        identidade = GetComponent<IdentidadeUnidade>();
        combustivelProprio = GetComponent<CombustivelUnidade>();
        if (combustivelProprio != null)
        {
            combustivelProprio.classe = ClasseCombustivelUnidade.Terrestre;
            combustivelProprio.ConfigurarSeNecessario(true);
        }

        cargaAtual = Mathf.Clamp(cargaAtual, 0f, capacidadeCarga);
    }

    private void Update()
    {
        if (abastecerAutomaticamente && Time.time >= proximaBusca)
        {
            float dtBusca = Mathf.Max(0.05f, intervaloBusca);
            proximaBusca = Time.time + dtBusca;
            TransferirParaAliadosProximos(taxaTransferencia * dtBusca);
        }

        if (EstaSelecionado() && Input.GetKey(teclaRecarregarCarga))
        {
            recargaManualAcumulada += taxaTransferencia * Time.deltaTime;
            if (recargaManualAcumulada >= 1f || EspacoCarga <= recargaManualAcumulada)
            {
                ServicoAbastecimento.TentarCarregarCaminhao(this, recargaManualAcumulada, out float carregado);
                recargaManualAcumulada = Mathf.Max(0f, recargaManualAcumulada - carregado);
            }
        }
        else
        {
            recargaManualAcumulada = 0f;
        }
    }

    private void OnGUI()
    {
        if (combustivelProprio != null && combustivelProprio.mostrarIndicadorMundo)
        {
            return;
        }

        if (!mostrarIndicadorSelecionado || !EstaSelecionado())
        {
            return;
        }

        if (cameraCache == null)
        {
            cameraCache = Camera.main;
        }

        if (cameraCache == null)
        {
            return;
        }

        Vector3 tela = cameraCache.WorldToScreenPoint(transform.position + offsetIndicador);
        if (tela.z < 0f)
        {
            return;
        }

        if (Time.unscaledTime >= proximaAtualizacaoTexto)
        {
            proximaAtualizacaoTexto = Time.unscaledTime + 0.25f;
            textoCache = $"Carga {Mathf.RoundToInt(PercentualCarga * 100f)}%";
        }

        Rect fundo = new Rect(tela.x - 52f, Screen.height - tela.y, 104f, 18f);
        Rect barra = new Rect(fundo.x + 4f, fundo.y + 13f, (fundo.width - 8f) * PercentualCarga, 3f);

        Color corAntiga = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(fundo, Texture2D.whiteTexture);
        GUI.color = new Color(0.25f, 0.8f, 1f, 0.95f);
        GUI.DrawTexture(barra, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(fundo.x + 5f, fundo.y + 1f, fundo.width - 10f, 14f), textoCache);
        GUI.color = corAntiga;
    }

    public float CarregarSemCusto(float quantidade)
    {
        if (quantidade <= 0f || CapacidadeCarga <= 0f)
        {
            return 0f;
        }

        float antes = cargaAtual;
        cargaAtual = Mathf.Clamp(cargaAtual + quantidade, 0f, capacidadeCarga);
        return cargaAtual - antes;
    }

    public float RemoverCarga(float quantidade)
    {
        if (quantidade <= 0f)
        {
            return 0f;
        }

        float retirado = Mathf.Min(quantidade, cargaAtual);
        cargaAtual -= retirado;
        return retirado;
    }

    private void TransferirParaAliadosProximos(float quantidadeDisponivelNoCiclo)
    {
        if (quantidadeDisponivelNoCiclo <= 0f || cargaAtual <= 0.01f)
        {
            return;
        }

        int total = Physics.OverlapSphereNonAlloc(transform.position, raioAbastecimento, alvos, camadasUnidades, QueryTriggerInteraction.Ignore);
        if (total <= 0)
        {
            return;
        }

        float restanteNoCiclo = Mathf.Min(quantidadeDisponivelNoCiclo, cargaAtual);
        int meuTime = identidade != null ? identidade.teamID : -1;

        for (int i = 0; i < total && restanteNoCiclo > 0.01f && cargaAtual > 0.01f; i++)
        {
            Collider col = alvos[i];
            if (col == null)
            {
                continue;
            }

            CombustivelUnidade alvo = col.GetComponentInParent<CombustivelUnidade>();
            if (alvo == null || alvo == combustivelProprio || !alvo.usaCombustivel || alvo.Capacidade <= 0f)
            {
                continue;
            }

            IdentidadeUnidade idAlvo = alvo.GetComponent<IdentidadeUnidade>();
            if (meuTime >= 0 && idAlvo != null && idAlvo.teamID != meuTime)
            {
                continue;
            }

            if (alvo.classe != ClasseCombustivelUnidade.Terrestre)
            {
                continue;
            }

            float espaco = alvo.Capacidade - alvo.CombustivelAtual;
            if (espaco <= 0.01f)
            {
                continue;
            }

            float transferencia = Mathf.Min(restanteNoCiclo, espaco, cargaAtual);
            float aplicado = alvo.Abastecer(transferencia);
            if (aplicado <= 0.01f)
            {
                continue;
            }

            RemoverCarga(aplicado);
            restanteNoCiclo -= aplicado;
        }
    }

    private bool EstaSelecionado()
    {
        if (controle == null)
        {
            controle = GetComponent<ControleUnidade>();
        }

        return controle != null && controle.selecionado;
    }
}
