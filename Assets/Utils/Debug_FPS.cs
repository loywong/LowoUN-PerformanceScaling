using UnityEngine;

namespace LowoUN.Util {
    public class Debug_FPS : MonoBehaviour {
        // 帧率计算参数
        [SerializeField] float _updateInterval = 1f; // 更新间隔（秒）
        private int _frameCount = 0;
        private float _accumulatedTime = 0f;
        private float _currentFPS = 0f;
        public int CurrentFPS => Mathf.CeilToInt (_currentFPS);

        void Update () {
            UpdateFPS ();
        }

        private void UpdateFPS () {
            _frameCount++;
            _accumulatedTime += Time.unscaledDeltaTime;

            if (_accumulatedTime >= _updateInterval) {
                _currentFPS = _frameCount / _accumulatedTime;
                _frameCount = 0;
                _accumulatedTime = 0f;
            }
        }

#if UNITY_EDITOR
        void OnGUI () {
            DisplayFPS ();
        }
        private void DisplayFPS () {
            GUI.skin.label.fontSize = 48;
            GUI.color = Color.white;
            // 背景框的位置和大小
            Rect bgRect = new Rect (Screen.width - 100, 30, 80, 48);
            // 绘制背景（使用 GUI.Box）
            GUI.Box (bgRect, ""); // 空字符串表示不显示文字
            // string fpsString = $"FPS: {_currentFPS:0.}";
            string fpsInfo = Mathf.CeilToInt (_currentFPS).ToString ();
            GUI.Label (bgRect, fpsInfo);
        }
#endif
    }
}