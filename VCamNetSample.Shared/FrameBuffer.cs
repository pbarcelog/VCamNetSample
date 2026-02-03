using System;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;

namespace VCamNetSample.Shared
{
    public sealed class FrameBuffer : IDisposable
    {
        // 64MB capacity to support up to 4K resoluion comfortably (4K RGBA is ~33MB)
        private const long Capacity = 64 * 1024 * 1024;
        private const string MapName = "Local\\VCamNetSample_SharedFrame";
        private const string MutexName = "Local\\VCamNetSample_Mutex";

        private readonly Mutex _mutex;
        private MemoryMappedFile? _mmf;
        private MemoryMappedViewAccessor? _accessor;

        public FrameBuffer()
        {
            // Create a security identifier for "Everyone" (S-1-1-0)
            // or better "All Application Packages" (S-1-15-2-1) + "Everyone".
            // For max compatibility in a dev sample, "Everyone" FullControl is easiest.
            var security = new MutexSecurity();
            security.AddAccessRule(new MutexAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, (SecurityIdentifier?)null), 
                MutexRights.FullControl, 
                AccessControlType.Allow));

            // Also allow ALL APPLICATION PACKAGES (useful if camera runs in AppContainer)
             // S-1-15-2-1
            // Optional: AllApplicationPackagesSid (commented out to ensure build success)
            /*
            try {
                security.AddAccessRule(new MutexAccessRule(
                    new SecurityIdentifier(WellKnownSidType.AllApplicationPackagesSid, null), 
                    MutexRights.ReadPermissions | MutexRights.Synchronize | MutexRights.Modify, 
                    AccessControlType.Allow));
            } catch { } // old windows or not applicable
            */

            bool createdNew;
            // Create without security first (avoid constructor/method missing issues)
            _mutex = new Mutex(false, MutexName, out createdNew);

            // Apply security afterwards (Windows only)
            try 
            {
                if (OperatingSystem.IsWindows())
                {
                    _mutex.SetAccessControl(security);
                }
            } 
            catch { } // Ignore on non-windows or if method missing
        }

        public void SetLatest(byte[] rgba, int w, int h)
        {
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            if (w <= 0 || h <= 0) throw new ArgumentOutOfRangeException();
            
            int expectedSize = w * h * 4;
            if (rgba.Length < expectedSize) throw new ArgumentException("rgba too small");
            
            // Basic header size = 16 bytes (long id + int w + int h)
            if (expectedSize + 16 > Capacity) return; // Too big for our buffer

            bool lockTaken = false;
            try
            {
                // Wait briefly for the lock
                lockTaken = _mutex.WaitOne(100);
                if (!lockTaken) return;

                EnsureMMF(create: true);
                if (_accessor == null) return;

                // Read current ID to increment it
                long currentId = _accessor.ReadInt64(0);
                long newId = currentId + 1;

                // Write Header: ID (0), Width (8), Height (12)
                _accessor.Write(0, newId);
                _accessor.Write(8, w);
                _accessor.Write(12, h);

                // Write Data at (16)
                _accessor.WriteArray(16, rgba, 0, expectedSize);
            }
            catch (Exception)
            {
                // Should log?
            }
            finally
            {
                if (lockTaken) _mutex.ReleaseMutex();
            }
        }

        public bool TryGetLatest(out byte[] rgba, out int w, out int h, out long id)
        {
            rgba = Array.Empty<byte>();
            w = 0;
            h = 0;
            id = 0;

            bool lockTaken = false;
            try
            {
                // Wait briefly
                lockTaken = _mutex.WaitOne(10);
                if (!lockTaken) return false;

                // Try to open existing map
                if (!EnsureMMF(create: false))
                {
                    return false;
                }
                
                if (_accessor == null) return false;

                long readId = _accessor.ReadInt64(0);
                int readW = _accessor.ReadInt32(8);
                int readH = _accessor.ReadInt32(12);

                if (readW <= 0 || readH <= 0 || readW > 10000 || readH > 10000)
                {
                    return false;
                }

                int size = readW * readH * 4;
                if (size > Capacity - 16) return false;

                rgba = new byte[size];
                _accessor.ReadArray(16, rgba, 0, size);
                
                w = readW;
                h = readH;
                id = readId;
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (lockTaken) _mutex.ReleaseMutex();
            }
        }

        private bool EnsureMMF(bool create)
        {
            if (_mmf != null && _accessor != null) return true;

            try
            {
                if (create)
                {
                    // Bridge (Producer) creates it 
                    // Security descriptors temporarily disabled due to .NET 10 compilation issue with MemoryMappedFileSecurity
                    /*
                    var mmfSecurity = new MemoryMappedFileSecurity();
                    mmfSecurity.AddAccessRule(new AccessRule<MemoryMappedFileRights>(
                        new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                        MemoryMappedFileRights.FullControl,
                        AccessControlType.Allow));
                    
                    _mmf = MemoryMappedFile.CreateOrOpen(
                        MapName, 
                        Capacity, 
                        MemoryMappedFileAccess.ReadWrite,
                        MemoryMappedFileOptions.None, 
                        mmfSecurity,
                        HandleInheritability.None);
                    */
                    
                    // Fallback: Create without explicit security (works for same-session apps)
                    _mmf = MemoryMappedFile.CreateOrOpen(MapName, Capacity, MemoryMappedFileAccess.ReadWrite, 
                        MemoryMappedFileOptions.None, HandleInheritability.None);
                }
                else
                {
                    // Camera (Consumer) opens it
                    _mmf = MemoryMappedFile.OpenExisting(MapName, MemoryMappedFileRights.ReadWrite);
                }
                _accessor = _mmf.CreateViewAccessor(0, Capacity, MemoryMappedFileAccess.ReadWrite);
                return true;
            }
            catch
            {
                // If it doesn't exist yet, we fail gracefully
                return false;
            }
        }

        public void Dispose()
        {
            _accessor?.Dispose();
            _mmf?.Dispose();
            _mutex?.Dispose();
        }
    }
}

