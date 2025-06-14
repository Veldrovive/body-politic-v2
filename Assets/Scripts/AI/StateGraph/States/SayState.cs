using System;
using UnityEngine;

public enum LogLevel
{
    Info,
    Warning,
    Error
}

[Serializable]
public class SayStateConfiguration : AbstractStateConfiguration
{
    public override Type AssociatedStateType => typeof(SayState);

    public string m_textToSay = "";
    public float m_textDuration = 0;  // How long the text should be visible
    public float m_waitDuration = 0;  // How long to wait before transitioning to the next state
    
    public string m_textToLog = "";
    public LogLevel m_logLevel = LogLevel.Info;
}

public enum SayStateOutcome
{
    Timeout,
}

public class SayState : GenericAbstractState<SayStateOutcome, SayStateConfiguration>
{
    [Tooltip("The text to say.")] [SerializeField]
    private string m_textToSay = "";

    [Tooltip("The duration to say the text.")] [SerializeField]
    private float m_textDuration = 0;

    [Tooltip("The duration to wait before transitioning to the next state.")] [SerializeField]
    private float m_waitDuration = 0;

    [Tooltip("The text to log.")] [SerializeField]
    private string m_textToLog = "";

    [Tooltip("The log level.")] [SerializeField]
    private LogLevel m_logLevel = LogLevel.Info;
    
    private float startTime = -1;
    private string bubbleId = null;

    public override void ConfigureState(SayStateConfiguration configuration)
    {
        m_textToSay = configuration.m_textToSay;
        m_textDuration = configuration.m_textDuration;
        m_waitDuration = configuration.m_waitDuration;
        m_textToLog = configuration.m_textToLog;
        m_logLevel = configuration.m_logLevel;
    }

    public override bool InterruptState()
    {
        if (!string.IsNullOrEmpty(m_textToSay))
        {
            npcContext.SpeechBubbleManager.RemoveBubble(bubbleId);
        }
        // Allow interrupting this state
        return true;
    }

    private void OnEnable()
    {
        // Log the text at the specified log level
        if (!string.IsNullOrEmpty(m_textToLog))
        {
            switch (m_logLevel)
            {
                case LogLevel.Info:
                    Debug.Log(m_textToLog);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(m_textToLog);
                    break;
                case LogLevel.Error:
                    Debug.LogError(m_textToLog);
                    break;
            }
        }
        
        if (!Mathf.Approximately(m_textDuration, 0))
        {
            bubbleId = npcContext.SpeechBubbleManager.ShowBubble(m_textToSay, m_textDuration);
        }
        // else: No point in showing the bubble if the duration is 0

        if (Mathf.Approximately(m_waitDuration, 0))
        {
            // Then we don't need to wait at all
            TriggerExit(SayStateOutcome.Timeout);
            return;
        }
        
        startTime = SaveableDataManager.Instance.time;
    }

    private void Update()
    {
        if (startTime >= 0 && SaveableDataManager.Instance.time - startTime >= m_waitDuration)
        {
            // Wait duration is over, trigger exit
            TriggerExit(SayStateOutcome.Timeout);
        }
    }
}