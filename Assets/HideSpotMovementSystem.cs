using System.Collections;
using UnityEngine;

/// <summary>
/// Controls the movement of the zombie hiding spot between screen positions.
/// Integrates with the ScrollerManager for synchronized movement.
/// </summary>
public class HideSpotMovementSystem : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Transform hideSpot;
	[SerializeField] private ScrollerManager scrollerManager;
	[SerializeField] private RollerCoasterGameManager gameManager;

	[Header("Movement Settings")]
	[SerializeField] private Vector3 offscreenRightPosition = new Vector3(25f, 0f, 0f);
	[SerializeField] private Vector3 centerScreenPosition = new Vector3(0f, 0f, 0f);
	[SerializeField] private Vector3 offscreenLeftPosition = new Vector3(-25f, 0f, 0f);
	[SerializeField] private float movementDuration = 3f;
	[SerializeField] private AnimationCurve movementEasingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	private Coroutine movementCoroutine;
	private RollerCoasterGameManager.GameState lastGameState;
	private bool isMoving = false;
	private float movementThreshold = 0.01f; // Add this variable

	// Add public property to expose offscreen right position
	public Vector3 OffscreenRightPosition => offscreenRightPosition;

	private void Awake()
	{
		// Validate references
		if (hideSpot == null)
		{
			Debug.LogError("HideSpotMovementSystem needs a reference to the hide spot transform!");
			enabled = false;
			return;
		}

		if (scrollerManager == null)
		{
			scrollerManager = FindObjectOfType<ScrollerManager>();
			if (scrollerManager == null)
			{
				Debug.LogError("HideSpotMovementSystem needs a reference to the ScrollerManager!");
				enabled = false;
				return;
			}
		}

		if (gameManager == null)
		{
			gameManager = FindObjectOfType<RollerCoasterGameManager>();
			if (gameManager == null)
			{
				Debug.LogError("HideSpotMovementSystem needs a reference to the RollerCoasterGameManager!");
				enabled = false;
				return;
			}
		}

		// Initialize position at center (CHANGED FROM OFFSCREEN RIGHT)
		hideSpot.position = centerScreenPosition;
		lastGameState = gameManager.CurrentState;
	}

	private void Update()
	{
		// Check for game state transitions
		if (lastGameState != gameManager.CurrentState)
		{
			HandleGameStateChange(gameManager.CurrentState);
			lastGameState = gameManager.CurrentState;
		}
		// Sync with rail movement during ride
		if (gameManager.CurrentState == RollerCoasterGameManager.GameState.RideInProgress)
		{
			float currentSpeed = scrollerManager.GetScrollSpeed();
			// Ignore micro-movements
			if (Mathf.Abs(currentSpeed) > movementThreshold)
			{
				Vector3 newPosition = hideSpot.position;
				newPosition.x -= currentSpeed * Time.deltaTime;
				hideSpot.position = newPosition;
			}
		}
		// Only sync with rail movement during active ride
		if (gameManager.CurrentState == RollerCoasterGameManager.GameState.RideInProgress)
		{
			float currentSpeed = scrollerManager.GetScrollSpeed();
			Vector3 newPosition = hideSpot.position;
			newPosition.x -= currentSpeed * Time.deltaTime;
			hideSpot.position = newPosition;
		}
		else // Reset position when not riding
		{
			// Keep centered unless explicitly moved
			if (hideSpot.position != centerScreenPosition)
			{
				hideSpot.position = centerScreenPosition;
			}
		}
	}

	private void HandleGameStateChange(RollerCoasterGameManager.GameState newState)
	{
		// Add reset for non-ride states
		if (newState == RollerCoasterGameManager.GameState.HumansGathering)
		{
			if (movementCoroutine != null)
				StopCoroutine(movementCoroutine);

			hideSpot.position = centerScreenPosition;
		}
		// Move hide spot to center when ride completes
		else if (newState == RollerCoasterGameManager.GameState.RideComplete)
		{
			StartCoroutine(AnimateRideCompletion());
		}
		// Cancel movement during non-ride states
		else if (newState != RollerCoasterGameManager.GameState.RideInProgress)
		{
			if (movementCoroutine != null)
				StopCoroutine(movementCoroutine);
		}
		// Move hide spot offscreen left when game restarts
		else if (newState == RollerCoasterGameManager.GameState.HumansGathering &&
				 lastGameState == RollerCoasterGameManager.GameState.RideRestarting)
		{
			MoveHideSpotOffscreenLeft();
		}
		// Move hide spot offscreen right when game is restarting
		else if (newState == RollerCoasterGameManager.GameState.RideRestarting)
		{
			StartCoroutine(DelayedMoveHideSpotOffscreenRight(1.5f));
		}
	}
	private IEnumerator AnimateRideCompletion()
	{
		// Teleport to right first
		SetHideSpotPosition(offscreenRightPosition);
		// Animate back to center
		yield return StartCoroutine(MoveHideSpot(offscreenRightPosition, centerScreenPosition));
	}
	/// <summary>
	/// Moves the hide spot from its current position to the center of the screen
	/// </summary>
	// Modify the MoveHideSpotToCenter method to ensure proper initialization
	public void MoveHideSpotToCenter()
	{
		if (isMoving && movementCoroutine != null)
		{
			StopCoroutine(movementCoroutine);
		}
		// Ensure starting from correct position
		SetHideSpotPosition(OffscreenRightPosition);
		movementCoroutine = StartCoroutine(MoveHideSpot(OffscreenRightPosition, centerScreenPosition));
	}

	/// <summary>
	/// Moves the hide spot from its current position to the left side offscreen
	/// </summary>
	public void MoveHideSpotOffscreenLeft()
	{
		if (isMoving && movementCoroutine != null)
		{
			StopCoroutine(movementCoroutine);
		}
		movementCoroutine = StartCoroutine(MoveHideSpot(hideSpot.position, offscreenLeftPosition));
	}

	/// <summary>
	/// Moves the hide spot from its current position to the right side offscreen
	/// </summary>
	public void MoveHideSpotOffscreenRight()
	{
		if (isMoving && movementCoroutine != null)
		{
			StopCoroutine(movementCoroutine);
		}
		movementCoroutine = StartCoroutine(MoveHideSpot(hideSpot.position, offscreenRightPosition));
	}

	/// <summary>
	/// Moves the hide spot from start position to end position over the specified duration
	/// </summary>
	private IEnumerator MoveHideSpot(Vector3 startPosition, Vector3 endPosition)
	{
		isMoving = true;
		float elapsedTime = 0f;

		// Calculate movement speed based on ScrollerManager's transition speed
		float adjustedDuration = scrollerManager.IsTransitioning() ?
			movementDuration * 0.75f : movementDuration;

		while (elapsedTime < adjustedDuration)
		{
			// Calculate progress with easing
			float normalizedTime = elapsedTime / adjustedDuration;
			float easedProgress = movementEasingCurve.Evaluate(normalizedTime);

			// Update position
			hideSpot.position = Vector3.Lerp(startPosition, endPosition, easedProgress);

			// Increment time
			elapsedTime += Time.deltaTime;
			yield return null;
		}

		// Ensure final position is exact
		hideSpot.position = endPosition;
		isMoving = false;
	}

	/// <summary>
	/// Delays the movement of the hide spot to the right side offscreen
	/// </summary>
	private IEnumerator DelayedMoveHideSpotOffscreenRight(float delay)
	{
		yield return new WaitForSeconds(delay);
		MoveHideSpotOffscreenRight();
	}

	/// <summary>
	/// Manually sets the hide spot to a specified position without animation
	/// </summary>
	public void SetHideSpotPosition(Vector3 position)
	{
		if (hideSpot != null)
		{
			if (isMoving && movementCoroutine != null)
			{
				StopCoroutine(movementCoroutine);
				isMoving = false;
			}
			hideSpot.position = position;
		}
	}

	/// <summary>
	/// Set a custom target position for center position
	/// </summary>
	public void SetCenterPosition(Vector3 position)
	{
		centerScreenPosition = position;
	}

#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		// Draw the three possible positions
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(offscreenRightPosition, 1f);
		UnityEditor.Handles.Label(offscreenRightPosition + Vector3.up * 1.5f, "Offscreen Right");

		Gizmos.color = Color.green;
		Gizmos.DrawWireSphere(centerScreenPosition, 1f);
		UnityEditor.Handles.Label(centerScreenPosition + Vector3.up * 1.5f, "Center Screen");

		Gizmos.color = Color.blue;
		Gizmos.DrawWireSphere(offscreenLeftPosition, 1f);
		UnityEditor.Handles.Label(offscreenLeftPosition + Vector3.up * 1.5f, "Offscreen Left");

		// Draw connection lines
		Gizmos.color = Color.yellow;
		Gizmos.DrawLine(offscreenRightPosition, centerScreenPosition);
		Gizmos.DrawLine(centerScreenPosition, offscreenLeftPosition);
	}
#endif
}