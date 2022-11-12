namespace Kalmia.EndGameSolver
{
    public interface IEndGameSolver 
    {
        public ulong InternalNodeCount { get; }
        public ulong LeafNodeCount { get; }
        public bool IsSearching { get; }
        public int SearchEllapsedMilliSec { get; }
        public float Nps { get; }
    }
}
