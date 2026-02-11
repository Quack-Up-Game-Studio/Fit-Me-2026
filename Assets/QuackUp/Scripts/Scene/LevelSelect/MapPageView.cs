using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Scene
{
    public class MapPageView : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect; // ✨ อย่าลืมลาก Scroll View มาใส่ใน Inspector
        [SerializeField] private Transform contentParent;
        [SerializeField] private MapChunkView[] chunkPrefab;

        private MapPageViewModel _vm;
        
        // จำว่าเราโหลด Chunk ไหนไปแล้วบ้าง
        private int _lowestLoadedChunk = -1;
        private int _highestLoadedChunk = -1;

        // กันการโหลดซ้อนทับกันหลายรอบ
        private bool _isLoading = false; 

        [Inject]
        public void Construct(MapPageViewModel vm)
        {
            _vm = vm;
            var initialChunks = vm.GetChunksToLoad();

            // 1. โหลดด่านชุดแรกตอนเปิดหน้าจอ
            if (initialChunks.Count > 0)
            {
                _lowestLoadedChunk = initialChunks[0];
                _highestLoadedChunk = initialChunks[initialChunks.Count - 1];

                foreach (var chunkIndex in initialChunks)
                {
                    LoadChunk(chunkIndex, isTop: true); // โหลดต่อยอดขึ้นไป
                }
            }

            // 2. ✨ แอบฟัง Event เวลานิ้วผู้เล่นปัดหน้าจอ
            scrollRect.onValueChanged.AddListener(OnScroll);
        }

        // ฟังก์ชันสำหรับเสก Chunk 1 แผ่น
        private void LoadChunk(int chunkIndex, bool isTop)
        {
            if (chunkIndex < 0 || chunkIndex >= chunkPrefab.Length) return;

            var chunk = Instantiate(chunkPrefab[chunkIndex], contentParent);
            
            // ⚠️ สำคัญ: จัดคิว (สมมติว่าคุณเปิด Reverse Arrangement ตามที่คุยกันรอบที่แล้ว)
            // ถ้าเป็นการโหลดด่านเก่า (แผ่นล่าง) ต้องดันไปอยู่คิวแรกสุดของ Hierarchy
            if (!isTop) 
            {
                chunk.transform.SetAsFirstSibling(); 
            }

            int startLevel = (chunkIndex * 20) + 1;
            chunk.Setup(startLevel, _vm);
        }

        // ฟังก์ชันนี้จะทำงานทุกเสี้ยววินาทีที่หน้าจอขยับ
        private void OnScroll(Vector2 scrollPos)
        {
            if (_isLoading) return;

            // ค่า scrollPos.y จะอยู่ระหว่าง 0 (ล่างสุด) ถึง 1 (บนสุด)
            
            // 🔼 ถ้าเลื่อนขึ้นไปใกล้จะสุดทาง (เช่น เกิน 80% ของจอ)
            if (scrollPos.y >= 0.8f) 
            {
                // ถ้ายังมีแผ่นให้โหลดต่อ
                if (_highestLoadedChunk < chunkPrefab.Length - 1)
                {
                    _isLoading = true; // ล็อกไว้ก่อน กันมันโหลดเบิ้ล
                    
                    _highestLoadedChunk++;
                    LoadChunk(_highestLoadedChunk, isTop: true);
                    
                    _isLoading = false;
                }
            }
            // 🔽 ถ้าเลื่อนลงมาใกล้แผ่นล่างสุด (เช่น ต่ำกว่า 20% ของจอ)
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
            // ล้าง Event คืนเมมโมรี่ตอนปิดฉาก
            if (scrollRect != null)
            {
                scrollRect.onValueChanged.RemoveListener(OnScroll);
            }
        }
    }
}
