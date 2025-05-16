using UnityEngine;
using System.Collections;

[RequireComponent(typeof(ZombieController))]
public class ZombieHidingSystem : MonoBehaviour
{
	[Header("Hiding")]
	[SerializeField] private Transform hideSpot;
	[SerializeField] private float hideMoveDuration = 1.5f;
	[SerializeField] private string hideAnimationState = "Hiding";
	[SerializeField] private float hideSpotClickRadius = 1.5f;

	[Header("Unhiding")]
	[SerializeField] private float unhideDelay = 0.5f;
	[SerializeField] private string unhideAnimationState = "Unhiding";

	[Header("Human Respawning")]
	[SerializeField] private float respawnDelay = 3.0f; // New parameter for respawn delay

	[Header("References")]
	[SerializeField] private RollerCoasterGameManager gameManager;
	[SerializeField] private InfiniteRailScroller railScroller; // Added reference to the rail scroller

	// Private variables
	private ZombieController zombieController;
	private bool isHiding = false;
	private bool isHidden = false;
	private bool autoHideDone = false;
	private Transform originalParent;
	private Vector3 originalLocalPosition;
	private bool humansScared = false; // New flag to track if humans were scared

	// Public access
	public bool IsHidden => isHidden;
	public bool IsHiding => isHiding;

	private void Awake()
	{
		zombieController = GetComponent<ZombieController>();

		if (hideSpot == null)
		{
			Debug.LogError("ZombieHidingSystem requires a hideSpot Transform reference!");
		}

		if (gameManager == null)
		{
			gameManager = FindObjectOfType<RollerCoasterGameManager>();
			if (gameManager == null)
			{
				Debug.LogError("ZombieHidingSystem requires a RollerCoasterGameManager reference!");
			}
		}

		// Find rail scroller if not assigned
		if (railScroller == null)
		{
			railScroller = FindObjectOfType<InfiniteRailScroller>();
			if (railScroller == null)
			{
				Debug.LogError("ZombieHidingSystem requires an InfiniteRailScroller reference!");
			}
		}

		// Store original parent and position
		originalParent = transform.parent;
		originalLocalPosition = transform.localPosition;
	}

	private void Start()
	{
		// Auto-hide at the beginning of the game
		StartCoroutine(AutoHideAtStart());
	}

	private IEnumerator AutoHideAtStart()
	{
		// Wait a moment for everything to initialize
		yield return new WaitForSeconds(0.5f);

		// Hide automatically at start
		HideZombie();
		autoHideDone = true;
	}

	// Add a condition to Update to prevent unwanted game restarting
	private void Update()
	{
		// Only process clicks when not already in transition and rail is not moving
		if (!isHiding && !IsRailMoving())
		{
			// Check player clicks on hide spot
			if (Input.GetMouseButtonDown(0))
			{
				// Get mouse position in world space
				Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

				// If the player clicked on the hide spot
				if (Vector2.Distance(mousePosition, hideSpot.position) <= hideSpotClickRadius)
				{
					if (!isHidden)
					{
						// Hide the zombie
						HideZombie();
					}
				}
			}
		}

		// Check game state to determine when to unhide
		if (isHidden && !isHiding && gameManager != null)
		{
			// Modified: Only unhide during the specific ZombieBoarding state
			if (gameManager.CurrentState == RollerCoasterGameManager.GameState.ZombieBoarding)
			{
				UnhideZombie();
			}
		}

		// Modified: Check if humans are boarding and zombie isn't hidden
		// Only trigger human screaming if the game isn't in ride progress or restart
		if (!isHidden && !isHiding && gameManager != null && autoHideDone && !humansScared)
		{
			// If humans are gathering or boarding and zombie isn't hidden, make them run away
			if (gameManager.CurrentState == RollerCoasterGameManager.GameState.HumansGathering ||
				gameManager.CurrentState == RollerCoasterGameManager.GameState.HumansBoardingTrain)
			{
				// Trigger scream and run away (only if not already running)
				TriggerHumanScreaming();
				humansScared = true; // Set flag to true to prevent multiple scares in succession
			}
		}
	}

	// New method to check if the rail is moving
	private bool IsRailMoving()
	{
		if (railScroller == null)
			return false;

		// If the scroll speed is greater than a small threshold, consider the rail moving
		return Mathf.Abs(railScroller.scrollSpeed) > 0.01f;
	}

