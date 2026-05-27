using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraUnidadeHUD : MonoBehaviour
{
    public static CameraUnidadeHUD Instancia { get; private set; }

    private Camera minhaCamera;
    private ControleUnidade targetUnit;
    private RenderTexture currentRT;

    [Header("Configurações de Foco")]
    [SerializeField] private Vector3 offsetBase = new Vector3(0f, 15f, -25f);
    [SerializeField] private float suavidadeSeguir = 8f;
    [SerializeField] private float suavidadeRotacao = 5f;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;

        minhaCamera = GetComponent<Camera>();
        
        // Garante que comece desativada para poupar performance
        DesativarDoMenu();
    }

    private void OnDestroy()
    {
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    public void AtivarNoMenu(RenderTexture targetRT)
    {
        currentRT = targetRT;
        if (minhaCamera != null && currentRT != null)
        {
            minhaCamera.targetTexture = currentRT;
            minhaCamera.enabled = true;
        }
        AtualizarFoco();
    }

    public void DesativarDoMenu()
    {
        targetUnit = null;
        if (minhaCamera != null)
        {
            minhaCamera.targetTexture = null;
            minhaCamera.enabled = false;
        }
    }

    public void DefinirTarget(ControleUnidade unidade)
    {
        targetUnit = unidade;
        if (targetUnit != null && minhaCamera != null && minhaCamera.enabled)
        {
            // Reposiciona instantaneamente na primeira seleção para não ter transição visual lenta/estranha
            float factor = Mathf.Max(targetUnit.transform.localScale.x, targetUnit.transform.localScale.z);
            Vector3 localOffset = offsetBase;
            if (factor > 5f)
            {
                localOffset = new Vector3(0f, factor * 2.5f, -factor * 4f);
            }
            else if (factor < 0.8f)
            {
                localOffset = new Vector3(0f, 1.8f, -2.8f);
            }
            else
            {
                localOffset = new Vector3(0f, 6f, -12f);
            }

            Vector3 targetPos = targetUnit.transform.position;
            Vector3 desiredPosition = targetUnit.transform.TransformPoint(localOffset);
            transform.position = desiredPosition;
            transform.LookAt(targetPos + Vector3.up * (factor * 0.4f + 0.5f));
        }
    }

    private void LateUpdate()
    {
        if (targetUnit == null)
        {
            // Tenta obter do GerenteSelecao caso não tenha sido definido manualmente
            var gerente = FindFirstObjectByType<GerenteSelecao>();
            if (gerente != null && gerente.unidadesSelecionadas != null && gerente.unidadesSelecionadas.Count > 0)
            {
                targetUnit = gerente.unidadesSelecionadas[0];
            }
        }

        if (targetUnit == null) return;

        // Ajuste de offset dinâmico baseado na escala da unidade (para navios/soldados)
        float factor = Mathf.Max(targetUnit.transform.localScale.x, targetUnit.transform.localScale.z);
        Vector3 localOffset = offsetBase;
        
        if (factor > 5f)
        {
            // Se for unidade grande (navio, avião grande)
            localOffset = new Vector3(0f, factor * 2.5f, -factor * 4f);
        }
        else if (factor < 0.8f)
        {
            // Se for unidade pequena (soldado)
            localOffset = new Vector3(0f, 1.8f, -2.8f);
        }
        else
        {
            // Se for unidade média (tanque, veículo)
            localOffset = new Vector3(0f, 6f, -12f);
        }

        Vector3 targetPos = targetUnit.transform.position;
        Vector3 desiredPosition = targetUnit.transform.TransformPoint(localOffset);

        // Suaviza a movimentação e rotação da câmera
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * suavidadeSeguir);
        
        Vector3 lookTarget = targetPos + Vector3.up * (factor * 0.4f + 0.5f);
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * suavidadeRotacao);
    }

    public void AtualizarFoco()
    {
        var gerente = FindFirstObjectByType<GerenteSelecao>();
        if (gerente != null && gerente.unidadesSelecionadas != null && gerente.unidadesSelecionadas.Count > 0)
        {
            DefinirTarget(gerente.unidadesSelecionadas[0]);
        }
        else
        {
            DefinirTarget(null);
        }
    }
}
