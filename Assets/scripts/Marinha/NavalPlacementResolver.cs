using UnityEngine;
using UnityEngine.AI;

public static class NavalPlacementResolver
{
    public struct StructurePose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float SeaLevel;
        public string Reason;
    }

    private const float WaterTolerance = 1.15f;
    private const float VisibleWaterDepthTolerance = 0.05f;
    private static float _nextExplicitWaterProbeTime = -1f;
    private static bool _hasExplicitWaterSurface;

    public static bool RequiresCoastalPlacement(GameObject target)
    {
        if (target == null)
            return false;
            
        string name = target.name.ToLower();
        return name.Contains("estaleiro") || name.Contains("pier");
    }

    public static float ResolveSeaLevel()
    {
        Construtor construtor = Construtor.Instancia != null
            ? Construtor.Instancia
            : UnityEngine.Object.FindFirstObjectByType<Construtor>();
        if (construtor != null)
        {
            float nivel = construtor.alturaDoMar;
            float referencia;
            if (TryResolveWaterReferenceHeight(out referencia) && Mathf.Abs(referencia - nivel) > 0.25f)
            {
                nivel = referencia;
            }
            return nivel;
        }

        IA_Suprema iaSuprema = UnityEngine.Object.FindFirstObjectByType<IA_Suprema>();
        if (iaSuprema != null)
        {
            float nivel = iaSuprema.nivelDoMar;
            float referencia;
            if (TryResolveWaterReferenceHeight(out referencia) && Mathf.Abs(referencia - nivel) > 0.25f)
            {
                nivel = referencia;
            }
            return nivel;
        }

        float fallback;
        if (TryResolveWaterReferenceHeight(out fallback))
        {
            return fallback;
        }

        return 0f;
    }

    private static bool TryResolveWaterReferenceHeight(out float height)
    {
        height = 0f;

        OceanAdvanced ocean = UnityEngine.Object.FindFirstObjectByType<OceanAdvanced>();
        if (ocean != null)
        {
            height = ocean.transform.position.y;
            return true;
        }

        GameObject agua = GameObject.Find("Agua") ?? GameObject.Find("Water") ?? GameObject.Find("Ocean");
        if (agua != null)
        {
            height = agua.transform.position.y;
            return true;
        }

        return false;
    }

    public static bool IsCurrentStructurePoseValid(GameObject structure, out string reason)
    {
        reason = string.Empty;
        if (!RequiresCoastalPlacement(structure))
        {
            return true;
        }

        float seaLevel = ResolveSeaLevel();
        float frontDistance;
        float backDistance;
        ResolveCoastalOffsets(structure, out frontDistance, out backDistance);

        float score;
        return EvaluateCoastalPose(
            SnapToSeaLevel(structure.transform.position, seaLevel),
            structure.transform.forward,
            frontDistance,
            backDistance,
            seaLevel,
            out score,
            out reason);
    }

    public static bool IsStructurePoseValid(GameObject target, Vector3 position, Quaternion rotation, out string reason)
    {
        reason = string.Empty;
        if (!RequiresCoastalPlacement(target))
        {
            return true;
        }

        float seaLevel = ResolveSeaLevel();
        float frontDistance;
        float backDistance;
        ResolveCoastalOffsets(target, out frontDistance, out backDistance);

        Vector3 forward = rotation * Vector3.forward;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = Vector3.forward;
        }

        float score;
        return EvaluateCoastalPose(
            SnapToSeaLevel(position, seaLevel),
            forward,
            frontDistance,
            backDistance,
            seaLevel,
            out score,
            out reason);
    }

    public static bool TryResolveStructurePose(GameObject target, Vector3 anchor, Quaternion fallbackRotation, out StructurePose pose)
    {
        pose = new StructurePose
        {
            Position = anchor,
            Rotation = fallbackRotation,
            SeaLevel = ResolveSeaLevel(),
            Reason = string.Empty
        };

        if (target == null)
        {
            pose.Reason = "prefab naval invalido";
            return false;
        }

        anchor = SnapToSeaLevel(anchor, pose.SeaLevel);

        if (!RequiresCoastalPlacement(target))
        {
            pose.Position = anchor;
            pose.Rotation = fallbackRotation;
            return true;
        }

        float frontDistance;
        float backDistance;
        ResolveCoastalOffsets(target, out frontDistance, out backDistance);

        Vector3 fallbackForward = fallbackRotation * Vector3.forward;
        if (fallbackForward.sqrMagnitude < 0.01f)
        {
            fallbackForward = Vector3.forward;
        }
        fallbackForward.Normalize();

        // Busca progressiva: aceita clique um pouco mar adentro e tenta "encaixar" na costa mais próxima.
        float mediumRadius = Mathf.Max(120f, Mathf.Abs(frontDistance) * 2.1f);
        float largeRadius = Mathf.Max(220f, Mathf.Abs(frontDistance) * 4.0f);
        float[] radii = { 0f, 20f, 45f, 70f, mediumRadius, largeRadius };
        bool found = false;
        float bestScore = float.MinValue;
        Vector3 bestPosition = anchor;
        Quaternion bestRotation = Quaternion.LookRotation(fallbackForward, Vector3.up);
        string bestReason = "sem costa valida";

        for (int r = 0; r < radii.Length; r++)
        {
            float radius = radii[r];
            // OTIMIZADO: Testando no máximo 6 posições per raio, não 10.
            int positionSamples = radius <= 0.01f ? 1 : 6;

            for (int p = 0; p < positionSamples; p++)
            {
                Vector3 candidate = anchor;
                if (radius > 0.01f)
                {
                    float angle = ((360f / positionSamples) * p) * Mathf.Deg2Rad;
                    candidate += new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                }

                candidate = SnapToSeaLevel(candidate, pose.SeaLevel);
                float nearbyRadius = Mathf.Max(10f, Mathf.Abs(frontDistance) * 0.35f);
                if (!HasWaterNearby(candidate, nearbyRadius, pose.SeaLevel))
                {
                    float relaxedRadius = Mathf.Max(nearbyRadius + 18f, Mathf.Abs(frontDistance) * 0.85f);
                    if (!HasWaterNearby(candidate, relaxedRadius, pose.SeaLevel))
                    {
                        continue;
                    }
                }

                if (TryPromoteBestPose(candidate, fallbackForward, frontDistance, backDistance, pose.SeaLevel, ref found, ref bestScore, ref bestPosition, ref bestRotation, ref bestReason))
                {
                    continue;
                }

                // OTIMIZADO: Reduzido de 12 rotações para apenas 8 para aliviar CPU.
                for (int d = 0; d < 8; d++)
                {
                    float angle = ((360f / 8f) * d) * Mathf.Deg2Rad;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    TryPromoteBestPose(candidate, direction, frontDistance, backDistance, pose.SeaLevel, ref found, ref bestScore, ref bestPosition, ref bestRotation, ref bestReason);
                }
            }

            // OTIMIZADO: Se encontrar um bom posto próximo já na primeira ou segunda volta, desiste de varrer mais longe!
            if (found && bestScore > 0f)
            {
                break;
            }
        }

        if (!found)
        {
            pose.Reason = bestReason;
            return false;
        }

        pose.Position = bestPosition;
        pose.Rotation = bestRotation;
        pose.Reason = string.Empty;
        return true;
    }

    public static bool TryResolveWaterSpawn(Vector3 anchor, Vector3 preferredForward, float minRadius, float maxRadius, out Vector3 spawnPosition, out float seaLevel, out string reason)
    {
        seaLevel = ResolveSeaLevel();
        reason = string.Empty;
        spawnPosition = SnapToSeaLevel(anchor, seaLevel);

        if (IsWaterAtPosition(spawnPosition, seaLevel))
        {
            return true;
        }

        preferredForward.y = 0f;
        if (preferredForward.sqrMagnitude < 0.01f)
        {
            preferredForward = Vector3.forward;
        }
        preferredForward.Normalize();

        float startRadius = Mathf.Max(0f, minRadius);
        float endRadius = Mathf.Max(startRadius + 12f, maxRadius);
        float step = endRadius > 220f ? 20f : 12f;

        for (float radius = startRadius; radius <= endRadius; radius += step)
        {
            for (int i = 0; i < 12; i++)
            {
                float signedAngle;
                switch (i)
                {
                    case 0: signedAngle = 0f; break;
                    case 1: signedAngle = 18f; break;
                    case 2: signedAngle = -18f; break;
                    case 3: signedAngle = 36f; break;
                    case 4: signedAngle = -36f; break;
                    case 5: signedAngle = 54f; break;
                    case 6: signedAngle = -54f; break;
                    case 7: signedAngle = 72f; break;
                    case 8: signedAngle = -72f; break;
                    case 9: signedAngle = 90f; break;
                    case 10: signedAngle = -90f; break;
                    default: signedAngle = 180f; break;
                }

                Vector3 dir = Quaternion.AngleAxis(signedAngle, Vector3.up) * preferredForward;
                Vector3 probe = SnapToSeaLevel(anchor + (dir * radius), seaLevel);
                if (IsWaterAtPosition(probe, seaLevel))
                {
                    spawnPosition = probe;
                    return true;
                }
            }
        }

        reason = "sem ponto de agua";
        return false;
    }

    public static bool TryResolveWaterDirection(
        Vector3 center,
        Vector3 fallbackForward,
        float minRadius,
        float maxRadius,
        out Vector3 waterDirection,
        out Vector3 waterPoint,
        out float seaLevel)
    {
        seaLevel = ResolveSeaLevel();
        waterDirection = fallbackForward;
        waterPoint = SnapToSeaLevel(center, seaLevel);

        fallbackForward.y = 0f;
        if (fallbackForward.sqrMagnitude < 0.01f)
        {
            fallbackForward = Vector3.forward;
        }
        fallbackForward.Normalize();

        float startRadius = Mathf.Max(8f, minRadius);
        float endRadius = Mathf.Max(startRadius + 12f, maxRadius);
        bool found = false;
        float bestScore = float.MinValue;

        for (float radius = startRadius; radius <= endRadius; radius += (endRadius > 220f ? 20f : 12f))
        {
            int samples = radius < 120f ? 12 : 16;
            for (int i = 0; i < samples; i++)
            {
                float signedAngle = ((360f / samples) * i) - 180f;
                Vector3 direction = Quaternion.AngleAxis(signedAngle, Vector3.up) * fallbackForward;
                Vector3 probe = SnapToSeaLevel(center + (direction * radius), seaLevel);
                if (!IsWaterAtPosition(probe, seaLevel))
                {
                    continue;
                }

                float alignment = Vector3.Dot(fallbackForward, direction.normalized);
                float score = (alignment * 0.75f) - (radius * 0.01f);
                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    waterPoint = probe;
                    waterDirection = direction.normalized;
                }
            }
        }

        if (!found)
        {
            string ignoredReason;
            Vector3 fallbackPoint;
            if (TryResolveWaterSpawn(center, fallbackForward, startRadius, endRadius, out fallbackPoint, out seaLevel, out ignoredReason))
            {
                Vector3 direction = fallbackPoint - center;
                direction.y = 0f;
                if (direction.sqrMagnitude >= 0.01f)
                {
                    waterPoint = fallbackPoint;
                    waterDirection = direction.normalized;
                    return true;
                }
            }

            return false;
        }

        return true;
    }

    public static float DistanceToMapEdge(Vector3 position)
    {
        Bounds mapBounds;
        if (!TryGetMapBounds(out mapBounds))
        {
            return Mathf.Infinity;
        }

        float distMinX = Mathf.Abs(position.x - mapBounds.min.x);
        float distMaxX = Mathf.Abs(mapBounds.max.x - position.x);
        float distMinZ = Mathf.Abs(position.z - mapBounds.min.z);
        float distMaxZ = Mathf.Abs(mapBounds.max.z - position.z);
        return Mathf.Min(Mathf.Min(distMinX, distMaxX), Mathf.Min(distMinZ, distMaxZ));
    }

    public static bool IsNearMapEdge(Vector3 position, float margin)
    {
        return DistanceToMapEdge(position) < Mathf.Max(0f, margin);
    }

    public static bool HasSafeLaunchCorridor(
        Vector3 center,
        Vector3 preferredForward,
        float minRadius,
        float maxRadius,
        float edgeMargin,
        out string reason)
    {
        reason = string.Empty;

        Vector3 waterDirection;
        Vector3 waterPoint;
        float seaLevel;
        if (!TryResolveWaterDirection(center, preferredForward, minRadius, maxRadius, out waterDirection, out waterPoint, out seaLevel))
        {
            reason = "sem corredor de agua";
            return false;
        }

        if (IsNearMapEdge(waterPoint, edgeMargin))
        {
            reason = "agua muito perto da borda do mapa";
            return false;
        }

        Vector3 secondWaterPoint;
        string secondReason;
        float outerMin = Mathf.Max(minRadius + 28f, 70f);
        float outerMax = Mathf.Max(maxRadius + 90f, outerMin + 80f);
        if (!TryResolveWaterSpawn(waterPoint, waterDirection, outerMin, outerMax, out secondWaterPoint, out seaLevel, out secondReason))
        {
            reason = "saida naval curta";
            return false;
        }

        if (IsNearMapEdge(secondWaterPoint, Mathf.Max(24f, edgeMargin * 0.7f)))
        {
            reason = "saida naval encurralada";
            return false;
        }

        return true;
    }

    public static bool IsWaterAtPosition(Vector3 position)
    {
        return IsWaterAtPosition(position, ResolveSeaLevel());
    }

    public static bool IsWaterAtPosition(Vector3 position, float seaLevel)
    {
        ClassificacaoSuperficieMapa classificacaoMarcada;
        float alturaMarcada;
        if (RegistroSuperficieMapa.TryClassify(position, out classificacaoMarcada, out alturaMarcada))
        {
            if (classificacaoMarcada == ClassificacaoSuperficieMapa.Agua || classificacaoMarcada == ClassificacaoSuperficieMapa.Costa)
            {
                return true;
            }

            if (classificacaoMarcada == ClassificacaoSuperficieMapa.Chao)
            {
                return false;
            }
        }

        float waterSurfaceHeight = float.MinValue;
        bool sawWaterSurface = false;
        float solidHeight = float.MinValue;
        bool sawSolid = false;

        RaycastHit[] hits = Physics.RaycastAll(
            new Vector3(position.x, seaLevel + 1000f, position.z),
            Vector3.down,
            3000f,
            BuildRayMask(),
            QueryTriggerInteraction.Collide);

        System.Array.Sort(hits, CompareHitsByDistance);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null)
            {
                continue;
            }

            if (IsDynamicObstacle(collider))
            {
                continue;
            }

            if (LooksLikeWater(collider))
            {
                waterSurfaceHeight = hits[i].point.y;
                sawWaterSurface = true;
                continue;
            }

            solidHeight = hits[i].point.y;
            sawSolid = true;
            break;
        }

        if (sawWaterSurface)
        {
            float effectiveWaterHeight = Mathf.Max(seaLevel, waterSurfaceHeight);
            if (!sawSolid)
            {
                return true;
            }

            return solidHeight <= effectiveWaterHeight - VisibleWaterDepthTolerance;
        }

        // Se existe água explícita na cena, mas não há colisor de água detectável,
        // ainda assim precisamos classificar "água" via nível do mar/altura do terreno.
        // Caso contrário, construções navais/costeiras ficam impossíveis de posicionar.
        if (Terrain.activeTerrain != null)
        {
            return SampleTerrainHeight(position) <= seaLevel + WaterTolerance;
        }

        // Fallback genérico: se o chão estiver abaixo do nível do mar, consideramos água.
        float alturaChao = SampleGroundHeight(position, seaLevel);
        return alturaChao <= seaLevel + WaterTolerance;
    }

    private static bool HasExplicitWaterSurfaceInScene()
    {
        float now = Application.isPlaying ? Time.unscaledTime : 0f;
        if (_nextExplicitWaterProbeTime >= 0f && now < _nextExplicitWaterProbeTime)
        {
            return _hasExplicitWaterSurface;
        }

        _hasExplicitWaterSurface =
            RegistroSuperficieMapa.HaSuperficie(TipoSuperficieMapa.Agua)
            || UnityEngine.Object.FindFirstObjectByType<OceanAdvanced>() != null
            || GameObject.Find("Agua") != null
            || GameObject.Find("Water") != null
            || GameObject.Find("Ocean") != null;

        _nextExplicitWaterProbeTime = now + 5f;
        return _hasExplicitWaterSurface;
    }

    private static bool TryGetMapBounds(out Bounds bounds)
    {
        if (RegistroSuperficieMapa.TryGetCombinedBounds(out bounds))
        {
            return true;
        }

        if (RegistroSuperficieMapa.TryGetBounds(TipoSuperficieMapa.Agua, out bounds))
        {
            return true;
        }

        return RegistroSuperficieMapa.TryGetBounds(TipoSuperficieMapa.Chao, out bounds);
    }

    private static bool TryPromoteBestPose(
        Vector3 candidate,
        Vector3 direction,
        float frontDistance,
        float backDistance,
        float seaLevel,
        ref bool found,
        ref float bestScore,
        ref Vector3 bestPosition,
        ref Quaternion bestRotation,
        ref string bestReason)
    {
        float score;
        string reason;
        if (!EvaluateCoastalPose(candidate, direction, frontDistance, backDistance, seaLevel, out score, out reason))
        {
            bestReason = reason;
            return false;
        }

        if (!found || score > bestScore)
        {
            found = true;
            bestScore = score;
            bestPosition = candidate;
            bestRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        return true;
    }

    private static bool EvaluateCoastalPose(
        Vector3 center,
        Vector3 forward,
        float frontDistance,
        float backDistance,
        float seaLevel,
        out float score,
        out string reason)
    {
        score = float.MinValue;
        reason = "costa invalida";

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        float front = Mathf.Max(8f, Mathf.Abs(frontDistance));
        float back = backDistance > -2f ? -Mathf.Max(10f, Mathf.Abs(backDistance)) : backDistance;

        Vector3 frontProbe = SnapToSeaLevel(center + (forward * front), seaLevel);
        Vector3 backProbe = SnapToSeaLevel(center + (forward * back), seaLevel);

        bool frontIsWater = IsWaterAtPosition(frontProbe, seaLevel);
        bool backIsWater = IsWaterAtPosition(backProbe, seaLevel);
        float backHeight = SampleGroundHeight(backProbe, seaLevel);
        bool centerIsWater = IsWaterAtPosition(center, seaLevel);

        if (!frontIsWater)
        {
            reason = "frente sem agua";
            return false;
        }

        // OTIMIZADO: Afrouxado a inclinação traseira significativamente
        if (backHeight < seaLevel - 0.5f)
        {
            reason = "traseira sem terra (praia muito funda)";
            return false;
        }

        float centerBias = centerIsWater ? 0.35f : 1.1f;
        float backBias = backIsWater ? -0.85f : 1.35f;
        score = ((backHeight - seaLevel) * 2.15f) + centerBias + backBias;
        reason = string.Empty;
        return true;
    }

    private static bool HasWaterNearby(Vector3 center, float radius, float seaLevel)
    {
        if (IsWaterAtPosition(center, seaLevel))
        {
            return true;
        }

        for (int i = 0; i < 8; i++)
        {
            float angle = ((360f / 8f) * i) * Mathf.Deg2Rad;
            Vector3 probe = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            if (IsWaterAtPosition(SnapToSeaLevel(probe, seaLevel), seaLevel))
            {
                return true;
            }
        }

        return false;
    }

    public static void ResolveCoastalOffsets(GameObject target, out float frontDistance, out float backDistance)
    {
        // OTIMIZADO: Mais curtos por padrão para encaixar mais fácil em margens
        frontDistance = 25f;
        backDistance = -10f;

        Estaleiro estaleiro = target.GetComponent<Estaleiro>();
        if (estaleiro != null)
        {
            frontDistance = Mathf.Abs(estaleiro.offsetAguaFrente);
            backDistance = estaleiro.offsetTerraTras;
            return;
        }

        PierMarinha pier = target.GetComponent<PierMarinha>();
        if (pier != null)
        {
            frontDistance = Mathf.Abs(pier.offsetAguaFrente);
            backDistance = pier.offsetTerraTras;
        }
    }

    private static float SampleGroundHeight(Vector3 position, float fallback)
    {
        float alturaMarcada;
        if (RegistroSuperficieMapa.TryGetAltura(position, TipoSuperficieMapa.Chao, out alturaMarcada))
        {
            return alturaMarcada;
        }

        if (Terrain.activeTerrain != null)
        {
            return SampleTerrainHeight(position);
        }

        RaycastHit[] hits = Physics.RaycastAll(
            new Vector3(position.x, fallback + 1000f, position.z),
            Vector3.down,
            3000f,
            BuildRayMask(),
            QueryTriggerInteraction.Collide);

        System.Array.Sort(hits, CompareHitsByDistance);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || IsDynamicObstacle(collider) || LooksLikeWater(collider))
            {
                continue;
            }

            return hits[i].point.y;
        }

        return fallback;
    }

    private static float SampleTerrainHeight(Vector3 position)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            return 0f;
        }

        return terrain.SampleHeight(position) + terrain.transform.position.y;
    }

    private static Vector3 SnapToSeaLevel(Vector3 value, float seaLevel)
    {
        value.y = seaLevel;
        return value;
    }

    private static bool LooksLikeWater(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        MarcadorSuperficieMapa marcador = collider.GetComponentInParent<MarcadorSuperficieMapa>();
        if (marcador != null)
        {
            return marcador.TipoSuperficie == TipoSuperficieMapa.Agua;
        }

        string normalizedName = Normalize(collider.name);
        if (normalizedName.Contains("agua")
            || normalizedName.Contains("water")
            || normalizedName.Contains("ocean")
            || normalizedName.Contains("sea")
            || normalizedName.Contains("mar")
            || normalizedName.Contains("oceano")
            || normalizedName.Contains("liquid"))
        {
            return true;
        }

        if (collider.gameObject.layer == 4)
        {
            return true;
        }

        // OTIMIZADO: GetComponents<Component> perigoso removido para matar GC Allocs e Lag Spikes!!
        // Com nomes de colliders e LayerMask resolvemos 99% sem travar a thread.
        return false;
    }

    private static bool IsDynamicObstacle(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        Transform root = collider.transform;
        return root.GetComponentInParent<IdentidadeUnidade>() != null
               || root.GetComponentInParent<NavMeshAgent>() != null
               || root.GetComponentInParent<ControleUnidade>() != null
               || root.GetComponentInParent<ControleNavioRealista>() != null
               || root.GetComponentInParent<ControleSubmarino>() != null
               || root.GetComponentInParent<ControleAviao>() != null
               || root.GetComponentInParent<Helicoptero>() != null;
    }

    private static int BuildRayMask()
    {
        int mask = ~0;
        int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreLayer >= 0)
        {
            mask &= ~(1 << ignoreLayer);
        }

        return mask;
    }

    private static int CompareHitsByDistance(RaycastHit a, RaycastHit b)
    {
        return a.distance.CompareTo(b.distance);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.ToLowerInvariant();
    }
}
