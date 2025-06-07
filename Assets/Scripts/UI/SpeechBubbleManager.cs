using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[Serializable]
public class SpeechBubbleDefinition
{
    public string ToSay { get; private set; }
    public float Duration { get; private set; }
    
    public bool Immediate { get; private set; }
    public bool ClearQueue { get; private set; }
    
    public Action OnFinishCallback { get; private set; }
    public UnityEngine.Object CallbackContext { get; private set; }

    public SpeechBubbleDefinition(string toSay, float duration, bool immediate=false, bool clearQueue=false, Action onFinishCallback=null, UnityEngine.Object callbackContext=null)
    {
        ToSay = toSay;
        Duration = duration;

        Immediate = immediate;
        ClearQueue = clearQueue;
        
        OnFinishCallback = onFinishCallback;
        CallbackContext = callbackContext;
    }
}

[RequireComponent(typeof(UIDocument))]
public class SpeechBubbleManager : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField] [Tooltip("The UI Document corresponding to the Speech Bubble UI")]
    private UIDocument bubbleUIDocument;

    [SerializeField] [Tooltip("The element to position the bubble on.")]
    private Transform trackedTransform;
    
    [SerializeField]
    [Tooltip("Camera for world-to-screen conversions. Defaults to Camera.main if unassigned.")]
    private Camera viewCamera;

    [SerializeField] [Tooltip("The time it takes to transition in a bubble.")]
    private float transitionInTime = 0.4f;

    [SerializeField] [Tooltip("The time it takes to transition out a bubble.")]
    private float transitionOutTime = 0.2f;

    [SerializeField] [Tooltip("The time between bubbles.")]
    private float betweenBubbleTime = 0.2f;
    
    [SerializeField] [Tooltip("Screen space offset for the bubble.")]
    private Vector2 screenSpaceOffset = new Vector2(0, 0);

    #endregion
    
    #region Internal Fields

    private VisualElement rootElement;
    private VisualElement mainContainer;
    private Label bubbleTextLabel;

    private Queue<SpeechBubbleDefinition> bubbleQueue = new Queue<SpeechBubbleDefinition>(); // Initialize the queue
    private Coroutine currentBubbleCoroutine = null; // Holds the reference to the active speech coroutine
    private Action currentOnFinishCallback = null;
    private UnityEngine.Object currentCallbackContext = null;
    
    private IPanel cachedPanel; // Cached panel reference for coordinate conversions

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }

        // Validate essential components
        if (viewCamera == null)
        {
            Debug.LogError("WSCMM: View Camera is missing and Camera.main could not be found!", this);
            enabled = false; // Disable script if camera is missing
            return;
        }
        
        if (bubbleUIDocument == null)
        {
            Debug.LogError("Parasite Speech UI Document is not assigned.", this);
            return;
        }

        rootElement = bubbleUIDocument.rootVisualElement;
        // Ensure that nothing here can be interacted with
        rootElement.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        
        // Ensure the element exists and is a Label
        bubbleTextLabel = rootElement.Q<Label>("SpeechText");
        if (bubbleTextLabel == null)
        {
            Debug.LogError("SpeechText Label element not found in the UI Document.", this);
            return;
        }
        
        mainContainer = rootElement.Q<VisualElement>("MainContainer");
        if (mainContainer == null)
        {
            Debug.LogError("MainContainer element not found in the UI Document.", this);
            return;
        }

        // Start with the text invisible
        bubbleTextLabel.style.opacity = 0;
    }

    private void LateUpdate()
    {
        if (currentBubbleCoroutine == null)
        {
            // No speech is currently being displayed so no reason to update the position
            return;
        }
        // Update the position of the speech bubble to follow the tracked transform
        Vector2? panelPosition = ConvertWorldToPanelPosition(trackedTransform.position);
        if (panelPosition.HasValue)
        {
            // Set the position of the speech bubble
            rootElement.transform.position = panelPosition.Value + screenSpaceOffset;
            // Debug.Log($"Speech Bubble Manager: Position updated to {panelPosition.Value + screenSpaceOffset} ({Time.time})", this);
        }
        else
        {
            // Debug.LogWarning("Speech Bubble Manager: Unable to convert world position to panel position. The speech bubble will not be displayed.");
        }
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
    public void ShowBubble(string toSay, float duration, bool immediate = false, bool clearQueue = false, Action onFinishCallback = null, UnityEngine.Object callbackContext = null)
    {
        SpeechBubbleDefinition speechDefinition = new SpeechBubbleDefinition(toSay, duration, immediate, clearQueue, onFinishCallback, callbackContext);

        if (immediate)
        {
            // Stop any currently running speech display
            if (currentBubbleCoroutine != null)
            {
                StopCoroutine(currentBubbleCoroutine);
                currentBubbleCoroutine = null; // Clear the reference
            }

            // Optionally clear the queue
            if (clearQueue)
            {
                bubbleQueue.Clear();
            }

            // If we force stopped the coroutine, it didn't call the currentOnFinishCallback so we need to do it here
            // before we start the next one
            // NOTE: This is not true. If the currentOnFinishCallback call actually causes a ShowBubble call then we
            // will cause a bug where we call the exit twice. To fix this we null currentOnFinishCallback early in DisplayBubbleCoroutine
            if (currentOnFinishCallback != null && currentCallbackContext != null)
            {
                try
                {
                    currentOnFinishCallback();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error calling OnFinishCallback: {e.Message}", this);
                }
            }
            currentOnFinishCallback = null;
            
            // Start displaying the new speech immediately
            currentBubbleCoroutine = StartCoroutine(DisplayBubbleCoroutine(speechDefinition));
        }
        else
        {
            // Add the speech to the queue
            bubbleQueue.Enqueue(speechDefinition);

            // If nothing is currently playing, start the process
            if (currentBubbleCoroutine == null)
            {
                TryProcessNextSpeech();
            }
        }
    }

    public void ShowBubble(SpeechBubbleDefinition bubbleDefinition)
    {
        ShowBubble(
            bubbleDefinition.ToSay,
            bubbleDefinition.Duration,
            bubbleDefinition.Immediate,
            bubbleDefinition.ClearQueue
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
        if (currentBubbleCoroutine == null && bubbleQueue.Count > 0)
        {
            SpeechBubbleDefinition nextSpeech = bubbleQueue.Dequeue();
            currentBubbleCoroutine = StartCoroutine(DisplayBubbleCoroutine(nextSpeech));
        }
    }

    /// <summary>
    /// Coroutine to handle the display lifecycle of a single bubble item: transition in, hold, transition out.
    /// </summary>
    /// <param name="bubble">The SpeechBubbleDefinition to display.</param>
    private IEnumerator DisplayBubbleCoroutine(SpeechBubbleDefinition bubble)
    {
        currentOnFinishCallback = bubble.OnFinishCallback;
        currentCallbackContext = bubble.CallbackContext;
        // Debug.Log($"SpeechBubbleManager: Showing speech bubble with text: {bubble.ToSay}, duration: {bubble.Duration}, immediate: {bubble.Immediate}, clearQueue: {bubble.ClearQueue}", this);
        // Set the text content
        bubbleTextLabel.text = bubble.ToSay;
        // Make sure the element is visible if it was hidden (optional, depends on Start() setup)
        // rootElement.style.display = DisplayStyle.Flex;
        
        rootElement.transform.position = Vector2.zero; // Reset position to avoid showing the bubble at the wrong place. There is something internally wrong with % translates when the size changes.

        // --- Fade In ---
        float elapsedTime = 0f;
        // Start with opacity 0 if not already (e.g., if interrupted during fade out)
        bubbleTextLabel.style.opacity = 0;
        while (elapsedTime < transitionInTime)
        {
            elapsedTime += Time.deltaTime;
            // Calculate opacity based on elapsed time
            float currentOpacity = Mathf.Clamp01(elapsedTime / transitionInTime);
            bubbleTextLabel.style.opacity = currentOpacity;
            // Wait for the next frame
            yield return null;
        }
        // Ensure opacity is exactly 1 at the end of fade in
        bubbleTextLabel.style.opacity = 1f;

        // --- Hold Duration ---
        // Wait for the specified duration
        yield return new WaitForSeconds(bubble.Duration);

        // --- Fade Out ---
        elapsedTime = 0f;
        while (elapsedTime < transitionOutTime)
        {
            elapsedTime += Time.deltaTime;
            // Calculate opacity fading from 1 to 0
            float currentOpacity = 1f - Mathf.Clamp01(elapsedTime / transitionOutTime);
            bubbleTextLabel.style.opacity = currentOpacity;
            // Wait for the next frame
            yield return null;
        }
        // Ensure opacity is exactly 0 at the end of fade out
        bubbleTextLabel.style.opacity = 0f;
        // Clear the text after fading out
        bubbleTextLabel.text = "";
        // Optionally hide the element again (depends on Start() setup)
        // rootElement.style.display = DisplayStyle.None;

        // --- Between Speech Delay ---
        // Wait before processing the next speech item
        yield return new WaitForSeconds(betweenBubbleTime);

        // --- Cleanup and Next ---
        // Mark the coroutine as finished
        var cachedCallback = currentOnFinishCallback;
        var cachedCallbackContext = currentCallbackContext;
        currentOnFinishCallback = null;
        currentCallbackContext = null;
        if (cachedCallback != null && cachedCallbackContext != null)
        {
            try
            {
                cachedCallback();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error calling OnFinishCallback: {e.Message}", this);
            }
        }
        
        // Check if there's more speech in the queue
        yield return null;
        currentBubbleCoroutine = null;
        TryProcessNextSpeech();
    }

    #endregion
    
    #region Coordinate Helpers

    /// <summary>
    /// Gets the IPanel associated with the UIDocument, caching it for efficiency.
    /// </summary>
    /// <returns>The IPanel interface, or null if unavailable.</returns>
    private IPanel GetPanel()
    {
        // Return cached panel if available
        if (cachedPanel == null && bubbleUIDocument?.rootVisualElement != null)
        {
            // Cache the panel reference the first time it's needed
            cachedPanel = bubbleUIDocument.rootVisualElement.panel;
        }
        return cachedPanel;
    }

    /// <summary>
    /// Converts a world-space position to a UI Toolkit panel position (origin top-left).
    /// Handles points behind the camera.
    /// </summary>
    /// <param name="worldPos">The world-space position to convert.</param>
    /// <returns>The corresponding panel position (Vector2), or null if conversion fails or point is behind camera.</returns>
    private Vector2? ConvertWorldToPanelPosition(Vector3 worldPos)
    {
        if (viewCamera == null) return null;
        IPanel panel = GetPanel();
        if (panel == null) return null;

        // Convert world position to screen coordinates (pixels, origin bottom-left)
        // screenPoint3D.z is distance from camera plane
        Vector3 screenPoint3D = viewCamera.WorldToScreenPoint(worldPos);

        // Check if the point is behind the camera's near clipping plane
        if (screenPoint3D.z < viewCamera.nearClipPlane)
        {
            return null; // Point is behind the camera, not visible on screen
        }

        // Convert screen coordinates (Vector2 part) to panel coordinates (origin bottom-left)
        // This accounts for panel scaling and offset within the screen.
        Vector2 panelPosBottomOrigin = RuntimePanelUtils.ScreenToPanel(panel, screenPoint3D);

        // Get the actual resolved height of the panel's root visual element
        float panelHeight = panel.visualTree.resolvedStyle.height;

        // Fallback to camera pixel height if panel height is invalid (e.g., during initial layout, or if panel covers whole screen)
        if (float.IsNaN(panelHeight) || float.IsInfinity(panelHeight) || panelHeight <= 0)
        {
            panelHeight = viewCamera.pixelHeight; // Use camera's render target height
            if (panelHeight <= 0) return null; // Cannot determine height
        }

        // Convert Y coordinate from bottom-left origin to top-left origin
        // Panel Y (top-left) = Panel Height - Panel Y (bottom-left)
        return new Vector2(panelPosBottomOrigin.x, panelHeight - panelPosBottomOrigin.y);
    }

    #endregion
}
