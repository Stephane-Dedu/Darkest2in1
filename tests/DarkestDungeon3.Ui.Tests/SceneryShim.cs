using UnityEngine;

namespace UnityEngine.ResourceManagement.AsyncOperations
{
    public enum AsyncOperationStatus { Succeeded, Failed }
    public struct AsyncOperationHandle<T> where T : class
    {
        internal sealed class State { public bool Valid = true; public T Result; }
        private State _state;
        internal AsyncOperationHandle(T result) => _state = new State { Result = result };
        public bool IsValid() => _state != null && _state.Valid;
        public bool IsDone => true;
        public AsyncOperationStatus Status => AsyncOperationStatus.Succeeded;
        public T Result => _state.Result;
        public event Action<AsyncOperationHandle<T>> Completed { add => value(this); remove { } }
        internal void Release() { _state.Valid = false; }
    }
}
namespace UnityEngine.AddressableAssets
{
    using UnityEngine.ResourceManagement.AsyncOperations;
    public static class Addressables
    {
        public static int LiveHandles;
        public static AsyncOperationHandle<T> LoadAssetAsync<T>(string key) where T : class
        { LiveHandles++; return new AsyncOperationHandle<T>(new Texture2D(2, 2, TextureFormat.RGBA32, false) as T); }
        public static void Release<T>(AsyncOperationHandle<T> handle) where T : class
        { LiveHandles--; handle.Release(); UnityEngine.Object.Destroy(handle.Result); }
    }
}
