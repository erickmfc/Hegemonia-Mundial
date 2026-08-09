using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.RTS
{
    public enum RTSOrderType
    {
        Move,
        AttackMove,
        Stop,
        HoldPosition,
        Patrol,
        Follow,
        Attack,
        Capture,
        Retreat,
        ReturnToBase,
        Repair,
        Refuel,
        Reorganize
    }

    [System.Serializable]
    public struct RTSOrderCommand
    {
        public RTSOrderType type;
        public Vector3 destination;
        public Transform target;
        public Transform followTarget;
        public Vector3[] patrolPoints;
        public float distance;
        public string source;

        public RTSOrderCommand(RTSOrderType type, Vector3 destination, Transform target = null)
        {
            this.type = type;
            this.destination = destination;
            this.target = target;
            followTarget = null;
            patrolPoints = null;
            distance = 40f;
            source = "RTS";
        }
    }

    public static class RTSOrderDispatcher
    {
        public static bool Execute(ControleUnidade unit, RTSOrderCommand command)
        {
            if (unit == null)
            {
                return false;
            }

            switch (command.type)
            {
                case RTSOrderType.Move:
                case RTSOrderType.Capture:
                case RTSOrderType.ReturnToBase:
                case RTSOrderType.Repair:
                case RTSOrderType.Refuel:
                case RTSOrderType.Reorganize:
                    return unit.EmitirOrdemMover(command.destination);

                case RTSOrderType.AttackMove:
                    if (command.target != null)
                    {
                        unit.DefinirAlvoPrioritario(command.target);
                    }
                    return unit.EmitirOrdemMover(command.destination);

                case RTSOrderType.Attack:
                    if (command.target == null)
                    {
                        return false;
                    }
                    unit.DefinirAlvoPrioritario(command.target);
                    return unit.EmitirOrdemMover(command.target.position);

                case RTSOrderType.Stop:
                    return unit.EmitirOrdemParar();

                case RTSOrderType.HoldPosition:
                    unit.DefinirModoCombate(true);
                    return unit.EmitirOrdemParar();

                case RTSOrderType.Patrol:
                    if (command.patrolPoints == null || command.patrolPoints.Length == 0)
                    {
                        return false;
                    }
                    return unit.EmitirOrdemPatrulha(new List<Vector3>(command.patrolPoints));

                case RTSOrderType.Follow:
                    return unit.EmitirOrdemSeguir(command.followTarget != null ? command.followTarget : command.target, command.distance);

                case RTSOrderType.Retreat:
                    return unit.EmitirOrdemRecuar(command.destination, command.distance);

                default:
                    return false;
            }
        }

        public static bool EmitirOrdemRTS(this ControleUnidade unit, RTSOrderCommand command)
        {
            return Execute(unit, command);
        }
    }
}
