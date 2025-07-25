using System;
using UnityEngine;

[DefaultExecutionOrder(-99)]
class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    public static DefaultInputActions InputActions;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("There is more than one instance of GameManager!");
            return;
        }

        Instance = this;

        // Initialize input actions
        InputActions = new DefaultInputActions();
    }

    private void OnEnable()
    {
        InputActions.CameraControl.Enable();
        InputActions.PlayerControl.Enable();
    }

    private void OnDisable()
    {
        InputActions.CameraControl.Disable();
        InputActions.PlayerControl.Disable();
    }
}