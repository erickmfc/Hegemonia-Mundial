using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraUnidadeHUD : MonoBehaviour
{
    private static CameraUnidadeHUD _instancia;
    private static bool _isQuitting = false;

    public static bool Instanciada => _instancia != null;

    public static CameraUnidadeHUD Instancia
    {
        get
        {
            if (_isQuitting) return null;
            if (_instancia == null)
            {
                _instancia = FindFirstObjectByType<CameraUnidadeHUD>(FindObjectsInactive.Include);
                if (_instancia == null)
                {
                    if (!Application.isPlaying) return null; // Não cria fora de PlayMode
                    GameObject go = new GameObject("CameraUnidadeHUD_Dynamic");
                    _instancia = go.AddComponent<CameraUnidadeHUD>();
                    
                    Camera cam = go.GetComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.02f, 0.05f, 0.02f, 1f); // Tom FLIR verde-escuro
                    cam.fieldOfView = 30f;
                    cam.farClipPlane = 6000f;
                    
                    int uiLayer = LayerMask.NameToLayer("UI");
                    if (uiLayer >= 0)
                    {
                        cam.cullingMask &= ~(1 << uiLayer);
                    }
                    
                    DontDestroyOnLoad(go);
                }
                else
                {
                    _instancia.gameObject.SetActive(true);
                }
            }
            return _instancia;
        }
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    private Camera minhaCamera;
    private ControleUnidade targetUnit;
    private RenderTexture currentRT;

    [Header("Configurações de Foco")]
    [SerializeField] private Vector3 offsetBase = new Vector3(0f, 15f, -25f);
    [SerializeField] private float suavidadeSeguir = 8f;
    [SerializeField] private float suavidadeRotacao = 5f;

    private void Awake()
    {
        if (_instancia != null && _instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        _instancia = this;

        minhaCamera = GetComponent<Camera>();
        
        // Garante que comece desativada para poupar performance
        DesativarDoMenu();
    }

    private void OnDestroy()
    {
        if (_instancia == this)
        {
            _instancia = null;
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

    public float zoomFactor = 1f;
    public float currentRotationY = 0f;

    public void AddZoom(float delta)
    {
        zoomFactor = Mathf.Clamp(zoomFactor - delta, 0.2f, 6f);
    }

    public void AddRotation(float deltaY)
    {
        currentRotationY += deltaY;
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

        // Aplica o zoom configurável
        localOffset *= zoomFactor;

        Vector3 targetPos = targetUnit.transform.position;
        
        // Aplica a rotação em volta da unidade
        Quaternion addRot = Quaternion.Euler(0f, currentRotationY, 0f);
        Vector3 rotatedOffset = addRot * targetUnit.transform.TransformDirection(localOffset);
        Vector3 desiredPosition = targetPos + rotatedOffset;

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
