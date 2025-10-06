using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScoreDisplayUI : MonoBehaviour
{
    [SerializeField] private CollectibleCounter counter;
    [SerializeField] private TMP_Text label;
    [SerializeField] private int scoreGoal = 10;
    [SerializeField] private string goalSceneName = "End";

    private bool sceneQueued;
    private bool subscribed;

    private void Reset()
    {
        if (label == null)
        {
            label = GetComponent<TMP_Text>();
        }
    }

    private void OnEnable()
    {
        sceneQueued = false;
        subscribed = false;

        if (counter == null)
        {
            counter = CollectibleCounter.Instance;
        }

        if (label == null)
        {
            label = GetComponent<TMP_Text>();
        }

        TrySubscribe();
    }

    private void OnDisable()
    {
        if (counter != null && subscribed)
        {
            counter.OnCounterChanged.RemoveListener(HandleCounterChanged);
        }
        counter = null;
        subscribed = false;
    }

    private void Update()
    {
        if (!subscribed)
        {
            TrySubscribe();
        }
    }
    private void TrySubscribe()
    {
        var currentInstance = CollectibleCounter.Instance;
        if (counter != currentInstance)
        {
            if (counter != null && subscribed)
            {
                counter.OnCounterChanged.RemoveListener(HandleCounterChanged);
                subscribed = false;
            }
            counter = currentInstance;
        }

        if (counter != null && !subscribed)
        {
            counter.OnCounterChanged.AddListener(HandleCounterChanged);
            subscribed = true;
            UpdateLabel(counter.CollectedCount, counter.TotalCount);
        }
        else if (!subscribed)
        {
            UpdateLabel(0, 0);
        }
    }

    private void HandleCounterChanged(int collected, int total)
    {
        UpdateLabel(collected, total);
        if (!sceneQueued && collected >= scoreGoal && !string.IsNullOrEmpty(goalSceneName))
        {
            sceneQueued = true;
            SceneManager.LoadScene(goalSceneName);
        }
    }

    private void UpdateLabel(int collected, int total)
    {
        if (label == null)
        {
            return;
        }

        label.text = scoreGoal > 0 ? $"{collected}/{scoreGoal}" : collected.ToString();
    }
}
