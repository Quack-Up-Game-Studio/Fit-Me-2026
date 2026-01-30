using Spine;

namespace QuackUp.Utils
{
    /// <summary>
    /// Interface for spine animator.
    /// </summary>
    /// <typeparam name="TKey">Type of the animation key.</typeparam>
    public interface ISpineAnimator<in TKey>
    {
        public TrackEntry Set(TKey key, int index, bool loop);
        public TrackEntry Add(TKey key, int index, bool loop, float delay);
        public TrackEntry SetEmpty(int index, float mixDuration);
        public void SetEmptyAll(float mixDuration);
        public TrackEntry AddEmpty(int index, float mixDuration, float delay);
        public void ClearTrack(int index);
        public void ClearTracks();
        public TrackEntry GetCurrent(int index);
    }
}