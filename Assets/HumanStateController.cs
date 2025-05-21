using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using static RollerCoasterGameManager;

public class HumanStateController : MonoBehaviour
{
	[Header("Timing Ranges (seconds)")]
	[SerializeField] private float minVulnerable = 2f;
	[SerializeField] private float maxVulnerable = 5f;
	[SerializeField] private float minDefensive = 3f;
	[SerializeField] private float maxDefensive = 6f;

	[Header("Animator State Names")]
	[Tooltip("Exact name of the Vulnerable state in your Animator Controller")]
	[SerializeField] private string vulnerableStateName = "Vulnerable";
	[Tooltip("Exact name of the Defensive state in your Animator Controller")]
	[SerializeField] private string defensiveStateName = "Defensive";
	[Tooltip("Exact name of the Death state in your Animator Controller")]
	[SerializeField] private string deathStateName = "Death";

	[Header("Debug")]
	[SerializeField] private bool showStateGizmo = true;

	private ZombieController zombie;

	private float vulnerableDuration;
	private float defensiveDuration;
	private float stateTimer;

	[Header("Panic Reactions")]
	[SerializeField] private float defensiveJumpDelay = 0.5f;
	[SerializeField] private float emptySeatJumpDelay = 1.0f;
	[SerializeField] private float zombieDetectionDelay = 0.2f;
	[SerializeField] private float zombieCheckInterval = 0.5f;

	private bool isCheckingSeat = false;
	private bool isCheckingForZombie = false;

	public enum State { Vulnerable, Defensive, Dead }
	private State currentState;

	private Animator animator;
	public bool IsVulnerable() => currentState == State.Vulnerable;
	// Add this method to HumanStateController
	public bool IsDefensive() => currentState == State.Defensive;
	RollerCoasterGameManager gameManager;
	public bool IsBeingDespawned { get; set; } // Add this property

	// Add a public getter to access the current state as a string (for debugging)
	public string CurrentStateAsString => currentState.ToString();

	void Awake()
	{
		gameManager = FindFirstObjectByType<RollerCoasterGameManager>();
		animator = GetComponentInChildren<Animator>();
		if (animator == null)
			Debug.LogError("HumanStateController requires an Animator component.");
	}

	void Start()
	{
		// Choose fixed durations once
		vulnerableDuration = Random.Range(minVulnerable, maxVulnerable);
		defensiveDuration = Random.Range(minDefensive, maxDefensive);

		// Start in Vulnerable
		currentState = State.Vulnerable;
		stateTimer = vulnerableDuration;

		// Make sure the DeadHumanThrower component is present
		if (GetComponent<DeadHumanThrower>() == null)
		{
			gameObject.AddComponent<DeadHumanThrower>();
		}
		zombie = FindFirstObjectByType<ZombieController>(); // Single FindObjectOfType call

		// Start zombie check immediately if starting in vulnerable state
		if (currentState == State.Vulnerable && !isCheckingForZombie)
		{
			isCheckingForZombie = true;
			StartCoroutine(CheckForZombieRoutine());
		}
	}

	void Update()
	{
		if (gameManager.CurrentState != GameState.RideInProgress)
		{
			return;
		}
		if (currentState == State.Dead)
		{
			return;
		}

		stateTimer -= Time.deltaTime;

		if (stateTimer <= 0f)
		{
			if (currentState == State.Vulnerable)
			{
				EnterDefensiveState();
			}
			else if (currentState == State.Defensive)
			{
				EnterVulnerableState();
			}
		}
	}

	private void EnterVulnerableState()
	{
		currentState = State.Vulnerable;
		stateTimer = vulnerableDuration;
		animator?.Play(vulnerableStateName);
		isCheckingSeat = false;

		// Start checking for zombies when entering vulnerable state
		if (!isCheckingForZombie)
		{
			isCheckingForZombie = true;
			StartCoroutine(CheckForZombieRoutine());
		}
	}

	private void EnterDefensiveState()
	{
		currentState = State.Defensive;
		stateTimer = defensiveDuration;
		animator?.Play(defensiveStateName);
		isCheckingForZombie = false;

		// Immediate check first
		CheckAdjacentSeatStatus();

		if (!isCheckingSeat)
		{
			isCheckingSeat = true;
			StartCoroutine(HandleDefensiveReactions());
		}
	}

