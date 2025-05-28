using UnityEngine;

[System.Serializable]
enum DoorOpenMode
{
    SlideUp,
    SlideRight,
    SlideDown,
    SlideLeft,
    FadeOnly
}

public class DoorOpener : AbstractDoorOpener
{
    [SerializeField] DoorOpenMode doorOpenMode = DoorOpenMode.SlideDown;
    
    [Header("Slide Settings")]
    [SerializeField] float slideDistance = 2.0f;
    [SerializeField] float slideSpeed = 1.0f;
    
    [Header("Fade Settings")]
    [SerializeField] float fadeDuration = 1.0f;
    [SerializeField] float fadedAlpha = 1.0f;
    
    private Vector3 closedPosition;
    private Vector3 openPosition;
    private Vector3 targetPosition;

    private MeshRenderer _renderer;
    private float? closedAlpha;
    private float? openAlpha;
    private float? targetAlpha;
    
    private bool isMoving = false;
    private bool isAnimating = false;
    
    public bool IsMoving => isMoving;
    public bool IsAnimating => isAnimating;
    
    public override void Open()
    {
        targetPosition = openPosition;
        targetAlpha = openAlpha;
        isOpen = true;
        isMoving = true;
        isAnimating = true;
    }
    
    public override void Close()
    { 
        targetPosition = closedPosition;
        targetAlpha = closedAlpha;
        isOpen = false;
        isMoving = true;
        isAnimating = true;
    }
    
    void Awake()
    {
        closedPosition = transform.position;
        Vector3 directionVector = GetDirectionVector(doorOpenMode);
        openPosition = closedPosition + directionVector * slideDistance;
        
        // Get the material and set the alpha values
        _renderer = GetComponent<MeshRenderer>();
        if (_renderer != null && _renderer.material != null)
        {
            Color color = _renderer.material.color;
            closedAlpha = color.a;
            openAlpha = fadedAlpha;
            targetAlpha = closedAlpha; // Start with closed alpha
        }
        else
        {
            closedAlpha = null;
            openAlpha = null;
            targetAlpha = null;
            Debug.LogWarning("DoorOpener requires a Renderer with a material to handle fade effects.", this);
        }
        
        targetPosition = closedPosition;
        transform.position = closedPosition;

        isMoving = false; // Start stationary
    }
    
    void Update()
    {
        if (isMoving)
        {
            float step = slideSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);

            if (Vector3.Distance(transform.position, targetPosition) <= step)
            {
                transform.position = targetPosition;
                isMoving = false;
            }
        }

        if (isAnimating)
        {
            if (_renderer != null)
            {
                if (targetAlpha.HasValue)
                {
                    Color color = _renderer.material.color;
                    color.a = Mathf.MoveTowards(color.a, targetAlpha.Value, (1.0f / fadeDuration) * Time.deltaTime);
                    _renderer.material.color = color;

                    if (Mathf.Approximately(color.a, targetAlpha.Value))
                    {
                        isAnimating = false; // Animation complete
                    }
                }
            }
            else
            {
                isAnimating = false;
            }
        }
    }
    
    private Vector3 GetDirectionVector(DoorOpenMode mode)
    {
        switch (mode)
        {
            case DoorOpenMode.SlideUp: return Vector3.up;
            case DoorOpenMode.SlideDown: return Vector3.down;
            case DoorOpenMode.SlideLeft: return Vector3.left;
            case DoorOpenMode.SlideRight: return Vector3.right;
            case DoorOpenMode.FadeOnly: return Vector3.zero;
            default: return Vector3.forward;
        }
    }
}