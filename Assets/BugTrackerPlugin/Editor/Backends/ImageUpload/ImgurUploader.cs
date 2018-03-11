using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;

namespace BugReporter
{
    public class ImgurUploader : ImageUploader
    {
        const string _uploaderName = "imgur";

        static ImgurUploader()
        {
            BugReporterPlugin.RegisterImageUploader(_uploaderName, new ImgurUploader());
        }

        public override void UploadFile(byte[] data, System.Action<bool, string> onUploadFinished)
        {
            var settings = BugReporterPlugin.settings.GetImageUploaderSetting(_uploaderName);

            if (settings.authentification == "")
            {
                var tokenEnter = EditorWindow.GetWindow<ImgurTokenEntry>();
                tokenEnter.onEntered = win =>
                {
                    settings.authentification = win.appID;
                    BugReporterPlugin.SaveSettings();
                    InternalUpload(data, onUploadFinished);
                };
            }
            else
                InternalUpload(data, onUploadFinished);
        }

        void InternalUpload(byte[] data, System.Action<bool, string> onUploadFinished)
        {
            var request = new UnityWebRequest("https://api.imgur.com/3/image", UnityWebRequest.kHttpVerbPOST);
            request.SetRequestHeader("Authorization", "Client-ID 7b2138351689000");

            request.uploadHandler = new UploadHandlerRaw(data);
            request.uploadHandler.contentType = "image/png";

            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

            var async = request.SendWebRequest();

            async.completed += op =>
            {
                UnityWebRequestAsyncOperation asyncop = op as UnityWebRequestAsyncOperation;
                if (asyncop.webRequest.isHttpError)
                {
                    Debug.LogErrorFormat("[Imgur Uploader] Error {0} : {1}", asyncop.webRequest.responseCode, asyncop.webRequest.error);

                    if (asyncop.webRequest.responseCode == 401)
                    {//unauthorized error, the access token is probably invalid, so remove it from the settings.
                        var settings = BugReporterPlugin.settings.GetImageUploaderSetting(_uploaderName);
                        settings.authentification = "";
                        BugReporterPlugin.SaveSettings();
                    }

                    onUploadFinished(false, "");
                }
                else
                {
                    DataWrapper<ImgurUploadResponse> response = JsonUtility.FromJson<DataWrapper<ImgurUploadResponse>>(asyncop.webRequest.downloadHandler.text);

                    onUploadFinished(true, response.data.link);
                }
            };
        }

        [System.Serializable]
        class ImgurUploadResponse
        {
            public string link;
        }

        //all imgur answer seem to be in a json object that just have "data" : {actuel answer}
        [System.Serializable]
        class DataWrapper<T>
        {
            public T data;
        }
    }

    public class ImgurTokenEntry : EditorWindow
    {
        public System.Action<ImgurTokenEntry> onEntered;

        public string appID;

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Please enter your Imgur app client ID", MessageType.Info);
            appID = EditorGUILayout.TextField("App id", appID);

            if(GUILayout.Button("Accept"))
            {
                onEntered(this);
                Close();
            }
        }
    }
}
