using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[BlackboardEnum]
public enum SayMultiActionType
{
    Random,
    Sequential,
    NonRepeating
}

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Say Multi", story: "[Self] says one of [Messages]", category: "Action", id: "64d80b619fd1a7c75c6c3167ccde4227")]
public partial class SayMultiAction : SaveableAction
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<List<string>> Messages;
    
    [SerializeReference] public BlackboardVariable<SayMultiActionType> Type;
    [SerializeReference] public BlackboardVariable<ReadingTimeEstimatorSO> DurationEstimator;
    [SerializeReference] public BlackboardVariable<SayActionWaitMode> WaitMode = new(SayActionWaitMode.WaitForBubble);
    [SerializeReference] public BlackboardVariable<float> WaitDuration = new(1f);
    
    // Stores the index of the last message displayed to support Sequential and NonRepeating types.
    [CreateProperty] private int currentMessageIndex = -1;
    
    private float _textDuration = 0f;
    private float _waitTimer;
    private NpcContext _npcContext;

    protected override Status OnLoad()
    {
        if (Messages.Value == null || Messages.Value.Count == 0)
        {
            return Status.Success;
        }
        
        // Select the next message index based on the chosen type.
        if (Type.Value == SayMultiActionType.Sequential)
        {
            // Cycle through messages in order.
            currentMessageIndex = (currentMessageIndex + 1) % Messages.Value.Count;
        }
        else if (Type.Value == SayMultiActionType.Random)
        {
            // Pick any message at random.
            currentMessageIndex = UnityEngine.Random.Range(0, Messages.Value.Count);
        }
        else if (Type.Value == SayMultiActionType.NonRepeating)
        {
            // If there's only one message, just use it.
            if (Messages.Value.Count <= 1)
            {
                currentMessageIndex = 0;
            }
            else
            {
                // Store the previous index to ensure the new one is different.
                int previousIndex = currentMessageIndex;
                
                // Keep picking a new random index until it's different from the previous one.
                do
                {
                    currentMessageIndex = UnityEngine.Random.Range(0, Messages.Value.Count);
                } while (currentMessageIndex == previousIndex);
            }
        }
        
        string message = Messages.Value[currentMessageIndex];
        if (DurationEstimator == null || DurationEstimator.Value == null)
        {
            if (GlobalData.Instance.defaultReadingTimeEstimator == null)
            {
                Debug.LogWarning("No ReadingTimeEstimatorSO set in GlobalData. Skipping SayAction.");
                return Status.Success;
            }
            DurationEstimator = new BlackboardVariable<ReadingTimeEstimatorSO>(GlobalData.Instance.defaultReadingTimeEstimator);
        }
        _textDuration = DurationEstimator.Value.Estimate(message);

        switch (WaitMode.Value)
        {
            case SayActionWaitMode.WaitForBubble:
                _waitTimer = _textDuration;
                break;
            case SayActionWaitMode.WaitForDuration:
                _waitTimer = WaitDuration.Value;
                break;
            case SayActionWaitMode.WaitForBubbleAndDuration:
                _waitTimer = _textDuration + WaitDuration.Value;
                break;
            case SayActionWaitMode.None:
                _waitTimer = 0f;  // No waiting, just show the bubble and return immediately.
                break;
        }
        if (!Self.Value.TryGetComponent<NpcContext>(out _npcContext))
        {
            return Status.Failure;
        }
        _npcContext.SpeechBubbleManager.ShowBubble(message, _textDuration);
        return _waitTimer > 0 ? Status.Running : Status.Success;
    }

    protected override Status OnStart()
    {
        base.OnStart();
        return OnLoad();
    }

    protected override Status OnUpdate()
    {
        base.OnUpdate();
        
        _waitTimer -= Time.deltaTime;
        if (_waitTimer <= 0)
        {
            return Status.Success;
        }
        else
        {
            return Status.Running;
        }
    }
}