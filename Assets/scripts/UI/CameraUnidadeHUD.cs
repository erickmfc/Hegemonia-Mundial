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
    private GerenteSelecao gerenteSelecaoCache;
    private int ultimaUnidadeSelecionadaId;
    private RenderTexture currentRT;
    private float proximoRefreshGerenteSelecao;
    private float proximoProcessamentoMarcacao;
    private readonly RaycastHit[] hitsMarcacao = new RaycastHit[32];
    private int layerMaskMarcacao;

    [Header("Configurações de Foco")]
    [SerializeField] private Vector3 offsetBase = new Vector3(0f, 15f, -25f);
    [SerializeField] private float suavidadeSeguir = 8f;
    [SerializeField] private float suavidadeRotacao = 5f;
    [SerializeField] private float tempoEstabilizacaoCamera = 0.12f;
    private Vector3 velocidadePosicaoCamera;

    private void Awake()
    {
        if (_instancia != null && _instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        _instancia = this;

        minhaCamera = GetComponent<Camera>();
        if (minhaCamera != null)
        {
            minhaCamera.farClipPlane = 8000f;
        }

        layerMaskMarcacao = ~LayerMask.GetMask("UI");

        // Garante que comece desativada para poupar performance
        DesativarDoMenu();
    }

    // A câmera de unidade não altera RenderSettings globalmente. Isso evita
    // disputa de neblina entre a câmera principal, minimapa e câmera HUD.
    // A câmera HUD não altera RenderSettings globalmente.

    private void OnEnable() { }

    private void OnDisable() { }

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
        if (minhaCamera != null)
        {
            minhaCamera.targetTexture = null;
            minhaCamera.enabled = false;
        }
    }

    public void DefinirTarget(ControleUnidade unidade)
    {
        DefinirTarget(unidade, false);
    }

    public void DefinirTarget(ControleUnidade unidade, bool manterModoDrone)
    {
        bool mesmaUnidade = targetUnit == unidade && unidade != null;
        targetUnit = unidade;
        currentLookedTarget = null;
        if (!mesmaUnidade)
        {
            velocidadePosicaoCamera = Vector3.zero;
            if (!manterModoDrone)
            {
                modoDroneCamera = false;
            }
            currentRotationX = 15f;
            currentRotationY = 0f;
            alvoTravadoCamera = null;
            pontoTravadoCamera = null;
        }
        if (minhaCamera != null)
        {
            minhaCamera.farClipPlane = 8000f;
        }
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
    public Transform alvoTravadoCamera = null;
    public Vector3? pontoTravadoCamera = null;
    private GameObject currentLookedTarget = null;

    public GameObject GetLookedTarget() => currentLookedTarget;

    private static string ObterNomeExibicao(GameObject obj)
    {
        if (obj == null) return "DESCONHECIDO";

        IdentidadeUnidade id = obj.GetComponent<IdentidadeUnidade>();
        if (id != null && !string.IsNullOrEmpty(id.nomeDeBatismo))
        {
            return id.nomeDeBatismo.ToUpperInvariant();
        }

        return SaveableEntity.NormalizarPrefabKey(obj.name).ToUpperInvariant();
    }

    public void AddZoom(float delta)
    {
        zoomFactor = Mathf.Clamp(zoomFactor - delta, 0.06f, 18f);
    }

    public void AddRotation(float deltaY)
    {
        currentRotationY += deltaY;
        alvoTravadoCamera = null;
        pontoTravadoCamera = null;
    }

    public void AddRotationVertical(float deltaX)
    {
        if (modoDroneCamera)
        {
            currentRotationX = Mathf.Clamp(currentRotationX + deltaX, -15f, 85f);
            alvoTravadoCamera = null;
            pontoTravadoCamera = null;
        }
        else
        {
            currentRotationX = Mathf.Clamp(currentRotationX + deltaX, -75f, 75f);
        }
    }

    private void LateUpdate()
    {
        GerenteSelecao gerenteAtual = ObterGerenteSelecaoCache();
        if (gerenteAtual != null && gerenteAtual.unidadesSelecionadas != null && gerenteAtual.unidadesSelecionadas.Count > 0)
        {
            ControleUnidade unidadeSelecionada = gerenteAtual.unidadesSelecionadas[0];
            if (unidadeSelecionada != null)
            {
                int idSelecionado = unidadeSelecionada.GetInstanceID();
                if (idSelecionado != ultimaUnidadeSelecionadaId || targetUnit != unidadeSelecionada)
                {
                    ultimaUnidadeSelecionadaId = idSelecionado;
                    DefinirTarget(unidadeSelecionada);
                }
            }
        }

        if (targetUnit == null) return;

        if (modoDroneCamera)
        {
            float scaleFactor = Mathf.Max(targetUnit.transform.localScale.x, targetUnit.transform.localScale.z);
            // Posição no nariz/frente do drone
            Vector3 localCameraPosition = new Vector3(0f, -0.2f * scaleFactor, 0.6f * scaleFactor);
            Vector3 worldCameraPos = targetUnit.transform.TransformPoint(localCameraPosition);
            
            float fatorPosicaoDrone = 1f - Mathf.Exp(-Mathf.Max(0.1f, suavidadeSeguir) * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, worldCameraPos, fatorPosicaoDrone);
            
            // Rotação: gimbal (drone rot + local gimbal yaw/pitch)
            // Não herda pitch/roll do avião ou helicóptero: o horizonte fica
            // estável mesmo enquanto o modelo faz a curva.
            Quaternion droneRot = Quaternion.Euler(0f, targetUnit.transform.eulerAngles.y, 0f);
            Quaternion gimbalRot = Quaternion.Euler(currentRotationX, currentRotationY, 0f);
            Quaternion desiredRotation = droneRot * gimbalRot;
            
            // Estabiliza o roll (eixo Z) mantendo o horizonte nivelado
            Vector3 forward;
            if (alvoTravadoCamera != null && alvoTravadoCamera.gameObject.activeInHierarchy)
            {
                float targetHeight = 1f;
                var targetDmg = alvoTravadoCamera.GetComponent<SistemaDeDanos>();
                if (targetDmg != null && targetDmg.vidaAtual <= 0)
                {
                    alvoTravadoCamera = null;
                    forward = desiredRotation * Vector3.forward;
                }
                else
                {
                    Vector3 targetLookPos = alvoTravadoCamera.position + Vector3.up * targetHeight;
                    forward = (targetLookPos - transform.position).normalized;

                    Quaternion localRot = Quaternion.Inverse(droneRot) * Quaternion.LookRotation(forward, Vector3.up);
                    Vector3 euler = localRot.eulerAngles;
                    float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
                    float yaw = euler.y > 180f ? euler.y - 360f : euler.y;
                    currentRotationX = Mathf.Clamp(pitch, -15f, 85f);
                    currentRotationY = yaw;
                }
            }
            else if (pontoTravadoCamera.HasValue)
            {
                Vector3 targetLookPos = pontoTravadoCamera.Value;
                forward = (targetLookPos - transform.position).normalized;

                Quaternion localRot = Quaternion.Inverse(droneRot) * Quaternion.LookRotation(forward, Vector3.up);
                Vector3 euler = localRot.eulerAngles;
                float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
                float yaw = euler.y > 180f ? euler.y - 360f : euler.y;
                currentRotationX = Mathf.Clamp(pitch, -15f, 85f);
                currentRotationY = yaw;
            }
            else
            {
                forward = desiredRotation * Vector3.forward;
            }

            Quaternion stabilizedRotation;
            if (Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f)
            {
                stabilizedRotation = desiredRotation;
            }
            else
            {
                stabilizedRotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            float fatorRotacaoDrone = 1f - Mathf.Exp(-Mathf.Max(0.1f, suavidadeRotacao) * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, stabilizedRotation, fatorRotacaoDrone);

            // Zoom FOV poderoso para o drone Hasaf
            float desiredFOV = Mathf.Clamp(30f / zoomFactor, 0.1f, 45f);
            if (minhaCamera != null)
            {
                minhaCamera.fieldOfView = Mathf.Lerp(minhaCamera.fieldOfView, desiredFOV, Time.deltaTime * 10f);
            }
            
            if (Time.unscaledTime >= proximoProcessamentoMarcacao)
            {
                proximoProcessamentoMarcacao = Time.unscaledTime + (DiagnosticoDesempenhoJogo.RuntimeSaturado()
                    ? 0.12f
                    : DiagnosticoDesempenhoJogo.RuntimeSobPressao()
                        ? 0.08f
                        : 0.05f);
                ProcessarMarcacaoAlvos();
            }
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
        bool unidadeAerea = targetUnit.DominioAtual == DominioControleUnidade.Aereo
            || targetUnit.GetComponent<Helicoptero>() != null
            || targetUnit.GetComponent<ControleAviao>() != null
            || targetUnit.GetComponent<C700TransporteAereo>() != null;
        Quaternion baseRotacao = unidadeAerea
            ? Quaternion.Euler(0f, targetUnit.transform.eulerAngles.y, 0f)
            : targetUnit.transform.rotation;
        Vector3 rotatedOffset = addRot * (baseRotacao * localOffset);
        Vector3 desiredPosition = targetPos + rotatedOffset;

        // Suavização independente do FPS e sem acompanhar pitch/roll do
        // veículo como se fosse vibração da câmera.
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocidadePosicaoCamera,
            Mathf.Max(0.03f, tempoEstabilizacaoCamera),
            Mathf.Infinity,
            Time.deltaTime);
        
        Vector3 lookTarget = targetPos + Vector3.up * (factor * 0.4f + 0.5f);
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
        float fatorRotacaoNormal = 1f - Mathf.Exp(-Mathf.Max(0.1f, suavidadeRotacao) * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, fatorRotacaoNormal);
    }

    private void ProcessarMarcacaoAlvos()
    {
        if (minhaCamera == null || targetUnit == null) return;

        Ray ray = minhaCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int quantidadeHits = Physics.RaycastNonAlloc(ray, hitsMarcacao, 5000f, layerMaskMarcacao, QueryTriggerInteraction.Ignore);
        RaycastHit? primeiroPontoValido = null;
        float menorDistanciaPonto = float.MaxValue;
        IdentidadeUnidade alvoEncontrado = null;
        float menorDistanciaAlvo = float.MaxValue;
        for (int i = 0; i < quantidadeHits; i++)
        {
            RaycastHit hit = hitsMarcacao[i];
            if (hit.collider == null || hit.transform == null) continue;
            if (hit.transform == targetUnit.transform
                || hit.transform.IsChildOf(targetUnit.transform)
                || targetUnit.transform.IsChildOf(hit.transform)) continue;

            if (hit.distance < menorDistanciaPonto)
            {
                menorDistanciaPonto = hit.distance;
                primeiroPontoValido = hit;
            }

            IdentidadeUnidade id = hit.collider.GetComponentInParent<IdentidadeUnidade>();
            if (id == null) id = hit.collider.GetComponentInChildren<IdentidadeUnidade>();
            if (id == null) continue;

            if (hit.distance < menorDistanciaAlvo)
            {
                menorDistanciaAlvo = hit.distance;
                alvoEncontrado = id;
            }
        }

        currentLookedTarget = alvoEncontrado != null ? alvoEncontrado.gameObject : null;
        bool confirmar = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
        if (!confirmar) return;

        if (alvoEncontrado != null)
        {
            // Descobre o time do controlador atual
            IdentidadeUnidade meuId = targetUnit != null ? targetUnit.GetComponent<IdentidadeUnidade>() : null;
            int meuTime = meuId != null ? meuId.teamID : 1;

            // Se for aliado (mesmo time) ou neutro: APENAS trava a camera, sem atacar
            if (alvoEncontrado.teamID == meuTime || alvoEncontrado.teamID == 0)
            {
                TravadoEmAlvo(alvoEncontrado.transform);
                if (MenuComandoController.Instancia != null)
                    MenuComandoController.Instancia.AdicionarLog("DRONE", $"SEGUINDO: {ObterNomeExibicao(alvoEncontrado.gameObject)}", "sistema");
            }
            else
            {
                // Inimigo: processa normalmente (ataque/missao)
                MarcarComoAlvo(alvoEncontrado);
            }
        }
        else if (primeiroPontoValido.HasValue)
        {
            MarcarCoordenada(primeiroPontoValido.Value.point);
        }
    }

    /// <summary>
    /// Trava a camera do drone em qualquer Transform (aliado, aviao, navio, etc.)
    /// sem emitir ordem de combate. Use via codigo ou pelo reticulo do drone.
    /// </summary>
    public void TravadoEmAlvo(Transform alvo)
    {
        if (alvo == null) { alvoTravadoCamera = null; pontoTravadoCamera = null; return; }
        alvoTravadoCamera = alvo;
        pontoTravadoCamera = null;
    }

    private void MarcarComoAlvo(IdentidadeUnidade id)
    {
        if (targetUnit == null || id == null) return;
        
        bool ordemCombateAplicada = false;
        if (id != null && id.transform != null)
        {
            ControleSubmarino submarinoManual = targetUnit.GetComponent<ControleSubmarino>();
            if (submarinoManual != null && submarinoManual.EmModoManualDisparo())
            {
                ordemCombateAplicada = submarinoManual.TentarDisparoManual(id.transform.position, id.transform);
                if (ordemCombateAplicada)
                {
                    alvoTravadoCamera = id.transform;
                    pontoTravadoCamera = null;
                }
            }

            if (!ordemCombateAplicada)
            {
            ordemCombateAplicada = targetUnit.EmitirMissaoAereaOfensiva(id.transform.position, id.transform);
            if (!ordemCombateAplicada && (targetUnit.EhUnidadeNaval() || targetUnit.GetComponent<ControleSubmarino>() != null))
            {
                ordemCombateAplicada = targetUnit.EmitirMissaoNavalOfensiva(id.transform.position, id.transform, false, false);
            }
            if (!ordemCombateAplicada)
            {
                targetUnit.DefinirModoCombate(true);
                targetUnit.DefinirAlvoPrioritario(id.transform);
                ordemCombateAplicada = true;
            }
            }
        }

        ControleAviao aviao = targetUnit.GetComponent<ControleAviao>();
        KamikazeDrone drone = targetUnit.GetComponent<KamikazeDrone>();
        ControleDroneHasaf droneHasaf = targetUnit.GetComponent<ControleDroneHasaf>();
        
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
            
            if (droneHasaf != null)
            {
                droneHasaf.AtribuirAlvo(id.transform);
            }
            
            try
            {
                AudioClip clip = Resources.Load<AudioClip>("Sons/alvo_fixado") ?? Resources.Load<AudioClip>("mp3/click");
                if (clip != null)
                {
                    AudioRuntime.PlayClipAtPoint(clip, id.transform.position, 0.8f);
                }
            }
            catch {}
        }

        alvoTravadoCamera = id.transform;
        pontoTravadoCamera = null;

        if (MenuComandoController.Instancia != null)
        {
            string origem = aviao != null ? "DRONE" : "OPS";
            string acao = ordemCombateAplicada ? "ALVO LOCK/COMBATE" : "ALVO LOCK";
            MenuComandoController.Instancia.AdicionarLog(origem, $"{acao}: {id.name.ToUpper()} EM {id.transform.position}", "sistema");
        }
    }
    
    private void MarcarCoordenada(Vector3 ponto)
    {
        if (targetUnit == null) return;
        
        ControleAviao aviao = targetUnit.GetComponent<ControleAviao>();
        ControleSubmarino submarino = targetUnit.GetComponent<ControleSubmarino>();
        ControleDroneHasaf droneHasaf = targetUnit.GetComponent<ControleDroneHasaf>();
        if (aviao != null)
        {
            targetUnit.EmitirMissaoAereaOfensiva(ponto, null);

            if (droneHasaf != null)
            {
                droneHasaf.AtribuirAlvo(null);
            }

            pontoTravadoCamera = ponto;
            alvoTravadoCamera = null;
            
            if (MenuComandoController.Instancia != null)
            {
                MenuComandoController.Instancia.AdicionarLog("DRONE", $"COORDENADAS ENVIADAS: {ponto:F1}", "sistema");
            }
            
            try
            {
                AudioClip clip = Resources.Load<AudioClip>("mp3/click");
                if (clip != null)
                {
                    AudioRuntime.PlayClipAtPoint(clip, targetUnit.transform.position, 0.5f);
                }
            }
            catch {}
        }
        else
        {
            if (submarino != null && submarino.EmModoManualDisparo())
            {
                bool disparou = submarino.TentarDisparoManual(ponto, null);
                pontoTravadoCamera = ponto;
                alvoTravadoCamera = null;

                if (disparou && MenuComandoController.Instancia != null)
                {
                    MenuComandoController.Instancia.AdicionarLog("SUB", $"MISSIL MANUAL: {ponto:F1}", "sistema");
                }
                return;
            }

            if (targetUnit.EhUnidadeNaval() || targetUnit.GetComponent<ControleSubmarino>() != null)
            {
                targetUnit.EmitirMissaoNavalOfensiva(ponto, null, false, true);
            }
            else
            {
                targetUnit.DefinirModoCombate(true);
                targetUnit.EmitirOrdemMover(ponto);
            }
            pontoTravadoCamera = ponto;
            alvoTravadoCamera = null;

            if (MenuComandoController.Instancia != null)
            {
                MenuComandoController.Instancia.AdicionarLog("OPS", $"COORDENADAS ENVIADAS: {ponto:F1}", "sistema");
            }
        }
    }

    public void AtualizarFoco()
    {
        if (targetUnit != null)
        {
            DefinirTarget(targetUnit);
            return;
        }

        var gerente = ObterGerenteSelecaoCache(true);
        if (gerente != null && gerente.unidadesSelecionadas != null && gerente.unidadesSelecionadas.Count > 0)
        {
            ultimaUnidadeSelecionadaId = gerente.unidadesSelecionadas[0] != null
                ? gerente.unidadesSelecionadas[0].GetInstanceID()
                : 0;
            DefinirTarget(gerente.unidadesSelecionadas[0]);
        }
        else
        {
            ultimaUnidadeSelecionadaId = 0;
            DefinirTarget(null);
        }
    }

    private GerenteSelecao ObterGerenteSelecaoCache(bool forcarRefresh = false)
    {
        if (!forcarRefresh && gerenteSelecaoCache != null && Time.unscaledTime < proximoRefreshGerenteSelecao)
        {
            return gerenteSelecaoCache;
        }

        proximoRefreshGerenteSelecao = Time.unscaledTime + 0.5f;
        gerenteSelecaoCache = FindFirstObjectByType<GerenteSelecao>();
        return gerenteSelecaoCache;
    }
}
