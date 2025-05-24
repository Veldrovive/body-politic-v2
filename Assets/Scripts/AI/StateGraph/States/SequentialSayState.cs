using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct SayBubbleData
{
    public string Text;
    public float TextDuration;
    public float WaitDuration;
}

[Serializable]
public class SequentialSayStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(SequentialSayState);

    public List<SayBubbleData> SayBubbleDataList = new List<SayBubbleData>();
}

public enum SequentialSayStateOutcome
{
    Timeout,
}

public class SequentialSayState : GenericAbstractState<SequentialSayStateOutcome, SequentialSayStateConfiguration>
{
    [Tooltip("The text to say.")] [SerializeField]
    private List<SayBubbleData> m_sayBubbleDataList = new List<SayBubbleData>();
    
    private float startTime = -1;
    private int currentIndex = 0;
    private float waitDuration = 0;
    
    private static string SEQUENCE_POSITION_DATA_KEY = "SequencePosition";

    public override void ConfigureState(SequentialSayStateConfiguration configuration)
    {
        m_sayBubbleDataList = configuration.SayBubbleDataList;
    }

    public override bool InterruptState()
    {
        // Allow interrupting this state
        return true;
    }

    private void OnEnable()
    {
        if (m_sayBubbleDataList == null || m_sayBubbleDataList.Count == 0)
        {
            Debug.LogError("No SayBubbleData found. Cannot proceed with SequentialSayState.");
            TriggerExit(SequentialSayStateOutcome.Timeout);
            return;
        }
        
        int index = GetStateData(SEQUENCE_POSITION_DATA_KEY, defaultValue: 0) % m_sayBubbleDataList.Count;
        SayBubbleData data = m_sayBubbleDataList[index];
        currentIndex = index;
        waitDuration = data.WaitDuration;
        
        if (!Mathf.Approximately(data.TextDuration, 0) && !string.IsNullOrEmpty(data.Text))
        {
            npcContext.SpeechBubbleManager.ShowBubble(data.Text, data.TextDuration);
        }
        // else: No point in showing the bubble if the duration is 0

        if (Mathf.Approximately(data.WaitDuration, 0))
        {
            // Then we don't need to wait at all
            SetStateData(SEQUENCE_POSITION_DATA_KEY, currentIndex + 1);
            TriggerExit(SequentialSayStateOutcome.Timeout);
            return;
        }
        
        startTime = Time.time;
    }

    private void Update()
    {
        if (startTime >= 0 && Time.time - startTime >= waitDuration)
        {
            // Wait duration is over, trigger exit
            SetStateData(SEQUENCE_POSITION_DATA_KEY, currentIndex + 1);
            TriggerExit(SequentialSayStateOutcome.Timeout);
        }
    }
}