using UnityEngine;

public class RootManager : MonoBehaviour
{
    public static RootManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning("Removing duplicate RootManager instance.");
            Destroy(gameObject);
        }
    }
}