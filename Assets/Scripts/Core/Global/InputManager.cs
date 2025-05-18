// Create file: Assets/Scripts/Input/InputManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems; // Required for checking clicks on UI

public class InputManager : MonoBehaviour
{
    // Singleton pattern
    public static InputManager Instance { get; private set; }

    // --- Configuration ---
    [Tooltip("The main camera used for raycasting.")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("Layers to consider for world interactions (hovering, clicking). UI layer should typically be excluded.")]
    [SerializeField] private LayerMask interactionLayers = ~0; // Default to everything
    [Tooltip("Maximum distance for raycasting.")]
    [SerializeField] private float maxRaycastDistance = 100f;
    [Tooltip("Whether hover raycasts should pass through UI elements.")]
    [SerializeField] private bool ignoreUIOnHover = false;

    // --- Events ---
    public event Action<HoverState> OnHoverChanged;
    public event Action<ClickState> OnClicked;
    public event Action<KeyState> OnKeyPressed;
    public event Action<KeyState> OnKeyReleased;
    public event Action<HeldKeyState> OnHeldModifierKeysChanged;

    // --- State Structs ---

    public struct HoverState
    {
        public GameObject HoveredObject { get; }
        public Vector2 ScreenPosition { get; }
        public Vector3 WorldPosition { get; }
        public Vector3 WorldNormal { get; }
        /// <summary>
        /// False either when not hovering any object or when hovering over UI.
        /// </summary>
        public bool HasHit { get; }

        public HoverState(Vector2 screenPosition, RaycastHit hitInfo)
        {
            ScreenPosition = screenPosition;
            HoveredObject = hitInfo.collider.gameObject;
            WorldPosition = hitInfo.point;
            WorldNormal = hitInfo.normal;
            HasHit = true;
        }

        // Constructor for no hit
        public HoverState(Vector2 screenPosition, bool hasHit = false)
        {
            ScreenPosition = screenPosition;
            HoveredObject = null;
            WorldPosition = Vector3.zero;
            WorldNormal = Vector3.zero;
            HasHit = hasHit;
        }
    }

    public enum MouseButton
    {
        Left = 0,
        Right = 1,
        Middle = 2,
        None = -1
    }

    public struct ClickState
    {
        public MouseButton Button { get; }
        public GameObject ClickedObject { get; } // Null if clicked on nothing or UI
        public Vector3 WorldPosition { get; }   // Point of click in world space
        public Vector3 WorldNormal { get; }     // Normal of the surface clicked
        public bool OverUI { get; }            // Was the click over a UI element?

        public ClickState(MouseButton button, RaycastHit hitInfo, bool overUI)
        {
            Button = button;
            ClickedObject = hitInfo.collider?.gameObject; // May be null if no hit but over UI
            WorldPosition = hitInfo.point;
            WorldNormal = hitInfo.normal;
            OverUI = overUI;
        }

         // Constructor for clicking UI or empty space
        public ClickState(MouseButton button, bool overUI, Vector3 worldPosIfNoHit = default)
        {
            Button = button;
            ClickedObject = null;
            WorldPosition = worldPosIfNoHit; // Can be useful for ground clicks
            WorldNormal = Vector3.zero;
            OverUI = overUI;
        }
    }

    public struct KeyState
    {
        public KeyCode Key { get; }
        // Add modifier keys if needed (Shift, Ctrl, Alt)
        // public bool ShiftHeld { get; }
        // public bool CtrlHeld { get; }
        // public bool AltHeld { get; }

        public KeyState(KeyCode key)
        {
            Key = key;
            // Initialize modifiers based on Input.GetKey
        }
    }

    public struct HeldKeyState
    {
        public HashSet<KeyCode> HeldKeys { get; }

        public HeldKeyState(HashSet<KeyCode> heldKeys)
        {
            HeldKeys = heldKeys; // Pass the reference
        }
    }

    // --- Internal State ---
    private HoverState _lastHoverState;
    private HashSet<KeyCode> _currentlyHeldModifierKeys = new HashSet<KeyCode>();
    private HashSet<KeyCode> _previousHeldModifierKeys = new HashSet<KeyCode>();
    
    private HashSet<KeyCode> _currentlyPressedKeys = new HashSet<KeyCode>();

