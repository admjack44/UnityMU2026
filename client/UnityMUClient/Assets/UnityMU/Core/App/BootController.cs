using UnityEngine;

namespace UnityMU.Core.App
{
    public sealed class BootController : MonoBehaviour
    {
        private async void Start()
        {
            await UnityMUApp.Instance.SceneLoader.LoadLoginAsync();
        }
    }
}