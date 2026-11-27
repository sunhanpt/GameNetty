using UnityEditor;
using UnityEngine;
using GameScripts.GameLogic;

namespace ET
{
    public static class ToolsEditor
    {
        private const string LocalServerMenu = "ET/连接本地服务器 (127.0.0.1)";

        [MenuItem(LocalServerMenu)]
        private static void ToggleLocalServer()
        {
            bool useLocalServer = !Main2NetClient_LoginHandler.UseLocalServer;
            Main2NetClient_LoginHandler.UseLocalServer = useLocalServer;
            Menu.SetChecked(LocalServerMenu, useLocalServer);
        }

        [MenuItem(LocalServerMenu, true)]
        private static bool ValidateLocalServerMenu()
        {
            Menu.SetChecked(LocalServerMenu, Main2NetClient_LoginHandler.UseLocalServer);
            return true;
        }

        [MenuItem("ET/Luban 转表 Client")]
        public static void BuildLubanExcelClient()
        { 
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            Application.OpenURL(Application.dataPath + @"/../../Tools/Luban/GenConfig_Client.sh");
#else
            Application.OpenURL(Application.dataPath + @"/../../Tools/Luban/GenConfig_Client.bat");
#endif
        }
        
        [MenuItem("ET/Luban 转表 Server")]
        public static void BuildLubanExcelServer()
        {
            Application.OpenURL(Application.dataPath + @"/../../Tools/Luban/GenConfig_Server.bat");
        }

        private const string clientMessagePath = "../Unity/Assets/GameScripts/GameProto/Generate/Message/";
        
        private const string serverMessagePath = "../Server/Model/Generate/Message/";

        private const string protoDir = "../Config/Proto";
        
        [MenuItem("ET/Build Proto2CS")]
        public static void Proto2CS()
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            const string tools = "./Tool";
#else
            const string tools = ".\\Tool.exe";
#endif
            // ShellHelper.Run($"{tools} --AppType=Proto2CS --Console=1 {protoDir} {clientMessagePath} {serverMessagePath}", "../Bin/");
            ShellHelper.Run($"{tools} --AppType=Proto2CS --Console=1", "../Bin/");
        }
    }
}
