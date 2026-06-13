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
        modoDroneCamera = false;
        currentRotationX = 15f;
        currentRotationY = 0f;
        currentLookedTarget = null;
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
    public float currentRotationX = 15f;
    public bool modoDroneCamera = false;
    private GameObject currentLookedTarget = null;

    public GameObject GetLookedTarget() => currentLookedTarget;

    public void AddZoom(float delta)
    {
        zoomFactor = Mathf.Clamp(zoomFactor - delta, 0.2f, 6f);
    }

    public void AddRotation(float deltaY)
    {
        currentRotationY += deltaY;
    }

    public void AddRotationVertical(float deltaX)
    {
        currentRotationX = Mathf.Clamp(currentRotationX + deltaX, -75f, 75f);
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

        // Se a câmera do drone estiver ativa e a unidade for um drone (tem KamikazeDrone)
        KamikazeDrone drone = targetUnit.GetComponent<KamikazeDrone>();
        if (modoDroneCamera && drone != null)
        {
            float scaleFactor = Mathf.Max(targetUnit.transform.localScale.x, targetUnit.transform.localScale.z);
            // Posição no nariz/frente do drone
            Vector3 localCameraPosition = new Vector3(0f, -0.2f * scaleFactor, 0.6f * scaleFactor);
            Vector3 worldCameraPos = targetUnit.transform.TransformPoint(localCameraPosition);
            
            transform.position = Vector3.Lerp(transform.position, worldCameraPos, Time.deltaTime * suavidadeSeguir);
            
            // Rotação: gimbal (drone rot + local gimbal yaw/pitch)
            Quaternion droneRot = targetUnit.transform.rotation;
            Quaternion gimbalRot = Quaternion.Euler(currentRotationX, currentRotationY, 0f);
            Quaternion desiredRotation = droneRot * gimbalRot;
            
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * suavidadeRotacao);
            
            // Zoom FOV
            float desiredFOV = Mathf.Clamp(30f / zoomFactor, 1f, 45f);
            if (minhaCamera != null)
            {
                minhaCamera.fieldOfView = Mathf.Lerp(minhaCamera.fieldOfView, desiredFOV, Time.deltaTime * 10f);
            }
            
            ProcessarMarcacaoAlvos();
            return;
        }
        else
        {
            if (minhaCamera != null)
            {
                minhaCamera.fieldOfView = Mathf.Lerp(minhaCamera.fieldOfView, 30f, Time.deltaTime * 5f);
            }
            currentLookedTarget = null;
        }

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

    private void ProcessarMarcacaoAlvos()
    {
        if (minhaCamera == null) return;
        
        Ray ray = minhaCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;
        int layerMask = ~LayerMask.GetMask("UI");
        
        if (Physics.Raycast(ray, out hit, 5000f, layerMask))
        {
            IdentidadeUnidade id = hit.collider.GetComponentInParent<IdentidadeUnidade>();
            if (id != null && id.gameObject != targetUnit.gameObject)
            {
                currentLookedTarget = id.gameObject;
                
                // Se pressionar Space ou clicar botão esquerdo
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                {
                    MarcarComoAlvo(id);
                }
            }
            else
            {
                currentLookedTarget = null;
                
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                {
                    MarcarCoordenada(hit.point);
                }
            }
        }
        else
        {
            currentLookedTarget = null;
        }
    }

    private void MarcarComoAlvo(IdentidadeUnidade id)
    {
        if (targetUnit == null) return;
        
        ControleAviao aviao = targetUnit.GetComponent<ControleAviao>();
        KamikazeDrone drone = targetUnit.GetComponent<KamikazeDrone>();
        
        if (aviao != null)
        {
            aviao.alvoEstrategico = id.transform.position;
            aviao.alvoGPSVoo = id.transform.position;
            
            if (drone != null)
            {
                drone.alvoAtual = id.transform;
                drone.kamikazeAtivo = true;
                aviao.velocidadeMaximaVoo = drone.velocidadeAtaque;
            }
            
            if (MenuComandoController.Instancia != null)
            {
                MenuComandoController.Instancia.AdicionarLog("DRONE", $"ALVO LOCK: {id.name.ToUpper()} EM {id.transform.position}", "sistema");
            }
            
            try
            {
                AudioClip clip = Resources.Load<AudioClip>("Sons/alvo_fixado") ?? Resources.Load<AudioClip>("mp3/click");
                if (clip != null)
                {
                    AudioSource.PlayClipAtPoint(clip, id.transform.position, 0.8f);
                }
            }
            catch {}
        }
    }
    
    private void MarcarCoordenada(Vector3 ponto)
    {
        if (targetUnit == null) return;
        
        ControleAviao aviao = targetUnit.GetComponent<ControleAviao>();
        if (aviao != null)
        {
            aviao.alvoEstrategico = ponto;
            aviao.alvoGPSVoo = ponto;
            
            if (MenuComandoController.Instancia != null)
            {
                MenuComandoController.Instancia.AdicionarLog("DRONE", $"COORDENADAS ENVIADAS: {ponto:F1}", "sistema");
            }
            
            try
            {
                AudioClip clip = Resources.Load<AudioClip>("mp3/click");
                if (clip != null)
                {
                    AudioSource.PlayClipAtPoint(clip, targetUnit.transform.position, 0.5f);
                }
            }
            catch {}
        }
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