	private IEnumerator CheckForZombieRoutine()
	{
		// Small initial delay before first check
		yield return new WaitForSeconds(zombieDetectionDelay);

		while (currentState == State.Vulnerable && !IsDead())
		{
			CheckForAdjacentZombie();
			yield return new WaitForSeconds(zombieCheckInterval);
		}

		isCheckingForZombie = false;
	}

	private void CheckForAdjacentZombie()
	{
		// Early exit if not in ride
		if (gameManager.CurrentState != GameState.RideInProgress) return;

		HumanSeatOccupant occupant = GetComponent<HumanSeatOccupant>();
		if (occupant == null || !occupant.IsSeated) return;

		RollerCoasterSeat currentSeat = occupant.OccupiedSeat;
		if (currentSeat == null) return;

		RollerCoasterCart cart = currentSeat.GetComponentInParent<RollerCoasterCart>();
		if (cart == null) return;

		int seatIndex = cart.GetSeatIndex(currentSeat);
		if (!cart.TryGetAdjacentSeat(seatIndex, out int adjacentIndex)) return;

		// Validate adjacent seat reference
		if (adjacentIndex < 0 || adjacentIndex >= cart.seats.Length) return;
		RollerCoasterSeat adjacentSeat = cart.seats[adjacentIndex];
		if (adjacentSeat == null) return;

		// Check specifically for zombie in adjacent seat
		bool zombiePresent = adjacentSeat.IsOccupiedByZombie;

		if (zombiePresent)
		{
			Debug.Log($"{name} detected zombie while vulnerable! Panicking!");

			// Only jump if not already screaming
			HumanScreamingState screamState = GetComponent<HumanScreamingState>();
			if (screamState != null && !screamState.IsScreaming)
			{
				StartCoroutine(JumpAfterDelay(zombieDetectionDelay, adjacentSeat));
			}
		}
	}

	private IEnumerator HandleDefensiveReactions()
	{
		// Wait for defensive animation to play briefly
		yield return new WaitForSeconds(defensiveJumpDelay);

		while (currentState == State.Defensive && !IsDead())
		{
			CheckAdjacentSeatStatus();
			yield return new WaitForSeconds(0.2f); // Increased from 0.5f to 0.2f
		}
	}

	private void CheckAdjacentSeatStatus()
	{
		// Early exit if not in ride
		if (gameManager.CurrentState != GameState.RideInProgress) return;

		HumanSeatOccupant occupant = GetComponent<HumanSeatOccupant>();
		if (occupant == null || !occupant.IsSeated) return;

		RollerCoasterSeat currentSeat = occupant.OccupiedSeat;
		if (currentSeat == null) return;

		RollerCoasterCart cart = currentSeat.GetComponentInParent<RollerCoasterCart>();
		if (cart == null) return;

		int seatIndex = cart.GetSeatIndex(currentSeat);
		if (!cart.TryGetAdjacentSeat(seatIndex, out int adjacentIndex)) return;

		// Validate adjacent seat reference
		if (adjacentIndex < 0 || adjacentIndex >= cart.seats.Length) return;
		RollerCoasterSeat adjacentSeat = cart.seats[adjacentIndex];
		if (adjacentSeat == null) return;

		// Check for empty seat, dead body or zombie
		bool seatEmpty = adjacentSeat.occupyingHuman == null && !adjacentSeat.IsOccupiedByZombie;
		bool deadBody = adjacentSeat.occupyingHuman != null && adjacentSeat.occupyingHuman.IsDead();
		bool zombiePresent = adjacentSeat.IsOccupiedByZombie;

		if (!seatEmpty && !deadBody && !zombiePresent) return;

		// Only jump if not already screaming
		HumanScreamingState screamState = GetComponent<HumanScreamingState>();
		if (screamState != null && !screamState.IsScreaming)
		{
			float jumpDelay = zombiePresent ? defensiveJumpDelay * 0.5f : (seatEmpty ? emptySeatJumpDelay : defensiveJumpDelay);
			StartCoroutine(JumpAfterDelay(jumpDelay, adjacentSeat));
		}
	}

