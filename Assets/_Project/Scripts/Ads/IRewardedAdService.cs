public interface IRewardedAdService
{
    bool IsReady { get; }
    void Show(System.Action onReward, System.Action onClosed, System.Action<string> onError);
}
