using UnityEngine;
using UnityEngine.Rendering.Universal;

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
                    cam.clearFlags = CameraClearFlags.Skybox;
                    cam.backgroundColor = new Color(0.44f, 0.68f, 0.88f, 1f); // Fallback caso a cena não tenha skybox.
                    cam.fieldOfView = 30f;
                    cam.farClipPlane = 6000f;
                    cam.allowHDR = false;
                    cam.allowMSAA = false;
                    
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
    private Rigidbody corpoRigidbodyAlvo;
    private RigidbodyInterpolation interpolacaoOriginalAlvo;
    private bool restaurarInterpolacaoAlvo;
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
            minhaCamera.clearFlags = CameraClearFlags.Skybox;
            minhaCamera.backgroundColor = new Color(0.44f, 0.68f, 0.88f, 1f);
            minhaCamera.farClipPlane = 8000f;
            minhaCamera.allowHDR = false;
            minhaCamera.allowMSAA = false;

            // A câmera satélite não precisa renderizar sombras próprias. Isso
            // economiza uma passada de sombras sem alterar as demais câmeras.
            UniversalAdditionalCameraData dadosCamera = GetComponent<UniversalAdditionalCameraData>();
            if (dadosCamera != null)
            {
                dadosCamera.renderShadows = false;
            }
        }

        // Começa com uma aproximação mais útil; o usuário continua podendo
        // ajustar livremente pelo slider ou pela roda do mouse.
        zoomFactor = Mathf.Clamp(Mathf.Max(zoomFactor, 2.57f), 0.06f, 18f);

        layerMaskMarcacao = ~LayerMask.GetMask("UI");

        // Garante que comece desativada para poupar performance
        DesativarDoMenu();
    }

    private void OnDestroy()
    {
        RestaurarInterpolacaoAlvo();
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
        RestaurarInterpolacaoAlvo();
    }

    public void DefinirTarget(ControleUnidade unidade)
    {
        DefinirTarget(unidade, false);
    }

    public void DefinirTarget(ControleUnidade unidade, bool manterModoDrone)
    {
        bool mesmaUnidade = targetUnit == unidade && unidade != null;
        if (!mesmaUnidade)
        {
            RestaurarInterpolacaoAlvo();
        }
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
            if (minhaCamera.enabled)
            {
                AplicarInterpolacaoAlvo();
            }
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
            Vector3 desiredPosition = targetPos + ObterOrientacaoHorizontal(targetUnit.transform) * localOffset;
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
        float deltaTempoCamera = Time.unscaledDeltaTime;
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
            
            float fatorPosicaoDrone = 1f - Mathf.Exp(-Mathf.Max(0.1f, suavidadeSeguir) * deltaTempoCamera);
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

            float fatorRotacaoDrone = 1f - Mathf.Exp(-Mathf.Max(0.1f, suavidadeRotacao) * deltaTempoCamera);
            transform.rotation = Quaternion.Slerp(transform.rotation, stabilizedRotation, fatorRotacaoDrone);

            // Zoom FOV poderoso para o drone Hasaf
            float desiredFOV = Mathf.Clamp(30f / zoomFactor, 0.1f, 45f);
            if (minhaCamera != null)
            {
                minhaCamera.fieldOfView = Mathf.Lerp(minhaCamera.fieldOfView, desiredFOV, deltaTempoCamera * 10f);
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
                minhaCamera.fieldOfView = Mathf.Lerp(minhaCamera.fieldOfView, 30f, deltaTempoCamera * 5f);
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
        localOffset /= Mathf.Max(0.06f, zoomFactor);

        Vector3 targetPos = targetUnit.transform.position;
        
        // Aplica a rotação em volta da unidade
        Quaternion addRot = Quaternion.Euler(0f, currentRotationY, 0f);
        // O acompanhamento orbita pelo rumo, mas não converte pitch/roll do
        // casco ou do veículo em deslocamentos laterais/verticais da câmera.
        Quaternion baseRotacao = ObterOrientacaoHorizontal(targetUnit.transform);
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
            deltaTempoCamera);
        
        Vector3 lookTarget = targetPos + Vector3.up * (factor * 0.4f + 0.5f);
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
        float fatorRotacaoNormal = 1f - Mathf.Exp(-Mathf.Max(0.1f, suavidadeRotacao) * deltaTempoCamera);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, fatorRotacaoNormal);
    }

    private void AplicarInterpolacaoAlvo()
    {
        if (corpoRigidbodyAlvo != null || targetUnit == null) return;

        corpoRigidbodyAlvo = targetUnit.GetComponent<Rigidbody>();
        if (corpoRigidbodyAlvo == null) return;

        interpolacaoOriginalAlvo = corpoRigidbodyAlvo.interpolation;
        restaurarInterpolacaoAlvo = interpolacaoOriginalAlvo != RigidbodyInterpolation.Interpolate;
        if (restaurarInterpolacaoAlvo)
        {
            corpoRigidbodyAlvo.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void RestaurarInterpolacaoAlvo()
    {
        if (corpoRigidbodyAlvo != null
            && restaurarInterpolacaoAlvo
            && corpoRigidbodyAlvo.interpolation == RigidbodyInterpolation.Interpolate)
        {
            corpoRigidbodyAlvo.interpolation = interpolacaoOriginalAlvo;
        }

        corpoRigidbodyAlvo = null;
        restaurarInterpolacaoAlvo = false;
    }

    private static Quaternion ObterOrientacaoHorizontal(Transform unidade)
    {
        return Quaternion.Euler(0f, unidade.eulerAngles.y, 0f);
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

                // Unidades sem executor de deslocamento ainda podem travar o
                // inimigo nas torretas. Aeronaves não: se o controlador aéreo
                // recusou por combustível, pista, tripulação ou C17, não
                // escreva o GPS diretamente e deixe uma missão fantasma.
                bool possuiExecutorAereo = targetUnit.GetComponent<ControleAviao>() != null
                    || targetUnit.GetComponent<Hegemonia.Aeronaves.C17.C17TransporteController>() != null;
                if (!ordemCombateAplicada && !possuiExecutorAereo)
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
        
        if (aviao != null && ordemCombateAplicada)
        {
            aviao.alvoEstrategico = id.transform.position;
            // A ordem já foi aceita pelo controlador aéreo central. Não escreva
            // alvoGPSVoo aqui: a coroutine de voo é a única autoridade sobre
            // esse destino e uma escrita paralela podia sobrescrever patrulha,
            // retorno ou uma nova ordem emitida no mesmo ciclo.
            
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
            bool ordemAceita = targetUnit.EmitirMissaoAereaOfensiva(ponto, null);
            if (!ordemAceita)
            {
                if (MenuComandoController.Instancia != null)
                {
                    MenuComandoController.Instancia.AdicionarLog(
                        "DRONE",
                        $"ORDEM AÉREA RECUSADA: {targetUnit.name.ToUpper()}",
                        "sistema");
                }
                return;
            }

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
