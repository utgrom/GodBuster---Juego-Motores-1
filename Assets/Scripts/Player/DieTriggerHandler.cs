using UnityEngine;

/// <summary>
/// Enables a UI panel when this Rigidbody enters a trigger tagged with the die tag.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DieTriggerHandler : MonoBehaviour
{
    [SerializeField] private string dieTag = "Die";
    [SerializeField] private GameObject deathScreen;
    [SerializeField] private bool hideUiOnStart = true;

    private bool hasTriggered;

    private void Awake()
    {
        if (hideUiOnStart && deathScreen != null)
        {
            deathScreen.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || !other.CompareTag(dieTag))
        {
            return;
        }

        hasTriggered = true;

        if (deathScreen != null)
        {
            deathScreen.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Debug.LogWarning($"{nameof(DieTriggerHandler)} on {name} needs a deathScreen reference.", this);
        }
    }
}
