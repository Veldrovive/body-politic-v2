using UnityEngine;
using System.Collections;

public class ResettingHoldable : MonoBehaviour
{

    [SerializeField] private float resetTime = 3f; // Time in seconds to wait before resetting the object

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private Holdable holdable;

    private Coroutine resetCoroutine;

    void Awake()
    {
        // Get the Holdable component attached to this GameObject
        holdable = GetComponent<Holdable>();
        if (holdable == null)
        {
            Debug.LogError("ResettingHoldable: No Holdable component found on this GameObject.", this);
            return;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Record the initial position and rotation of the object
        Vector3 initialPosition = transform.position;
        Quaternion initialRotation = transform.rotation;

        this.initialPosition = initialPosition;
        this.initialRotation = initialRotation;

        holdable.OnDropped += OnDrop;
        holdable.OnPickedUp += OnPickUp;
    }

    public void OnPickUp(GameObject holder)
    {
        // Stop any ongoing reset coroutine if the object is picked up
        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
            resetCoroutine = null;
        }
    }

    public void OnDrop()
    {
        // Start a coroutine to reset the transform after 3 seconds
        if (resetCoroutine == null)
        {
            resetCoroutine = StartCoroutine(ResetAfterDelay(resetTime));
        }
    }

    private IEnumerator ResetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Reset the transform and clear the coroutine reference
        ResetTransform();
        resetCoroutine = null;
    }

    public void ResetTransform()
    {
        // Reset the position and rotation of the object to its initial state
        transform.position = initialPosition;
        transform.rotation = initialRotation;
    }
}
