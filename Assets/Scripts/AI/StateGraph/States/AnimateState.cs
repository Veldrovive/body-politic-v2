using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class AnimateStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(AnimateState);

    [SerializeField] public string animationName;
    [SerializeField] public float duration;
    [SerializeField] public bool endAnimationOnComplete = true;
    [SerializeField] public bool endAnimationOnInterrupt = true;
    
    public AnimateStateConfiguration()
    {
        animationName = string.Empty;
        duration = 0f;
        endAnimationOnComplete = true;
        endAnimationOnInterrupt = true;
    }
    
    public AnimateStateConfiguration(string animationName, float duration, bool endAnimationOnComplete, bool endAnimationOnInterrupt)
    {
        this.animationName = animationName;
        this.duration = duration;
        this.endAnimationOnComplete = endAnimationOnComplete;
        this.endAnimationOnInterrupt = endAnimationOnInterrupt;
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
    [Tooltip("Whether to end the animation when exiting this state.")]
    [SerializeField] private bool endAnimationOnExit;
    [Tooltip("Whether to end the animation when interrupting this state.")]
    [SerializeField] private bool endAnimationOnInterrupt;
    

    private float startTime;

    public override void ConfigureState(AnimateStateConfiguration configuration)
    {
        animationName = configuration.animationName;
        duration = configuration.duration;
        endAnimationOnExit = configuration.endAnimationOnComplete;
        endAnimationOnInterrupt = configuration.endAnimationOnInterrupt;
    }
    
    public override bool InterruptState()
    {
        // Allow interrupting this state
        if (!string.IsNullOrEmpty(animationName) && endAnimationOnInterrupt)
        {
            // End the animation if specified
            npcContext.AnimationManager.End();
        }
        return true;
    }

    private void OnEnable()
    {
        startTime = SaveableDataManager.Instance.time;
        if (!string.IsNullOrEmpty(animationName))
        {
            npcContext.AnimationManager.Play(animationName);
        }
        // else: This is basically just a WaitState
    }

    private void Update()
    {
        if (SaveableDataManager.Instance.time - startTime >= duration)
        {
            // Transition to the next state
            if (!string.IsNullOrEmpty(animationName) && endAnimationOnExit)
            {
                npcContext.AnimationManager.End();
            }
            TriggerExit(AnimateStateOutcome.Timeout);
        }
    }
}