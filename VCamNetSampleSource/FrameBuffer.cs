using System;

namespace VCamNetSampleSource
{
    internal sealed class FrameBuffer
    {
        private readonly object _lock = new();
        private byte[]? _rgba; // 4 bytes/pixel
        public int Width { get; private set; }
        public int Height { get; private set; }
        public long FrameId { get; private set; }

        public void SetLatest(byte[] rgba, int w, int h)
        {
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            if (w <= 0 || h <= 0) throw new ArgumentOutOfRangeException();
            if (rgba.Length < w * h * 4) throw new ArgumentException("rgba too small");

            lock (_lock)
            {
                _rgba = rgba;
                Width = w;
                Height = h;
                FrameId++;
            }
        }

        public bool TryGetLatest(out byte[] rgba, out int w, out int h, out long id)
        {
            lock (_lock)
            {
                if (_rgba == null)
                {
                    rgba = Array.Empty<byte>();
                    w = h = 0;
                    id = 0;
                    return false;
                }

                rgba = _rgba;
                w = Width;
                h = Height;
                id = FrameId;
                return true;
            }
        }
    }
}

