using System;
using System.Collections;
using System.IO;
using UnityEngine;
using LittleCastle.CameraSystem;

namespace LittleCastle.World
{
    /// <summary>Controls for the isolated authored terrain sample, never used by a match.</summary>
    public sealed class GroundBiomeStudyControls : MonoBehaviour
    {
        public Material terrainMaterial;
        public Camera studyCamera;
        public Light sun;
        public MeshRenderer terrainRenderer;
        public Material baselineTerrain;
        public GameObject grassRoot;
        public GameObject treesRoot;
        private int comparisonMode = 3;
        private bool surfaceDetails = true;
        private bool showUI = true;
        private bool freeCamera;
        private bool capture;
        private string errors = "";
        private float snowPath = .76f;
        private int view;
        private readonly string[] viewNames = { "Общий вид", "Снег и дорожка", "Вода и берег", "Мягкие холмы" };

        private void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Application.logMessageReceived += Log;
        }
        private void OnDestroy() { Application.logMessageReceived -= Log; }
        private void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors += message + "\n" + stack + "\n";
        }
        private IEnumerator Start()
        {
            SetView(0);
            if (baselineTerrain) ApplyComparison(3);
            string[] args = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-groundCapture");
            if (index < 0 || index + 1 >= args.Length) yield break;
            capture = true; showUI = false;
            string output = args[index + 1]; Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(3);
            if (baselineTerrain)
            {
                yield return CaptureComparison(output);
                Application.Quit(string.IsNullOrEmpty(errors) ? 0 : 2);
                yield break;
            }
            for (int i = 0; i < 4; i++)
            {
                SetView(i); yield return new WaitForSecondsRealtime(.6f);
                yield return new WaitForEndOfFrame();
                CaptureFrame(Path.Combine(output, "0" + (i + 1) + "_" + new[] { "overview", "snow_path", "shore", "hills" }[i] + ".png"));
                yield return null;
            }
            SetView(2); yield return new WaitForSecondsRealtime(2);
            yield return new WaitForEndOfFrame();
            CaptureFrame(Path.Combine(output, "05_water_later.png"));
            yield return new WaitForSecondsRealtime(.5f);
            SetView(0);
            // Check the same surface under a lower sun without changing any saved scene.
            Quaternion rotation = sun.transform.rotation;
            Color sunColor = sun.color;
            sun.transform.rotation = Quaternion.Euler(24, -55, 0); sun.color = new Color(1, .83f, .64f);
            FindAnyObjectByType<LittleCastle.Rendering.StylizedLightingGlobals>()?.ApplyGlobals();
            yield return new WaitForSecondsRealtime(.5f); yield return new WaitForEndOfFrame();
            CaptureFrame(Path.Combine(output, "06_late_light.png"));
            yield return new WaitForSecondsRealtime(.5f);
            sun.transform.rotation = rotation; sun.color = sunColor;
            File.WriteAllText(Path.Combine(output, "player_result.txt"), "Unity=" + Application.unityVersion +
                "\nPipeline=" + UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline +
                "\nDevice=" + SystemInfo.graphicsDeviceName + "\nAuthored sample; 4 camera views + water motion + lower sun.\nErrors:\n" + errors);
            Application.Quit(string.IsNullOrEmpty(errors) ? 0 : 2);
        }
        private static void CaptureFrame(string path)
        {
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG()); Destroy(tex);
        }
        private IEnumerator CaptureComparison(string output)
        {
            string[] modes = { "baseline", "png_surface", "grass", "trees" };
            for (int mode = 0; mode < 4; mode++)
            {
                ApplyComparison(mode);
                for (int shot = 0; shot < 2; shot++)
                {
                    SetView(shot); yield return new WaitForSecondsRealtime(.55f);
                    yield return new WaitForEndOfFrame();
                    CaptureFrame(Path.Combine(output, modes[mode] + (shot == 0 ? "_overview.png" : "_close.png")));
                }
            }
            ApplyComparison(1);SetView(1);
            terrainMaterial.SetFloat("_SurfaceDetailStrength", 0);
            yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();CaptureFrame(Path.Combine(output,"png_no_normals_ao.png"));
            terrainMaterial.SetFloat("_SurfaceDetailStrength", .82f);
            ApplyComparison(3);SetView(2);yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();CaptureFrame(Path.Combine(output,"river.png"));
            SetView(3);yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();CaptureFrame(Path.Combine(output,"hills.png"));
            SetView(4);yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();CaptureFrame(Path.Combine(output,"vegetation_close.png"));
            yield return new WaitForSecondsRealtime(2);yield return new WaitForEndOfFrame();CaptureFrame(Path.Combine(output,"vegetation_close_later.png"));
            SetView(0);yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();CaptureFrame(Path.Combine(output,"final_overview.png"));
            yield return null;
            File.WriteAllText(Path.Combine(output,"player_result.txt"),"Unity="+Application.unityVersion+"\nPipeline="+UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline+"\nDevice="+SystemInfo.graphicsDeviceName+"\nCompared baseline / PNG surface / grass / trees at identical cameras and light.\nErrors:\n"+errors);
        }
        public void ApplyComparison(int mode)
        {
            comparisonMode=mode;
            if(terrainRenderer) terrainRenderer.sharedMaterial=mode==0?baselineTerrain:terrainMaterial;
            if(grassRoot)grassRoot.SetActive(mode>=2);
            if(treesRoot)treesRoot.SetActive(mode>=3);
        }
        public void SetView(int index)
        {
            view = index; freeCamera = false;
            var controller = studyCamera.GetComponent<StrategyCameraController>();
            if (controller) controller.enabled = false;
            Vector3[] eyes = { new Vector3(8, 65, -75), new Vector3(-8, 19, -11), new Vector3(29, 13, -33), new Vector3(-26, 16, 9), new Vector3(-30, 6, 10) };
            Vector3[] targets = { new Vector3(0, 0, 4), new Vector3(8, .5f, 8), new Vector3(16, -.2f, -12), new Vector3(-7, 2, 28), new Vector3(-24, 3, 22) };
            studyCamera.transform.position = eyes[index]; studyCamera.transform.LookAt(targets[index]);
        }
        private void Update()
        {
            if (capture) return;
            if (Input.GetKeyDown(KeyCode.H)) showUI = !showUI;
            for (int i = 0; i < 4; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SetView(i);
            if(baselineTerrain && Input.GetKeyDown(KeyCode.Alpha5))SetView(4);
            if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
        }
        private void OnGUI()
        {
            if (!showUI) return;
            float scale = Mathf.Clamp(Screen.width / 1600f, .8f, 1.5f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUILayout.BeginArea(new Rect(18, 18, 306, baselineTerrain ? 475 : 282), GUI.skin.box);
            GUILayout.Label("ЗЕМЛЯ · СНЕГ · ВОДА");
            GUILayout.Label("Ручной участок · URP");
            GUILayout.Space(5);
            for (int i = 0; i < 4; i++) if (GUILayout.Button((i + 1) + "  " + viewNames[i], GUILayout.Height(27))) SetView(i);
            if(baselineTerrain)
            {
                if(GUILayout.Button("5  Растительность вблизи",GUILayout.Height(27)))SetView(4);
                GUILayout.Space(5);GUILayout.Label("Сравнение при одинаковом свете");
                string[] labels={"Исходный участок","PNG поверхности","Поверхности + трава","Поверхности + трава + деревья"};
                for(int i=0;i<4;i++)if(GUILayout.Button((comparisonMode==i?"● ":"")+labels[i]))ApplyComparison(i);
                bool detail=GUILayout.Toggle(surfaceDetails,"Рельеф материалов (Normal + AO)");
                if(detail!=surfaceDetails){surfaceDetails=detail;terrainMaterial.SetFloat("_SurfaceDetailStrength",detail?.82f:0);}
            }
            GUILayout.Label("Снег на дороге");
            float next = GUILayout.HorizontalSlider(1 - snowPath, 0, 1);
            if (!Mathf.Approximately(next, 1 - snowPath)) { snowPath = 1 - next; terrainMaterial.SetFloat("_SnowPathClearance", snowPath); }
            if (GUILayout.Button(freeCamera ? "Камера свободна · вернуться к виду" : "Свободно осмотреть участок"))
            {
                if (freeCamera) SetView(view);
                else { freeCamera = true; studyCamera.GetComponent<StrategyCameraController>().enabled = true; }
            }
            GUILayout.EndArea();
            GUI.Box(new Rect(18, Screen.height / scale - 40, 700, 25), "1–4: ракурсы   ·   H: скрыть панель   ·   Камера: WASD / колесо / правая кнопка   ·   Esc: выход");
        }
    }
}