	public void HideZombie()
	{
		// Added check to prevent hiding when rail is moving
		if (isHiding || isHidden || IsRailMoving())
		{
			if (IsRailMoving())
			{
				Debug.Log("Cannot hide while rail is moving!");
			}
			return;
		}

		StartCoroutine(HideZombieCoroutine());
	}

	public void UnhideZombie()
	{
		if (isHiding || !isHidden)
			return;

		StartCoroutine(UnhideZombieCoroutine());
	}

	private IEnumerator HideZombieCoroutine()
	{
		// Double-check rail is not moving before proceeding
		if (IsRailMoving())
		{
			Debug.Log("Cannot hide while rail is moving!");
			yield break;
		}

		isHiding = true;

		// Play hide animation
		Animator animator = GetComponentInChildren<Animator>();
		if (animator != null && !string.IsNullOrEmpty(hideAnimationState))
		{
			animator.Play(hideAnimationState);
		}

		// Move to hiding spot
		Vector3 startPos = transform.position;
		Vector3 endPos = hideSpot.position;

		float elapsedTime = 0f;
		while (elapsedTime < hideMoveDuration)
		{
			transform.position = Vector3.Lerp(startPos, endPos, elapsedTime / hideMoveDuration);
			elapsedTime += Time.deltaTime;
			yield return null;
		}

		// Ensure final position is exact
		transform.position = endPos;

		// Parent to the hiding spot (optional, depends on your game setup)
		transform.SetParent(hideSpot);

		isHiding = false;
		isHidden = true;
		humansScared = false; // Reset the flag when zombie hides

		Debug.Log("Zombie is now hidden!");
	}

