using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[Serializable]
public class ParasiteSpeechDefinition
{
    public string ToSay { get; private set; }
    public float Duration { get; private set; }
    
    public bool Immediate { get; private set; }
    public bool ClearQueue { get; private set; }

    public ParasiteSpeechDefinition(string toSay, float duration, bool immediate=false, bool clearQueue=false)
    {
        ToSay = toSay;
        Duration = duration;

        Immediate = immediate;
        ClearQueue = clearQueue;
    }
}


[RequireComponent(typeof(UIDocument))]
public class ParasiteSpeechUIManager : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField] [Tooltip("The UI Document corresponding to the Parasite Speech UI")]
    private UIDocument parasiteSpeechUIDocument;

    [SerializeField] [Tooltip("The time it takes to fade in speech.")]
    private float fadeInTime = 0.4f;

    [SerializeField] [Tooltip("The time it takes to fade out speech.")]
    private float fadeOutTime = 0.2f;

    [SerializeField] [Tooltip("The time between speech.")]
    private float betweenSpeechTime = 0.2f;

    #endregion

    #region Internal Fields

    private VisualElement rootElement;
    private Label speechTextLabel;

    private Queue<ParasiteSpeechDefinition> speechQueue = new Queue<ParasiteSpeechDefinition>(); // Initialize the queue
    private Coroutine currentSpeechCoroutine = null; // Holds the reference to the active speech coroutine

    #endregion

    public static ParasiteSpeechUIManager Instance { get; private set; }

    #region Unity Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("There is more than one instance of ParasiteSpeechUIManager!");
            Destroy(gameObject); // Destroy duplicate instance
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (parasiteSpeechUIDocument == null)
        {
            Debug.LogError("Parasite Speech UI Document is not assigned.", this);
            return;
        }

        rootElement = parasiteSpeechUIDocument.rootVisualElement;
        // Ensure that nothing here can be interacted with
        rootElement.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        
        // Ensure the element exists and is a Label
        speechTextLabel = rootElement.Q<Label>("SpeechText");
        if (speechTextLabel == null)
        {
            Debug.LogError("SpeechText Label element not found in the UI Document.", this);
            return;
        }

        // Start with the text invisible
        speechTextLabel.style.opacity = 0;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Enqueues speech to be shown. Speech will follow the sequence it was enqueued by default.
    /// If immediate is true, the current speech (if any) is interrupted, the queue may optionally be cleared, and the new speech is shown immediately.
    /// </summary>
    /// <param name="toSay">The text content to display.</param>
    /// <param name="duration">How long the text should remain fully visible.</param>
    /// <param name="immediate">Shows the new speech immediately, interrupting any current speech and potentially clearing the queue.</param>
    /// <param name="clearQueue">If true and immediate is true, clears any pending speech in the queue.</param>
    public void ShowSpeech(string toSay, float duration, bool immediate = false, bool clearQueue = false)
    {
        ParasiteSpeechDefinition speechDefinition = new ParasiteSpeechDefinition(toSay, duration, immediate, clearQueue);

        if (immediate)
        {
            // Stop any currently running speech display
            if (currentSpeechCoroutine != null)
            {
                StopCoroutine(currentSpeechCoroutine);
                currentSpeechCoroutine = null; // Clear the reference
            }

            // Optionally clear the queue
            if (clearQueue)
            {
                speechQueue.Clear();
            }

            // Start displaying the new speech immediately
            currentSpeechCoroutine = StartCoroutine(DisplaySpeechCoroutine(speechDefinition));
        }
        else
        {
            // Add the speech to the queue
            speechQueue.Enqueue(speechDefinition);

            // If nothing is currently playing, start the process
            if (currentSpeechCoroutine == null)
            {
                TryProcessNextSpeech();
            }
        }
    }

    public void ShowSpeech(ParasiteSpeechDefinition speechDefinition)
    {
        ShowSpeech(
            speechDefinition.ToSay,
            speechDefinition.Duration,
            speechDefinition.Immediate,
            speechDefinition.ClearQueue
        );
    }
    
    #endregion

    #region Private Methods

    /// <summary>
    /// Checks the queue and starts displaying the next speech item if available and nothing is currently running.
    /// </summary>
    private void TryProcessNextSpeech()
    {
        // Ensure no coroutine is running and there's speech waiting
        if (currentSpeechCoroutine == null && speechQueue.Count > 0)
        {
            ParasiteSpeechDefinition nextSpeech = speechQueue.Dequeue();
            currentSpeechCoroutine = StartCoroutine(DisplaySpeechCoroutine(nextSpeech));
        }
    }

    /// <summary>
    /// Coroutine to handle the display lifecycle of a single speech item: fade in, hold, fade out.
    /// </summary>
    /// <param name="speech">The ParasiteSpeechDefinition to display.</param>
    private IEnumerator DisplaySpeechCoroutine(ParasiteSpeechDefinition speech)
    {
        // Set the text content
        speechTextLabel.text = speech.ToSay;
        // Make sure the element is visible if it was hidden (optional, depends on Start() setup)
        // rootElement.style.display = DisplayStyle.Flex;

        // --- Fade In ---
        float elapsedTime = 0f;
        // Start with opacity 0 if not already (e.g., if interrupted during fade out)
        speechTextLabel.style.opacity = 0;
        while (elapsedTime < fadeInTime)
        {
            elapsedTime += Time.deltaTime;
            // Calculate opacity based on elapsed time
            float currentOpacity = Mathf.Clamp01(elapsedTime / fadeInTime);
            speechTextLabel.style.opacity = currentOpacity;
            // Wait for the next frame
            yield return null;
        }
        // Ensure opacity is exactly 1 at the end of fade in
        speechTextLabel.style.opacity = 1f;

        // --- Hold Duration ---
        // Wait for the specified duration
        yield return new WaitForSeconds(speech.Duration);

        // --- Fade Out ---
        elapsedTime = 0f;
        while (elapsedTime < fadeOutTime)
        {
            elapsedTime += Time.deltaTime;
            // Calculate opacity fading from 1 to 0
            float currentOpacity = 1f - Mathf.Clamp01(elapsedTime / fadeOutTime);
            speechTextLabel.style.opacity = currentOpacity;
            // Wait for the next frame
            yield return null;
        }
        // Ensure opacity is exactly 0 at the end of fade out
        speechTextLabel.style.opacity = 0f;
        // Clear the text after fading out
        speechTextLabel.text = "";
        // Optionally hide the element again (depends on Start() setup)
        // rootElement.style.display = DisplayStyle.None;

        // --- Between Speech Delay ---
        // Wait before processing the next speech item
        yield return new WaitForSeconds(betweenSpeechTime);

        // --- Cleanup and Next ---
        // Mark the coroutine as finished
        currentSpeechCoroutine = null;
        // Check if there's more speech in the queue
        TryProcessNextSpeech();
    }

    #endregion
}