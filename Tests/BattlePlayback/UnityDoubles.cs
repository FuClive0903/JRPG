using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// Only the managed playback contract is tested here, not Unity rendering or lifecycle dispatch.
namespace UnityEngine
{
    public class Object
    {
        public static readonly List<(GameObject prefab, Transform parent, bool worldPositionStays)> Instantiations = new();
        public static GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        {
            Instantiations.Add((prefab, parent, worldPositionStays));
            return new GameObject();
        }
    }
    public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int value) { } }
    public class SerializeField : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public class ContextMenu : Attribute { public ContextMenu(string name) { } }
    public class CreateAssetMenuAttribute : Attribute { public string menuName, fileName; }
    public class ScriptableObject { }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }
    public class Component : Object
    {
        public GameObject gameObject = new GameObject();
        private Transform ownTransform;
        public Transform transform => this as Transform ?? (ownTransform ??= new Transform());
        public Component[] Children = Array.Empty<Component>();
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : Component =>
            Children.OfType<T>().ToArray();
        public T GetComponentInChildren<T>() where T : Component => Children.OfType<T>().FirstOrDefault();
    }
    public class Transform : Component, IEnumerable
    {
        public Transform parent;
        public void SetAsLastSibling() { }
        public Vector3 localPosition;
        public Vector3 localScale = Vector3.one;
        public IEnumerator GetEnumerator() => Children.OfType<Transform>().GetEnumerator();
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 Scale(Vector2 a, Vector2 b) => new Vector2(a.x*b.x, a.y*b.y);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x+b.x, a.y+b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x-b.x, a.y-b.y);
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) =>
            new Vector2(Mathf.Lerp(a.x,b.x,t), Mathf.Lerp(a.y,b.y,t));
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x*b.x, a.y*b.y, a.z*b.z);
        public static Vector3 operator -(Vector3 v) => new Vector3(-v.x, -v.y, -v.z);
        public static Vector3 operator *(Vector3 v, float n) => new Vector3(v.x*n, v.y*n, v.z*n);
    }
    public struct Rect { public float width, height; public Vector2 size => new Vector2(width,height); }
    public class RectTransform : Transform
    {
        public Vector2 anchorMin, anchorMax, anchoredPosition, sizeDelta;
        public Rect rect;
    }
    public class GameObject : Object
    {
        private Transform ownTransform;
        public Transform transform => ownTransform ??= new Transform();
        public bool activeInHierarchy => activeSelf;
        public bool activeSelf = true;
        public void SetActive(bool value) { activeSelf = value; }
    }
    public class Sprite { public Rect rect; public Vector2 pivot; public float pixelsPerUnit = 64; }
    public class MonoBehaviour : Component
    {
        public bool enabled = true;
        public bool isActiveAndEnabled => enabled;
        private readonly List<Coroutine> routines = new List<Coroutine>();
        public Coroutine StartCoroutine(IEnumerator routine)
        {
            var coroutine = new Coroutine(routine);
            routines.Add(coroutine);
            if (!coroutine.Step()) routines.Remove(coroutine);
            return coroutine;
        }
        public void StopAllCoroutines() => routines.Clear();
        public void StepFrame()
        {
            Time.frameCount++;
            foreach (var routine in routines.ToArray())
                if (!routine.Step()) routines.Remove(routine);
        }
    }
    public class Coroutine
    {
        private readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        public Coroutine(IEnumerator routine) { stack.Push(routine); }
        public bool Step()
        {
            while (stack.Count > 0)
            {
                var current = stack.Peek();
                if (!current.MoveNext()) { stack.Pop(); continue; }
                if (current.Current is IEnumerator child) { stack.Push(child); continue; }
                return true;
            }
            return false;
        }
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f)
        { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color operator *(Color x, Color y) =>
            new Color(x.r * y.r, x.g * y.g, x.b * y.b, x.a * y.a);
    }
    public class SpriteRenderer : Component
    {
        public Color color = Color.white;
        public bool enabled = true, flipX, flipY;
        public Sprite sprite;
    }
    public static class Time
    {
        public static float timeScale = 1f;
        public static float deltaTime = 0.1f, unscaledDeltaTime = 0.1f;
        public static int frameCount;
    }
    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogError(object message, object context) { }
        public static void LogWarning(object message, object context = null) { }
    }
    public static class Mathf
    {
        public static float Clamp(float v, float min, float max) => Math.Clamp(v, min, max);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp01(float v) => Math.Clamp(v, 0, 1);
        public static float Lerp(float a, float b, float t) => a + (b-a)*Clamp01(t);
        public static float SmoothStep(float a, float b, float t)
        { t = Clamp01(t); return a + (b-a)*t*t*(3-2*t); }
    }
    public static class Random
    {
        public static int Range(int min, int max) => min;
        public static float value => 0f;
    }
    public class TextAsset { public string text; }
    public static class Resources
    {
        private static readonly Dictionary<string, object> assets =
            new Dictionary<string, object>();

        public static T Load<T>(string path)
        {
            return assets.TryGetValue(path, out object asset) ? (T)asset : default(T);
        }

        public static void Register(string path, object asset) { assets[path] = asset; }
        public static void Clear() { assets.Clear(); }
    }
    public static class JsonUtility
    {
        public static Func<string, Type, object> Deserialize;
        public static Func<object, string> Serialize;
        public static T FromJson<T>(string json) => Deserialize == null
            ? throw new NotSupportedException() : (T)Deserialize(json, typeof(T));
        public static string ToJson(object value, bool prettyPrint = false) => Serialize == null
            ? throw new NotSupportedException() : Serialize(value);
    }
}
namespace TMPro
{
    public class TextMeshProUGUI : UnityEngine.Component
    {
        public string text;
        public float alpha;
        public bool raycastTarget;
        public UnityEngine.Color color;
        public UnityEngine.RectTransform rectTransform = new UnityEngine.RectTransform();
    }
}
namespace UnityEngine.InputSystem
{
    public class KeyControl { public bool wasPressedThisFrame, isPressed; }
    public class Keyboard
    {
        public static Keyboard current;
        public KeyControl leftArrowKey, rightArrowKey, zKey, xKey;
        public KeyControl upArrowKey, downArrowKey, escapeKey;
    }
    public class Mouse { public static Mouse current; public KeyControl rightButton; }
}
namespace Game.Battle
{
    public class BattleFloatingNumberView : UnityEngine.MonoBehaviour
    {
        public UnityEngine.Coroutine Show(UnityEngine.Transform target, float healthChange,
            BattleNumberStyle style) => null;
    }
}
