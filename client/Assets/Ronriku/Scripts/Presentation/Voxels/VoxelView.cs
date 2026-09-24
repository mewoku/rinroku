using Ronriku.Domain.Figures;
using Ronriku.Presentation.Accessibility;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Voxels
{
    /// <summary>
    /// Pixelated 3D: renders a voxel mesh with its own orthographic camera into a small point-filtered
    /// RenderTexture shown as this element's background. Each view lives on an isolated layer far from
    /// the origin. Turntable spin, drag to rotate, gyro tilt. Resources are released on detach.
    /// </summary>
    public sealed class VoxelView : VisualElement
    {
        public const int Layer = 31;
        private static int _slots;

        private readonly int _resolution;
        private Figure _figure;
        private Mesh _mesh;
        private bool _dirty = true;
        private GameObject _root;
        private Transform _model;
        private Camera _camera;
        private RenderTexture _texture;
        private Material _material;
        private float _yaw = 35f;
        private float _spinSpeed;
        private float _dragVelocity;
        private float _lastPointerX;
        private int _pointer = -1;
        private float _hop;
        private IVisualElementScheduledItem _tick;

        /// <param name="resolution">Render texture edge in pixels; lower = chunkier pixels.</param>
        public VoxelView(Figure figure, int resolution = 96, float spinDegreesPerSecond = 24f, bool interactive = true)
            : this(VoxelMeshBuilder.Build(figure), resolution, spinDegreesPerSecond, interactive)
        {
            _figure = figure;
        }

        public VoxelView(Mesh mesh, int resolution = 96, float spinDegreesPerSecond = 24f, bool interactive = true)
        {
            _mesh = mesh;
            _resolution = resolution;
            _spinSpeed = spinDegreesPerSecond;
            pickingMode = interactive ? PickingMode.Position : PickingMode.Ignore;
            style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            RegisterCallback<AttachToPanelEvent>(_ => Create());
            RegisterCallback<DetachFromPanelEvent>(_ => Release());
            if (interactive)
            {
                RegisterCallback<PointerDownEvent>(OnDown);
                RegisterCallback<PointerMoveEvent>(OnMove);
                RegisterCallback<PointerUpEvent>(OnUp);
                RegisterCallback<ClickEvent>(_ => Hop());
            }
        }

        public void SetFigure(Figure figure)
        {
            if (_mesh != null) Object.Destroy(_mesh);
            _figure = figure;
            _mesh = VoxelMeshBuilder.Build(figure);
            if (_model != null)
            {
                _model.GetComponent<MeshFilter>().sharedMesh = _mesh;
                Frame();
                _dirty = true;
            }
        }

        /// <summary>Little jump with squash, for tactile idle play.</summary>
        public void Hop()
        {
            _hop = 1f;
            Feedback.Tap();
        }

        private void Create()
        {
            if (_root != null) return;
            if (_mesh == null && _figure != null) _mesh = VoxelMeshBuilder.Build(_figure);
            int slot = _slots++;
            _root = new GameObject("VoxelView") { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
            _root.transform.position = new Vector3(10000f + slot * 100f, 10000f, 0f);

            var model = new GameObject("Model") { layer = Layer };
            model.transform.SetParent(_root.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _material = new Material(Resources.Load<Shader>("Shaders/VoxelLit"));
            model.AddComponent<MeshRenderer>().sharedMaterial = _material;
            _model = model.transform;

            var cameraObject = new GameObject("Camera") { layer = Layer };
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0, 0, 0, 0);
            _camera.cullingMask = 1 << Layer;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 200f;
            _camera.allowMSAA = false;
            _camera.allowHDR = false;
            // Rendered on demand from Tick: static views cost one render, not one per frame.
            _camera.enabled = false;

            _texture = new RenderTexture(_resolution, _resolution, 16, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point,
                antiAliasing = 1,
                name = "VoxelView"
            };
            _texture.Create();
            _camera.targetTexture = _texture;
            style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_texture));
            Frame();
            _tick = schedule.Execute(Tick).Every(16);
        }

        private void Frame()
        {
            Bounds b = _mesh.bounds;
            float radius = b.extents.magnitude;
            Vector3 target = _root.transform.position + b.center;
            _camera.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            _camera.transform.position = target - _camera.transform.forward * (radius * 4f + 10f);
            _camera.orthographicSize = radius * 1.08f;
        }

        private void Tick()
        {
            if (_model == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            bool reduced = MotionSettings.ReducedMotion;
            if (_pointer < 0)
            {
                _yaw += (reduced ? 0f : _spinSpeed) * dt + _dragVelocity;
                _dragVelocity *= 0.9f;
            }
            Vector2 tilt = Tilt.Current;
            _hop = Mathf.Max(0f, _hop - dt * 3.2f);
            float jump = Mathf.Sin(_hop * Mathf.PI) * 0.9f;
            float squash = 1f + (_hop > 0.85f ? (_hop - 0.85f) * 0.8f : 0f);
            Quaternion rotation = Quaternion.Euler(tilt.y * 8f, _yaw + tilt.x * 18f, 0f);
            var position = new Vector3(0f, jump, 0f);
            var scale = new Vector3(squash, 1f / squash, squash);
            bool changed = _dirty || rotation != _model.localRotation || position != _model.localPosition || scale != _model.localScale;
            if (!changed) return;
            _model.localRotation = rotation;
            _model.localPosition = position;
            _model.localScale = scale;
            _dirty = false;
            _camera.Render();
        }

        private void OnDown(PointerDownEvent e)
        {
            _pointer = e.pointerId;
            _lastPointerX = e.position.x;
            _dragVelocity = 0f;
            this.CapturePointer(e.pointerId);
        }

        private void OnMove(PointerMoveEvent e)
        {
            if (e.pointerId != _pointer) return;
            float dx = e.position.x - _lastPointerX;
            _lastPointerX = e.position.x;
            _yaw -= dx * 1.2f;
            _dragVelocity = -dx * 0.3f;
        }

        private void OnUp(PointerUpEvent e)
        {
            if (e.pointerId != _pointer) return;
            this.ReleasePointer(e.pointerId);
            _pointer = -1;
        }

        private void Release()
        {
            _tick?.Pause();
            if (_camera != null) _camera.targetTexture = null;
            if (_texture != null)
            {
                _texture.Release();
                Object.Destroy(_texture);
            }
            if (_material != null) Object.Destroy(_material);
            if (_mesh != null && _figure != null)
            {
                // Owned mesh: rebuilt from the figure if the view is attached again.
                Object.Destroy(_mesh);
                _mesh = null;
            }
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _model = null;
            _camera = null;
            _texture = null;
        }
    }
}
