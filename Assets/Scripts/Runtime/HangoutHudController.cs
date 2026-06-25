using UnityEngine;
using UnityEngine.UI;

public sealed class HangoutHudController : MonoBehaviour
{
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private Button detailButton;
    [SerializeField] private Text detailButtonText;
    [SerializeField] private Text progressText;
    [SerializeField] private Image progressFill;
    [SerializeField] private Text detailSummaryText;
    [SerializeField] private float demoCycleSeconds = 120f;

    private bool isExpanded;
    private float currentProgress;

    public void Bind(
        GameObject boundDetailPanel,
        Button boundDetailButton,
        Text boundDetailButtonText,
        Text boundProgressText,
        Image boundProgressFill,
        Text boundDetailSummaryText)
    {
        detailPanel = boundDetailPanel;
        detailButton = boundDetailButton;
        detailButtonText = boundDetailButtonText;
        progressText = boundProgressText;
        progressFill = boundProgressFill;
        detailSummaryText = boundDetailSummaryText;

        InitializeBindings();
    }

    private void Awake()
    {
        InitializeBindings();
    }

    private void Update()
    {
        var cycle = Mathf.Max(1f, demoCycleSeconds);
        SetProgress(Mathf.Repeat(Time.unscaledTime / cycle, 1f));
    }

    public void ToggleDetails()
    {
        SetDetailsVisible(!isExpanded);
    }

    public void SetDetailsVisible(bool visible)
    {
        isExpanded = visible;

        if (detailPanel == null)
        {
            Debug.LogError("HangoutHudController 缺少 DetailPanel 引用，无法切换详情面板。", this);
            return;
        }

        detailPanel.SetActive(isExpanded);
        RefreshTexts();
    }

    public void SetProgress(float normalizedProgress)
    {
        currentProgress = Mathf.Clamp01(normalizedProgress);

        if (progressFill != null)
        {
            progressFill.fillAmount = currentProgress;
        }

        RefreshTexts();
    }

    private void InitializeBindings()
    {
        if (detailButton == null)
        {
            return;
        }

        detailButton.onClick.RemoveListener(ToggleDetails);
        detailButton.onClick.AddListener(ToggleDetails);

        if (detailPanel != null)
        {
            detailPanel.SetActive(isExpanded);
        }

        RefreshTexts();
    }

    private void RefreshTexts()
    {
        var percent = Mathf.RoundToInt(currentProgress * 100f);

        if (progressText != null)
        {
            progressText.text = $"挂机进程 {percent}%";
        }

        if (detailButtonText != null)
        {
            detailButtonText.text = isExpanded ? "收起" : "详情";
        }

        if (detailSummaryText != null)
        {
            var secondsLeft = Mathf.CeilToInt((1f - currentProgress) * Mathf.Max(1f, demoCycleSeconds));
            detailSummaryText.text =
                $"本轮进度：{percent}%\n" +
                $"预计结算：{secondsLeft:00} 秒后\n" +
                "当前状态：自动挂机中\n" +
                "收益速度：演示数据";
        }
    }
}
