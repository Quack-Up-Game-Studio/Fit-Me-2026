using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace QuackUp.Input
{
    public interface IPointerHandler
    {
        Vector2 MouseWorldPosition { get; }
        Vector2 MouseCanvasPosition { get; }
        Vector2 WorldToLocalCanvasPosition(Vector3 worldPosition);
        Vector2 WorldToWorldCanvasPosition(Vector3 worldPosition);
    }
    
    public class PointerHandler : IPointerHandler
    { 
        private readonly Camera _gameCamera;
        private readonly Canvas _gameCanvas;
        
        [Inject]
        public PointerHandler(
            Camera gameCamera,
            Canvas gameCanvas)
        {
            _gameCamera = gameCamera;
            _gameCanvas = gameCanvas;
        }
        
        #region Properties
        public Vector2 MouseWorldPosition
        {
            get
            {
                var rawPosition = Pointer.current.position.ReadValue();
                var mousePosition = _gameCamera.ScreenToWorldPoint(rawPosition);
                var final = new Vector2(mousePosition.x, mousePosition.y);
                return final;
            }
        }
        
        public Vector2 MouseCanvasPosition
        {
            get
            {
                var rawPosition = Pointer.current.position.ReadValue();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _gameCanvas.transform as RectTransform, 
                    rawPosition, 
                    _gameCanvas.worldCamera, 
                    out var localPoint);
                return _gameCanvas.transform.TransformPoint(localPoint);
            }
        }
        #endregion

        #region Utils
        
        public Vector2 WorldToLocalCanvasPosition(Vector3 worldPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _gameCanvas.transform as RectTransform, 
                _gameCamera.WorldToScreenPoint(worldPosition), 
                _gameCanvas.worldCamera, 
                out var localPoint);
            return localPoint;
        }

        public Vector2 WorldToWorldCanvasPosition(Vector3 worldPosition)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _gameCanvas.transform as RectTransform,
                _gameCamera.WorldToScreenPoint(worldPosition),
                _gameCanvas.worldCamera,
                out var worldPoint);
            return worldPoint;
        }
        #endregion
    }
}