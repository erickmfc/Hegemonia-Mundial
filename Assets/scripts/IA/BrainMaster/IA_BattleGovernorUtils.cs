using UnityEngine;

namespace Hegemonia.AI.BrainMaster
{
    public static class IA_BattleGovernorUtils
    {
        public static int GetEngagementCost(GameObject unit)
        {
            if (unit == null)
            {
                return 0;
            }

            string normalizedName = IA_Text.Normalize(unit.name);
            if (IsCarrier(unit, normalizedName))
            {
                return 5;
            }

            if (IsNavalCombatUnit(unit, normalizedName))
            {
                return 4;
            }

            if (IsAirCombatUnit(unit, normalizedName))
            {
                return 3;
            }

            if (IsHeavyGroundUnit(unit, normalizedName))
            {
                return 2;
            }

            if (IsTransportUnit(unit, normalizedName))
            {
                return 2;
            }

            return 1;
        }

        public static int EstimateTransportCapacity(GameObject unit)
        {
            if (unit == null)
            {
                return 0;
            }

            string normalizedName = IA_Text.Normalize(unit.name);
            if (IsNavalTransport(unit, normalizedName))
            {
                return 8;
            }

            if (IsHoverTransport(unit, normalizedName))
            {
                return 4;
            }

            if (IsAirTransport(unit, normalizedName))
            {
                return 3;
            }

            if (IsGroundTransport(unit, normalizedName))
            {
                return 2;
            }

            return 0;
        }

        public static bool IsTransportUnit(GameObject unit)
        {
            return IsTransportUnit(unit, IA_Text.Normalize(unit != null ? unit.name : string.Empty));
        }

        public static bool IsTransportUnit(GameObject unit, string normalizedName)
        {
            return IsGroundTransport(unit, normalizedName)
                   || IsHoverTransport(unit, normalizedName)
                   || IsNavalTransport(unit, normalizedName)
                   || IsAirTransport(unit, normalizedName);
        }

        public static bool IsGroundTransport(GameObject unit, string normalizedName)
        {
            if (unit == null)
            {
                return false;
            }

            if (unit.GetComponent<TransporteTerrestre>() != null)
            {
                return true;
            }

            return normalizedName.Contains("truck")
                   || normalizedName.Contains("caminhao")
                   || (normalizedName.Contains("transporte")
                       && !normalizedName.Contains("aereo")
                       && !normalizedName.Contains("air")
                       && !normalizedName.Contains("heli")
                       && !normalizedName.Contains("hover")
                       && !normalizedName.Contains("navio"));
        }

        public static bool IsHoverTransport(GameObject unit, string normalizedName)
        {
            return unit != null
                   && (unit.GetComponent<HovercraftTransporte>() != null
                       || normalizedName.Contains("hovercraft")
                       || normalizedName.Contains("hover"));
        }

        public static bool IsNavalTransport(GameObject unit, string normalizedName)
        {
            if (unit == null)
            {
                return false;
            }

            return unit.GetComponent<TransporteAnfibio>() != null
                   || normalizedName.Contains("liberty")
                   || normalizedName.Contains("barco ww transporte")
                   || normalizedName.Contains("navio transporte")
                   || normalizedName.Contains("transporte anfibio")
                   || (normalizedName.Contains("transporte")
                       && (normalizedName.Contains("naval")
                           || normalizedName.Contains("barco")
                           || normalizedName.Contains("navio")));
        }

        public static bool IsAirTransport(GameObject unit, string normalizedName)
        {
            if (unit == null)
            {
                return false;
            }

            if (unit.GetComponent<Helicoptero>() == null)
            {
                return false;
            }

            return normalizedName.Contains("heli")
                   || normalizedName.Contains("transport")
                   || normalizedName.Contains("ray")
                   || normalizedName.Contains("vans")
                   || normalizedName.Contains("air");
        }

        public static bool IsCarrier(GameObject unit, string normalizedName)
        {
            return unit != null
                   && (normalizedName.Contains("porta")
                       || normalizedName.Contains("carrier")
                       || normalizedName.Contains("sovereign"));
        }

        public static bool IsNavalCombatUnit(GameObject unit, string normalizedName)
        {
            if (unit == null)
            {
                return false;
            }

            if (unit.GetComponent<ControleSubmarino>() != null)
            {
                return true;
            }

            if (unit.GetComponent<ControleNavioRealista>() == null)
            {
                return false;
            }

            return !IsNavalTransport(unit, normalizedName) && !IsCarrier(unit, normalizedName);
        }

        public static bool IsAirCombatUnit(GameObject unit, string normalizedName)
        {
            if (unit == null)
            {
                return false;
            }

            return unit.GetComponent<ControleAviao>() != null
                   || unit.GetComponent<ControleAviaoCaca>() != null
                   || (unit.GetComponent<Helicoptero>() != null && !IsAirTransport(unit, normalizedName));
        }

        public static bool IsHeavyGroundUnit(GameObject unit, string normalizedName)
        {
            if (unit == null)
            {
                return false;
            }

            return normalizedName.Contains("tank")
                   || normalizedName.Contains("mbt")
                   || normalizedName.Contains("arthur")
                   || normalizedName.Contains("artilh")
                   || normalizedName.Contains("hack")
                   || normalizedName.Contains("lancador");
        }
    }
}