    void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate InputManager instance found. Destroying this one.", gameObject);
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Find main camera if not assigned
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        if (mainCamera == null)
        {
            Debug.LogError("InputManager: Main camera not found or assigned!", this);
            enabled = false; // Disable if no camera
            return;
        }
        _lastHoverState = new HoverState(); // Initialize
    }

    void Update()
    {
        // --- Hover Detection ---
        HandleHover();

        // --- Click Detection ---
        HandleClicks();

        // --- Key Press Detection ---
        HandleKeyPresses();

        // --- Held Key Detection ---
        HandleHeldModifierKeys();
    }

    private void HandleHover()
    {
        HoverState currentHoverState;
        Vector2 mousePosition = Input.mousePosition;

        // Check if pointer is over UI first
        if (EventSystem.current.IsPointerOverGameObject() && !ignoreUIOnHover)
        {
            currentHoverState = new HoverState(mousePosition, false); // Treat UI hover as no world hit
        }
        else
        {
            Ray ray = mainCamera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hitInfo, maxRaycastDistance, interactionLayers))
            {
                currentHoverState = new HoverState(mousePosition, hitInfo);
                // Debug.Log($"Hover State: {currentHoverState.HoveredObject?.name ?? "None"} at {currentHoverState.WorldPosition} from mouse position {Input.mousePosition}");
            }
            else
            {
                currentHoverState = new HoverState(mousePosition, false); // No hit
                // Debug.Log("Hover State: None");
            }
        }

        OnHoverChanged?.Invoke(currentHoverState);
        _lastHoverState = currentHoverState;
    }

     private void HandleClicks()
    {
        bool isOverUI = EventSystem.current.IsPointerOverGameObject();

        CheckMouseButton(MouseButton.Left, isOverUI);
        CheckMouseButton(MouseButton.Right, isOverUI);
        CheckMouseButton(MouseButton.Middle, isOverUI);
    }

    private void CheckMouseButton(MouseButton button, bool isOverUI)
    {
        if (Input.GetMouseButtonDown((int)button))
        {
            ClickState clickState;
            if (isOverUI)
            {
                // Click happened over UI, create ClickState without world object info
                clickState = new ClickState(button, true);
            }
            else
            {
                // Click not over UI, perform raycast to see if it hit the world
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                 if (Physics.Raycast(ray, out RaycastHit hitInfo, maxRaycastDistance, interactionLayers))
                {
                    // Click hit something in the world
                    clickState = new ClickState(button, hitInfo, false);
                }
                else
                {
                    // Clicked on empty space (not UI, not world object)
                    // Optionally calculate world position on a default plane (e.g., y=0)
                    Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                    float rayDistance;
                    Vector3 worldPos = Vector3.zero;
                    if (groundPlane.Raycast(ray, out rayDistance)) {
                         worldPos = ray.GetPoint(rayDistance);
                    }
                    clickState = new ClickState(button, false, worldPos);
                }
            }
            OnClicked?.Invoke(clickState);
        }
    }

    private void HandleKeyPresses()
    {
        // This checks every frame if *any* key was pressed down.
        // More efficient ways exist if you only care about specific keys.
        if (Input.anyKeyDown || _currentlyPressedKeys.Count > 0)
        {
            // Iterate through known KeyCodes to find which one(s) were pressed.
            // This is not the most performant way for many keys, but simple.
            // Consider using Unity's newer Input System for better event-based handling.
            foreach (KeyCode kcode in Enum.GetValues(typeof(KeyCode)))
            {
                // Ignore "None" and potentially mouse buttons if handled separately
                 if (kcode == KeyCode.None || kcode >= KeyCode.Mouse0 && kcode <= KeyCode.Mouse6)
                    continue;

                if (Input.GetKeyDown(kcode))
                {
                    OnKeyPressed?.Invoke(new KeyState(kcode));
                    _currentlyPressedKeys.Add(kcode);
                }
                else if (_currentlyPressedKeys.Contains(kcode) && !Input.GetKey(kcode))
                {
                    OnKeyReleased?.Invoke(new KeyState(kcode));
                    _currentlyPressedKeys.Remove(kcode);
                }
            }
        }
    }

    public bool IsKeyHeld(KeyCode key)
    {
        return Input.GetKey(key);
    }

     private void HandleHeldModifierKeys()
    {
        _currentlyHeldModifierKeys.Clear();
        bool changed = false;

        // Check common modifier keys (extend as needed)
        CheckAndTrackHeldModifierKey(KeyCode.LeftShift, ref changed);
        CheckAndTrackHeldModifierKey(KeyCode.RightShift, ref changed);
        CheckAndTrackHeldModifierKey(KeyCode.LeftControl, ref changed);
        CheckAndTrackHeldModifierKey(KeyCode.RightControl, ref changed);
        CheckAndTrackHeldModifierKey(KeyCode.LeftAlt, ref changed);
        CheckAndTrackHeldModifierKey(KeyCode.RightAlt, ref changed);
        // Add any other specific keys you want to track as "held"

        // If the set of held keys has changed since last frame
        if (changed)
        {
            OnHeldModifierKeysChanged?.Invoke(new HeldKeyState(_currentlyHeldModifierKeys));
            // Update previous state *after* invoking event
             _previousHeldModifierKeys.Clear();
             foreach(var key in _currentlyHeldModifierKeys) {
                 _previousHeldModifierKeys.Add(key);
             }
        }
    }

    public bool IsModifierKeyHeld(KeyCode key)
    {
        return _currentlyHeldModifierKeys.Contains(key);
    }

    private void CheckAndTrackHeldModifierKey(KeyCode key, ref bool changed)
    {
        bool isHeld = Input.GetKey(key);
        bool wasHeld = _previousHeldModifierKeys.Contains(key);

        if (isHeld)
        {
            _currentlyHeldModifierKeys.Add(key);
             if (!wasHeld) changed = true; // Key is now held, wasn't before
        }
        else
        {
             // If key is not held now, but WAS held before, it's a change
             if (wasHeld) changed = true;
        }
    }
}