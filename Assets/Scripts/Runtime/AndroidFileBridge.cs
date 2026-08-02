using System;
using System.IO;
using UnityEngine;

namespace DecisionDisc
{
    public sealed class AndroidFileBridge : MonoBehaviour
    {
        public Action<string> TextImported;
        public Action<string> ImageImported;
        public Action<string> Error;
        private static AndroidFileBridge instance;

        private void Awake() { instance = this; gameObject.name = "DecisionDiscFileBridge"; }

        public void PickJson()
        {
            UserActionLog.Add("打开 JSON 文件选择器");
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
            UserActionLog.Add("打开图片文件选择器");
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
            ExportText(json, "decision-disc-history.json", "application/json");
        }

        public void ExportText(string text, string fileName, string mimeType)
        {
            UserActionLog.Add("打开文件导出选择器：" + fileName);
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var plugin = new AndroidJavaClass("com.decisiondisc.filepicker.DecisionFilePicker"))
                plugin.CallStatic("createText", player.GetStatic<AndroidJavaObject>("currentActivity"), gameObject.name, text, fileName, mimeType);
#else
            string path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllText(path, text);
            Debug.Log("Exported file: " + path);
#endif
        }

        // Called by the Android plugin through UnitySendMessage.
        public void OnTextPicked(string payload) { UserActionLog.Add("已读取导入文件"); TextImported?.Invoke(payload); }
        public void OnImagePicked(string path) { UserActionLog.Add("已读取所选图片"); ImageImported?.Invoke(path); }
        public void OnFilePickerError(string message) { UserActionLog.Add("文件选择器返回：" + message); Error?.Invoke(message); Debug.LogWarning("File picker: " + message); }
    }
}
