using UnityEngine;


[System.Serializable]
enum SlideDirection
{
    Up,
    Down,
    Left,
    Right
}

public class BasicDoorMover : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField]
    private SlideDirection slideDirection = SlideDirection.Down;
    [SerializeField]
    private float slideDistance = 2.0f;
    [SerializeField]
    private float slideSpeed = 1.0f;
    
    private Vector3 closedPosition;
    private Vector3 openPosition;
    private Vector3 targetPosition;
    private bool isMoving = false;
    private bool isOpen = false;
    
    public bool IsOpen => isOpen;
    public bool IsMoving => isMoving;
    
    public void Open()
    {
        if (isOpen && !isMoving) return;

        targetPosition = openPosition;
        isOpen = true;
        isMoving = true;
    }
    
    public void Close()
    {
        if (!isOpen && !isMoving) return;

        targetPosition = closedPosition;
        isOpen = false;
        isMoving = true;
    }
    
    public void Toggle()
    {
        if (isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }
    
    void Awake()
    {
        closedPosition = transform.position;
        Vector3 directionVector = GetDirectionVector(slideDirection);
        openPosition = closedPosition + directionVector * slideDistance;
        
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
    }
    
    private Vector3 GetDirectionVector(SlideDirection direction)
    {
        switch (direction)
        {
            case SlideDirection.Up: return Vector3.up;
            case SlideDirection.Down: return Vector3.down;
            case SlideDirection.Left: return Vector3.left;
            case SlideDirection.Right: return Vector3.right;
            default: return Vector3.forward;
        }
    }
}
