// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("YTwuARKUJ/eOs7jCVovhQsQWXbblpUZ0EVFfmIF6ngqtr4UCThMo4s0FAR6cf0XCLlHnlxfw8p6DKeRGNRnAZ/uV8Mp1ubXYQJS9AgMSxW5z8P7xwXPw+/Nz8PDxXABeDfZynVNIakRVYnBz0wj4RVMn5D+LCPP69c/lTUHT6Y0yg+BZIrkOD/Ks8oNJBQ7uPrdBwOH4y/p6HY9Ur66xtL2bNsWrGvXYvoAsrQXvGKSYlLrS4AIIXj+DFYq2J0gQIHtmYCjtD8jBc/DTwfz3+Nt3uXcG/PDw8PTx8gNq0mqLrge6apyBJd5+8x6sdwYM/+6dKTcYwJoEHNGD5Ftii/KvVvxIUz3ZsegU5KiSeorANcdjiDP3U0NYvWAV1/IDTvPy8PHw");
        private static int[] order = new int[] { 7,13,8,10,13,12,8,13,12,9,13,13,13,13,14 };
        private static int key = 241;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
