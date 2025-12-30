

public interface INeedleService
{
    int GetNeedleUsageCount(int needleId);
    int GetNeedleMaxCount(int needleId);
    void IncrementNeedleCount(int needleId);
    void ResetNeedle(int needleId);
}