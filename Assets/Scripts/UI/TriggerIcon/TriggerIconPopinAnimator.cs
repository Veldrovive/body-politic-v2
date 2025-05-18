using UnityEngine;

/// <summary>
/// Animates the GameObject's position and sprite opacity on initialization (pop-in) relative to a target Transform,
/// continues to follow the target after pop-in, and handles a fade-out animation before self-destructing.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))] // Ensures a SpriteRenderer exists
public class TriggerIconPopInAnimator : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The Transform that this icon should follow.")]
    [SerializeField] private Transform targetTransform; // Exposed in inspector for optional initial assignment or debugging

    [Header("Pop-In Animation")]
    [Tooltip("Duration of the pop-in animation in seconds.")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [Tooltip("How far below the target position the icon starts (in world units).")]
    [SerializeField] private float fadeInVerticalOffset = 0.2f;
    [Tooltip("Starting opacity for pop-in (0 = transparent, 1 = opaque).")]
    [Range(0f, 1f)]
    [SerializeField] private float fadeInStartOpacity = 0.1f;
    [Tooltip("Easing function to use for the pop-in animation.")]
    [SerializeField] private EaseType fadeInEasing = EaseType.EaseOutQuad;

    [Header("Fade-Out Animation")]
    [Tooltip("Duration of the fade-out animation in seconds.")]
    [SerializeField] private float fadeOutDuration = 0.2f;
    [Tooltip("How far upwards the icon moves during fade-out (relative to its position when fade-out starts). 0 for no movement.")]
    [SerializeField] private float fadeOutVerticalOffset = 0.1f;
    [Tooltip("Target opacity for fade-out (usually 0).")]
    [Range(0f, 1f)]
    [SerializeField] private float fadeOutEndOpacity = 0.0f;
    [Tooltip("Easing function to use for the fade-out animation.")]
    [SerializeField] private EaseType fadeOutEasing = EaseType.Linear;


    // Enum defining different easing options
    public enum EaseType { Linear, EaseOutQuad, SmoothStep }

    // --- Private State ---
    private SpriteRenderer _spriteRenderer;
    private Color _initialColor; // Stores the original color from the SpriteRenderer (used as fade-in target alpha)
    private Color _fadeOutStartColor; // Color at the moment fade-out begins

    // Animation State
    private Vector3 _fadeInStartPosition; // Calculated starting world position for pop-in (based on initial target pos)
    private float _fadeInElapsedTime = 0f;
    private bool _isFadingIn = false;

    private Vector3 _fadeOutStartPosition; // Position when fade-out starts
    private Vector3 _fadeOutTargetPosition; // Calculated target position for fade-out animation
    private float _fadeOutElapsedTime = 0f;
    private bool _isFadingOut = false;


    /// <summary>
    /// Standard Unity function called when the script instance is being loaded.
    /// Caches component references and initial values.
    /// </summary>
    void Awake()
    {
        // Cache the SpriteRenderer component
        _spriteRenderer = GetComponent<SpriteRenderer>();

        // Check if SpriteRenderer exists
        if (_spriteRenderer == null)
        {
            Debug.LogError("TriggerIconPopInAnimator requires a SpriteRenderer component.", this);
            enabled = false; // Disable script if component is missing
            return;
        }

        // Store the intended final color (assuming prefab is set to fully opaque)
        // This will be the target alpha for the fade-in.
        _initialColor = _spriteRenderer.color;

        // Component remains disabled until Initialize is called.
        enabled = false;
    }

    /// <summary>
    /// Initializes the animator, sets the target Transform to follow,
    /// and starts the pop-in animation. Called externally (e.g., by TriggerIconManager).
    /// </summary>
    /// <param name="target">The Transform this icon should visually follow.</param>
    public void Initialize(Transform target)
    {
        // Ensure we have a valid target and renderer
        if (target == null)
        {
            Debug.LogError("Initialize called with a null target Transform.", this);
            enabled = false;
            return;
        }
         if (_spriteRenderer == null)
        {
            // This should have been caught in Awake, but check again.
            Debug.LogError("Initialize called but SpriteRenderer is missing.", this);
            enabled = false;
            return;
        }

        // Set the target to follow
        targetTransform = target;

        // --- Reset and Initialize Pop-In Animation ---
        _fadeInElapsedTime = 0f; // Reset timer
        _fadeOutElapsedTime = 0f; // Reset fade-out timer in case of re-initialization
        _isFadingIn = true; // Start animation flag
        _isFadingOut = false; // Ensure fade-out isn't running

        // Capture the initial target position at the moment of initialization
        Vector3 initialTargetPos = targetTransform.position;

        // Calculate the starting position based on the offset relative to the initial target position
        _fadeInStartPosition = initialTargetPos - (Vector3.up * fadeInVerticalOffset);

        // --- Set Initial State for Pop-In ---
        // Apply the starting position
        transform.position = _fadeInStartPosition;

        // Apply the starting opacity (preserving original RGB)
        _spriteRenderer.color = new Color(_initialColor.r, _initialColor.g, _initialColor.b, fadeInStartOpacity);

        // Ensure the component is enabled to run Update
        enabled = true;
    }


    /// <summary>
    /// Called externally (e.g., by TriggerIconManager) to initiate the fade-out sequence.
    /// </summary>
    public void StartFadeOutAnimation()
    {
        // Don't start fading out if already doing so, or if component is invalid/uninitialized
        if (_isFadingOut || _spriteRenderer == null) return;

        // --- Initialize Fade-Out Animation ---
        _fadeOutElapsedTime = 0f;
        _isFadingOut = true; // Set fade-out flag
        _isFadingIn = false; // Ensure pop-in animation stops / doesn't interfere

        // Record state at the start of fade-out
        _fadeOutStartPosition = transform.position; // Capture current position
        _fadeOutStartColor = _spriteRenderer.color; // Capture current color/alpha

        // Calculate target position for fade-out movement (offset from where fade-out started)
        _fadeOutTargetPosition = _fadeOutStartPosition + (Vector3.up * fadeOutVerticalOffset);

        // Ensure Update loop runs for the fade-out (might be redundant if already enabled, but safe)
        enabled = true;
    }


    /// <summary>
    /// Standard Unity function called every frame, if the Behaviour is enabled.
    /// Updates the position and opacity during pop-in or fade-out animations,
    /// and follows the target Transform after pop-in is complete.
    /// </summary>
    void Update()
    {
        // If the target transform is lost somehow, disable to prevent errors.
        if (targetTransform == null)
        {
             if (!_isFadingOut) // Allow fade-out to complete even if target is lost
             {
                Debug.LogWarning("Target Transform lost for TriggerIconPopInAnimator. Disabling.", this);
                StartFadeOutAnimation();
                return;
             }
             // If fading out, let it finish without target. Position will be interpolated based on _fadeOutStartPosition.
        }

        // Prioritize fade-out animation if it's running
        if (_isFadingOut)
        {
            // Increment fade-out timer
            _fadeOutElapsedTime += Time.deltaTime;

            // Calculate normalized progress (0 to 1)
            float progress = Mathf.Clamp01(_fadeOutElapsedTime / fadeOutDuration);

            // Apply easing
            float easedProgress = ApplyEasing(progress, fadeOutEasing);

            // --- Interpolate Values for Fade-Out ---
            // Interpolate position from where fade-out started towards the offset position
            transform.position = Vector3.LerpUnclamped(_fadeOutStartPosition, _fadeOutTargetPosition, easedProgress);

            // Interpolate alpha (from captured start alpha to target end alpha)
            float currentAlpha = Mathf.LerpUnclamped(_fadeOutStartColor.a, fadeOutEndOpacity, easedProgress);
            _spriteRenderer.color = new Color(_fadeOutStartColor.r, _fadeOutStartColor.g, _fadeOutStartColor.b, currentAlpha);

            // --- Check for Fade-Out Completion ---
            if (_fadeOutElapsedTime >= fadeOutDuration)
            {
                // Animation finished, destroy the GameObject
                Destroy(gameObject);
                // Important: return immediately after Destroy to prevent further code execution on this frame
                return;
            }
        }
        // Only run pop-in logic if not fading out
        else if (_isFadingIn)
        {
            // Increment pop-in timer
            _fadeInElapsedTime += Time.deltaTime;

            // Calculate normalized progress (0 to 1)
            float progress = Mathf.Clamp01(_fadeInElapsedTime / fadeInDuration);

            // Apply easing
            float easedProgress = ApplyEasing(progress, fadeInEasing);

            // Get the current position of the target transform THIS frame
            Vector3 currentTargetPos = targetTransform.position;

            // --- Interpolate Values for Pop-In ---
            // Interpolate position from the calculated start position towards the *current* target position
            transform.position = Vector3.LerpUnclamped(_fadeInStartPosition, currentTargetPos, easedProgress);

            // Interpolate alpha (from start alpha to initial full alpha)
            float currentAlpha = Mathf.LerpUnclamped(fadeInStartOpacity, _initialColor.a, easedProgress);
            _spriteRenderer.color = new Color(_initialColor.r, _initialColor.g, _initialColor.b, currentAlpha);

            // --- Check for Pop-In Completion ---
            if (_fadeInElapsedTime >= fadeInDuration)
            {
                // Animation finished
                _isFadingIn = false;

                // Snap to the exact final values RELATIVE to the current target position
                // We snap to the target's position *now*, ensuring alignment even if it moved during the last frame.
                transform.position = currentTargetPos;
                _spriteRenderer.color = _initialColor;

                // Keep 'enabled = true' so Update continues to run, allowing the icon
                // to follow the target transform after the pop-in completes.
            }
        }
        // If not fading in OR out, and initialized with a target, follow the target
        else if (targetTransform != null) // Check targetTransform again for safety
        {
            // Ensures the icon sticks exactly to the target transform's position after the pop-in animation is done.
            transform.position = targetTransform.position;
        }
    }

    /// <summary>
    /// Applies a selected easing function to the input value t.
    /// </summary>
    /// <param name="t">Normalized time/progress (0 to 1).</param>
    /// <param name="easeType">The type of easing to apply.</param>
    /// <returns>The eased value (usually 0 to 1).</returns>
    private float ApplyEasing(float t, EaseType easeType)
    {
        // Applies the mathematical formula for the chosen easing function.
        switch (easeType)
        {
            case EaseType.EaseOutQuad:
                // Decelerates quickly. Formula: 1 - (1-t)^2
                return 1f - (1f - t) * (1f - t);
            case EaseType.SmoothStep:
                // S-curve easing, starts and ends slow. Formula: t^2 * (3 - 2*t)
                // Mathf.SmoothStep handles the clamping and formula internally.
                return Mathf.SmoothStep(0f, 1f, t);
            case EaseType.Linear:
            default:
                // No easing, constant speed.
                return t;
        }
    }
}