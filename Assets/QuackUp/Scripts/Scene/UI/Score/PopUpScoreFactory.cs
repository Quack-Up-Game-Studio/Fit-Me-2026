using FitMe.Scene.UI.Score;
using QuackUp.Utils;        
using UnityEngine;
using VContainer;

namespace FitMe.Scene.UI.Score
{
    public class PopUpScoreFactory : IGameObjectFactory<PopUpScoreView>
    {
        private readonly PopUpScoreView _popUpPrefab;
        private readonly Transform _parent;
        private readonly Transform _scoreTransform;
        private readonly Transform _fitmeTransform;
        
        private Canvas _canvas;
        private Camera _uiCamera;
        
        public const string PopUpScoreParent = "ScoreParent";
        
        [Inject]
        public PopUpScoreFactory(
            PopUpScoreView popUpPrefab,
            [Key(PopUpScoreParent)] Transform parent,
            [Key(ScoreView.ScoreTransformKey)] Transform scoreTransform,
            [Key(ScoreView.FitmeTransformKey)] Transform fitmeTransform)
        {
            _popUpPrefab = popUpPrefab;
            _parent = parent;
            _scoreTransform = scoreTransform;
            _fitmeTransform = fitmeTransform;
            
            _canvas = _parent.GetComponentInParent<Canvas>();
            if (_canvas != null)
            {
                _uiCamera = _canvas.worldCamera; // กล้องที่ใช้ Render UI
            }
        }

        public PopUpScoreView Current { get; private set; }
        public GameObject CurrentGameObject { get; private set; }

        public PopUpScoreView Create(int score, Vector3 position, string name)
        {
            var targetTransform = Vector3.zero;
            switch (name)
            {
                case "Score":
                    targetTransform = _scoreTransform.position;
                    break;
                case "Fitme":
                    targetTransform = _fitmeTransform.position;
                    break;
            }
            
            return CreateInternal(score, position, targetTransform, Quaternion.identity, out _, null);
        }

        public PopUpScoreView Create()
        {
            return Create(Vector3.zero, Quaternion.identity, out _);
        }

        public PopUpScoreView Create(Vector3 position, Quaternion rotation, out GameObject gameObject, InstantiateParameters? instantiateParameters = null)
        {
            return CreateInternal(0, position, _scoreTransform.position, rotation, out gameObject, instantiateParameters);
        }

        private PopUpScoreView CreateInternal(int score, Vector3 worldPosition, Vector3 targetPosition, Quaternion rotation, out GameObject gameObject, InstantiateParameters? instantiateParameters)
        {
            instantiateParameters ??= new InstantiateParameters
            {
                parent = _parent,
            };

            var view = Object.Instantiate(_popUpPrefab, _parent);

            if (_canvas != null && Camera.main != null)
            {
                Vector3 screenPoint = Camera.main.WorldToScreenPoint(worldPosition);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parent as RectTransform, screenPoint, _uiCamera, out Vector2 localPoint
                );
                
                var rectTransform = view.transform as RectTransform;
                if (rectTransform != null)
                {
                    rectTransform.anchoredPosition = localPoint;
                    rectTransform.localPosition = new Vector3(rectTransform.localPosition.x, rectTransform.localPosition.y, 0f);
                }
            }
            else
            {
                view.transform.position = worldPosition;
            }
            var viewModel = new PopUpScoreViewModel(score);
            view.Construct(viewModel, targetPosition);
            
            Current = view;
            CurrentGameObject = view.gameObject;
            gameObject = CurrentGameObject;

            return view;
        }
    }
}