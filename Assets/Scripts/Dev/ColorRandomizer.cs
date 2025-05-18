using UnityEngine;

/// <summary>
/// A simple MonoBehaviour script to randomize the color of the GameObject it's attached to when a public RandomizeColor(InteractionContext) method is called.
/// </summary>
public class ColorRandomizer : MonoBehaviour
{
    private Renderer objectRenderer;

    void Awake()
    {
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer == null)
        {
            Debug.LogError("No Renderer found on this GameObject. ColorRandomizer requires a Renderer component.");
        }
    }

    /// <summary>
    /// Randomizes the color of the GameObject's material.
    /// This method can be called from other scripts or events.
    /// </summary>
    public void RandomizeColor(InteractionContext context)
    {
        if (objectRenderer != null)
        {
            Color randomColor = new Color(Random.value, Random.value, Random.value);
            objectRenderer.material.color = randomColor;
            // Debug.Log($"Color randomized to: {randomColor}");
        }
        else
        {
            Debug.LogError("Renderer is not set. Cannot randomize color.");
        }
    }
}