	private IEnumerator UnhideZombieCoroutine()
	{
		isHiding = true;

		// Short delay before unhiding
		yield return new WaitForSeconds(unhideDelay);

		// Play unhide animation
		Animator animator = GetComponentInChildren<Animator>();
		if (animator != null && !string.IsNullOrEmpty(unhideAnimationState))
		{
			animator.Play(unhideAnimationState);
		}

		// Restore original parent
		transform.SetParent(originalParent);

		// Get the last cart (assuming it's the one where zombie should start)
		RollerCoasterCart[] carts = FindObjectsOfType<RollerCoasterCart>();

		// Sort carts by position (front to back)
		System.Array.Sort(carts, (a, b) => b.transform.position.x.CompareTo(a.transform.position.x));

		// The last cart is the one at the back
		RollerCoasterCart backCart = carts[carts.Length - 1];

		// Find an empty seat in the back cart
		int seatIndex = -1;
		for (int i = 0; i < backCart.seats.Length; i++)
		{
			if (!backCart.seats[i].isOccupied)
			{
				seatIndex = i;
				break;
			}
		}

		if (seatIndex == -1)
		{
			// If no empty seat, use the first seat 
			// (or handle this differently as needed)
			seatIndex = 0;
			Debug.LogWarning("No empty seats in the back cart!");
		}

		// Move to the seat position
		Vector3 startPos = transform.position;
		Vector3 endPos = backCart.seats[seatIndex].transform.position;

		float elapsedTime = 0f;
		while (elapsedTime < hideMoveDuration)
		{
			transform.position = Vector3.Lerp(startPos, endPos, elapsedTime / hideMoveDuration);
			elapsedTime += Time.deltaTime;
			yield return null;
		}

		// Ensure final position is exact
		transform.position = endPos;

		// Update the ZombieController's cart reference
		// Access the currentCart field via reflection
		System.Reflection.FieldInfo cartField = typeof(ZombieController)
			.GetField("currentCart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		if (cartField != null)
		{
			cartField.SetValue(zombieController, backCart);
		}

		// Access the currentSeatIndex field via reflection
		System.Reflection.FieldInfo seatField = typeof(ZombieController)
			.GetField("currentSeatIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		if (seatField != null)
		{
			seatField.SetValue(zombieController, seatIndex);
		}

		isHiding = false;
		isHidden = false;

		Debug.Log($"Zombie is now unhidden and placed in the back cart at seat {seatIndex}!");
	}

	private void TriggerHumanScreaming()
	{
		// Find all humans in the scene
		HumanStateController[] humans = FindObjectsOfType<HumanStateController>();

		foreach (HumanStateController human in humans)
		{
			// Make humans scream and run away
			StartCoroutine(MakeHumanScreamAndRun(human));
		}

		// Schedule respawning of humans after they're all removed
		StartCoroutine(RespawnHumansAfterDelay());
	}

	private IEnumerator MakeHumanScreamAndRun(HumanStateController human)
	{
		if (human == null)
			yield break;

		// Get the HumanScreamingState component (your custom class)
		HumanScreamingState screamingState = human.GetComponent<HumanScreamingState>();

		if (screamingState != null)
		{
			// Use the existing method to make the human scream and run
			screamingState.ScreamAndRunAway(transform.position);
		}
		else
		{
			// Fallback if the human doesn't have the HumanScreamingState component
			// Play screaming animation
			Animator animator = human.GetComponent<Animator>();
			if (animator != null)
			{
				animator.Play("Screaming");
			}

			// Get movement controller
			HumanMovementController movement = human.GetComponent<HumanMovementController>();

			if (movement != null)
			{
				// Run away from the zombie (pick a random direction)
				Vector3 runDirection = new Vector3(
					Random.Range(-1f, 1f),
					Random.Range(-1f, 1f),
					0
				).normalized;

				// Run a shorter distance away (slower movement)
				Vector3 runTarget = human.transform.position + runDirection * 0.1f;
				movement.SetDestination(runTarget);
			}
		}

		// Wait longer before removing human (more noticeable)
		yield return new WaitForSeconds(5.0f);

		// Destroy the human
		if (human != null)
		{
			Destroy(human.gameObject);
		}
	}

	// New method to handle respawning humans after all are scared away
	private IEnumerator RespawnHumansAfterDelay()
	{
		// Wait for all humans to be removed and for the respawn delay
		yield return new WaitForSeconds(5.0f + respawnDelay);

		// Check if zombie is still visible (not hidden)
		if (!isHidden)
		{
			// Use GameManager to restart the human spawning process
			// We'll force the game state back to HumansGathering
			if (gameManager != null)
			{
				// Use reflection to access and invoke a private method in the game manager
				// This is a bit of a hack but works without modifying the GameManager class
				System.Reflection.MethodInfo clearMethod = typeof(RollerCoasterGameManager)
					.GetMethod("ClearSpawnedHumans", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				if (clearMethod != null)
				{
					clearMethod.Invoke(gameManager, null);
				}

				// Reset the scared flag to allow scaring the new humans
				humansScared = false;

				// Use reflection to restart the game loop coroutine
				// This approach assumes the game manager has a "GameLoop" coroutine
				// that we can restart to initiate a new round of humans spawning
				System.Reflection.FieldInfo currentStateField = typeof(RollerCoasterGameManager)
					.GetField("currentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				if (currentStateField != null)
				{
					// Force the game state back to HumansGathering
					currentStateField.SetValue(gameManager, RollerCoasterGameManager.GameState.HumansGathering);

					// Start a coroutine that will call SpawnAndGatherHumans in the game manager
					StartCoroutine(CallSpawnAndGatherHumans());
				}
			}
		}
	}

	// Helper method to call the private SpawnAndGatherHumans method in the game manager
	private IEnumerator CallSpawnAndGatherHumans()
	{
		// Short delay before spawning new humans
		yield return new WaitForSeconds(0.5f);

		// Use reflection to call SpawnAndGatherHumans
		System.Reflection.MethodInfo spawnMethod = typeof(RollerCoasterGameManager)
			.GetMethod("SpawnAndGatherHumans", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		if (spawnMethod != null)
		{
			// Start the coroutine in the game manager
			gameManager.StartCoroutine((IEnumerator)spawnMethod.Invoke(gameManager, null));
			Debug.Log("Respawning humans after they were scared away!");
		}
	}

	private void OnDrawGizmos()
	{
		// Draw hide spot
		if (hideSpot != null)
		{
			Gizmos.color = Color.magenta;
			Gizmos.DrawWireSphere(hideSpot.position, hideSpotClickRadius);

			// Label
			UnityEditor.Handles.color = Color.white;
			UnityEditor.Handles.Label(hideSpot.position + Vector3.up * 1f, "Zombie Hide Spot");
		}

		// Show current state when playing
		if (Application.isPlaying)
		{
			string stateText = isHidden ? "Hidden" : (isHiding ? "Hiding/Unhiding" : "Visible");

			// Add rail movement state to the display
			if (railScroller != null && Mathf.Abs(railScroller.scrollSpeed) > 0.01f)
			{
				stateText += " (Rail Moving)";
			}

			// Add human scared state
			if (humansScared)
			{
				stateText += " (Humans Scared)";
			}

			UnityEditor.Handles.color = Color.yellow;
			UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, stateText);
		}
	}
}