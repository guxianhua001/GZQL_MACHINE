using Core.Models;

namespace Core.Abstraction
{
    public interface ITreeConfigService
    {
        Task<List<TreeNode>> LoadTreeStructureAsync();
        Task SaveTreeStructureAsync(List<TreeNode> nodes);
        TreeNode FindNodeByPath(List<TreeNode> nodes, string path);
    }
}
