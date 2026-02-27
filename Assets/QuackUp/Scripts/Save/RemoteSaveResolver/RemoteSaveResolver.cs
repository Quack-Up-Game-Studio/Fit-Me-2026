using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using VContainer;

namespace QuackUp.Save
{
    public class RemoteSaveResolver
    {
        private readonly RemoteSaveResolverConfig _config;
        
        [Inject]
        public RemoteSaveResolver(
            RemoteSaveResolverConfig config)
        {
            _config = config;
        }
        
        public async UniTask<ConflictSolution> ResolveConflictAsync(DeserializedSaveData local, DeserializedSaveData remote)
        {
            foreach (var provider in _config.SolutionProviders)
            {
                var solution = await provider.GetConflictSolutionAsync(local, remote);
                DebugUtils.Log($"Solution conflict resolved: {solution}");
                if (solution != ConflictSolution.PassThrough) return solution;
            }
            return _config.FallbackSolution is ConflictSolution.PassThrough ? ConflictSolution.Abort : _config.FallbackSolution;
        }
    }
}