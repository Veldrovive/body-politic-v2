using System;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(DoorRoleDetector))]
public class RoleDoor : MonoBehaviour
{
    [Header("Access Roles")]
    [Tooltip("Roles that can enter this door.")]
    [SerializeField] private List<NpcRoleSO> rolesCanEnter = new List<NpcRoleSO>();

    [Header("Sneak in Logic")]
    [Tooltip("If true, an NPC is able to enter if the door is already open.")]
    [SerializeField] private bool canSneakIn = false;
    
    [Tooltip("If true, the -z direction link will allow anyone through.")]
    [SerializeField] private bool zMinusUnlocked = false;
    
    [Tooltip("If true, the +z direction link will allow anyone through.")]
    [SerializeField] private bool zPlusUnlocked = false;
    
    [Header("Movers")]
    [Tooltip("Reference to door mover components.")]
    [SerializeField] private List<AbstractDoorOpener> doorMovers = new List<AbstractDoorOpener>();
    
    
    private DoorRoleDetector doorRoleDetector;
    private bool shouldBeCheckingRoles = false;  // True whenever there is an NPC in the detector zone.
    
    private bool isOpen = false;
    public bool IsOpen => isOpen;
    
    private Vector3 closedPosition;
    private Vector3 openPosition;
    private Vector3 targetPosition;

    private void Awake()
    {
        doorRoleDetector = GetComponent<DoorRoleDetector>();
        
        // We should initialize the door movers here
        // There is a child object that has the name "Movers" that then has children with the "AbstractDoorOpener" component
        // For each of these that are not already in the list, add them to the list
        // We don't want to override the list in case there are extra movers somewhere else
        if (doorMovers.Count == 0)
        {
            AbstractDoorOpener[] movers = GetComponentsInChildren<AbstractDoorOpener>();
            foreach (AbstractDoorOpener mover in movers)
            {
                if (!doorMovers.Contains(mover))
                {
                    doorMovers.Add(mover);
                }
            }
        }
    }

    private void Start()
    {
        doorRoleDetector.OnNpcEnteredZoneEvent += HandleDetectorEnter;
        doorRoleDetector.OnNpcExitedZoneEvent += HandleDetectorExit;
    }
    
    private void OnDestroy()
    {
        doorRoleDetector.OnNpcEnteredZoneEvent -= HandleDetectorEnter;
        doorRoleDetector.OnNpcExitedZoneEvent -= HandleDetectorExit;
    }

    private void OpenDoor()
    {
        // Debug.Log($"{gameObject.name} door opening");

        isOpen = true;
        foreach (AbstractDoorOpener doorMover in doorMovers)
        {
            doorMover.Open();
        }
    }

    private void CloseDoor()
    {
        // Debug.Log($"{gameObject.name} door closing");

        isOpen = false;
        foreach (AbstractDoorOpener doorMover in doorMovers)
        {
            doorMover.Close();
        }
    }

    private bool isInZMinusDirection(Transform target)
    {
        // Returns true if the target is in the -z direction of the door
        Vector3 direction = target.position - transform.position;
        return Vector3.Dot(transform.forward, direction) < 0;
    }
    
    private void CheckShouldBeOpen()
    {
        IEnumerable<NpcContext> detectedNpcs = doorRoleDetector.GetDetectedNpcs();
        bool shouldBeOpen = false;
        foreach (var detectedNpc in detectedNpcs)
        {
            if (CanEnter(detectedNpc))
            {
                shouldBeOpen = true;
                break;
            }
        }
        
        if (!isOpen && shouldBeOpen)
        {
            OpenDoor();
        }
        else if (isOpen && !shouldBeOpen)
        {
            CloseDoor();
        }
    }
    
    private void HandleDetectorEnter(DetectedNpcData detectedNpc)
    {
        // NpcContext context = detectedNpc.NpcContext;
        // Debug.Log($"{context.name} entered the door detector for {gameObject.name}");
        
        CheckShouldBeOpen();
        shouldBeCheckingRoles = true;
    }

    private void HandleDetectorExit(DetectedNpcData detectedNpc)
    {
        // NpcContext context = detectedNpc.NpcContext;
        // Debug.Log($"{context.name} exited the door detector for {gameObject.name}");
        
        CheckShouldBeOpen();
        if (doorRoleDetector.NpcCount == 0)
        {
            shouldBeCheckingRoles = false;
        }
    }

    public bool CanEnter(NpcContext npcContext)
    {
        if (canSneakIn && isOpen)
        {
            // If the door is open, we can sneak in
            return true;
        }
        bool isInMinusZDirection = isInZMinusDirection(npcContext.transform);
        if (isInMinusZDirection && zMinusUnlocked)
        {
            // If the door is in the -z direction and unlocked, we can enter
            return true;
        }
        else if (!isInMinusZDirection && zPlusUnlocked)
        {
            // If the door is in the +z direction and unlocked, we can enter
            return true;
        }
        return npcContext.Identity.HasAnyRole(rolesCanEnter);
    }
    
    public List<NpcRoleSO> GetMissingRoles(NpcContext npcContext)
    {
        if (npcContext.Identity.HasAnyRole(rolesCanEnter))
        {
            // Then you aren't missing anything because you can enter
            return new List<NpcRoleSO>();
        }
        
        // Otherwise you are missing everything because if you had anything you would be let in
        return rolesCanEnter;
    }

    private void Update()
    {
        if (shouldBeCheckingRoles)
        {
            CheckShouldBeOpen();
            if (doorRoleDetector.NpcCount == 0)
            {
                shouldBeCheckingRoles = false;
            }
        }
    }
}
