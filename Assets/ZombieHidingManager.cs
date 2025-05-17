using System.Collections;
using UnityEngine;

/// <summary>
/// Manager class that integrates the HideSpotMovementSystem with the existing ZombieHidingSystem.
/// Acts as an intermediary to ensure the hide spot position and zombie hiding logic work together.
/// </summary>
public class ZombieHidingManager : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private ZombieHidingSystem zombieHidingSystem;
	[SerializeField] private HideSpotMovementSystem hideSpotMovementSystem;
	[SerializeField] private RollerCoasterGameManager gameManager;
	[SerializeField] private ScrollerManager scrollerManager;
	[SerializeField] private Transform hideSpot;

	[Header("Positions")]
	[SerializeField] private Vector3 defaultHidePosition = new Vector3(0f, 0f, 0f);
	[SerializeField] private float entryDelayTime = 2f;
	[SerializeField] private float exitDelayTime = 1.5f;

	private bool initialSetupDone = false;
	private Vector3 originalHideSpotPosition;

	private RollerCoasterGameManager.GameState lastGameState;

	private void Awake()
	{
		// Find references if not assigned
		if (zombieHidingSystem == null)
		{
			zombieHidingSystem = FindObjectOfType<ZombieHidingSystem>();
			if (zombieHidingSystem == null)
			{
				Debug.LogError("ZombieHidingManager needs a reference to ZombieHidingSystem!");
				enabled = false;
				return;
			}
		}

		if (hideSpotMovementSystem == null)
		{
			hideSpotMovementSystem = GetComponent<HideSpotMovementSystem>();
			if (hideSpotMovementSystem == null)
			{
				hideSpotMovementSystem = gameObject.AddComponent<HideSpotMovementSystem>();
			}
		}

		if (gameManager == null)
		{
			gameManager = FindObjectOfType<RollerCoasterGameManager>();
			if (gameManager == null)
			{
				Debug.LogError("ZombieHidingManager needs a reference to RollerCoasterGameManager!");
				enabled = false;
				return;
			}
		}

		if (scrollerManager == null)
		{
			scrollerManager = FindObjectOfType<ScrollerManager>();
			if (scrollerManager == null)
			{
				Debug.LogError("ZombieHidingManager needs a reference to ScrollerManager!");
				enabled = false;
				return;
			}
		}

		// Find hide spot if not assigned
		if (hideSpot == null)
		{
			// Get via reflection from ZombieHidingSystem
			var hideSpotField = zombieHidingSystem.GetType().GetField("hideSpot",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

			if (hideSpotField != null)
			{
				hideSpot = hideSpotField.GetValue(zombieHidingSystem) as Transform;
			}

			if (hideSpot == null)
			{
				Debug.LogError("ZombieHidingManager couldn't find the hide spot transform!");
				enabled = false;
				return;
			}
		}

		// Store original hide spot position
		originalHideSpotPosition = hideSpot.position;
	}

	private void Start()
	{
		// Delay initial setup to ensure all systems are initialized
		StartCoroutine(DelayedInitialSetup());
	}

	private IEnumerator DelayedInitialSetup()
	{
		// Wait for all systems to initialize
		yield return new WaitForSeconds(0.2f);

		if (hideSpotMovementSystem != null)
		{
			// Set the center position to our default hide position or original position
			hideSpotMovementSystem.SetCenterPosition(defaultHidePosition != Vector3.zero ?
				defaultHidePosition : originalHideSpotPosition);

			// Start entry sequence after a delay
			StartCoroutine(EntrySequence());
		}

		initialSetupDone = true;
	}

	private IEnumerator EntrySequence()
	{
		yield return new WaitForSeconds(entryDelayTime);
	}

	private void Update()
	{
		if (!initialSetupDone)
			return;

		// Monitor game state for transitions
		MonitorGameState();

		// Update the hide spot movement system with the current game state
		// Check for game state transitions
		if (lastGameState != gameManager.CurrentState)
		{
			HandleGameStateChange(gameManager.CurrentState);
			lastGameState = gameManager.CurrentState;
		}

	}
	private void HandleGameStateChange(RollerCoasterGameManager.GameState newState)
	{
		// Handle hide spot position when ride completes
		if (newState == RollerCoasterGameManager.GameState.RideComplete)
		{
			if (hideSpotMovementSystem != null)
			{
				hideSpotMovementSystem.MoveHideSpotToCenter();
			}
		}
	}
	private void MonitorGameState()
	{
		// If the ride is complete and the zombie is hidden, initiate unhiding
		if (gameManager.CurrentState == RollerCoasterGameManager.GameState.RideComplete &&
			zombieHidingSystem.IsHidden)
		{
			StartCoroutine(DelayedZombieUnhide(1.0f));
		}
	}

	private IEnumerator DelayedZombieUnhide(float delay)
	{
		yield return new WaitForSeconds(delay);

		// Access the zombieHidingSystem to unhide the zombie
		System.Reflection.MethodInfo unhideMethod = zombieHidingSystem.GetType()
			.GetMethod("UnhideZombie", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

		if (unhideMethod != null)
		{
			unhideMethod.Invoke(zombieHidingSystem, null);
		}
	}

	/// <summary>
	/// Handles the exit sequence when the game is restarting
	/// </summary>
	public void StartExitSequence()
	{
		StartCoroutine(ExitSequence());
	}

	private IEnumerator ExitSequence()
	{
		yield return new WaitForSeconds(exitDelayTime);

		// Move hide spot off-screen left when exiting
		if (hideSpotMovementSystem != null)
		{
			hideSpotMovementSystem.MoveHideSpotOffscreenLeft();
		}

		// After moving off-screen, prepare for the next cycle
		yield return new WaitForSeconds(3.0f);

		// Set hide spot back to off-screen right for next entry
		hideSpotMovementSystem.SetHideSpotPosition(new Vector3(25f, hideSpot.position.y, hideSpot.position.z));
	}

	/// <summary>
	/// Public method to force the hide spot to move to center position
	/// </summary>
	public void ForceHideSpotToCenter()
	{
		// Only allow during ride completion
		if (hideSpotMovementSystem != null &&
			gameManager.CurrentState == RollerCoasterGameManager.GameState.RideComplete)
		{
			hideSpotMovementSystem.SetHideSpotPosition(hideSpotMovementSystem.OffscreenRightPosition);
			hideSpotMovementSystem.MoveHideSpotToCenter();
		}
	}

	/// <summary>
	/// Public method to force the hide spot off-screen
	/// </summary>
	public void ForceHideSpotOffscreen()
	{
		if (hideSpotMovementSystem != null)
		{
			hideSpotMovementSystem.MoveHideSpotOffscreenLeft();
		}
	}

#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		if (hideSpot != null)
		{
			// Draw the current hide spot position
			Gizmos.color = Color.magenta;
			Gizmos.DrawWireSphere(hideSpot.position, 1.2f);
			UnityEditor.Handles.Label(hideSpot.position + Vector3.up * 2f, "Current Hide Spot Position");

			// Draw the default hide position
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(defaultHidePosition, 0.8f);
			UnityEditor.Handles.Label(defaultHidePosition + Vector3.up * 1.5f, "Default Hide Position");
		}
	}
#endif
}