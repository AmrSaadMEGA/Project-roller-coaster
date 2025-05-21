using System.Collections;
using UnityEngine;

/// <summary>
/// Manages synchronized scrolling between rail and background scrollers,
/// providing smooth acceleration and deceleration.
/// </summary>
public class ScrollerManager : MonoBehaviour
{
	[Header("Scroller References")]
	[SerializeField] private InfiniteRailScroller railScroller;
	[SerializeField] private InfiniteBackgroundScroller backgroundScroller;

	[Header("Transition Settings")]
	[Tooltip("Time in seconds to accelerate/decelerate")]
	[SerializeField] private float transitionDuration = 1.5f;
	[Tooltip("Easing curve for acceleration/deceleration (1=linear, 2=quadratic, etc)")]
	[SerializeField] private float easingPower = 2f;

	// Target speeds
	private float targetRailSpeed = 0f;
	private float targetBackgroundSpeed = 0f;

	// Starting speeds (for transitions)
	private float startRailSpeed = 0f;
	private float startBackgroundSpeed = 0f;

	// Original max speeds (stored on initialization)
	private float maxRailSpeed = 5f;
	private float maxBackgroundSpeed = 2f;

	// Transition tracking
	private float transitionTime = 0f;
	private bool isTransitioning = false;
	private Coroutine transitionCoroutine;

	// Public accessor for rail scroller - added for hide spot movement integration
	public InfiniteRailScroller RailScroller => railScroller;
	public bool IsActivelyScrolling =>
	Mathf.Abs(railScroller.scrollSpeed) > 0.01f &&
	!IsStopped();

	private void Awake()
	{
		// Validate references
		if (railScroller == null)
		{
			Debug.LogError("ScrollerManager: Rail scroller reference is missing!");
			enabled = false;
			return;
		}

		if (backgroundScroller == null)
		{
			Debug.LogError("ScrollerManager: Background scroller reference is missing!");
			enabled = false;
			return;
		}

		// Store original max speeds
		maxRailSpeed = railScroller.scrollSpeed;
		maxBackgroundSpeed = backgroundScroller.scrollSpeed;

		// Initialize with stopped state
		railScroller.scrollSpeed = 0f;
		backgroundScroller.scrollSpeed = 0f;
	}

	/// <summary>
	/// Start both scrollers with smooth acceleration
	/// </summary>
	/// <param name="railSpeed">Target rail speed</param>
	/// <param name="backgroundSpeed">Target background speed (if 0, uses proportional value)</param>
	public void StartScrolling(float railSpeed = -1f, float backgroundSpeed = 0f)
	{
		// Use default speeds if not specified
		if (railSpeed < 0) railSpeed = maxRailSpeed;
		if (backgroundSpeed <= 0) backgroundSpeed = maxBackgroundSpeed;

		// Set target speeds
		targetRailSpeed = railSpeed;
		targetBackgroundSpeed = backgroundSpeed;

		// Store current speeds as starting points
		startRailSpeed = railScroller.scrollSpeed;
		startBackgroundSpeed = backgroundScroller.scrollSpeed;

		// Start transition
		if (transitionCoroutine != null)
			StopCoroutine(transitionCoroutine);

		transitionCoroutine = StartCoroutine(TransitionSpeed());
	}
	public float GetScrollSpeed()
	{
		return railScroller != null ? railScroller.scrollSpeed : 0f;
	}

	/// <summary>
	/// Stop both scrollers with smooth deceleration
	/// </summary>
	public void StopScrolling()
	{
		// Set target speeds to zero
		targetRailSpeed = 0f;
		targetBackgroundSpeed = 0f;

		// Store current speeds as starting points
		startRailSpeed = railScroller.scrollSpeed;
		startBackgroundSpeed = backgroundScroller.scrollSpeed;

		// Start transition
		if (transitionCoroutine != null)
			StopCoroutine(transitionCoroutine);

		transitionCoroutine = StartCoroutine(TransitionSpeed());
	}

	/// <summary>
	/// Set speed with instant change (no smoothing)
	/// </summary>
	public void SetSpeedInstant(float railSpeed, float backgroundSpeed = 0f)
	{
		// If background speed not specified, calculate proportionally
		if (backgroundSpeed <= 0 && maxRailSpeed > 0)
			backgroundSpeed = railSpeed * (maxBackgroundSpeed / maxRailSpeed);

		// Cancel any ongoing transition
		if (transitionCoroutine != null)
		{
			StopCoroutine(transitionCoroutine);
			isTransitioning = false;
		}

		// Set speeds directly
		railScroller.scrollSpeed = railSpeed;
		backgroundScroller.scrollSpeed = backgroundSpeed;

		// Update targets to match current speeds
		targetRailSpeed = railSpeed;
		targetBackgroundSpeed = backgroundSpeed;
	}

	/// <summary>
	/// Check if scrollers are currently stopped
	/// </summary>
	public bool IsStopped()
	{
		// Consider stopped if both speeds are near zero
		return Mathf.Approximately(railScroller.scrollSpeed, 0f) &&
			   Mathf.Approximately(backgroundScroller.scrollSpeed, 0f);
	}

	/// <summary>
	/// Check if scrollers are currently in a transition
	/// </summary>
	public bool IsTransitioning()
	{
		return isTransitioning;
	}

	/// <summary>
	/// Coroutine to smoothly transition speed
	/// </summary>
	private IEnumerator TransitionSpeed()
	{
		isTransitioning = true;
		transitionTime = 0f;

		while (transitionTime < transitionDuration)
		{
			transitionTime += Time.deltaTime;
			float t = Mathf.Clamp01(transitionTime / transitionDuration);

			// Apply easing function (power curve)
			float easedT = Mathf.Pow(t, easingPower);

			// Interpolate speeds
			railScroller.scrollSpeed = Mathf.Lerp(startRailSpeed, targetRailSpeed, easedT);
			backgroundScroller.scrollSpeed = Mathf.Lerp(startBackgroundSpeed, targetBackgroundSpeed, easedT);

			yield return null;
		}

		// Ensure final speed is exactly the target
		railScroller.scrollSpeed = targetRailSpeed;
		backgroundScroller.scrollSpeed = targetBackgroundSpeed;

		isTransitioning = false;
	}
}