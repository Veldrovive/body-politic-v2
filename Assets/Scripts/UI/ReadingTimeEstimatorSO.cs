using UnityEngine;

/// <summary>
/// SO that holds the configuration for the reading time estimator and exposes an Estimate method that
/// returns the estimated reading time based on the configuration.
/// </summary>
[CreateAssetMenu(fileName = "ReadingTimeEstimator", menuName = "Body Politic/ReadingTimeEstimator", order = 1)]
public class ReadingTimeEstimatorSO : ScriptableObject
{
    [SerializeField] private EstimationMode mode = EstimationMode.WordsPerMinute;
    [SerializeField] private int rate = 225; // Default WPM
    [SerializeField] private float minDuration = 3f; // Default minimum duration in seconds
    
    private ReadingTimeEstimator _estimator;
    
    private void OnEnable()
    {
        _estimator = new ReadingTimeEstimator(mode, rate, minDuration);
    }
    
    public float Estimate(string text)
    {
        _estimator ??= new ReadingTimeEstimator(mode, rate, minDuration);
        float readingTime = _estimator.Estimate(text);
        // Debug.Log($"Estimated {readingTime} seconds for text: \"{text}\" with mode {mode}, rate {rate}, min duration {minDuration}");
        return readingTime;
    }
}