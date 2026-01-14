using UnityEngine;

namespace FitMe.Grid
{
    public class Atom : MonoBehaviour
    {
        public SpriteRenderer SpriteRenderer { get; private set; }
        public Block ParentBlock { get; set; }
        //public SpriteOutlineController SpriteOutlineController { get; private set; }

        void Awake()
        {
            SpriteRenderer = GetComponent<SpriteRenderer>();
            //SpriteOutlineController = GetComponent<SpriteOutlineController>();
        }
    }
}
