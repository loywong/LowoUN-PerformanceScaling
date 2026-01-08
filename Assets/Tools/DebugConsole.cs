using System.Linq;
using LowoUN.Module.Perf;
using LowoUN.Util;
using UnityEngine;

namespace LowoUN.Tool {
    public class DebugConsole : SingletonMono<DebugConsole> {
        Color defaultBtnColor;
        bool isShowGUI;
        public void Init () {
            isShowGUI = GameSettings._instance.ifShowGUITest;
            defaultBtnColor = GUI.backgroundColor;
        }

        bool isOpen = false;

        int targetBattleLevelId = 90999;

        string equipId;
        string equipCount = "1";

        string addPlayerExp;

        readonly float offsetTopLeftY = 150;
        float alignY;
        public override void Awake () {
            base.Awake ();
            alignY = Screen.height  /  2;
            // DontDestroyOnLoad(gameObject);
        }

#if UNITY_EDITOR
        int allMeshTriangles; //all have708724 triangles
        void ShowMeshInfo () {
            var meshes = FindObjectsOfType<MeshFilter> ()
                .Select (mf => new { Name = mf.name, Triangles = mf.sharedMesh.triangles.Length / 3 })
                .OrderByDescending (x => x.Triangles);

            foreach (var mesh in meshes) {
                allMeshTriangles += mesh.Triangles;
                Debug.Log ($"{mesh.Name}: {mesh.Triangles} triangles");
            }
            Debug.Log ($"all: {allMeshTriangles} triangles");
        }
        void Update () {
            if (Input.GetKey (KeyCode.O)) {
                isShowGUI = !isShowGUI;
            }
        }
#endif

        // 通过特殊 屏幕操作指令 触发 显示与隐藏 调试总开关
        bool isOpenDebugBtn = true;
        public void ToggleDebugBtn () {
            isOpenDebugBtn = !isOpenDebugBtn;
        }

        void OnGUI () {
            ShowAppEnvInfo ();

            if (!isShowGUI)
                return;

            if (!isOpenDebugBtn)
                return;

            GUI.skin.textField.fontSize  = 32;
            GUI.skin.button.fontSize = 36;
            GUI.backgroundColor = Color.green;

            if (GUI.Button (new Rect (10, alignY - 240 - 60, 240, 48), "开关Debug")) {
                isOpen = !isOpen;
            }

            if (!isOpen)
                return;

            GUI.backgroundColor = Color.red;

            GUI.backgroundColor = defaultBtnColor;

        }

        void ShowAppEnvInfo () {
            if (!GameSettings._instance.isShowEnvInfo)
                return;

            GUI.skin.label.fontSize = 24;
            GUI.skin.label.alignment = TextAnchor.LowerRight;
            GUI.backgroundColor = Color.white;

            // 1 包名 2 包版本号（强更时修改）3 资源版本号(非脚本+脚本)
            GUI.Label (new  Rect (Screen.width - 1920, Screen.height - 32, 1905, 32),  $"[PerfLev:{PerfManager.Self.CurPerfLevelType}] {PerfManager.Self.perfLevDesc}");
        }
    }
}