// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("Z6+rtDbV72iE+009vVpYNCmDTuzi+ZdzG0K+TgI40CBqn23JIpld+RcxnG8BsF9yFCqGB69Fsg4yPhB42VpUW2vZWlFZ2VpaW/aq9Kdc2DdVRDeDnbJqMK62eylO8cghWAX8VqnAeMAhBK0QwDYrj3TUWbQG3aym46+kRJQd62pLUmFQ0Lcl/gUEGx754sDu/8ja2XmiUu/5jU6VIaJZUJ+zas1RP1pg3xMfcuo+F6ipuG/EX2VP5+t5QyeYKUrziBOkpVgGWClKqKL0lSm/IByN4rqK0czKgkelYsuWhKu4Po1dJBkSaPwhS+huvPcca9laeWtWXVJx3RPdrFZaWlpeW1hPD+zeu/v1MivQNKAHBS+o5LmCSOnyF8q/fVip5FlYWlta");
        private static int[] order = new int[] { 8,6,5,6,11,6,11,12,10,11,11,12,12,13,14 };
        private static int key = 91;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
