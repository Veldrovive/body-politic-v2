using System;
using Unity.VisualScripting;
using UnityEngine;

[Serializable]
public class AnimateStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(AnimateState);

    [SerializeField] public string animationName;
    [SerializeField] public float duration;
    
    public AnimateStateConfiguration()
    {
        animationName = string.Empty;
        duration = 0f;
    }
    
    public AnimateStateConfiguration(string animationName, float duration)
    {
        this.animationName = animationName;
        this.duration = duration;
    }
}

public enum AnimateStateOutcome
{
    Timeout,
}

public class AnimateState : GenericAbstractState<AnimateStateOutcome, AnimateStateConfiguration>
{
    [Tooltip("The name of the animation to play.")]
    [SerializeField] private string animationName;
    [Tooltip("The duration to wait before transitioning to the next state.")]
    [SerializeField] private float duration;
    

    private float startTime;

    public override void ConfigureState(AnimateStateConfiguration configuration)
    {
        animationName = configuration.animationName;
        duration = configuration.duration;
    }
    
    public override bool InterruptState()
    {
        // Allow interrupting this state
        return true;
    }

    private void OnEnable()
    {
        startTime = Time.time;
        if (!string.IsNullOrEmpty(animationName))
        {
            npcContext.AnimationManager.Play(animationName);
        }
        // else: This is basically just a WaitState
    }

    private void Update()
    {
        if (Time.time - startTime >= duration)
        {
            // Transition to the next state
            TriggerExit(AnimateStateOutcome.Timeout);
        }
    }

    private void OnDisable()
    {
        // In any case, when the state is disabled, we tell the animator to stop playing the animation if it's playing
        if (!string.IsNullOrEmpty(animationName))
        {
            npcContext.AnimationManager.End(animationName);
        }
    }
}