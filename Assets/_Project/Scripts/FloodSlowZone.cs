
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(Collider))]
public sealed class FloodSlowZone : MonoBehaviour
{
    [Header("Configuração da Lentidão")]
    [Tooltip("0.5 = 50% da velocidade normal.")]
    [SerializeField, Range(0.1f, 1f)]
    private float speedMultiplier = 0.5f;
    private readonly Dictionary<RobotClickMover, HashSet<Collider>> occupants =
        new Dictionary<RobotClickMover, HashSet<Collider>>();

    private void Reset()
    {
        Collider zoneCollider = GetComponent<Collider>();

        if (zoneCollider)
            zoneCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterRobot(other);
    }

    private void OnTriggerStay(Collider other)
    {
        RegisterRobot(other);
    }

    private void OnTriggerExit(Collider other)
    {
        UnregisterRobot(other);
    }

    private void RegisterRobot(Collider other)
    {
        RobotClickMover robot = other.GetComponentInParent<RobotClickMover>();

        if (!robot)
            return;

        if (!occupants.TryGetValue(robot, out HashSet<Collider> colliders))
        {
            colliders = new HashSet<Collider>();
            occupants.Add(robot, colliders);
        }

        if (colliders.Add(other) && colliders.Count == 1)
        {
            robot.SetMovementSpeedModifier(this, speedMultiplier);

            Debug.Log("[FloodSlowZone] Robô entrou na água. Velocidade reduzida.");
        }
    }

    private void UnregisterRobot(Collider other)
    {
        RobotClickMover robot = other.GetComponentInParent<RobotClickMover>();

        if (!robot || !occupants.TryGetValue(robot, out HashSet<Collider> colliders))
            return;

        if (!colliders.Remove(other))
            return;

        if (colliders.Count == 0)
        {
            robot.RemoveMovementSpeedModifier(this);
            occupants.Remove(robot);

            Debug.Log("[FloodSlowZone] Robô saiu da água. Velocidade restaurada.");
        }
    }

    private void OnDisable()
    {
        foreach (KeyValuePair<RobotClickMover, HashSet<Collider>> entry in occupants)
        {
            if (entry.Key)
                entry.Key.RemoveMovementSpeedModifier(this);
        }

        occupants.Clear();
    }
}
