using System;
using System.IO;
using UnityEngine;

namespace DecisionDisc
{
    public sealed class AndroidFileBridge : MonoBehaviour
    {
        public Action<string> TextImported;
        public Action<string> ImageImported;
        private static AndroidFileBridge instance;

        private void Awake() { instance = this; gameObject.name = "DecisionDiscFileBridge"; }

        public void PickJson()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var plugin = new AndroidJavaClass("com.decisiondisc.filepicker.DecisionFilePicker"))
                plugin.CallStatic("pickText", player.GetStatic<AndroidJavaObject>("currentActivity"), gameObject.name);
#else
            Debug.Log("Android file picker is available on device builds.");
#endif
        }

        public void PickImage()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var plugin = new AndroidJavaClass("com.decisiondisc.filepicker.DecisionFilePicker"))
                plugin.CallStatic("pickImage", player.GetStatic<AndroidJavaObject>("currentActivity"), gameObject.name);
#else
            Debug.Log("Android image picker is available on device builds.");
#endif
        }

        public void ExportJson(string json)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var plugin = new AndroidJavaClass("com.decisiondisc.filepicker.DecisionFilePicker"))
                plugin.CallStatic("createText", player.GetStatic<AndroidJavaObject>("currentActivity"), gameObject.name, json);
#else
            string path = Path.Combine(Application.persistentDataPath, "decision-disc-history.json");
            File.WriteAllText(path, json);
            Debug.Log("Exported history: " + path);
#endif
        }

        // Called by the Android plugin through UnitySendMessage.
        public void OnTextPicked(string payload) { TextImported?.Invoke(payload); }
        public void OnImagePicked(string path) { ImageImported?.Invoke(path); }
        public void OnFilePickerError(string message) { Debug.LogWarning("File picker: " + message); }
    }
}
