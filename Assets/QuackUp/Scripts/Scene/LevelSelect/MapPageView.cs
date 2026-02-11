using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Scene
{
    public class MapPageView : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect; 
        [SerializeField] private Transform contentParent;
        [SerializeField] private MapChunkView[] chunkPrefab;
        
        private MapPageViewModel _vm;
        
        private int _lowestLoadedChunk = -1;
        private int _highestLoadedChunk = -1;

        private bool _isLoading = false; 

        [Inject]
        public void Construct(MapPageViewModel vm)
        {
            _vm = vm;
            var initialChunks = vm.GetChunksToLoad();

            if (initialChunks.Count > 0)
            {
                _lowestLoadedChunk = initialChunks[0];
                _highestLoadedChunk = initialChunks[initialChunks.Count - 1];

                foreach (var chunkIndex in initialChunks)
                {
                    LoadChunk(chunkIndex, isTop: true);
                }
            }

            scrollRect.onValueChanged.AddListener(OnScroll);
        }

        private void LoadChunk(int chunkIndex, bool isTop)
        {
            if (chunkIndex < 0 || chunkIndex >= chunkPrefab.Length) return;

            var chunk = Instantiate(chunkPrefab[chunkIndex], contentParent);

            if (!isTop) 
            {
                chunk.transform.SetAsFirstSibling(); 
            }

            int startLevel = (chunkIndex * 20) + 1;
            chunk.Setup(startLevel, _vm);
        }


        private void OnScroll(Vector2 scrollPos)
        {
            if (_isLoading) return;

            if (scrollPos.y >= 0.8f) 
            {
                if (_highestLoadedChunk < chunkPrefab.Length - 1)
                {
                    _isLoading = true;
                    
                    _highestLoadedChunk++;
                    LoadChunk(_highestLoadedChunk, isTop: true);
                    
                    _isLoading = false;
                }
            }
            else if (scrollPos.y <= 0.2f)
            {
                if (_lowestLoadedChunk > 0)
                {
                    _isLoading = true;
                    
                    _lowestLoadedChunk--;
                    LoadChunk(_lowestLoadedChunk, isTop: false);
                    
                    _isLoading = false;
                }
            }
        }

        private void OnDestroy()
        {
            if (scrollRect != null)
            {
                scrollRect.onValueChanged.RemoveListener(OnScroll);
            }
        }
    }
}
