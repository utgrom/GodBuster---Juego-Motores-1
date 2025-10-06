using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CollectorEntity : MonoBehaviour
{
    [SerializeField] private string collectibleTag = "Recolectable";

    [Header("Fallback SFX (optional if item lacks SFX)")]
    [SerializeField] private AudioClip pickupSfx;
    [SerializeField] private bool playSfxAtListener = true;

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other.gameObject);
    }

    private void TryCollect(GameObject other)
    {
        if (other == null || !other.CompareTag(collectibleTag))
        {
            return;
        }

        var item = other.GetComponent<CollectibleItem>();
        if (item == null)
        {
            item = other.GetComponentInParent<CollectibleItem>();
        }

        if (item != null)
        {
            item.Collect(gameObject);
        }
        else
        {
            if (pickupSfx != null)
            {
                Vector3 pos = (playSfxAtListener && Camera.main != null) ? Camera.main.transform.position : other.transform.position;
                AudioSource.PlayClipAtPoint(pickupSfx, pos);
            }
            Destroy(other);
        }

        CollectibleCounter.Instance?.ReportCollected(1);
    }
}
