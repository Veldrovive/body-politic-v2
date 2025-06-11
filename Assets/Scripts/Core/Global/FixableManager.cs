using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Collections;
using System;
using UnityEditor;

class FixingContext
{
    public Fixable Fixable;
    public NpcContext CurrentFixer;
    
    public HashSet<NpcContext> AttemptedFixers = new HashSet<NpcContext>();
    public IEnumerable<NpcRoleSO> AllowedRoles;

    public Action FixedHandler;
    public string StepGUID = null;  // Holds a reference to the step that was added to the fixer.
}

// TODO: Implement save system for Fixable state

[DefaultExecutionOrder(-50)]
public class FixableManager : MonoBehaviour
{
    [Tooltip("The SO used to denote that this NPC will receive fix requests.")]
    [SerializeField] private NpcRoleSO fixerRole;
    
    [Tooltip("The amount of time the action camera should linger on the fixer.")]
    [SerializeField] private float actionCameraDuration = 10f;
    
    private HashSet<NpcContext> inUseFixers = new HashSet<NpcContext>();
    private Dictionary<Fixable, (FixingContext, Coroutine)> currentFixOperations = new Dictionary<Fixable, (FixingContext, Coroutine)>();
    
    public static FixableManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null) {
            Debug.LogError("There is more than one instance!");
            return;
        }
        Instance = this;
    }

    #region Handlers

    /// <summary>
    /// Used to inform the FixableManager that a fixable object has been broken.
    /// This spawns a process that will select a fixer to fix the object
    /// </summary>
    /// <param name="fixable"></param>
    /// <param name="allowedRoles"></param>
    public void NotifyFixableBroken(Fixable fixable, IEnumerable<NpcRoleSO> allowedRoles)
    {
        Debug.Log($"Fix requested for {fixable.name} by {allowedRoles.Count()} fixers.");

        if (currentFixOperations.ContainsKey(fixable))
        {
            Debug.LogWarning($"Fix was called twice for {fixable.name}. This is not allowed.");
            // It's now allowed, but we try to handle it gracefully by restarting the coroutine
            CleanupFixing(fixable);
        }
        
        FixingContext fixingContext = new FixingContext
        {
            Fixable = fixable,
            AllowedRoles = allowedRoles
        };
        Coroutine coroutine = StartCoroutine(FixFixable(fixingContext));
        currentFixOperations.Add(fixable, (fixingContext, coroutine));
    }

    #endregion

    #region Main Logic

    private IEnumerator FixFixable(FixingContext fixingContext)
    {
        yield return null;  // We immediately wait a frame to allow the data structures to be updated
        bool isFixed = false;
        
        Fixable fixable = fixingContext.Fixable;
        IEnumerable<NpcRoleSO> allowedRoles = fixingContext.AllowedRoles;

        fixingContext.FixedHandler = () =>
        {
            Debug.Log($"FixableManager: {fixable.name} was fixed");
            isFixed = true;
        };

        fixable.OnFixed += fixingContext.FixedHandler;

        while (!isFixed)
        {
            // The first step is to find a fixer.
            fixingContext.CurrentFixer = null;
            NpcContext fixer = null;
            int count = 0;
            while (fixer == null)
            {
                // The player might fix the object while we are waiting. We should handle that case.
                if (isFixed) break;
                
                HashSet<NpcContext> disallowedFixers = new HashSet<NpcContext>();
                disallowedFixers.UnionWith(inUseFixers);  // Can't use a fixer that is already doing something
                disallowedFixers.UnionWith(fixingContext.AttemptedFixers);
                fixer = SelectFixer(fixable.PathfindTransform, allowedRoles, disallowedFixers);
                if (fixer == null)
                {
                    yield return new WaitForSeconds(0.5f);  // No need to spam the selection
                
                    if (count >= 20)
                    {
                        // We've tried a lot and not found a fixer. Let's clear the attempted fixers and try again.
                        // This will allow already rejected fixers to have another go at it.
                        fixingContext.AttemptedFixers.Clear();
                    }
                    count++;
                }
            }
            if (isFixed) break;
            fixingContext.CurrentFixer = fixer;

            // We now have a fixer. Let's send them to fix the object.
            fixingContext.StepGUID = TryInterruptFixer(fixer, fixable);
            fixingContext.AttemptedFixers.Add(fixer);  // Ensures we will not try this fixer again if they fail
            if (string.IsNullOrEmpty(fixingContext.StepGUID))
            {
                break;  // As long as isFixed is false, this will restart the loop and try again
            }

            inUseFixers.Add(fixer);
            yield return null;  // Wait a frame to allow the fixer to start the step
            // TODO: Decide if this should instead be HasGraphInQueue so that we don't find a new fixer until
            // the current one has completely removed the step from their queue. Right now when a fixer did something
            // like get distracted by a coin or got infected the fix action would be removed entirely and we would
            // find a new fixer. This is strange behavior. What we should probably do instead is make the fix graph
            // saveable and check for existence in the queue instead of the current state graph.
            while (fixer.StateGraphController.CurrentStateGraph?.id == fixingContext.StepGUID)
            {
                if (isFixed) break;
                
                // The fixer is still executing the step. We should wait for them to finish.
                yield return new WaitForSeconds(0.1f);  // No need to spam the check
            }
            if (isFixed) break;
            
            // At this point either the fixer finished the fix interaction or they were interrupted.
            // if the fix interaction finished, next go-round of the loop isFixed will evaluate to true and we will exit.
            // If they failed, then isFixed will still be false and we will try again.
            yield return new WaitForSeconds(0.1f);  // We wait a bit just to let everything settle in the interaction system.
        }
        
        CleanupFixing(fixable);
    }

    private void CleanupFixing(Fixable fixable)
    {
        if (!currentFixOperations.ContainsKey(fixable))
        {
            Debug.LogWarning($"Tried to cleanup fixing for {fixable.name} but it was not in the list of current fix operations.");
        }
        
        var (fixingContext, coroutine) = currentFixOperations[fixable];
        
        // Clean up the coroutine
        StopCoroutine(coroutine);
        fixingContext.Fixable.OnFixed -= fixingContext.FixedHandler;

        NpcContext fixer = fixingContext.CurrentFixer;
        // Remove this fixable from the cached values
        // Remove from inUseFixers so that this fixer can be used again
        inUseFixers.Remove(fixingContext.CurrentFixer);
        // And remove from the currentFixOperations so we don't try to end this cleanup again
        currentFixOperations.Remove(fixable);
        
        // If the fixer is still pursuing the fixable, we should tell them that they can stop
        if (!string.IsNullOrEmpty(fixingContext.StepGUID))
        {
            // if (fixer.ModeController.ExecutingStep(fixingContext.StepGUID))
            if (fixer.StateGraphController.CurrentStateGraph.id == fixingContext.StepGUID)
            {
                bool interrupted = fixer.StateGraphController.TryProceed();
                if (!interrupted)
                {
                    Debug.LogWarning($"Failed to interrupt fixer {fixer.name} while cleaning up fixing for {fixable.name}. This should not happen.");
                    // Nothing else to be done though, so we can just leave it.
                }
            }
        }
        
    }
    
    #endregion

    #region Helpers

    /// <summary>
    /// Constructs an interrupt to be sent to the fixer
    /// </summary>
    /// <param name="fixer"></param>
    /// <param name="fixable"></param>
    /// <returns>The GUID of the added step</returns>
    private string TryInterruptFixer(NpcContext fixer, Fixable fixable)
    {
        if (fixer.StateGraphController.IdleOnExit)
        {
            // This actually wasn't a valid option as they are currently player controlled
            return null;
        }
        
        // // We need to create a new step for the fixer to follow
        // StepDefinition step = new StepDefinition()
        // {
        //     new MoveToStateConfiguration(fixable.PathfindTransform),
        //     new InteractionStateConfiguration_v1(fixable.GetInteractable(), fixable.FixInteractionDefinition)
        // };
        // InterruptRequestData request = new InterruptRequestData(step);
        // if (!fixer.RoutineController.TryInterrupt(request))
        // {
        //     // The fixer rejected the interrupt. We should not use them.
        //     return null;
        // }
        string graphId = System.Guid.NewGuid().ToString();
        MoveAndUseGraphFactory factory = new MoveAndUseGraphFactory(new MoveAndUseGraphConfiguration()
        {
            GraphId = graphId,
            MoveToTargetTransform = fixable.PathfindTransform,
            RequireExactPosition = true,
            RequireFinalAlignment = true,
            TargetInteractable = fixable.GetInteractable(),
            TargetInteractionDefinition = fixable.FixInteractionDefinition,
            
            ActionCamConfig = new ActionCamSource(graphId, 0, fixer.transform, ActionCameraMode.ThirdPerson, actionCameraDuration)
        });
        if (!fixer.StateGraphController.TryInterrupt(factory, false, false))
        {
            // The fixer rejected the interrupt. We should not use them.
            return null;
        }
        return factory.AbstractConfig.GraphId;
    }

    public List<NpcContext> GetFixers()
    {
        // Gets all of the NPCs that have the fixer role and are doing their routine (not in an interrupt or player controlled)
        // and are not already in use by another fixable object
        return InfectionManager.Instance?.GetNpcsWithAnyRoles(new []{fixerRole})
            .Where(x => x != null && x.StateGraphController.IsInRoutine)
            .Where(x => !inUseFixers.Contains(x))
            .ToList();
    }

    /// <summary>
    /// Selects the best candidate fixer for the target object.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="allowedRoles"></param>
    /// <param name="disallowedFixers"></param>
    /// <returns></returns>
    public NpcContext SelectFixer(Transform target, IEnumerable<NpcRoleSO> allowedRoles, HashSet<NpcContext> disallowedFixers)
    {
        // Finds the nearest allowed fixer that has any of the roles
        float closestDistance = float.MaxValue;
        NpcContext closestFixer = null;
        foreach (var fixer in GetFixers())
        {
            if (disallowedFixers.Contains(fixer)) continue;
            
            if (fixer.Identity.HasAnyRole(allowedRoles))
            {
                float distance = Vector3.Distance(target.position, fixer.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestFixer = fixer;
                }
            }
        }

        return closestFixer;
    }

    #endregion
}