	private IEnumerator JumpAfterDelay(float delay, RollerCoasterSeat adjacentSeat)
	{
		// Debug logging
		string jumpReason = adjacentSeat.IsOccupiedByZombie ? "zombie" :
							(adjacentSeat.occupyingHuman == null ? "empty seat" : "dead body");
		Debug.Log($"{name} detected {jumpReason}, jumping in {delay} seconds");

		yield return new WaitForSeconds(delay);

		// Double-check conditions before jumping
		if ((currentState == State.Defensive || currentState == State.Vulnerable) && !IsDead())
		{
			Debug.Log($"{name} executing jump");
			HumanScreamingState screamState = GetComponent<HumanScreamingState>();
			screamState?.JumpAndDespawn();
		}
		else
		{
			Debug.Log($"{name} aborted jump - now in state: {currentState}");
		}
	}

	public bool IsDead()
	{
		// Enforce state consistency
		if (currentState == State.Dead)
		{
			if (CurrentStateAsString != "Dead")
			{
				Debug.LogError($"State mismatch! Enum: {currentState} String: {CurrentStateAsString}");
			}
			return true;
		}
		return false;
	}

	public void EscapeAndDespawn()
	{
		if (currentState == State.Dead) return;

		// Immediately update state FIRST
		currentState = State.Dead;

		// Clean up components
		HumanSeatOccupant occupant = GetComponent<HumanSeatOccupant>();
		if (occupant != null)
		{
			occupant.LeaveSeat();
			Destroy(occupant); // Remove seat management
		}

		// Visual escape
		HumanScreamingState screamState = GetComponent<HumanScreamingState>();
		if (screamState != null)
		{
			screamState.JumpAndDespawn();
		}
		else
		{
			Destroy(gameObject); // Fallback
		}
	}

	public bool IsOnRide()
	{
		HumanSeatOccupant occupant = GetComponent<HumanSeatOccupant>();
		var gameManager = FindFirstObjectByType<RollerCoasterGameManager>();

		return occupant != null && occupant.IsSeated &&
			   gameManager != null &&
			   (gameManager.CurrentState == GameState.RideInProgress ||
				gameManager.CurrentState == GameState.HumansBoardingTrain);
	}

	/// <summary>
	/// Call this to kill the human: plays the death animation and stops further state changes.
	/// </summary>
	public void TriggerPanicInRadius(float radius)
	{
		Collider2D[] nearbyHumans = Physics2D.OverlapCircleAll(transform.position, radius);
		foreach (Collider2D col in nearbyHumans)
		{
			HumanScreamingState screamingHuman = col.GetComponent<HumanScreamingState>();
			if (screamingHuman != null && !screamingHuman.IsSeated() && zombie != null && zombie.GetComponent<ZombieHidingSystem>().IsHidden == false)
			{
				screamingHuman.ScreamAndRunAway(zombie.transform.position);
			}
		}
	}

	public void Die(bool causedByZombie = false)
	{
		if (currentState == State.Dead) return;

		// Don't trigger normal death if escaping
		var screamState = GetComponent<HumanScreamingState>();
		if (screamState != null && screamState.IsScreaming)
			return;

		currentState = State.Dead;
		if (animator != null)
			animator.Play(deathStateName);

		// REMOVED: TriggerPanicInRadius call
	}

	// Draw gizmos to visualize state - works in both 2D and 3D
	void OnDrawGizmos()
	{
		if (!showStateGizmo)
			return;

		// Only draw state when playing
		if (!Application.isPlaying)
			return;

		// Show state as a sphere above the human's head
		Vector3 iconPosition = transform.position + Vector3.up * 1f;

		switch (currentState)
		{
			case State.Vulnerable:
				Gizmos.color = Color.red; // Red = vulnerable/attackable
				break;
			case State.Defensive:
				Gizmos.color = Color.blue; // Blue = defensive/protected
				break;
			case State.Dead:
				Gizmos.color = Color.black; // Black = dead
				break;
		}

		Gizmos.DrawSphere(iconPosition, 0.2f);

		// Draw timer progress
		if (currentState != State.Dead)
		{
			float maxDuration = (currentState == State.Vulnerable) ? vulnerableDuration : defensiveDuration;
			float progress = stateTimer / maxDuration;

			Vector3 timerStart = iconPosition + Vector3.right * -0.5f;
			Vector3 timerEnd = timerStart + Vector3.right * progress;

			Gizmos.color = Color.yellow;
			Gizmos.DrawLine(timerStart, timerEnd);
		}
	}
}