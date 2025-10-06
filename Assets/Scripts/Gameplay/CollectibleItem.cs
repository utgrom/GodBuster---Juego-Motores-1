using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CollectibleItem : MonoBehaviour
{
    [Header("Feedback (optional)")]
    [SerializeField] private GameObject collectedVfx;
    [SerializeField] private AudioClip collectedSfx;
    [SerializeField] private bool playSfxAtListener = true;

    private bool hasBeenCollected;

    private void Reset()
    {
        // Use trigger so the player collector can process OnTriggerEnter
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        // Set Tag to "Recolectable" in the Inspector.
    }

    public void Collect(GameObject collector = null)
    {
        if (hasBeenCollected) return;
        hasBeenCollected = true;

        if (collectedVfx != null)
        {
            Instantiate(collectedVfx, transform.position, Quaternion.identity);
        }
        if (collectedSfx != null)
        {
            Vector3 pos = (playSfxAtListener && Camera.main != null) ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(collectedSfx, pos);
        }

        Destroy(gameObject);
    }
}
