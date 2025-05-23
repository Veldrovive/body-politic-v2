using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent( typeof(NpcContext))]
public class NpcAnimationManager : MonoBehaviour
{

    #region Serialized Fields

    /// <summary>
    /// Smoothing factor for the angular velocity calculation to reduce jitter.
    /// </summary>
    [Tooltip("Smoothing factor for angular velocity calculation. Lower values are smoother but less responsive.")]
    [Range(0.01f, 1.0f)]
    [SerializeField] private float angularVelocitySmoothing = 0.1f;

    #endregion

    #region Internal Fields

    private Animator _animator;
    private NpcContext _npcContext;

    private Vector3 _previousForward;
    private float _smoothedAngularVelocity = 0f;

    #endregion
    
    #region Animator Keys
    
    private static readonly int velocityKey = Animator.StringToHash("Velocity");
    private static readonly int zVelocityKey = Animator.StringToHash("Z Velocity");
    private static readonly int angVelocityKey = Animator.StringToHash("Ang Velocity");
    
    #endregion


    #region Unity Lifecycle

    private void OnEnable()
    {
        _animator = GetComponentInChildren<Animator>();
        if (_animator == null)
        {
            Debug.LogError($"Animator on {gameObject.name} children is null");
        }
        
        _npcContext = GetComponent<NpcContext>();
        if (_npcContext == null)
        {
            Debug.LogError($"Rigidbody on {gameObject.name} is null");
        }

        _previousForward = transform.forward;
    }

    private void Update()
    {
        if (_animator == null || _npcContext == null) return;
        
        Vector3 worldVelocity = _npcContext.MovementManager.velocity;
        Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);
        float zVelocity = localVelocity.z;
        
        Vector3 currentForward = transform.forward;
        float angleDelta = Vector3.SignedAngle(_previousForward, currentForward, Vector3.up);
        float currentAngularVelocityDeg = (Time.deltaTime > Mathf.Epsilon) ? angleDelta / Time.deltaTime : 0f;
        _smoothedAngularVelocity = Mathf.Lerp(_smoothedAngularVelocity, currentAngularVelocityDeg, angularVelocitySmoothing);
        _previousForward = currentForward;
        
        SetAngularVelocity(currentAngularVelocityDeg);
        SetZVelocity(zVelocity);
    }

    #endregion

    #region Event Handlers

    
    
    #endregion

    #region Animator Interaction

    void SetForwardVelocity(float velocity)
    {
        _animator.SetFloat(velocityKey, velocity);
    }

    void SetZVelocity(float velocity)
    {
        _animator.SetFloat(zVelocityKey, velocity);
    }
    
    void SetAngularVelocity(float velocity)
    {
        _animator.SetFloat(angVelocityKey, velocity);
    }
    
    public void SetTrigger(string triggerName)
    {
        if (_animator == null) return;
        _animator.SetTrigger(triggerName);
    }
    
    public void Play(string animationName)
    {
        if (_animator == null) return;
        _animator.Play(animationName);
    }
    
    public bool End(string animationName)
    {
        throw new NotImplementedException();
    }
    
    #endregion
}
