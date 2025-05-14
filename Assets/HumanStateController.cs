using UnityEngine;

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

	private float vulnerableDuration;
	private float defensiveDuration;
	private float stateTimer;

	private enum State { Vulnerable, Defensive, Dead }
	private State currentState;

	private Animator animator;
	public bool IsVulnerable() => currentState == State.Vulnerable;

	// Add a public getter to access the current state as a string (for debugging)
	public string CurrentStateAsString => currentState.ToString();

	void Awake()
	{
		animator = GetComponent<Animator>();
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

		// Make sure the Animator shows the initial state
		if (animator != null)
			animator.Play(vulnerableStateName);
	}

	void Update()
	{
		if (currentState == State.Dead)
			return; // no more state changes once dead

		stateTimer -= Time.deltaTime;
		if (stateTimer <= 0f)
		{
			if (currentState == State.Vulnerable)
			{
				currentState = State.Defensive;
				stateTimer = defensiveDuration;
				if (animator != null)
					animator.Play(defensiveStateName);
			}
			else if (currentState == State.Defensive)
			{
				currentState = State.Vulnerable;
				stateTimer = vulnerableDuration;
				if (animator != null)
					animator.Play(vulnerableStateName);
			}
		}
	}

	/// <summary>
	/// Call this to kill the human: plays the death animation and stops further state changes.
	/// </summary>
	public void Die()
	{
		if (currentState == State.Dead) return;
		currentState = State.Dead;
		if (animator != null)
			animator.Play(deathStateName);

		Debug.Log($"Human {gameObject.name} has died!");
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