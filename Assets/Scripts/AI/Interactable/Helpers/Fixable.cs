using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// TODO: Implement save system for Fixable state

public class Fixable : AbstractInteractionReactor
{
    #region Serialized Fields

    [Tooltip("The interaction definition that will be used to break this object.")]
    [SerializeField] private InteractionDefinitionSO breakInteractionDefinition;
    
    [Tooltip("The interaction definition that will be used to fix this object.")]
    [SerializeField] private InteractionDefinitionSO fixInteractionDefinition;

    public InteractionDefinitionSO FixInteractionDefinition => fixInteractionDefinition;
    
    [Tooltip("These interactions can only be taken when the object is not broken.")]
    [SerializeField] private InteractionDefinitionSO[] nonBrokenInteractions;
    
    [Tooltip("The transform to pathfind to when the object is broken.")]
    [SerializeField] private Transform pathfindTransform;
    public Transform PathfindTransform => pathfindTransform;
    
    #endregion
    
    #region Internal Fields

    private bool areDefinitionsValid = false;
    private FixableManager manager;
    private bool isBroken = false;
    
    #endregion

    #region Events

    public event Action OnFixed;
    public event Action OnBroken;

    
    #endregion
    
    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();
        
        manager = FixableManager.Instance;
        if (manager == null)
        {
            Debug.LogError("FixableManager instance not found. Please ensure it is present in the scene.", this);
            return;
        }
    }

    private void Initialize()
    {
        areDefinitionsValid = true;
        // Make sure that the Interactable has the break and fix interactions
        if (fixInteractionDefinition == null)
        {
            Debug.LogWarning($"{name} requires a fix interaction definition.", this);
            areDefinitionsValid = false;
        }
        else if (!HasInteractionInstanceFor(fixInteractionDefinition))
        {
            // The associated interactable does not have the fix interaction
            Debug.LogWarning($"{name} requires a fix interaction instance for {fixInteractionDefinition.name}.", this);
            areDefinitionsValid = false;
        }
        
        if (breakInteractionDefinition == null)
        {
            Debug.LogWarning($"{name} requires a break interaction definition.", this);
            areDefinitionsValid = false;
        }
        else if (!HasInteractionInstanceFor(breakInteractionDefinition))
        {
            // The associated interactable does not have the break interaction
            Debug.LogWarning($"{name} requires a break interaction instance for {breakInteractionDefinition.name}.", this);
            areDefinitionsValid = false;
        }

        if (areDefinitionsValid)
        {
            // Then we will connect up the events
            SafelyRegisterInteractionLifecycleCallback(InteractionLifecycleEvent.OnEnd, breakInteractionDefinition, Break);
            SafelyRegisterInteractionLifecycleCallback(InteractionLifecycleEvent.OnEnd, fixInteractionDefinition, Fix);
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        Initialize();
    }

    private void Start()
    {
        SetIsBroken(false);
        Initialize();
    }

    #endregion
    
    #region Interactable Event Handlers

    /// <summary>
    /// Called when the break interaction completes. Uses the FixableManager singleton to request somebody
    /// who can complete the fix interaction come and do that.
    /// </summary>
    public void Break(InteractionContext interactionContext)
    {
        if (isBroken)
        {
            Debug.LogWarning($"Fixable {name} had break called when it was already broken!", this);
            return;
        }
        
        Debug.Log($"{name} is broken!", this);
        
        SetIsBroken(true);
        IEnumerable<NpcRoleSO> fixerRoles = fixInteractionDefinition.RolesCanExecuteNoSuspicion;
        if (!fixerRoles.Any())
        {
            Debug.LogWarning($"Fixable {name} has no fixer roles defined for the fix interaction! It will not automatically be fixed.", this);
            return;
        }
        manager.NotifyFixableBroken(this, fixerRoles);
        OnBroken?.Invoke();
    }

    
    /// <summary>
    /// Called when the fix interaction completes.
    /// </summary>
    public void Fix(InteractionContext interactionContext)
    {   
        if (!isBroken)
        {
            Debug.LogWarning($"Fixable {name} had fix called when it was not broken!", this);
            return;
        }
        
        Debug.Log($"{name} is fixed!", this);
        
        SetIsBroken(false);
        OnFixed?.Invoke();
    }

    #endregion
    
    #region Helpers

    public void SetIsBroken(bool newIsBroken)
    {
        isBroken = newIsBroken;
        SetInteractionEnabled(fixInteractionDefinition, isBroken);
        SetInteractionEnabled(breakInteractionDefinition, !isBroken);
        foreach (var interaction in nonBrokenInteractions)
        {
            SetInteractionEnabled(interaction, !isBroken);
        }
    }
    
    #endregion
}
