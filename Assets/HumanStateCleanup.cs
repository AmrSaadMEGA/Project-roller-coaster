using UnityEngine;

[RequireComponent(typeof(HumanStateController))]
public class HumanStateCleanup : MonoBehaviour
{
    private HumanStateController state;

    void Awake() => state = GetComponent<HumanStateController>();
    
    void Update()
    {
        if (state.IsDead() && state.CurrentStateAsString != "Dead")
        {
            Debug.LogError($"State mismatch! Force cleanup {name}");
            Destroy(gameObject);
        }
    }
}