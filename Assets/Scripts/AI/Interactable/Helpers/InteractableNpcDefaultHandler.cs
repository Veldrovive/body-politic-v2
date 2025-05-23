using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A interaction reactor that sets a boolean value on part of the interactable lifecycle
/// </summary>
public class InteractableNpcDefaultHandler : AbstractInteractionReactor
{
    [SerializeField] private InteractionDefinitionSO infectNpcInteractionDefinition;
    [SerializeField] private InteractionDefinitionSO shootNpcInteractionDefinition;

    [Tooltip("If the shot NPC is above this suspicion threshold, the shooter will not be suspicious.")]
    [SerializeField] private float shooterSuspicionThreshold = 5f;
    [Tooltip("If the shot NPC is below the suspicion threshold, the shooter will be assigned this suspicion value.")]
    [SerializeField] private int shooterSuspicionValue = 9;
    [Tooltip("The suspicion from shooting will last this long.")]
    [SerializeField] private float shooterSuspicionDuration = 20f;

    private NpcContext npcContext;

    private void Initialize()
    {
        bool hasDefinitions = true;
        if (infectNpcInteractionDefinition == null)
        {
            Debug.LogWarning("InteractableNpcDefaultHandler requires an infect interaction definition.", this);
            hasDefinitions = false;
        } else if (!HasInteractionInstanceFor(infectNpcInteractionDefinition))
        {
            Debug.LogWarning($"InteractableNpcDefaultHandler requires an interaction instance for {infectNpcInteractionDefinition.name}.", this);
            hasDefinitions = false;
        }
        
        if (shootNpcInteractionDefinition == null)
        {
            Debug.LogWarning("InteractableNpcDefaultHandler requires a shoot interaction definition.", this);
            hasDefinitions = false;
        } else if (!HasInteractionInstanceFor(shootNpcInteractionDefinition))
        {
            Debug.LogWarning($"InteractableNpcDefaultHandler requires an interaction instance for {shootNpcInteractionDefinition.name}.", this);
            hasDefinitions = false;
        }
        
        npcContext = GetComponent<NpcContext>();
        if (npcContext == null)
        {
            Debug.LogWarning("InteractableNpcDefaultHandler requires a NpcContext on the interactable component.", this);
            hasDefinitions = false;
        }


        if (hasDefinitions)
        {
            // Set up infection handlers
            // If the Npc is already infected, we can disable the infect interaction
            if (InfectionManager.Instance != null && InfectionManager.Instance.IsNpcInfected(npcContext))
            {
                SetInteractionEnabled(infectNpcInteractionDefinition, false, true, "Already infected.");
            }
            else
            {
                SetInteractionEnabled(infectNpcInteractionDefinition, true);
            }

            if (InfectionManager.Instance != null)
            {
                InfectionManager.Instance.OnNpcInfected += (infectedNpc) =>
                {
                    if (infectedNpc == npcContext)
                    {
                        // Disable the interaction so it can't be used again
                        SetInteractionEnabled(infectNpcInteractionDefinition, false, true, "Already infected.");
                    }
                };
            }
            
            SafelyRegisterInteractionLifecycleCallback(
                InteractionLifecycleEvent.OnEnd, infectNpcInteractionDefinition,
                HandleNpcInfected  // Inform the infection manager of infection
            );
            SafelyRegisterInteractionLifecycleCallback(
                InteractionLifecycleEvent.OnInterrupted, infectNpcInteractionDefinition,
                HandleNpcEscapedInfection  // Interrupt into state to run and find guard
            );
            SafelyRegisterInteractionLifecycleCallback(
                InteractionLifecycleEvent.OnStart, infectNpcInteractionDefinition,
                HandleNpcInfectionStart  // Cause noise
            );
            
            
            // Set up shooting handlers
            // Anyone can be shot at any time
            SetInteractionEnabled(shootNpcInteractionDefinition, true);
            SafelyRegisterInteractionLifecycleCallback(
                InteractionLifecycleEvent.OnEnd, shootNpcInteractionDefinition,
                HandleNpcShooting
            );
        }
    }

    private void OnValidate()
    {
        Initialize();
    }

    private void Start()
    {
        Initialize();
    }


    #region Handlers
    
    /// <summary>
    /// This is called when the NPC has been successfully infected. It informs the InfectionManager of the infection
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void HandleNpcInfected(InteractionContext context)
    {
        if (!context.InteractableComponent.gameObject.TryGetComponent(out NpcContext shotNpcContext))
        {
            // This somehow wasn't an NPC. Something very weird happened. This component should be targetting
            // InteractableNpcs so the InteractableComponent should be an InteractableNpc and therefore have a npcContext
            Debug.LogWarning("InteractableNpcDefaultHandler requires a NpcContext on the interactable component.", this);
            return;
        }
        InfectionManager.Instance.NotifyInfection(shotNpcContext);
    }

    /// <summary>
    /// When an infection is interrupted we interrupt the targetted NPC into a state where they run away and try to
    /// find a guard. If the guard sees you within a certain amount of time, they will be highly suspicious of you.
    /// </summary>
    /// <param name="context"></param>
    private void HandleNpcEscapedInfection(InteractionContext context)
    {
        Debug.LogWarning("HandleNpcEscapedInfection is not implemented yet.");
    }

    /// <summary>
    /// When a forcible infection like this starts we make a loud noise at high suspicion so we will draw the attention
    /// of nearby NPCs.
    /// </summary>
    /// <param name="context"></param>
    private void HandleNpcInfectionStart(InteractionContext context)
    {
        Debug.LogWarning("HandleNpcInfectionStart is not implemented yet.");
    }

    /// <summary>
    /// When an NPC is shot we need to do a check to see if people should get suspicious of the shooter.
    /// Oh and then the shot NPC should die.
    /// </summary>
    /// <param name="context"></param>
    private void HandleNpcShooting(InteractionContext context)
    {
        if (!context.InteractableComponent.gameObject.TryGetComponent(out NpcContext shotNpcContext))
        {
            // This somehow wasn't an NPC. Something very weird happened. This component should be targetting
            // InteractableNpcs so the InteractableComponent should be an InteractableNpc and therefore have a npcContext
            Debug.LogWarning("InteractableNpcDefaultHandler requires a NpcContext on the interactable component.", this);
            return;
        }

        if (shotNpcContext.SuspicionTracker.CurrentSuspicionLevel < shooterSuspicionThreshold)
        {
            // Then they did not deserve to get shot. The shooter is now very suspicious.
            if (!context.Initiator.TryGetComponent(out NpcContext shooterNpcContext))
            {
                // Again, somehow this NPC got shot by something that wasn't an NPC. This one is straight up impossible.
                Debug.LogWarning("InteractableNpcDefaultHandler requires a NpcContext on the initiator.", this);
            }
            else
            {
                // Then we need to add suspicion to the shooter
                shooterNpcContext.SuspicionTracker.AddSuspicionSource(
                    "ShotNpc",
                    shooterSuspicionValue,
                    shooterSuspicionDuration
                );
            }
        }
        
        // In any case, the shooting happened so the shotNpcContext should now die.
        shotNpcContext.TriggerDeath();
    }

    #endregion
}