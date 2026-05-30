namespace Recipe.Models
{
    public class BatchSwitchContext
    {
        public string TargetRecipeName { get; }
        public string PoolId { get; }
        public string PoolName { get; }
        public bool IsBatchMode => true;

        public BatchSwitchContext(string targetRecipeName, string poolId, string poolName)
        {
            TargetRecipeName = targetRecipeName;
            PoolId = poolId;
            PoolName = poolName;
        }
    }
}
