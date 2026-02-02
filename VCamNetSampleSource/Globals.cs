using System.Threading;

namespace VCamNetSampleSource
{
    internal static class Globals
    {
        public static readonly FrameBuffer Frames = new();

        private static int _started = 0;
        private static Timer? _timer;

        // Llamar a esto desde Generate(), se auto-protege para arrancar solo una vez
        public static void EnsureFakeFramesStarted()
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
                return;

            const int w = 640;
            const int h = 360;

            bool toggle = false;

            _timer = new Timer(_ =>
            {
                toggle = !toggle;

                var rgba = new byte[w * h * 4];
                byte r = toggle ? (byte)255 : (byte)0;
                byte g = toggle ? (byte)0 : (byte)255;

                for (int i = 0; i < rgba.Length; i += 4)
                {
                    rgba[i + 0] = r;   // R
                    rgba[i + 1] = g;   // G
                    rgba[i + 2] = 0;   // B
                    rgba[i + 3] = 255; // A
                }

                Frames.SetLatest(rgba, w, h);

            }, null, dueTime: 0, period: 500); // cada 500 ms
        }
    }
}

