using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace QuackUp.Utils
{
    public static class Texture2DUtils
    {
        public static Texture2D Decompress(this Texture2D source)
        {
            if (!source)
                return null;
            RenderTexture renderTex = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear);

            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            Texture2D readableText = new Texture2D(source.width, source.height);
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            return readableText;
        }

        public static Sprite ToSprite(this Texture2D texture)
        {
            return !texture
                ? null
                : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        
        public static async UniTask<Texture2D> LoadTextureFromUrl(string url)
        {
            using var uwr = UnityWebRequestTexture.GetTexture(url);
            var operation = uwr.SendWebRequest();
            
            while (!operation.isDone)
            {
                await UniTask.Yield();
            }
            
            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to load image: {uwr.error}");
                return null;
            }
            
            return DownloadHandlerTexture.GetContent(uwr);
        }
    }
}