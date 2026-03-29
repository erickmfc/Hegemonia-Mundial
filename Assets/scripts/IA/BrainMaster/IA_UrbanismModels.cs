using System;
using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public enum IA_UrbanSectorType
    {
        None,
        Command,
        Civil,
        Industrial,
        Military,
        Airfield,
        Naval,
        Logistics,
        Road,
        Buffer,
        Forbidden
    }

    [Serializable]
    public sealed class IA_SemanticCell
    {
        public Vector2Int Index;
        public Vector3 Center;
        public IA_TerrainType Terrain;
        public IA_ZoneType LegacyZone;
        public IA_UrbanSectorType Sector;
        public bool Buildable;
        public bool Occupied;
        public bool Reserved;
        public bool Forbidden;
        public float Height;
        public float Slope;
        public float Threat;
        public float BaseDistance;
        public float CoastDistance;
        public float Clearance;
    }

    [Serializable]
    public sealed class IA_LotCandidate
    {
        public string ItemKey;
        public IA_ZoneType Zone;
        public IA_UrbanSectorType Sector;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector2 HalfExtents;
        public float Score;
        public bool Valid;
        public string ValidationMessage;
    }

    [Serializable]
    public sealed class IA_BaseBlueprintProfile
    {
        public string Id = "brainmaster_default";
        public float CommandRadius = 110f;
        public float CivilBand = 170f;
        public float IndustrialBand = 220f;
        public float MilitaryBand = 240f;
        public float AirfieldBand = 320f;
        public float NavalBand = 220f;
        public float RoadSpacing = 48f;

        public static IA_BaseBlueprintProfile CreateDefault()
        {
            return new IA_BaseBlueprintProfile();
        }
    }
}
