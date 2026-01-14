using UnityEngine;
using VContainer;

namespace FitMe.Grid
{
    public class AtomView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        private AtomViewModel _viewModel;
        
        [Inject]
        public void Construct(AtomViewModel viewModel)
        {
            _viewModel = viewModel;
        }
    }
}
