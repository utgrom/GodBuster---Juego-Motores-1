using UnityEngine;
using UnityEngine.UI;

public class ChargeBarUI : MonoBehaviour
{
    [SerializeField] private BeastMovementController controller;
    [SerializeField] private Slider slider;
    [SerializeField] private Image fillImage;
    [SerializeField] private Color lowColor = Color.yellow;
    [SerializeField] private Color highColor = Color.red;
    [SerializeField] private bool useCanvasGroup = true;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeSpeed = 10f;

    private void Reset()
    {
        if (slider == null)
            slider = GetComponentInChildren<Slider>();
        if (controller == null)
            controller = FindFirstObjectByType<BeastMovementController>();
        if (fillImage == null && slider != null)
            fillImage = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        if (useCanvasGroup && canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>();
    }

    private void Awake()
    {
        if (controller == null)
            controller = FindFirstObjectByType<BeastMovementController>();
        if (slider == null)
            slider = GetComponentInChildren<Slider>();
        if (fillImage == null && slider != null)
            fillImage = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        if (useCanvasGroup && canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>();
        if (slider != null && controller != null)
        {
            slider.minValue = 0f;
            slider.maxValue = Mathf.Max(1f, controller.ChargeGoal);
        }
    }

    private void Update()
    {
        if (controller == null || slider == null)
            return;

        float goal = Mathf.Max(1f, controller.ChargeGoal);
        slider.maxValue = goal;
        slider.value = Mathf.Min(controller.CurrentCharge, goal);

        float t = controller.ChargeGoal > 0f ? Mathf.Clamp01(controller.CurrentCharge / controller.ChargeGoal) : 1f;
        if (fillImage != null)
            fillImage.color = Color.Lerp(lowColor, highColor, Mathf.SmoothStep(0f, 1f, t));

        bool shouldShow = controller.CurrentCharge >= controller.MinChargeThreshold;
        if (useCanvasGroup && canvasGroup != null)
        {
            float target = shouldShow ? 1f : 0f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, fadeSpeed * Time.deltaTime);
            canvasGroup.interactable = shouldShow;
            canvasGroup.blocksRaycasts = shouldShow;
        }
        else if (slider.gameObject.activeSelf != shouldShow)
        {
            slider.gameObject.SetActive(shouldShow);
        }
    }
}
