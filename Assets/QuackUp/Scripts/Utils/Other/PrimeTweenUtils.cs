using PrimeTween;
using UnityEngine;
using UnityEngine.Assertions;

namespace QuackUp.Utils
{
    public static class PrimeTweenUtils
    {
        /// <summary>
        /// Play the sequence in a specific direction.
        /// </summary>
        /// <param name="seq"></param>
        /// <param name="isForwardDirection"></param>
        /// <returns></returns>
        public static Sequence ApplyDirection(this Sequence seq, bool isForwardDirection) 
        {
            Assert.IsTrue(seq.isAlive);
            Assert.AreNotEqual(0f, seq.durationTotal);
            Assert.AreNotEqual(-1, seq.cyclesTotal);
            seq.timeScale = Mathf.Abs(seq.timeScale) * (isForwardDirection ? 1f : -1f);
            if (isForwardDirection) 
            {
                if (seq.progressTotal >= 1f) 
                {
                    seq.progressTotal = 0f;
                }
            } 
            else 
            {
                if (seq.progressTotal == 0f) 
                {
                    seq.progressTotal = 1f;
                }
            }
            return seq;
        }
        
        #region ToRelative
        public static TweenSettings<Vector4> ToRelative(this TweenSettings<Vector4> settings, Vector4 start)
        {
            return new TweenSettings<Vector4>
            {
                startValue = start + settings.startValue,
                endValue = settings.endValue + start,
                settings = settings.settings,
                startFromCurrent = settings.startFromCurrent,
            };
        }
        
        public static TweenSettings<Vector3> ToRelative(this TweenSettings<Vector3> settings, Vector3 start)
        {
            return new TweenSettings<Vector3>
            {
                startValue = start + settings.startValue,
                endValue = settings.endValue + start,
                settings = settings.settings,
                startFromCurrent = settings.startFromCurrent,
            };
        }
        
        public static TweenSettings<Vector2> ToRelative(this TweenSettings<Vector2> settings, Vector2 start)
        {
            return new TweenSettings<Vector2>
            {
                startValue = start + settings.startValue,
                endValue = settings.endValue + start,
                settings = settings.settings,
                startFromCurrent = settings.startFromCurrent,
            };
        }
        
        public static TweenSettings<float> ToRelative(this TweenSettings<float> settings, float start)
        {
            return new TweenSettings<float>
            {
                startValue = start + settings.startValue,
                endValue = settings.endValue + start,
                settings = settings.settings,
                startFromCurrent = settings.startFromCurrent,
            };
        }
        
        public static TweenSettings<Quaternion> ToRelative(this TweenSettings<Quaternion> settings, Quaternion start)
        {
            return new TweenSettings<Quaternion>
            {
                startValue = start * settings.startValue,
                endValue = settings.endValue * start,
                settings = settings.settings,
                startFromCurrent = settings.startFromCurrent,
            };
        }
        
        public static TweenSettings<int> ToRelative(this TweenSettings<int> settings, int start)
        {
            return new TweenSettings<int>
            {
                startValue = start + settings.startValue,
                endValue = settings.endValue + start,
                settings = settings.settings,
                startFromCurrent = settings.startFromCurrent,
            };
        }
        
        public static TweenSettings<Color> ToRelative(this TweenSettings<Color> settings, Color start)
        {
            return new TweenSettings<Color>
            {
                startValue = start + settings.startValue,
                endValue = settings.endValue + start,
                settings = settings.settings,
                startFromCurrent = settings.startFromCurrent,
            };
        }
        #endregion
        
        public static TweenSettings<Vector2> ToVector2(this TweenSettings<Vector3> settings)
        {
            return new TweenSettings<Vector2>
            {
                startValue = settings.startValue,
                endValue = settings.endValue,
                settings = settings.settings,
                startFromCurrent = settings.startFromCurrent,
            };
        }
        
        public static TweenSettings<Vector3> ToVector3(this TweenSettings<Vector2> settings, float z = 0f)
        {
            return new TweenSettings<Vector3>
            {
                startValue = new Vector3(settings.startValue.x, settings.startValue.y, z),
                endValue = new Vector3(settings.endValue.x, settings.endValue.y, z),
                settings = settings.settings,
                startFromCurrent = settings.startFromCurrent,
            };
        }
    }
}