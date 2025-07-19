using System;
using System.Text.RegularExpressions;
using UnityEngine; // Using this for Mathf.Max, can be swapped for System.Math.Max

/// <summary>
/// Defines the method used for estimating reading time.
/// </summary>
public enum EstimationMode
{
    /// <summary>
    /// Bases the estimation on the number of words (best for space-delimited languages).
    /// </summary>
    WordsPerMinute,
    /// <summary>
    /// Bases the estimation on the number of characters (best for logographic languages like Chinese, Japanese, Korean).
    /// </summary>
    CharactersPerMinute
}

/// <summary>
/// A configurable, reusable class to dynamically estimate the reading time of a string.
/// This is a plain C# object, not a MonoBehaviour.
/// </summary>
public class ReadingTimeEstimator
{
    // A pre-compiled regex for splitting text into words.
    // This is static and readonly for performance, so it's only compiled once.
    private static readonly Regex WordRegex = new Regex(@"\W+");

    private readonly EstimationMode _mode;
    private readonly int _rate; // The WPM or CPM rate.
    private readonly float _minDuration;

    /// <summary>
    /// Creates a new instance of the reading time estimator with a specific configuration.
    /// </summary>
    /// <param name="mode">The estimation mode (WPM or CPM).</param>
    /// <param name="rate">The rate (e.g., 225 for WPM, 500 for CPM). Must be greater than 0.</param>
    /// <param name="minDuration">The minimum time in seconds the text should be displayed. Must be non-negative.</param>
    public ReadingTimeEstimator(EstimationMode mode, int rate, float minDuration)
    {
        if (rate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), "Rate must be a positive integer.");
        }
        if (minDuration < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minDuration), "Minimum duration cannot be negative.");
        }

        _mode = mode;
        _rate = rate;
        _minDuration = minDuration;
    }

    /// <summary>
    /// Estimates how long it will take to read the given text based on the estimator's configuration.
    /// </summary>
    /// <param name="text">The string to analyze.</param>
    /// <returns>The estimated reading time in seconds.</returns>
    public float Estimate(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return _minDuration; // Return minimum for empty strings
        }

        float rawTime;
        switch (_mode)
        {
            case EstimationMode.WordsPerMinute:
                // Count words using the robust regex method
                int wordCount = WordRegex.Split(text.Trim()).Length;
                rawTime = (float)wordCount / _rate * 60f; // Convert minutes to seconds
                break;

            case EstimationMode.CharactersPerMinute:
                // Count all characters
                int charCount = text.Length;
                rawTime = (float)charCount / _rate * 60f; // Convert minutes to seconds
                break;
            
            default:
                throw new InvalidOperationException("Unknown estimation mode configured.");
        }

        // Return the calculated time, but never less than the configured minimum duration.
        return Mathf.Max(rawTime, _minDuration);
    }
}

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
        Debug.Log($"Estimated {readingTime} seconds for text: \"{text}\" with mode {mode}, rate {rate}, min duration {minDuration}");
        return readingTime;
    }
}