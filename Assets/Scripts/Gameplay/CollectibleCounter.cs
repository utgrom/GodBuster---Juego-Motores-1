using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class CollectibleCounter : MonoBehaviour
{
    public static CollectibleCounter Instance { get; private set; }

    [System.Serializable]
    public class CounterChangedEvent : UnityEvent<int, int> { }

    [Header("Configuration")]
    [SerializeField]
    private bool persistAcrossScenes = false;

    [Header("Events")]
    [SerializeField]
    private CounterChangedEvent onCounterChanged = new CounterChangedEvent();

    [SerializeField]
    private UnityEvent onAllCollected = new UnityEvent();

    private readonly HashSet<CollectibleItem> activeCollectibles = new HashSet<CollectibleItem>();
    private int collectedCount;

    public int CollectedCount => collectedCount;
    public int RemainingCount => activeCollectibles.Count;
    public int TotalCount => collectedCount + activeCollectibles.Count;
    public CounterChangedEvent OnCounterChanged => onCounterChanged;
    public UnityEvent OnAllCollected => onAllCollected;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple CollectibleCounter instances detected. Destroying the newest one.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    internal void Register(CollectibleItem collectible)
    {
        if (collectible == null)
        {
            return;
        }

        if (activeCollectibles.Add(collectible))
        {
            RaiseCounterChanged();
        }
    }

    internal void Unregister(CollectibleItem collectible)
    {
        if (collectible == null)
        {
            return;
        }

        if (activeCollectibles.Remove(collectible))
        {
            RaiseCounterChanged();
        }
    }

    internal void ReportCollected(CollectibleItem collectible, int amount)
    {
        if (collectible != null)
        {
            activeCollectibles.Remove(collectible);
        }

        ReportCollected(amount);
    }

    internal void ReportCollected(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        collectedCount += amount;
        RaiseCounterChanged();

        if (RemainingCount == 0 && TotalCount > 0)
        {
            onAllCollected.Invoke();
        }
    }

    private void RaiseCounterChanged()
    {
        onCounterChanged.Invoke(collectedCount, TotalCount);
    }
}
