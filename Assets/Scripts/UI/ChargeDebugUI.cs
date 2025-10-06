using UnityEngine;
using UnityEngine.UI;

public class ChargeDebugUI : MonoBehaviour
{
    [SerializeField] private BeastMovementController controller;
    [SerializeField] private Text label;
    [SerializeField] private bool showCameraState = true;

    private void Reset()
    {
        label = GetComponent<Text>();
        if (controller == null)
            controller = FindFirstObjectByType<BeastMovementController>();
    }

    private void Awake()
    {
        if (label == null)
            label = GetComponent<Text>();
        if (controller == null)
            controller = FindFirstObjectByType<BeastMovementController>();
    }

    private void Update()
    {
        if (label == null || controller == null)
            return;

        float current = controller.CurrentCharge;
        float goal = controller.ChargeGoal;
        float cap = controller.ChargeMax;
        bool idleReady = controller.IdleReady;
        float decay = controller.CurrentChargeDecayPerSecond;
        float minThreshold = controller.MinChargeThreshold;

        System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
        sb.Append("Charge: ").Append(Mathf.RoundToInt(current)).Append(" / ").Append(Mathf.RoundToInt(goal)).Append(" (cap ").Append(Mathf.RoundToInt(cap)).Append(")");
        sb.Append("\nIdleReady: ").Append(idleReady ? "YES" : "NO").Append(" (tap ").Append(Mathf.RoundToInt(controller.ChargePerTap)).Append(")");
        sb.Append("\nDecay: ").Append(decay.ToString("0.0")).Append("/s");
        sb.Append("\nMinThreshold: ").Append(Mathf.RoundToInt(minThreshold));
        if (showCameraState)
            sb.Append(" | CamLocked: ").Append(controller.IsChargeCameraActive ? "YES" : "NO");
        label.text = sb.ToString();
    }
}

